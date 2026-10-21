#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the Pester tests against the built module in the current PowerShell edition.
.DESCRIPTION
    Imports the pinned Pester from .tools (run tools/Install-Tools.ps1 first) and writes NUnit XML results
    to out/TestResults. Run it once in powershell.exe and once in pwsh.exe.
.PARAMETER ExcludeTag
    Skips tests with these tags. 'Store' skips every test that touches the credential store;
    'Help' skips the help tests for a module built with -SkipHelp.
#>
[CmdletBinding()]
param(
    [string]$ModulePath,

    [string]$ToolsPath,

    [string]$ResultPath,

    [string[]]$ExcludeTag
)

$ErrorActionPreference = 'Stop'

# Defaults are set here, not in param(): Windows PowerShell 5.1 leaves $PSScriptRoot empty
# while it evaluates parameter defaults of a script run with -File.
if (-not $ModulePath) { $ModulePath = Join-Path $PSScriptRoot '../out/WinCred/WinCred.psd1' }
if (-not $ToolsPath) { $ToolsPath = Join-Path $PSScriptRoot '../.tools' }
if (-not $ResultPath) { $ResultPath = Join-Path $PSScriptRoot '../out/TestResults' }

$pesterVersion = (Import-PowerShellDataFile (Join-Path $PSScriptRoot '../tools/tools.psd1')).Pester.Version
Import-Module (Join-Path $ToolsPath "Pester/$pesterVersion/Pester.psd1") -Force

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
if ($ExcludeTag) {
    $configuration.Filter.ExcludeTag = $ExcludeTag
}

Invoke-Pester -Configuration $configuration
