#
# Module manifest for module 'TUN.CredentialManager'
#

@{

# Script module that loads the assembly for the running PowerShell edition
RootModule = 'TUN.CredentialManager.psm1'

# Version number of this module. Keep in sync with <Version> in Directory.Build.props.
ModuleVersion = '4.0.0'

# Supported PSEditions
CompatiblePSEditions = @('Desktop', 'Core')

# ID used to uniquely identify this module
GUID = 'f2acf888-651e-4495-b950-b820b737920f'

# Author of this module
Author = 'Dave Garnar and Markus Szumovski'

# Company or vendor of this module
CompanyName = 'http://blog.davotronic5000.co.uk (Dave Garnar) and ThingsUNeed (https://github.com/echalone , Markus Szumovski)'

# Copyright statement for this module
Copyright = '(c) 2016 Dave Garnar and 2022 Markus Szumovski. All rights reserved.'

# Description of the functionality provided by this module
Description = 'Provides access to credentials in the Windows Credential Manager (continuation of CredentialManager by Dave Garnar)'

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

# Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        Tags = 'Windows','Credential','Credentials','Manager','CredentialManager','CredentialsManager','WindowsCredential','WindowsCredentials','WindowsCredentialManager','WindowsCredentialsManager','StoredCredentials','PSEdition_Desktop','PSEdition_Core'

        # A URL to the license for this module.
        LicenseUri = 'https://github.com/echalone/PowerShell_Credential_Manager/blob/master/License'

        # A URL to the main website for this project.
        ProjectUri = 'https://github.com/echalone/PowerShell_Credential_Manager'

        # A URL to an icon representing this module.
        IconUri = 'https://raw.githubusercontent.com/echalone/TUN/master/Media/Icons/ThingsUNeed.ico'

        # ReleaseNotes of this module
        ReleaseNotes = 'V 2.0: Initial version by Dave Garnar (up to versoin 2.0, also named CredentialManager instead of TUN.CredentialManager)
V 2.1: Made Powershell Core compatible and changed name of module to TUN.CredentialManager
V 3.0: Using secure strings internally when possible and extending password length to 1280 unicode characters or 2560 bytes
V 3.0.1: Bumped version number for more consistant handling of version by powershell
V 3.0.2: Fix in help of Type parameter
V 4.0.0: Requires PowerShell 5.1 or 7+. Separate native builds for Windows PowerShell and PowerShell 7. Breaking: Get-StoredCredential -AsCredentialObject no longer fills Password unless -IncludeClearPassword is given; New-StoredCredential requires a password; property PaswordSize renamed to PasswordSize; helper assemblies merged into TUN.CredentialManager.dll. See README for all changes.'

    } # End of PSData hashtable

} # End of PrivateData hashtable

}
