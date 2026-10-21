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

### Target

```
Get-WinCredential [[-Target] <String[]>] [-Type <CredType[]>] [-IncludeSecret]
 [-Encoding <CredEncoding>] [<CommonParameters>]
```

### TargetAsPSCredential

```
Get-WinCredential [[-Target] <String[]>] -AsPSCredential [-Type <CredType[]>]
 [-Encoding <CredEncoding>] [<CommonParameters>]
```

### LiteralTarget

```
Get-WinCredential -LiteralTarget <String[]> [-Type <CredType[]>] [-IncludeSecret]
 [-Encoding <CredEncoding>] [<CommonParameters>]
```

### LiteralTargetAsPSCredential

```
Get-WinCredential -LiteralTarget <String[]> -AsPSCredential [-Type <CredType[]>]
 [-Encoding <CredEncoding>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Gets credentials from Windows Credential Manager for the current user.

By default, it returns WinCredential objects without the secret and without attribute values. Add -IncludeSecret to fill the Secret property with a SecureString, or -AsPSCredential to get PSCredential objects. The cmdlet never returns the secret as plain text.

-Target accepts wildcards; -LiteralTarget does not. An exact target that does not exist writes a CredentialNotFound error; a wildcard without matches returns nothing.

An exact target is read with CredReadW, once per type, so Windows decrypts only that credential. A wildcard is read with CredEnumerateW, filtered by the pattern's literal prefix where it has one; Windows decrypts every credential the filter passes. Without -Target, Windows decrypts the whole store of the current user. WinCred zeroes each decrypted secret it does not return before it frees Windows' buffer.

## EXAMPLES

### Example 1: List the credentials

```powershell
PS C:\> Get-WinCredential

TargetName              Type    UserName   Persist      LastWritten
----------              ----    --------   -------      -----------
backup01                Generic svc-backup LocalMachine 21/10/2026 09:12:40 +02:00
git:https://example.com Generic octocat    LocalMachine 02/09/2026 17:03:11 +02:00
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

### Example 5: Get a target that contains wildcard characters

```powershell
PS C:\> Get-WinCredential -LiteralTarget 'server[1]'
PS C:\> Get-WinCredential -Target 'server`[1`]'
```

Both commands get only the target server[1].

## PARAMETERS

### -AsPSCredential

Returns PSCredential objects. Credentials without a user name, and DomainPassword or DomainCertificate credentials, whose secret Windows does not return, write an error instead.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: TargetAsPSCredential
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: LiteralTargetAsPSCredential
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

The encoding the secret was stored in. Windows does not record it. Unicode (UTF-16LE) is what Windows and most programs use. A secret that is not valid in this encoding is never altered: the cmdlet writes a SecretNotDecodable error and returns the credential without its secret. The default is Unicode.

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

Fills the Secret property with the secret as a SecureString, and fills the Value of each attribute. Without it, the cmdlet decodes no secret and returns no attribute values.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Target
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: LiteralTarget
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -LiteralTarget

The exact target names to get. No character is a wildcard. A NUL character (U+0000) is rejected, because Windows would end the name there. Credential objects piped to the cmdlet bind here through their TargetName property. Target names are not case-sensitive.

```yaml
Type: System.String[]
DefaultValue: None
SupportsWildcards: false
Aliases:
- TargetName
ParameterSets:
- Name: LiteralTarget
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: LiteralTargetAsPSCredential
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Target

The target names to get. Wildcards are supported; escape wildcard characters with a backtick to match them literally, or use -LiteralTarget. Target names are not case-sensitive. Without -Target, the cmdlet returns all credentials.

```yaml
Type: System.String[]
DefaultValue: All credentials
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: Target
  Position: 0
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: TargetAsPSCredential
  Position: 0
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
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

You can pipe target names to -Target.

### WinCred.WinCredential

You can pipe credentials from Get-WinCredential. They bind to -LiteralTarget and -Type through their TargetName and Type properties.

## OUTPUTS

### WinCred.WinCredential

By default, and with -IncludeSecret.

### System.Management.Automation.PSCredential

With -AsPSCredential.

## NOTES

Windows returns the secret of DomainPassword and DomainCertificate credentials only to the Local Security Authority, so their Secret property is always empty.

## RELATED LINKS

- [New-WinCredential](New-WinCredential.md)
- [Set-WinCredential](Set-WinCredential.md)
- [Remove-WinCredential](Remove-WinCredential.md)
- [about_WinCred](about_WinCred.md)
