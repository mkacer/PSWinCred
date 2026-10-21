#
# Module manifest for module 'StoredCredential'
#

@{

# Script module that loads the assembly for the running PowerShell edition
RootModule = 'StoredCredential.psm1'

# Version number of this module. Keep in sync with <Version> in Directory.Build.props.
ModuleVersion = '4.0.0'

# Supported PSEditions
CompatiblePSEditions = @('Desktop', 'Core')

# ID used to uniquely identify this module
GUID = '3cdddf68-dff4-4409-81c8-f0b7d627824d'

# Author of this module
Author = 'Michael Kacer, Dave Garnar, Markus Szumovski'

# Copyright statement for this module
Copyright = '(c) 2016 Dave Garnar, 2022 Markus Szumovski, 2026 Michael Kacer. MIT License.'

# Description of the functionality provided by this module
Description = 'Reads, writes and deletes credentials in the Windows Credential Manager. Fork of TUN.CredentialManager by Markus Szumovski and CredentialManager by Dave Garnar.'

# Minimum version of the PowerShell engine required by this module
PowerShellVersion = '5.1'

# Minimum version of the .NET Framework required by this module. Applies to Windows PowerShell only.
DotNetFrameworkVersion = '4.6.2'

# Cmdlets to export from this module
CmdletsToExport = @(
    'Get-StoredCredential'
    'New-StoredCredential'
    'Remove-StoredCredential'
    'Get-StrongPassword'
)

FunctionsToExport = @()
VariablesToExport = @()
AliasesToExport = @()

# Private data to pass to the module specified in RootModule. The PSData hashtable holds metadata used by online galleries.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        Tags = 'Windows','Credential','Credentials','CredentialManager','WindowsCredentialManager','StoredCredential','PSEdition_Desktop','PSEdition_Core'

        # A URL to the license for this module.
        LicenseUri = 'https://github.com/mkacer/PowerShell_Credential_Manager/blob/master/License'

        # A URL to the main website for this project.
        ProjectUri = 'https://github.com/mkacer/PowerShell_Credential_Manager'

        # ReleaseNotes of this module
        ReleaseNotes = 'https://github.com/mkacer/PowerShell_Credential_Manager/blob/master/README.md#history'

    } # End of PSData hashtable

} # End of PrivateData hashtable

}
