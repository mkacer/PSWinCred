#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies and imports the pinned build and test tools in .tools.
.DESCRIPTION
    Every file of a tool MUST carry a valid Authenticode signature from its pinned signer, except files whose
    extension is in $NonCodeExtensions: help, documentation and package metadata that PowerShell does not run
    and that cannot carry an Authenticode signature. A tool is pinned in tools/tools.psd1 by either
    SignerThumbprint (every file is signed by exactly that certificate) or RootThumbprint (every signing
    certificate chains to one of these roots, independent of the roots this computer trusts). FileRoots adds
    roots for single named files only, and FileSigners pins single named files to exact certificates, so an
    exception for one file does not extend to the others.

    Import-Tool verifies a tool again each time it imports it, because any program running as the user can
    change .tools after installation. A change between the check and the import is not detected.
#>

Set-StrictMode -Version 3.0
# Functions in a module do not see the caller's preference; every error here MUST stop verification
$ErrorActionPreference = 'Stop'

# Files with these extensions are not verified: PowerShell does not run them and they cannot carry an
# Authenticode signature. .p7s is the NuGet package signature that Save-PSResource keeps; .json holds data,
# such as PSScriptAnalyzer's settings and compatibility profiles. Every other file is verified, so an
# unexpected file fails closed.
$NonCodeExtensions = @('.md', '.txt', '.xml', '.xsd', '.dtd', '.json', '.png', '.jpg', '.gif', '.ico', '.svg', '.nuspec', '.psmdcp', '.rels', '.p7s', '')

function Get-ToolManifest {
    [OutputType([hashtable])]
    param()
    Import-PowerShellDataFile (Join-Path $PSScriptRoot 'tools.psd1')
}

function Get-ToolDirectory {
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$ToolsPath)
    $tool = (Get-ToolManifest)[$Name]
    if (-not $tool) {
        throw "Tool '$Name' is not pinned in tools/tools.psd1."
    }
    Join-Path $ToolsPath "$Name/$($tool.Version)"
}

function Get-RootCertificate {
    <#
    .SYNOPSIS
        Returns the root of the certificate's chain, or $null when the chain does not reach a root.
    #>
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param([Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    # Only identifies the root. Get-AuthenticodeSignature has already checked trust, time and timestamp.
    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
        $chain.ChainPolicy.VerificationFlags = [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::IgnoreNotTimeValid
        [void]$chain.Build($Certificate)
        $root = $chain.ChainElements[$chain.ChainElements.Count - 1].Certificate
        if ($root.Subject -ne $root.Issuer) {
            return $null
        }
        $root
    }
    finally {
        $chain.Dispose()
    }
}

function Assert-ToolSignature {
    <#
    .SYNOPSIS
        Throws unless every file of the tool is signed by its pinned signer. Lists every failing file.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$ToolsPath)

    if (-not (Get-Command Get-AuthenticodeSignature -ErrorAction SilentlyContinue)) {
        throw 'Tool signatures can only be verified on Windows.'
    }

    # Indexers, not dot notation: strict mode throws on a missing key read with a dot
    $tool = (Get-ToolManifest)[$Name]
    if (-not $tool) {
        throw "Tool '$Name' is not pinned in tools/tools.psd1."
    }
    $signerPin = $tool['SignerThumbprint']
    $rootPins = @($tool['RootThumbprint'] | Where-Object { $_ })
    # Extra roots for single files, keyed by path relative to the module folder with forward slashes
    $fileRoots = $tool['FileRoots']
    if (-not $fileRoots) { $fileRoots = @{} }
    # Signer pins for single files, which replace the root check for that file
    $fileSigners = $tool['FileSigners']
    if (-not $fileSigners) { $fileSigners = @{} }
    $moduleDir = Get-ToolDirectory -Name $Name -ToolsPath $ToolsPath
    if (-not (Test-Path -LiteralPath (Join-Path $moduleDir "$Name.psd1") -PathType Leaf)) {
        throw "$Name $($tool.Version) is not installed in '$moduleDir'. Run tools/Install-Tools.ps1."
    }
    $moduleDir = (Resolve-Path -LiteralPath $moduleDir).ProviderPath
    if ([bool]$signerPin -eq ($rootPins.Count -gt 0)) {
        throw "tools/tools.psd1 MUST pin $Name by exactly one of SignerThumbprint and RootThumbprint."
    }

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($exceptions in @(@{ Key = 'FileRoots'; Paths = $fileRoots.Keys }, @{ Key = 'FileSigners'; Paths = $fileSigners.Keys })) {
        foreach ($path in $exceptions.Paths) {
            if (-not (Test-Path -LiteralPath (Join-Path $moduleDir $path) -PathType Leaf)) {
                $failures.Add("$path`: named in $($exceptions.Key) but not in the package; remove the stale exception")
            }
        }
    }
    $checked = 0
    foreach ($item in Get-ChildItem -LiteralPath $moduleDir -Recurse -Force) {
        # A symbolic link or junction lets Import-Module load files this walk never sees: PowerShell 6 and
        # later do not recurse into linked folders. Packages from PowerShell Gallery contain no links.
        if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            $failures.Add("$($item.FullName.Substring($moduleDir.Length).TrimStart('\', '/'))`: is a symbolic link, junction or other reparse point")
            continue
        }
        if ($item.PSIsContainer) { continue }
        $file = $item
        if ($NonCodeExtensions -contains $file.Extension.ToLowerInvariant()) { continue }
        $checked++

        $relative = $file.FullName.Substring($moduleDir.Length).TrimStart('\', '/').Replace('\', '/')
        $signature = Get-AuthenticodeSignature -FilePath $file.FullName
        if ($signature.Status -ne 'Valid') {
            $failures.Add("$relative`: signature status $($signature.Status)")
            continue
        }

        $signer = $signature.SignerCertificate
        $signerAllowed = if ($fileSigners.ContainsKey($relative)) { @($fileSigners[$relative]) } elseif ($signerPin) { @($signerPin) } else { $null }
        if ($signerAllowed) {
            if ($signerAllowed -notcontains $signer.Thumbprint) {
                $failures.Add("$relative`: signed by $($signer.Thumbprint) ('$($signer.Subject)')")
            }
        }
        else {
            $allowed = $rootPins + @($fileRoots[$relative] | Where-Object { $_ })
            $root = Get-RootCertificate -Certificate $signer
            if (-not $root) {
                $failures.Add("$relative`: signed by '$($signer.Subject)', whose chain does not reach a root")
            }
            elseif ($allowed -notcontains $root.Thumbprint) {
                $failures.Add("$relative`: signed by $($signer.Thumbprint) ('$($signer.Subject)') under root $($root.Thumbprint) ('$($root.Subject)')")
            }
        }
    }

    if ($checked -eq 0) {
        $failures.Add('no file was verified')
    }
    if ($failures.Count -gt 0) {
        throw ("$Name $($tool.Version) failed verification in $($failures.Count) of $checked file(s) in '$moduleDir'. " +
            'Nothing was imported. If the files were changed, delete the folder and run tools/Install-Tools.ps1 again:' +
            [Environment]::NewLine + ($failures -join [Environment]::NewLine))
    }
    Write-Verbose "$Name $($tool.Version): $checked file(s) verified."
}

function Import-Tool {
    <#
    .SYNOPSIS
        Verifies a pinned tool in .tools, then imports it.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$ToolsPath)

    Assert-ToolSignature -Name $Name -ToolsPath $ToolsPath
    $manifest = Join-Path (Get-ToolDirectory -Name $Name -ToolsPath $ToolsPath) "$Name.psd1"
    Import-Module -Name $manifest -Force -Global
}

Export-ModuleMember -Function Assert-ToolSignature, Import-Tool, Get-ToolManifest
