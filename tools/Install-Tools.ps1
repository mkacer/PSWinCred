#Requires -Version 5.1
<#
.SYNOPSIS
    Saves the pinned build and test tools to .tools and verifies their signatures.
.DESCRIPTION
    Reads the pinned versions and signers from tools/tools.psd1. Every file of a tool, except help and
    package metadata, MUST carry a valid Authenticode signature from the pinned signer; see Tools.psm1.
    Any other result stops the script and lists every failing file. Import-Tool repeats the check
    whenever a tool is imported.

    Uses PSResourceGet, which ships with PowerShell 7.4 and later. Run this script with pwsh; the saved tools
    then work in both editions. To run it in Windows PowerShell 5.1 instead, install PSResourceGet first:
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

Import-Module (Join-Path $PSScriptRoot 'Tools.psm1') -Force

if (-not (Get-Command Save-PSResource -ErrorAction SilentlyContinue)) {
    throw ("Install-Tools.ps1 needs PSResourceGet (Save-PSResource), which ships with PowerShell 7.4 and later. " +
        "Run 'pwsh -NoProfile -File ./tools/Install-Tools.ps1'; the saved tools then work in Windows PowerShell 5.1 too. " +
        "Or install it for Windows PowerShell: Install-Module Microsoft.PowerShell.PSResourceGet -Scope CurrentUser")
}

$tools = Get-ToolManifest
New-Item -Path $Path -ItemType Directory -Force | Out-Null

foreach ($toolName in @($tools.Keys | Sort-Object)) {
    if ($Name -and $toolName -notin $Name) { continue }

    $tool = $tools[$toolName]
    $moduleDir = Join-Path $Path "$toolName/$($tool.Version)"
    if (-not (Test-Path $moduleDir)) {
        # [x] is the NuGet range for exactly version x
        Save-PSResource -Name $toolName -Version "[$($tool.Version)]" -Repository PSGallery -TrustRepository -AuthenticodeCheck -Path $Path
    }

    Assert-ToolSignature -Name $toolName -ToolsPath $Path
    Write-Information "$toolName $($tool.Version): signatures verified." -InformationAction Continue
}
