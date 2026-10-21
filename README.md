# WinCred
A modern, secure, auditable PowerShell interface to Windows Credential Manager.

WinCred talks to the native Credential Manager API (`CredReadW`, `CredWriteW`, `CredDeleteW`, `CredEnumerateW`, `CredFree`) and nothing else. Windows owns the credential store and its format; WinCred adds no storage of its own. Other programs, in any language, can use the same credentials through the same API: **Credential Manager is the shared interface.**

WinCred is intentionally small. It is not a secret-management framework, an encrypted-file library or a vault abstraction.

## Requirements

* Windows
* Windows PowerShell 5.1 with .NET Framework 4.6.2 or later, or PowerShell 7.4 or later

The module ships two builds of WinCred.dll: `net462` for Windows PowerShell 5.1 and `net8.0` for PowerShell 7.4+. WinCred.psm1 loads the right one for the running edition. Neither build uses PowerShell 7's Windows PowerShell compatibility session. WinCred has no runtime dependencies.

## Installation

1. Download WinCred.zip from the releases page of this repository.
2. Unzip it and copy the WinCred folder to a folder in `$env:PSModulePath`.
3. Run `Import-Module WinCred`.

## Usage

```powershell
# Store a credential. The secret comes from a prompt, never from plain text on the command line.
Get-Credential -UserName svc-backup | Set-WinCredential -Target backup01 -Persist LocalMachine

# Store a token without a user name; it lasts until logoff (the default)
Set-WinCredential -Target api.example.com -Secret (Read-Host -AsSecureString -Prompt Token)

# List credentials: no secrets are read
Get-WinCredential -Target 'backup*'

# Use one as a PSCredential
Invoke-Command -ComputerName backup01 -Credential (Get-WinCredential -Target backup01 -AsPSCredential) -ScriptBlock { hostname }

# Preview, then delete
Get-WinCredential -Target 'test.*' | Remove-WinCredential -WhatIf
Remove-WinCredential -Target backup01
```

Full help: `Get-Help Get-WinCredential -Full` and `Get-Help about_WinCred`. The help source is in [docs/en-US](docs/en-US) and [src/WinCred/en-US](src/WinCred/en-US).

## Security defaults

* `Set-WinCredential` requires a secret as a SecureString or PSCredential. It fails without one; it never prompts and never generates one. There is no plain-text secret parameter.
* New credentials persist for the logon session. Longer persistence MUST be requested with `-Persist`.
* `Get-WinCredential` reads no secret unless you add `-IncludeSecret` or `-AsPSCredential`, and never returns plain text.
* Secrets never appear in messages, errors or formatted output. Unmanaged copies are zeroed before they are freed, and every buffer from Windows is released with `CredFree`.
* Invalid input fails before anything is written. Windows' limits are checked up front: target 32767 characters (Generic) or 337 (DomainPassword), user name 513, comment 256, secret 2560 bytes after encoding.

## Encoding

Windows stores the secret as opaque bytes. WinCred uses Unicode (UTF-16LE) by default, which is what Windows and most programs use. Programs that share credentials with WinCred MUST agree on the encoding; use `-Encoding UTF8` for programs that store UTF-8.

## Migrating from CredentialManager or TUN.CredentialManager

| CredentialManager / TUN.CredentialManager | WinCred |
|---|---|
| `Import-Module TUN.CredentialManager` | `Import-Module WinCred` |
| `Get-StoredCredential -Target x` (PSCredential) | `Get-WinCredential -Target x -AsPSCredential` |
| `Get-StoredCredential -Target x -AsCredentialObject` | `Get-WinCredential -Target x` (no secret) or `-IncludeSecret` |
| `.Password` (plain text) on credential objects | `[System.Net.NetworkCredential]::new('', $c.Secret).Password`, in your own code |
| `Get-StoredCredential` (all) | `Get-WinCredential`; `-Target` accepts wildcards |
| `New-StoredCredential -Target x -UserName u -Password p` | `Set-WinCredential -Target x -UserName u -Secret (Read-Host -AsSecureString)` |
| `New-StoredCredential -Credentials $c` | `Set-WinCredential -Credential $c` or `$c \| Set-WinCredential -Target x` |
| `New-StoredCredential` without a password (stored a random one) | Fails with `MissingSecret` |
| `-Target` defaulted to the computer name | `-Target` is required |
| `-UserName` defaulted to the current user; `-Comment` to "Updated by ..." | No defaults: omitted means not stored |
| `New-StoredCredential` returned the credential | `Set-WinCredential` returns nothing; add `-PassThru` |
| `Remove-StoredCredential -Target x` | `Remove-WinCredential -Target x`, or pipe from `Get-WinCredential` |
| `Get-StrongPassword` | Removed: generating passwords is outside this module's scope |
| `CredType` values `Maximum`, `MaximumEx` | Removed: they are not credential types |

## Development

You need the .NET SDK from [global.json](global.json), and Windows for the tests that touch the credential store.

```powershell
dotnet test WinCred.slnx                   # Native-layer unit tests (MSTest); net462 needs Windows
./tools/Install-Tools.ps1                  # Pinned Pester, PSScriptAnalyzer and platyPS into .tools, signatures verified
./tools/Invoke-ScriptAnalysis.ps1          # PSScriptAnalyzer with PSScriptAnalyzerSettings.psd1
./build.ps1                                # Module with help into out/WinCred

# Pester tests in both editions. They only touch targets named WinCred.Test.<guid>.* and remove them again.
powershell -NoProfile -NonInteractive -File ./tests/Invoke-Tests.ps1
pwsh -NoProfile -NonInteractive -File ./tests/Invoke-Tests.ps1
pwsh -File ./tests/Invoke-Tests.ps1 -ExcludeTag Store   # Without touching your credential store
```

Tool versions are pinned in [tools/tools.psd1](tools/tools.psd1). The version number MUST be changed in both `Directory.Build.props` and `src/WinCred/WinCred.psd1`; `build.ps1` fails when they differ.

GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs the unit tests, PSScriptAnalyzer and the build, then the Pester tests in Windows PowerShell 5.1 and PowerShell 7.4, 7.5 and 7.6. A tag starting with `v` creates a draft release with WinCred.zip.

## History

### WinCred 1.0.0
- New identity: module WinCred with cmdlets Get-, Set- and Remove-WinCredential, and a new GUID
- A thin layer over the native CredMan API: SafeHandle-owned buffers, `CREDENTIAL_ATTRIBUTEW` attributes read into managed objects, Win32 error messages
- Secure defaults: required secret, no plain-text input or output, Session persistence, no generated or implied values
- `-Encoding` (Unicode or UTF8), `-WhatIf`/`-Confirm`, `-PassThru`, wildcard targets, pipeline from Get- to Remove-WinCredential
- Size limits checked before writing; oversized secrets never reach Windows
- Builds for net462 (Windows PowerShell 5.1) and net8.0 (PowerShell 7.4+); no runtime dependencies
- Tests: MSTest for the native layer, Pester 6 against the real credential store in both editions; PSScriptAnalyzer; pinned, signature-checked tools

### TUN.CredentialManager history

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

Maintained as WinCred by Michael Kacer (mkacer)
https://github.com/mkacer

## License


This software is licensed under the [The MIT License (MIT)](http://opensource.org/licenses/MIT).

	Copyright (C) 2016 Dave Garnar, 2022 Markus Szumovski and 2026 Michael Kacer

	Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
