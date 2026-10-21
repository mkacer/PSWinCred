---
document type: cmdlet
external help file: WinCred.dll-Help.xml
HelpUri: 
Module Name: WinCred
ms.date: 10/21/2026
PlatyPS schema version: 2024-05-01
---

# Get-WinCredential

## SYNOPSIS

Gets credentials from Windows Credential Manager.

## SYNTAX

### WinCredential (Default)

```
Get-WinCredential [[-Target] <String[]>] [-Type <CredType[]>] [-IncludeSecret]
 [-Encoding <CredEncoding>] [<CommonParameters>]
```

### PSCredential

```
Get-WinCredential [[-Target] <String[]>] -AsPSCredential [-Type <CredType[]>]
 [-Encoding <CredEncoding>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Gets credentials from Windows Credential Manager for the current user.

By default, it returns WinCredential objects without the secret. Add -IncludeSecret to fill the Secret property with a SecureString, or -AsPSCredential to get PSCredential objects. The cmdlet never returns the secret as plain text.

-Target accepts wildcards. An exact target that does not exist writes a CredentialNotFound error; a wildcard without matches returns nothing.

## EXAMPLES

### Example 1: List the credentials

```powershell
PS C:\> Get-WinCredential

TargetName              Type    UserName   Persist      LastWritten
----------              ----    --------   -------      -----------
backup01                Generic svc-backup LocalMachine 21/10/2026 09:12:40
git:https://example.com Generic octocat    LocalMachine 02/09/2026 17:03:11
```

Lists every credential of the current user, without secrets.

### Example 2: Use a credential with another cmdlet

```powershell
PS C:\> Invoke-Command -ComputerName backup01 -Credential (Get-WinCredential -Target backup01 -AsPSCredential) -ScriptBlock { hostname }
```

### Example 3: Read a token stored without a user name

```powershell
PS C:\> $token = (Get-WinCredential -Target api.example.com -IncludeSecret).Secret
PS C:\> $headers = @{ Authorization = "Bearer $([System.Net.NetworkCredential]::new('', $token).Password)" }
```

The plain-text conversion is explicit and stays in the caller's code.

### Example 4: Read a credential that another program stored as UTF-8

```powershell
PS C:\> Get-WinCredential -Target 'git:https://example.com' -IncludeSecret -Encoding UTF8
```

### Example 5: Find a target that contains wildcard characters

```powershell
PS C:\> Get-WinCredential -Target 'server`[1`]'
```

The backticks make the brackets literal, so only the target server[1] matches.

## PARAMETERS

### -AsPSCredential

Returns PSCredential objects. Credentials without a user name, and DomainPassword or DomainCertificate credentials, whose secret Windows does not return, write an error instead.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: PSCredential
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Encoding

The encoding of the stored secret. Windows and most programs use Unicode (UTF-16LE). Use UTF8 for secrets that another program stored as UTF-8 bytes. With the wrong encoding the secret comes back garbled; the cmdlet cannot detect this. The default is Unicode.

```yaml
Type: WinCred.CredEncoding
DefaultValue: Unicode
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues:
- Unicode
- UTF8
HelpMessage: ''
```

### -IncludeSecret

Fills the Secret property with the secret as a SecureString. Without it, the cmdlet does not decode any secret.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: WinCredential
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Target

The target names to get. Wildcards are supported; escape wildcard characters with a backtick to match them literally. Target names are not case-sensitive. Without -Target, the cmdlet returns all credentials.

```yaml
Type: System.String[]
DefaultValue: All credentials
SupportsWildcards: true
Aliases:
- TargetName
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Type

Returns only credentials of these types. Without it, credentials of every type match.

```yaml
Type: WinCred.CredType[]
DefaultValue: All types
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues:
- Generic
- DomainPassword
- DomainCertificate
- DomainVisiblePassword
- GenericCertificate
- DomainExtended
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]

You can pipe target names to this cmdlet.

### WinCred.CredType[]

You can pipe the Type property of credentials from Get-WinCredential.

## OUTPUTS

### WinCred.WinCredential

By default, and with -IncludeSecret.

### System.Management.Automation.PSCredential

With -AsPSCredential.

## NOTES

Windows returns the secret of DomainPassword and DomainCertificate credentials only to the Local Security Authority, so their Secret property is always empty.

## RELATED LINKS

- [Set-WinCredential](Set-WinCredential.md)
- [Remove-WinCredential](Remove-WinCredential.md)
- [about_WinCred](about_WinCred.md)
