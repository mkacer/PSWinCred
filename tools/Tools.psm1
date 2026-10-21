#Requires -Version 5.1
<#
.SYNOPSIS
    Verifies and imports the pinned build and test tools in .tools.
.DESCRIPTION
    Every file of a tool MUST carry a valid Authenticode signature from its pinned signer, except files whose
    extension is in $NonCodeExtensions: help, documentation and package metadata that PowerShell does not run
    and that cannot carry an Authenticode signature. A tool is pinned in tools/tools.psd1 by either
    SignerThumbprint (every file is signed by exactly that certificate) or RootThumbprint (every signing
    certificate chains to one of these roots, independent of the roots this computer trusts).

    Import-Tool verifies a tool again each time it imports it, because any program running as the user can
    change .tools after installation. A change between the check and the import is not detected.
#>

Set-StrictMode -Version 3.0

# Files with these extensions are not verified. Everything else is, including files without an extension
# PowerShell cannot load, so an unexpected file fails closed.
$NonCodeExtensions = @('.md', '.txt', '.xml', '.xsd', '.dtd', '.png', '.jpg', '.gif', '.ico', '.svg', '.nuspec', '.psmdcp', '.rels', '')

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

function Get-RootThumbprint {
    [OutputType([string])]
    param([Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    # Only identifies the root. Get-AuthenticodeSignature has already checked trust, time and timestamp.
    $chain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    try {
        $chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
        $chain.ChainPolicy.VerificationFlags = [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::IgnoreNotTimeValid
        [void]$chain.Build($Certificate)
        $root = $chain.ChainElements[$chain.ChainElements.Count - 1].Certificate
        # An incomplete chain ends below the root; report it so it cannot match a pin
        if ($root.Subject -ne $root.Issuer) {
            return "incomplete chain ending at $($root.Thumbprint)"
        }
        $root.Thumbprint
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

    $tool = (Get-ToolManifest)[$Name]
    $moduleDir = Get-ToolDirectory -Name $Name -ToolsPath $ToolsPath
    if (-not (Test-Path (Join-Path $moduleDir "$Name.psd1"))) {
        throw "$Name $($tool.Version) is not installed in '$moduleDir'. Run tools/Install-Tools.ps1."
    }
    $moduleDir = (Resolve-Path -LiteralPath $moduleDir).ProviderPath
    if ([bool]$tool.SignerThumbprint -eq [bool]$tool.RootThumbprint) {
        throw "tools/tools.psd1 MUST pin $Name by exactly one of SignerThumbprint and RootThumbprint."
    }

    $failures = [System.Collections.Generic.List[string]]::new()
    $checked = 0
    foreach ($file in Get-ChildItem -Path $moduleDir -Recurse -File -Force) {
        if ($NonCodeExtensions -contains $file.Extension.ToLowerInvariant()) { continue }
        $checked++

        $relative = $file.FullName.Substring($moduleDir.Length).TrimStart('\', '/')
        $signature = Get-AuthenticodeSignature -FilePath $file.FullName
        if ($signature.Status -ne 'Valid') {
            $failures.Add("$relative`: signature status $($signature.Status)")
            continue
        }

        $signer = $signature.SignerCertificate
        if ($tool.SignerThumbprint) {
            if ($signer.Thumbprint -ne $tool.SignerThumbprint) {
                $failures.Add("$relative`: signed by $($signer.Thumbprint) ($($signer.Subject))")
            }
        }
        else {
            $root = Get-RootThumbprint -Certificate $signer
            if (@($tool.RootThumbprint) -notcontains $root) {
                $failures.Add("$relative`: signed by '$($signer.Subject)' under root $root")
            }
        }
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
    Import-Module $manifest -Force -Global
}

Export-ModuleMember -Function Assert-ToolSignature, Import-Tool, Get-ToolManifest
