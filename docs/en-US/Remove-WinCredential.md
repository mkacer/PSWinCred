---
document type: cmdlet
external help file: WinCred.dll-Help.xml
HelpUri: 
Module Name: WinCred
ms.date: 10/21/2026
PlatyPS schema version: 2024-05-01
---

# Remove-WinCredential

## SYNOPSIS

Deletes a credential from Windows Credential Manager.

## SYNTAX

### __AllParameterSets

```
Remove-WinCredential [-Target] <String> [-Type <CredType>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Deletes a credential from Windows Credential Manager. If no credential matches the target and type, the cmdlet writes a CredentialNotFound error.

-Target is always literal. To delete several credentials, pipe them from Get-WinCredential.

The cmdlet asks for confirmation by default, because a deleted credential cannot be recovered. In scripts, add -Confirm:$false.

## EXAMPLES

### Example 1: Delete a credential

```powershell
PS C:\> Remove-WinCredential -Target backup01
```

### Example 2: Delete every credential that matches a pattern, after previewing

```powershell
PS C:\> Get-WinCredential -Target 'test.*' | Remove-WinCredential -WhatIf
PS C:\> Get-WinCredential -Target 'test.*' | Remove-WinCredential -Confirm:$false
```

## PARAMETERS

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

### -Target

The target name of the credential to delete. Always literal: no character is a wildcard. A NUL character (U+0000) is rejected, because Windows would end the name there. Credential objects piped to the cmdlet bind here through their TargetName property.

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
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Type

The type of the credential to delete. The default is Generic.

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
- DomainCertificate
- DomainVisiblePassword
- GenericCertificate
- DomainExtended
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

### System.String

You can pipe a target name to this cmdlet.

### WinCred.WinCredential

You can pipe credentials from Get-WinCredential. They bind to -Target and -Type through their TargetName and Type properties.

## OUTPUTS

### None

## NOTES

## RELATED LINKS

- [Get-WinCredential](Get-WinCredential.md)
- [New-WinCredential](New-WinCredential.md)
- [Set-WinCredential](Set-WinCredential.md)
- [about_WinCred](about_WinCred.md)
