#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the Pester tests against the built module in the current PowerShell edition.
.DESCRIPTION
    Verifies and imports the pinned Pester from .tools (run tools/Install-Tools.ps1 first) and writes NUnit XML results
    to out/TestResults. Run it once in powershell.exe and once in pwsh.exe.
.PARAMETER ExcludeTag
    Skips tests with these tags. 'Store' skips every test that touches the credential store;
    'Help' skips the help tests for a module built with -SkipHelp.
.PARAMETER IncludePersistent
    Also runs the tests tagged 'Persistent' and 'Roaming'. They write LocalMachine credentials, which outlive
    the logon session, and Enterprise credentials, which can roam to other computers with the user profile.
    The tests remove them, but an interrupted run can leave them behind until the next run.
#>
[CmdletBinding()]
param(
    [string]$ModulePath,

    [string]$ToolsPath,

    [string]$ResultPath,

    [string[]]$ExcludeTag,

    [switch]$IncludePersistent
)

$ErrorActionPreference = 'Stop'

# Defaults are set here, not in param(): Windows PowerShell 5.1 leaves $PSScriptRoot empty
# while it evaluates parameter defaults of a script run with -File.
if (-not $ModulePath) { $ModulePath = Join-Path $PSScriptRoot '../out/WinCred/WinCred.psd1' }
if (-not $ToolsPath) { $ToolsPath = Join-Path $PSScriptRoot '../.tools' }
if (-not $ResultPath) { $ResultPath = Join-Path $PSScriptRoot '../out/TestResults' }

Import-Module (Join-Path $PSScriptRoot '../tools/Tools.psm1') -Force
Import-Tool -Name Pester -ToolsPath $ToolsPath

New-Item -Path $ResultPath -ItemType Directory -Force | Out-Null
$edition = "$($PSVersionTable.PSEdition)-$($PSVersionTable.PSVersion)"

$configuration = New-PesterConfiguration
$configuration.Run.Container = New-PesterContainer -Path (Join-Path $PSScriptRoot 'WinCred.Tests.ps1') -Data @{ ModulePath = (Resolve-Path $ModulePath).Path }
$configuration.Run.Exit = $true
$configuration.Output.Verbosity = 'Detailed'
$configuration.TestResult.Enabled = $true
$configuration.TestResult.OutputFormat = 'NUnitXml'
$configuration.TestResult.OutputPath = Join-Path $ResultPath "WinCred-$edition.xml"
$configuration.TestResult.TestSuiteName = "WinCred ($edition)"
$excluded = @($ExcludeTag | Where-Object { $_ })
if (-not $IncludePersistent) {
    $excluded += 'Persistent', 'Roaming'
}
if ($excluded.Count -gt 0) {
    $configuration.Filter.ExcludeTag = $excluded
}

Invoke-Pester -Configuration $configuration
