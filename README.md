# WinCred
A modern, secure, auditable PowerShell interface to Windows Credential Manager.

The module is named **WinCred** (`Import-Module WinCred`); this repository is [PSWinCred](https://github.com/mkacer/PSWinCred).

WinCred talks to the native Credential Manager API (`CredReadW`, `CredWriteW`, `CredDeleteW`, `CredEnumerateW`, `CredFree`) and nothing else. Windows owns the credential store and its format; WinCred adds no storage of its own. Other programs, in any language, can use the same credentials through the same API: **Credential Manager is the shared interface.**

WinCred is intentionally small. It is not a secret-management framework, an encrypted-file library or a vault abstraction.

## Requirements

* Windows
* Windows PowerShell 5.1 with .NET Framework 4.6.2 or later, or a supported PowerShell 7 release (see below)

Supported PowerShell versions: Windows PowerShell 5.1, the supported LTS releases of PowerShell 7, and any supported non-LTS release newer than the latest LTS. Today that is 7.6 LTS, and 7.4 LTS until it reaches end of support on 10 November 2026. Other 7.x releases from 7.4 on can load the module but are not tested.

The module ships two builds of WinCred.dll: `net462` for Windows PowerShell 5.1 and `net8.0` for PowerShell 7.4+. WinCred.psm1 loads the right one for the running edition. Neither build uses PowerShell 7's Windows PowerShell compatibility session. WinCred has no runtime dependencies.

## Installation

1. Download WinCred.zip from the [releases page](https://github.com/mkacer/PSWinCred/releases).
2. Clear the downloaded-from-the-internet mark before you unzip it: `Unblock-File .\WinCred.zip`. Windows copies the mark to every extracted file, and then the RemoteSigned execution policy blocks the module's script files, and Windows PowerShell 5.1 may refuse to load WinCred.dll.
3. Unzip it and copy the WinCred folder to a folder in `$env:PSModulePath`, for example `$HOME\Documents\PowerShell\Modules` (PowerShell 7) or `$HOME\Documents\WindowsPowerShell\Modules` (Windows PowerShell 5.1).
4. Run `Import-Module WinCred`.

### Uninstallation

1. Close every PowerShell session that imported WinCred, or run `Remove-Module WinCred` in each.
2. Delete the WinCred folder you copied: `(Get-Module WinCred -ListAvailable).ModuleBase | Remove-Item -Recurse` lists and removes every copy PowerShell can find.

Uninstalling WinCred does not remove the credentials you stored with it; they belong to Windows Credential Manager. Remove them first with `Remove-WinCredential` if you no longer need them.

## Usage

```powershell
# Create a credential. The secret comes from a prompt, never from plain text on the command line.
Get-Credential -UserName svc-backup | New-WinCredential -Target backup01 -Persist LocalMachine

# Create a token without a user name; it lasts until logoff (the default)
New-WinCredential -Target api.example.com -Secret (Read-Host -AsSecureString -Prompt Token)

# Change only the secret; user name, persistence and comment stay as they are
Set-WinCredential -Target backup01 -Secret (Read-Host -AsSecureString -Prompt 'New password')

# List credentials: no secrets are returned
Get-WinCredential -Target 'backup*'

# Use one as a PSCredential
Invoke-Command -ComputerName backup01 -Credential (Get-WinCredential -Target backup01 -AsPSCredential) -ScriptBlock { hostname }

# Preview, then delete. Set- and Remove-WinCredential ask for confirmation unless -Confirm:$false is given.
Get-WinCredential -Target 'test.*' | Remove-WinCredential -WhatIf
Remove-WinCredential -Target backup01
```

Full help: `Get-Help Get-WinCredential -Full` and `Get-Help about_WinCred`. The help source is in [docs/en-US](docs/en-US) and [src/WinCred/en-US](src/WinCred/en-US).

## Security defaults

* `New-WinCredential` requires a secret as a SecureString or PSCredential. It fails without one; it never prompts and never generates one. There is no plain-text secret parameter.
* `New-WinCredential` fails if the credential exists. `Set-WinCredential` fails if it does not, and changes only the fields you name: without a new secret, Windows keeps the stored one and WinCred never decodes it.
* New credentials persist for the logon session. Longer persistence MUST be requested with `-Persist`.
* `Set-WinCredential` and `Remove-WinCredential` ask for confirmation by default.
* `Get-WinCredential` returns no secret unless you add `-IncludeSecret` or `-AsPSCredential`, and no attribute values unless you add `-IncludeSecret`. It never returns a secret as plain text. An exact target is read with `CredReadW`, so Windows decrypts only credentials with exactly that name: one per type, or only the type you name with `-Type`.
* The secret never appears in messages, errors or formatted output, and messages do not reveal its length. Attribute values, returned only with `-IncludeSecret`, are plain bytes: formatting or serializing a credential shows them, so clear them (`[Array]::Clear`) when they may be secret. WinCred zeroes the unmanaged copies of a secret that it makes, and every decrypted secret and attribute value in a buffer from Windows, before freeing them. Every buffer from Windows is released with `CredFree`.
* Secrets are never altered to fit an encoding: invalid text fails on write, and invalid bytes fail on read.
* Invalid input fails before the store is touched. Windows' limits are checked up front (see [Limits](#limits)), and so are control characters and the `DOMAIN\user` or `user@domain` form of DomainPassword user names.

## What WinCred cannot protect against

Credential Manager protects credentials at rest for a user account. It does not separate programs that run as that user.

* Any program running as the same user can read the secret of every Generic credential of that user, with or without WinCred.
* An administrator, or a program running as SYSTEM, can get the secrets of other users, for example by reading process memory.
* A SecureString limits how long plain text sits in memory, but it is not a secret boundary. PowerShell, .NET and the programs that use a credential make plain-text copies that WinCred cannot zero. Microsoft [recommends against SecureString](https://github.com/dotnet/platform-compat/blob/master/docs/DE0001.md) for new .NET code.
* Converting a secret to plain text creates a string that stays in memory until the garbage collector reuses it.
* Windows decrypts every credential that `CredEnumerateW` returns. `Get-WinCredential` without `-Target`, or with a wildcard that has no literal ASCII prefix (the prefix ends at the first wildcard or non-ASCII character), decrypts the user's whole store into the process before WinCred zeroes the secrets it does not return. `-Type` does not narrow what Windows decrypts for a wildcard.
* `Set-WinCredential` reads the stored credential to keep the fields you do not change. For a Generic credential, Windows decrypts its secret into the process during that read, even when the secret does not change; WinCred does not decode it and zeroes it before freeing it.
* `Enterprise` credentials roam with the user profile, so copies end up on other computers and in profile storage.
* Windows has no create-only or compare-and-swap write. Another program can change a credential between `New-WinCredential`'s existence check and its write, or between `Set-WinCredential`'s read and its write.

## Encoding

Windows stores the secret as opaque bytes and does not record their encoding. WinCred uses Unicode (UTF-16LE) by default, which is what Windows and most programs use. Programs that share credentials with WinCred MUST agree on the encoding; use `-Encoding UTF8` for programs that store UTF-8. WinCred reports bytes that are not valid in the chosen encoding, but many wrong encodings still decode without error and give a garbled secret.

## Limits

Measured on Windows; WinCred checks them before writing.

| Field | Limit |
|---|---|
| Target, Generic | 32766 characters |
| Target, DomainPassword | A host name: 253 bytes in UTF-8, labels of 63 bytes |
| User name | 512 characters |
| Comment | 255 characters |
| Secret | 2560 bytes after encoding: 1280 characters in Unicode |

The character limits are one less than the `wincred.h` constants, which count the terminating null character. Windows returns the secret of DomainPassword and DomainCertificate credentials only to the Local Security Authority, so WinCred can write DomainPassword credentials but never read their secret.

## Migrating from CredentialManager or TUN.CredentialManager

| CredentialManager / TUN.CredentialManager | WinCred |
|---|---|
| `Import-Module TUN.CredentialManager` | `Import-Module WinCred` |
| `Get-StoredCredential -Target x` (PSCredential) | `Get-WinCredential -Target x -AsPSCredential` |
| `Get-StoredCredential -Target x -AsCredentialObject` | `Get-WinCredential -Target x` (no secret) or `-IncludeSecret` |
| `.Password` (plain text) on credential objects | `[System.Net.NetworkCredential]::new('', $c.Secret).Password`, in your own code |
| `Get-StoredCredential` (all) | `Get-WinCredential`; `-Target` accepts wildcards, `-LiteralTarget` does not |
| `New-StoredCredential -Target x -UserName u -Password p` | `New-WinCredential -Target x -UserName u -Secret (Read-Host -AsSecureString)` |
| `New-StoredCredential -Credentials $c` | `New-WinCredential -Target x -Credential $c` or `$c \| New-WinCredential -Target x` |
| `New-StoredCredential` over an existing credential (replaced every field) | `New-WinCredential` fails with `CredentialExists`; `Set-WinCredential` changes only the fields you name |
| `New-StoredCredential` without a password (stored a random one) | Fails with `MissingSecret` |
| `-Target` defaulted to the computer name | `-Target` is required |
| `-UserName` defaulted to the current user; `-Comment` to "Updated by ..." | No defaults: omitted means not stored |
| `New-StoredCredential` returned the credential | `New-` and `Set-WinCredential` return nothing; add `-PassThru` |
| `Remove-StoredCredential -Target x` | `Remove-WinCredential -Target x`, which asks for confirmation; or pipe from `Get-WinCredential` |
| `Get-StrongPassword` | Removed: generating passwords is outside this module's scope |
| `CredType` values `Maximum`, `MaximumEx` | Removed: they are not credential types |

## Development

You need the .NET SDK from [global.json](global.json), and Windows for the tests that touch the credential store.

```powershell
dotnet test --solution WinCred.slnx        # Native-layer unit tests (MSTest on Microsoft.Testing.Platform); net462 needs Windows
pwsh -File ./tools/Install-Tools.ps1       # Pinned Pester, PSScriptAnalyzer and PlatyPS into .tools, signatures verified
./tools/Invoke-ScriptAnalysis.ps1          # PSScriptAnalyzer with PSScriptAnalyzerSettings.psd1
./build.ps1                                # Module with help into out/WinCred

# Pester tests in both editions. They only touch targets named WinCred.Test.*, remove leftovers from
# earlier runs first, and fail if anything is left behind.
powershell -NoProfile -NonInteractive -File ./tests/Invoke-Tests.ps1
pwsh -NoProfile -NonInteractive -File ./tests/Invoke-Tests.ps1
pwsh -File ./tests/Invoke-Tests.ps1 -IncludePersistent   # Also LocalMachine and Enterprise (roaming) credentials
pwsh -File ./tests/Invoke-Tests.ps1 -ExcludeTag Store    # Without touching your credential store
```

By default the Pester tests write only Session credentials. `-IncludePersistent` adds tests that write LocalMachine and Enterprise credentials; an interrupted run can leave those behind until the next run removes them, and Enterprise credentials can roam to other computers in the meantime. CI passes `-IncludePersistent` because its runners are discarded.

Tool versions and signers are pinned in [tools/tools.psd1](tools/tools.psd1). Every file of a tool, except help and package metadata, MUST be signed by the pinned certificate (Pester) or under a pinned Microsoft root (PSScriptAnalyzer, PlatyPS). `Install-Tools.ps1` checks this after download, and the scripts that use a tool check it again before every import, through `Import-Tool` in [tools/Tools.psm1](tools/Tools.psm1). The check needs Windows. `Install-Tools.ps1` uses PSResourceGet, which ships with PowerShell 7.4+, so run it with `pwsh`; the saved tools are imported by path and work in Windows PowerShell 5.1 too. To run it in Windows PowerShell 5.1 instead, first run `Install-Module Microsoft.PowerShell.PSResourceGet -Scope CurrentUser`. Help is written in [Microsoft.PowerShell.PlatyPS](https://github.com/PowerShell/platyPS) markdown; PlatyPS 1.0 does not write parameter default values into the compiled help, so each description states its default. The version number MUST be changed in both `Directory.Build.props` and `src/WinCred/WinCred.psd1`; `build.ps1` fails when they differ.

GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs the unit tests, PSScriptAnalyzer and the build, then the Pester tests in Windows PowerShell 5.1 and PowerShell 7.4 and 7.6. A tag starting with `v` creates a draft release with WinCred.zip. [CodeQL](.github/workflows/codeql.yml) analyzes the C# code and the workflows on every push, pull request and weekly. [Dependabot](.github/dependabot.yml) proposes updates to the pinned Actions and NuGet packages after they are 14 days old.

### Telemetry

WinCred itself collects and sends nothing. Some of the development tools send usage telemetry to Microsoft by default:

| Tool | Opt-out environment variable |
|---|---|
| .NET CLI (`dotnet build`, `dotnet test`) | `DOTNET_CLI_TELEMETRY_OPTOUT=1` |
| Microsoft.Testing.Platform, the runner behind the native tests | `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` |
| PowerShell 7 (`pwsh`), when it starts | `POWERSHELL_TELEMETRY_OPTOUT=1` |

CI sets all three. To opt out on your machine, set them for your user account and then open a new terminal, because each tool reads them when it starts:

```powershell
foreach ($name in 'DOTNET_CLI_TELEMETRY_OPTOUT', 'TESTINGPLATFORM_TELEMETRY_OPTOUT', 'POWERSHELL_TELEMETRY_OPTOUT') {
    [Environment]::SetEnvironmentVariable($name, '1', 'User')
}
```

## History

### WinCred 1.0.0
- New identity: module WinCred with cmdlets Get-, New-, Set- and Remove-WinCredential, and a new GUID
- `New-WinCredential` only creates; `Set-WinCredential` only changes the fields you name and keeps the stored secret without decoding it
- A thin layer over the native CredMan API: SafeHandle-owned buffers, `CREDENTIAL_ATTRIBUTEW` attributes read into managed objects, Win32 error messages
- Secure defaults: required secret, no plain-text input or output, Session persistence, no generated or implied values
- `-Encoding` (Unicode or UTF8, strict in both directions), `-WhatIf`/`-Confirm`, `-PassThru`, wildcard and literal targets, pipeline from Get- to Set- and Remove-WinCredential
- Decrypted secrets and attribute values zeroed in Windows' buffers before `CredFree`; exact lookups decrypt only credentials with the requested name
- Measured limits, control characters and DomainPassword user names checked before the store is touched
- Builds for net462 (Windows PowerShell 5.1) and net8.0 (PowerShell 7.4+); no runtime dependencies
- Tests: MSTest on Microsoft.Testing.Platform for the native layer, Pester 6 against the real credential store in both editions; PSScriptAnalyzer; .NET analyzers at the recommended level; CodeQL; pinned tools installed with PSResourceGet and verified against pinned signers before every use; help built with Microsoft.PowerShell.PlatyPS 1.0

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

MIT. See [License](License).
