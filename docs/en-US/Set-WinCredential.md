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

Changes an existing credential in Windows Credential Manager.

## SYNTAX

### Secret (Default)

```
Set-WinCredential [-Target] <String> [-Type <CredType>] [-UserName <String>] [-Secret <SecureString>]
 [-Persist <CredPersist>] [-Comment <String>] [-Encoding <CredEncoding>] [-PassThru] [-WhatIf]
 [-Confirm] [<CommonParameters>]
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

Changes an existing credential in Windows Credential Manager. Only the fields you specify change. Every other field keeps its stored value: the user name, the secret, the persistence, the comment, the target alias, the attributes, and the flags Windows accepts on a write (it rejects some it sets itself, such as CRED_FLAGS_WILDCARD_MATCH). A new user name also clears CRED_FLAGS_USERNAME_TARGET, which says the target is the user name. An empty string removes the user name or the comment.

Without -Secret or -Credential, the stored secret is kept and never decoded. The cmdlet still reads the stored credential to keep the other fields, and for a Generic credential Windows decrypts the secret into the process during that read; WinCred zeroes it before freeing it.

If the credential does not exist, the cmdlet writes a CredentialNotFound error and creates nothing. To create a credential, use New-WinCredential.

The cmdlet asks for confirmation by default, because it overwrites stored values. In scripts, add -Confirm:$false.

## EXAMPLES

### Example 1: Replace a secret

```powershell
PS C:\> Set-WinCredential -Target backup01 -Secret (Read-Host -AsSecureString -Prompt 'New password')
```

The user name, persistence and comment stay as they are.

### Example 2: Change only the comment

```powershell
PS C:\> Set-WinCredential -Target backup01 -Comment 'Rotated 2026-10-21' -Confirm:$false
```

### Example 3: Change the persistence of several credentials

```powershell
PS C:\> Get-WinCredential -Target 'test.*' -Type Generic | Set-WinCredential -Persist Session
```

Each piped credential binds to -Target and -Type, and each change asks for confirmation.

## PARAMETERS

### -Comment

The new comment. At most 255 characters; control characters are rejected. An empty string removes the comment.

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

The new user name and secret as a PSCredential object. Replaces both.

```yaml
Type: System.Management.Automation.PSCredential
DefaultValue: None
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

The encoding to store the new secret in. Valid only together with -Secret or -Credential. Windows and most programs expect Unicode (UTF-16LE). DomainPassword credentials MUST use Unicode, because Windows signs in with them. The default is Unicode.

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

The new persistence: Session (until logoff), LocalMachine (on this computer), or Enterprise (roams with the user profile). Without it, the stored persistence is kept.

```yaml
Type: WinCred.CredPersist
DefaultValue: Stored value
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

The new secret as a SecureString. Without it, the stored secret is kept and never decoded.

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

The target name of the credential to change. Always literal. A NUL character (U+0000) is rejected, because Windows would end the name there. Credential objects piped to the cmdlet bind here through their TargetName property.

```yaml
Type: System.String
DefaultValue: None
SupportsWildcards: false
Aliases:
- TargetName
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Type

The type of the credential to change. Only Generic and DomainPassword can be written. The default is Generic.

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
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues:
- Generic
- DomainPassword
HelpMessage: ''
```

### -UserName

The new user name. At most 512 characters; control characters are rejected. DomainPassword credentials require DOMAIN\user or user@domain. An empty string removes the user name of a Generic credential.

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

### WinCred.WinCredential

You can pipe credentials from Get-WinCredential. They bind to -Target and -Type through their TargetName and Type properties.

## OUTPUTS

### None

By default, this cmdlet returns no output.

### WinCred.WinCredential

With -PassThru, without the secret.

## NOTES

Windows Credential Manager allows at most 2560 bytes of secret: 1280 characters in Unicode, or fewer for non-ASCII characters in UTF-8.

When Windows refuses to read or write one piped credential, the cmdlet writes a ReadFailed or WriteFailed error and goes on with the next one. An unusable secret stops the cmdlet, because it would fail for every credential.

The cmdlet reads the stored credential and writes it back with the changes. If another program changes the credential between those two steps, its change to the fields this cmdlet writes back is lost. If another program deletes it, the cmdlet writes a CredentialNotFound error when the secret is kept, and recreates the credential when a new secret is given.

## RELATED LINKS

- [Get-WinCredential](Get-WinCredential.md)
- [New-WinCredential](New-WinCredential.md)
- [Remove-WinCredential](Remove-WinCredential.md)
- [about_WinCred](about_WinCred.md)
