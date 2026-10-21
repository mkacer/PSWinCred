#Requires -Version 5.1
<#
.SYNOPSIS
    Runs PSScriptAnalyzer on the repository's PowerShell files with PSScriptAnalyzerSettings.psd1.
.DESCRIPTION
    Fails when any rule reports an error or warning. Run tools/Install-Tools.ps1 first.
#>
[CmdletBinding()]
param(
    [string]$ToolsPath = (Join-Path $PSScriptRoot '../.tools')
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$version = (Import-PowerShellDataFile (Join-Path $PSScriptRoot 'tools.psd1')).PSScriptAnalyzer.Version
Import-Module (Join-Path $ToolsPath "PSScriptAnalyzer/$version/PSScriptAnalyzer.psd1") -Force

$excluded = '[\\/](\.tools|out|bin|obj|\.git)[\\/]'
$files = Get-ChildItem -Path $root -Recurse -File -Include '*.ps1', '*.psm1', '*.psd1' |
    Where-Object { $_.FullName -notmatch $excluded }

$settings = Join-Path $root 'PSScriptAnalyzerSettings.psd1'
$results = @(foreach ($file in $files) { Invoke-ScriptAnalyzer -Path $file.FullName -Settings $settings })

if ($results.Count -gt 0) {
    $results | Format-Table -Property Severity, RuleName, ScriptName, Line, Message -AutoSize -Wrap | Out-String -Width 200 |
        Write-Information -InformationAction Continue
    throw "PSScriptAnalyzer reported $($results.Count) issue(s)."
}
Write-Information "PSScriptAnalyzer: $($files.Count) files, no issues." -InformationAction Continue
