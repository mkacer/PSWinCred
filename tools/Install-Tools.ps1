#Requires -Version 5.1
#Requires -Modules Microsoft.PowerShell.PSResourceGet
<#
.SYNOPSIS
    Saves the pinned build and test tools to .tools and verifies their signatures.
.DESCRIPTION
    Reads the pinned versions from tools/tools.psd1. Every .ps1, .psm1 and .dll file in a tool, and its
    module manifest, MUST carry a valid Authenticode signature, and the manifest MUST be signed by the
    expected publisher. Any other result stops the script, so an unexpected package is never imported.

    Uses PSResourceGet, which ships with PowerShell 7.4 and later. In Windows PowerShell 5.1, install it first:
    Install-Module Microsoft.PowerShell.PSResourceGet -Scope CurrentUser
.PARAMETER Path
    Folder to save the tools in. Defaults to .tools at the repository root.
.PARAMETER Name
    Installs only these tools.
#>
[CmdletBinding()]
param(
    [string]$Path,

    [string[]]$Name
)

$ErrorActionPreference = 'Stop'

# Defaults are set here, not in param(): Windows PowerShell 5.1 leaves $PSScriptRoot empty
# while it evaluates parameter defaults of a script run with -File.
if (-not $Path) { $Path = Join-Path $PSScriptRoot '../.tools' }

function Assert-ToolSignature {
    param([string]$ToolName, [hashtable]$Tool, [string]$ModuleDir)

    $manifest = Get-Item (Join-Path $ModuleDir "$ToolName.psd1")
    $files = @(Get-ChildItem -Path $ModuleDir -Recurse -File -Include '*.ps1', '*.psm1', '*.dll') + $manifest
    foreach ($file in $files) {
        $signature = Get-AuthenticodeSignature -FilePath $file.FullName
        if ($signature.Status -ne 'Valid') {
            throw "$ToolName $($Tool.Version): '$($file.FullName)' has signature status '$($signature.Status)'."
        }
    }

    $signer = (Get-AuthenticodeSignature -FilePath $manifest.FullName).SignerCertificate
    if ($Tool.SignerThumbprint -and $signer.Thumbprint -ne $Tool.SignerThumbprint) {
        throw "$ToolName $($Tool.Version) is signed by certificate $($signer.Thumbprint) ($($signer.Subject)), not the pinned $($Tool.SignerThumbprint)."
    }
    if ($Tool.SignerSubject -and $signer.Subject -notlike $Tool.SignerSubject) {
        throw "$ToolName $($Tool.Version) is signed by '$($signer.Subject)', not '$($Tool.SignerSubject)'."
    }
}

$tools = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'tools.psd1')
New-Item -Path $Path -ItemType Directory -Force | Out-Null

foreach ($toolName in @($tools.Keys | Sort-Object)) {
    if ($Name -and $toolName -notin $Name) { continue }

    $tool = $tools[$toolName]
    $moduleDir = Join-Path $Path "$toolName/$($tool.Version)"
    if (-not (Test-Path $moduleDir)) {
        # [x] is the NuGet range for exactly version x
        Save-PSResource -Name $toolName -Version "[$($tool.Version)]" -Repository PSGallery -TrustRepository -AuthenticodeCheck -Path $Path
    }

    Assert-ToolSignature -ToolName $toolName -Tool $tool -ModuleDir $moduleDir
    Write-Information "$toolName $($tool.Version): signature verified." -InformationAction Continue
}
