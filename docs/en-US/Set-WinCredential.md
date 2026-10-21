---
document type: cmdlet
external help file: WinCred.dll-Help.xml
HelpUri: 
Module Name: WinCred
ms.date: 10/21/2026
PlatyPS schema version: 2024-05-01
---

# Set-WinCredential

## SYNOPSIS

Creates or replaces a credential in Windows Credential Manager.

## SYNTAX

### Secret (Default)

```
Set-WinCredential [-Target] <String> [-UserName <String>] [-Secret <SecureString>]
 [-Type <CredType>] [-Persist <CredPersist>] [-Comment <String>] [-Encoding <CredEncoding>]
 [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PSCredential

```
Set-WinCredential [-Target] <String> -Credential <PSCredential> [-Type <CredType>]
 [-Persist <CredPersist>] [-Comment <String>] [-Encoding <CredEncoding>] [-PassThru] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Creates a credential in Windows Credential Manager. If a credential with the same target and type exists, it is replaced: every field takes the value of this call.

You MUST give the secret with -Secret or -Credential. Without one, the cmdlet fails; it never prompts for a secret and never generates one. There is no plain-text secret parameter, so secrets do not end up in command history or transcripts.

The credential persists for the logon session unless -Persist says otherwise.

## EXAMPLES

### Example 1: Store a credential from Get-Credential

```powershell
PS C:\> Get-Credential -UserName svc-backup | Set-WinCredential -Target backup01 -Persist LocalMachine
```

### Example 2: Store an API token without a user name

```powershell
PS C:\> Set-WinCredential -Target api.example.com -Secret (Read-Host -AsSecureString -Prompt Token) -Persist LocalMachine
```

### Example 3: Store a secret for a program that reads UTF-8

```powershell
PS C:\> Set-WinCredential -Target myapp -UserName app -Secret (Read-Host -AsSecureString) -Encoding UTF8 -PassThru
```

## PARAMETERS

### -Comment

A comment stored with the credential. At most 255 characters.

```yaml
Type: System.String
DefaultValue: None
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
AcceptedValues: []
HelpMessage: ''
```

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases:
- cf
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Credential

The user name and secret as a PSCredential object.

```yaml
Type: System.Management.Automation.PSCredential
DefaultValue: None
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: PSCredential
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Encoding

The encoding to store the secret in. Windows and most programs expect Unicode (UTF-16LE). Use UTF8 only when the program that reads the credential expects UTF-8 bytes. The 2560-byte limit applies to the encoded secret. The default is Unicode.

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

### -PassThru

Returns the stored credential, without its secret.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
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
AcceptedValues: []
HelpMessage: ''
```

### -Persist

How long the credential persists: Session (until logoff), LocalMachine (on this computer), or Enterprise (roams with the user profile). Longer persistence MUST be requested explicitly. The default is Session.

```yaml
Type: WinCred.CredPersist
DefaultValue: Session
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
- Session
- LocalMachine
- Enterprise
HelpMessage: ''
```

### -Secret

The secret as a SecureString. Required in this parameter set, but not marked mandatory, so that a missing secret fails instead of prompting.

```yaml
Type: System.Security.SecureString
DefaultValue: None
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Secret
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

The target name of the credential. At most 32766 characters for Generic credentials. DomainPassword targets are host names, such as server.example.com or *.example.com: at most 253 characters, with dot-separated labels of at most 63. Wildcard characters have no special meaning.

```yaml
Type: System.String
DefaultValue: None
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Type

The credential type. Only Generic and DomainPassword can be written. The default is Generic.

```yaml
Type: WinCred.CredType
DefaultValue: Generic
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
- Generic
- DomainPassword
HelpMessage: ''
```

### -UserName

The user name of the credential. At most 512 characters. Without it, the credential has no user name.

```yaml
Type: System.String
DefaultValue: None
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Secret
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Shows what would happen if the cmdlet runs. The cmdlet does not run and nothing is written.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
SupportsWildcards: false
Aliases:
- wi
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Management.Automation.PSCredential

You can pipe a PSCredential object to this cmdlet.

## OUTPUTS

### None

By default, this cmdlet returns no output.

### WinCred.WinCredential

With -PassThru, without the secret.

## NOTES

Windows Credential Manager allows at most 2560 bytes of secret: 1280 characters in Unicode, or fewer for non-ASCII characters in UTF-8.

## RELATED LINKS

- [Get-WinCredential](Get-WinCredential.md)
- [Remove-WinCredential](Remove-WinCredential.md)
- [about_WinCred](about_WinCred.md)
