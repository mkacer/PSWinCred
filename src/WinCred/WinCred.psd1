#
# Module manifest for module 'WinCred'
#

@{

# Script module that loads the assembly for the running PowerShell edition
RootModule = 'WinCred.psm1'

# Version number of this module. Keep in sync with <Version> in Directory.Build.props.
ModuleVersion = '1.0.0'

# Supported PSEditions
CompatiblePSEditions = @('Desktop', 'Core')

# ID used to uniquely identify this module
GUID = '174a9137-78e3-43f8-803a-81d4c6131dc2'

# Author of this module
Author = 'Michael Kacer, Dave Garnar, Markus Szumovski'

# Copyright statement for this module
Copyright = '(c) 2016 Dave Garnar, 2022 Markus Szumovski, 2026 Michael Kacer. MIT License.'

# Description of the functionality provided by this module
Description = 'A secure, thin PowerShell interface to Windows Credential Manager (CredMan). Successor of CredentialManager by Dave Garnar and TUN.CredentialManager by Markus Szumovski.'

# Minimum version of the PowerShell engine required by this module. PowerShell 7 needs 7.4 or later; WinCred.psm1 checks it.
PowerShellVersion = '5.1'

# Minimum version of the .NET Framework required by this module. Applies to Windows PowerShell only.
DotNetFrameworkVersion = '4.6.2'

# Format files (.ps1xml) to be loaded when importing this module
FormatsToProcess = @('WinCred.Format.ps1xml')

# Cmdlets to export from this module
CmdletsToExport = @(
    'Get-WinCredential'
    'New-WinCredential'
    'Set-WinCredential'
    'Remove-WinCredential'
)

FunctionsToExport = @()
VariablesToExport = @()
AliasesToExport = @()

# Private data to pass to the module specified in RootModule. The PSData hashtable holds metadata used by online galleries.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        Tags = 'Windows','Credential','Credentials','CredentialManager','CredMan','WinCred','Security','PSEdition_Desktop','PSEdition_Core'

        # A URL to the license for this module.
        LicenseUri = 'https://github.com/mkacer/PSWinCred/blob/master/License'

        # A URL to the main website for this project.
        ProjectUri = 'https://github.com/mkacer/PSWinCred'

        # ReleaseNotes of this module
        ReleaseNotes = 'https://github.com/mkacer/PSWinCred/blob/master/README.md#history'

    } # End of PSData hashtable

} # End of PrivateData hashtable

}
