#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the WinCred module into out/WinCred.
.DESCRIPTION
    Compiles both target frameworks, copies the manifest, loader, format file, about topic and license,
    and compiles docs/en-US/*.md to MAML help with Microsoft.PowerShell.PlatyPS from .tools.
    Run tools/Install-Tools.ps1 first.
.PARAMETER SkipHelp
    Builds without cmdlet help, for machines without Microsoft.PowerShell.PlatyPS.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ToolsPath = (Join-Path $PSScriptRoot '.tools'),

    [switch]$SkipHelp
)

$ErrorActionPreference = 'Stop'

$moduleName = 'WinCred'
$projectDir = Join-Path $PSScriptRoot "src/$moduleName"
$outDir = Join-Path $PSScriptRoot "out/$moduleName"
$frameworks = 'net462', 'net8.0'

# The manifest and the assembly MUST carry the same version
$manifestVersion = (Import-PowerShellDataFile (Join-Path $projectDir "$moduleName.psd1")).ModuleVersion
$propsVersion = ([xml](Get-Content -Raw (Join-Path $PSScriptRoot 'Directory.Build.props'))).Project.PropertyGroup.Version
if ($manifestVersion -ne $propsVersion) {
    throw "ModuleVersion '$manifestVersion' in $moduleName.psd1 does not match Version '$propsVersion' in Directory.Build.props."
}

dotnet build $projectDir --configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item $outDir -ItemType Directory | Out-Null

Copy-Item -Path (Join-Path $projectDir "$moduleName.psd1"), (Join-Path $projectDir "$moduleName.psm1"),
    (Join-Path $projectDir "$moduleName.Format.ps1xml"), (Join-Path $PSScriptRoot 'License') -Destination $outDir

# About topics are found in the module's own culture folder
Copy-Item -Path (Join-Path $projectDir 'en-US') -Destination $outDir -Recurse

foreach ($framework in $frameworks) {
    $frameworkDir = New-Item (Join-Path $outDir $framework) -ItemType Directory
    Copy-Item (Join-Path $projectDir "bin/$Configuration/$framework/$moduleName.dll") $frameworkDir
}

if (-not $SkipHelp) {
    $platyPS = 'Microsoft.PowerShell.PlatyPS'
    $platyPSVersion = (Import-PowerShellDataFile (Join-Path $PSScriptRoot 'tools/tools.psd1')).$platyPS.Version
    Import-Module (Join-Path $ToolsPath "$platyPS/$platyPSVersion/$platyPS.psd1") -Force

    $markdown = @(Get-ChildItem (Join-Path $PSScriptRoot 'docs/en-US') -Filter '*.md').FullName
    $invalid = @(Test-MarkdownCommandHelp -Path $markdown -DetailView | Where-Object { -not $_.IsValid })
    if ($invalid.Count -gt 0) {
        throw "Invalid help markdown:`n$(($invalid | ForEach-Object { "$($_.Path): $($_.Messages -join '; ')" }) -join "`n")"
    }

    # Export-MamlCommandHelp writes <folder>/WinCred/WinCred.dll-Help.xml.
    # Cmdlet help is found next to the assembly that defines the cmdlets.
    $mamlDir = Join-Path $outDir 'maml'
    Import-MarkdownCommandHelp -Path $markdown | Export-MamlCommandHelp -OutputFolder $mamlDir -Force | Out-Null
    foreach ($framework in $frameworks) {
        $helpDir = New-Item (Join-Path $outDir "$framework/en-US") -ItemType Directory
        Copy-Item (Join-Path $mamlDir "$moduleName/$moduleName.dll-Help.xml") $helpDir
    }
    Remove-Item $mamlDir -Recurse -Force
}

Write-Information "Module built in $outDir" -InformationAction Continue
