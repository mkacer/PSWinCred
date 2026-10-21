# Loads the assembly built for the running PowerShell edition:
# net462 for Windows PowerShell 5.1, netstandard2.0 for PowerShell 7+.
if ($PSEdition -eq 'Core') {
    if (-not $IsWindows) {
        throw 'StoredCredential requires Windows: it uses the Windows Credential Manager API.'
    }
    $framework = 'netstandard2.0'
}
else {
    $framework = 'net462'
}

$assemblyPath = Join-Path -Path $PSScriptRoot -ChildPath "$framework\StoredCredential.dll"
$binaryModule = Import-Module -Name $assemblyPath -PassThru -ErrorAction Stop

Export-ModuleMember -Cmdlet @($binaryModule.ExportedCmdlets.Keys)
