# PowerShell Credential Manager
PowerShell Module to Read and Write Credentials from the Windows Credential Manager

## Ongoing Development and Support
This is a fork of the original CredentialManager module by Dave Garnar and runs natively on both Windows PowerShell 5.1 and PowerShell 7. It uses the same cmdlet names as the original module; see "New with version 4.0" for breaking changes.

## Requirements

* Windows
* Windows PowerShell 5.1 with .NET Framework 4.6.2 or later, or PowerShell 7 or later

The module ships two builds of TUN.CredentialManager.dll: `net462` for Windows PowerShell 5.1 and `netstandard2.0` for PowerShell 7. TUN.CredentialManager.psm1 loads the right one for the running edition. Neither build depends on PowerShell 7's compatibility layer for Windows PowerShell modules.

## Installation
### PowerShell Gallery Installation
The module is available on the PowerShell Gallery: https://www.powershellgallery.com/packages/TUN.CredentialManager.

1. PS> Save-Module -Name TUN.CredentialManager -Path <path>
2. PS> Install-Module -Name TUN.CredentialManager

### Manual Installation

1. Download TUN.CredentialManager.zip from the releases page of this repository.
2. Unzip it and copy the TUN.CredentialManager folder to a folder in `$env:PSModulePath`, for example `C:\Users\UserName\Documents\WindowsPowerShell\Modules`.
3. In your PowerShell session run the command `Import-Module TUN.CredentialManager`

## Usage

Import the module in to your PowerShell session and full help is available in the module with Get-Help. The help source is in [docs/en-US](docs/en-US).

### New with version 4.0
* Requires Windows PowerShell 5.1 or PowerShell 7. Windows PowerShell 3.0 and 4.0 are no longer supported.
* **Breaking:** `Get-StoredCredential -AsCredentialObject` no longer fills `Password` and `PasswordSize`. Add `-IncludeClearPassword` to get them. `-ExcludeClearPassword` has no effect anymore, except that it overrides `-IncludeClearPassword`.
* **Breaking:** `New-StoredCredential` requires a password through `-SecurePassword`, `-Password` or `-Credentials`. It no longer stores a random password when you give none. In an interactive session it prompts for `-SecurePassword`. To store a random password, use `-Password (Get-StrongPassword)`.
* **Breaking:** The `PaswordSize` property of Credential objects is renamed to `PasswordSize`.
* **Breaking:** PSCredentialManager.Api.dll and PSCredentialManager.Common.dll are merged into TUN.CredentialManager.dll. Namespaces and type names are unchanged.
* **Breaking:** Errors have meaningful IDs (`ReadFailed`, `EnumerateFailed`, `WriteFailed`, `PasswordTooLong`, `CredentialNotFound`, `DeleteFailed`, `TooManySpecialCharacters`) instead of `1`. `Remove-StoredCredential` reports a missing credential with the `ObjectNotFound` category.
* `Get-StoredCredential` returns credentials with an empty password as PSCredential objects instead of writing a warning.

## Building

You need the .NET SDK version from [global.json](global.json) and PowerShell 5.1 or 7.

```powershell
# Unit tests for both builds. The net462 tests need Windows.
dotnet test TUN.CredentialManager.slnx

# Help is compiled from docs/en-US with Microsoft's platyPS module.
Install-Module platyPS -RequiredVersion 0.14.2 -Scope CurrentUser
./build.ps1          # Writes the module to out/TUN.CredentialManager

# End-to-end tests against the real credential store. Run them in both editions.
powershell -File ./tests/Smoke.ps1
pwsh -File ./tests/Smoke.ps1
```

The version number MUST be changed in both `Directory.Build.props` and `src/TUN.CredentialManager/TUN.CredentialManager.psd1`. `build.ps1` fails when they differ.

GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs all of the above on every push and pull request. Pushing a tag that starts with `v` also creates a draft GitHub release with the module zip.

## Contributing

1. Fork it!
2. Create your feature branch: `git checkout -b my-new-feature`
3. Commit your changes: `git commit -am 'Add some feature'`
4. Push to the branch: `git push origin my-new-feature`
5. Submit a pull request :D

## History

### v4.0
- Requires Windows PowerShell 5.1 or PowerShell 7; separate native builds for each edition
- Breaking: clear-text password in Credential objects is opt-in with `-IncludeClearPassword`
- Breaking: New-StoredCredential requires a password
- Breaking: `PaswordSize` renamed to `PasswordSize`; helper assemblies merged into TUN.CredentialManager.dll; meaningful error IDs
- Zeroes and frees the unmanaged copy of the password after writing a credential
- Error records no longer contain the clear-text password
- Fixed: a failed read no longer writes an empty object; enumerated Credential objects are separate pipeline objects; an empty store returns nothing instead of an error; `LastWritten` could be off by about 7 minutes
- Get-StrongPassword uses only the cryptographic random number generator, without modulo bias
- SDK-style projects, MSTest instead of Visual Studio Fakes, platyPS instead of XmlDoc2CmdletDoc, GitHub Actions instead of AppVeyor; no third-party dependencies


### v3.0
- Extended possible password length to 1280 unicode characters (2560 bytes)
- Rewriting internal code to work mainly with secure strings if possible
- Expanded stored credential object with property SecurePassword to store password as secure string
- Expanded Get-StoredCredential with switches IncludeSecurePassword and ExcludeClearPassword
- Returning only clear password in New-StoredCredential if clear Password parameter was provided, but not if secure string SecurePassword parameter or PSCredential Credentials parameter was provided (in this case, only return secure string password in SecurePassword)
- Extended UnitTests with new test cases
- Better error messages if credentials to remove weren't found or if password in provided credentials object was too long

### v2.1
- Explicit naming of cmdlets to export in psd1 for Powershell Core compatibility
- Rewriting of some internal code to make it compatible with Powershell Core
- Expanded copyright notices and counted up version number
- Renamed module from CredentialManager to TUN.CredentialManager

### v2.0
- Implemented pipeline support for Get-StoredCredential.
- Implemented pipeline support for New-StoredCredential.
- Implemented pipeline support for Remove-StoredCredential.
- Improved error handling to respect the Error Action Preference in PowerShell.
- Changed AsPSCredential to a Switch parameter and renamed to AsCredentialObject on Get-StoredCredential to make it easier to use.
- Added Credentials parameter to New-StoredCredential which accepts a PSCredential object instead of User name and Password.
- Added SecuserPassword parameter to New-StoredCredential which accepts a SecureString as the password.
- Credential object now returns LastWritten as a DateTime instead of a ComType.FILETIME
- Changing license to MIT from GPL
- General refactoring and bug fixes. 

### v1.1 Bug Fix
Fixed a bug where the username specified in the -UserName parameter was not being used to create the credential in the store. The username for the logged on user was being used instead. Issue logged https://github.com/davotronic5000/PowerShell_Credential_Manager/issues/8


### v1.0 Initial Release
Implementing basic functionality
- Get-StoredCredential - Gets one or more credentials from the Windows Credential Manager.
- New-Stored Credential - Adds a new credential to the Windows Credential Manager.
- Remove-StoredCredential - Deletes a credential from the Windows Credential Manager.
- Get-StrongPassword - Randomly generates a new password.

## Credits

Written by Dave Garnar (@davotronic5000)
http://blog.davotronic5000.co.uk

Edited by Markus Szumovski (echalone)
https://github.com/echalone

## License


This software is licensed under the [The MIT License (MIT)](http://opensource.org/licenses/MIT).

	Copyright (C) 2016 Dave Garnar and 2022 Markus Szumovski

	Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
