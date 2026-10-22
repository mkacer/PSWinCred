@{
    Severity = @('Error', 'Warning')

    Rules    = @{
        # The module and its scripts MUST run on Windows PowerShell 5.1 and PowerShell 7.4+
        PSUseCompatibleSyntax = @{
            Enable         = $true
            TargetVersions = @('5.1', '7.4')
        }
    }
}
