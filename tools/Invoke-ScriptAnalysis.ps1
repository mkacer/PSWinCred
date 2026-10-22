#Requires -Version 5.1
<#
.SYNOPSIS
    Runs PSScriptAnalyzer on the repository's PowerShell files with PSScriptAnalyzerSettings.psd1.
.DESCRIPTION
    Fails when any rule reports an error or warning. Run tools/Install-Tools.ps1 first.
#>
[CmdletBinding()]
param(
    [string]$ToolsPath
)

$ErrorActionPreference = 'Stop'

# Defaults are set here, not in param(): Windows PowerShell 5.1 leaves $PSScriptRoot empty
# while it evaluates parameter defaults of a script run with -File.
if (-not $ToolsPath) { $ToolsPath = Join-Path $PSScriptRoot '../.tools' }

$root = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'Tools.psm1') -Force
Import-Tool -Name PSScriptAnalyzer -ToolsPath $ToolsPath

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
