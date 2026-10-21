#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the StoredCredential module into out/StoredCredential.
.DESCRIPTION
    Compiles both target frameworks, copies the manifest, loader and license,
    and compiles docs/en-US/*.md to MAML help with Microsoft's platyPS module.
    Install platyPS first: Install-Module platyPS -RequiredVersion 0.14.2 -Scope CurrentUser
.PARAMETER SkipHelp
    Builds without help files, for machines without platyPS.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipHelp
)

$ErrorActionPreference = 'Stop'

$moduleName = 'StoredCredential'
$projectDir = Join-Path $PSScriptRoot "src/$moduleName"
$outDir = Join-Path $PSScriptRoot "out/$moduleName"
$frameworks = 'net462', 'netstandard2.0'

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

Copy-Item (Join-Path $projectDir "$moduleName.psd1"), (Join-Path $projectDir "$moduleName.psm1"), (Join-Path $PSScriptRoot 'License') $outDir

foreach ($framework in $frameworks) {
    $frameworkDir = New-Item (Join-Path $outDir $framework) -ItemType Directory
    Copy-Item (Join-Path $projectDir "bin/$Configuration/$framework/$moduleName.dll") $frameworkDir
}

if (-not $SkipHelp) {
    Import-Module platyPS -RequiredVersion 0.14.2
    # PowerShell looks for cmdlet help next to the assembly that defines the cmdlets
    foreach ($framework in $frameworks) {
        New-ExternalHelp -Path (Join-Path $PSScriptRoot 'docs/en-US') -OutputPath (Join-Path $outDir "$framework/en-US") -Force | Out-Null
    }
}

Write-Host "Module built in $outDir"
