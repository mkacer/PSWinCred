# Loads the assembly built for the running PowerShell edition:
# net462 for Windows PowerShell 5.1, net8.0 for PowerShell 7.4 and later.
if ($PSEdition -eq 'Core') {
    if (-not $IsWindows) {
        throw 'WinCred requires Windows: it is an interface to Windows Credential Manager.'
    }
    if ($PSVersionTable.PSVersion -lt [version]'7.4') {
        throw "WinCred requires PowerShell 7.4 or later, or Windows PowerShell 5.1. This is PowerShell $($PSVersionTable.PSVersion)."
    }
    $framework = 'net8.0'
}
else {
    $framework = 'net462'
}

$assemblyPath = Join-Path -Path $PSScriptRoot -ChildPath "$framework\WinCred.dll"
$binaryModule = Import-Module -Name $assemblyPath -PassThru -ErrorAction Stop

Export-ModuleMember -Cmdlet @($binaryModule.ExportedCmdlets.Keys)
