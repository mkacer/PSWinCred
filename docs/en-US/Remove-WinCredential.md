---
external help file: WinCred.dll-Help.xml
Module Name: WinCred
online version:
schema: 2.0.0
---

# Remove-WinCredential

## SYNOPSIS
Deletes a credential from Windows Credential Manager.

## SYNTAX

```
Remove-WinCredential [-Target] <String> [-Type <CredType>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Deletes a credential from Windows Credential Manager. If no credential matches the target and type, the cmdlet writes a CredentialNotFound error.

-Target is always literal. To delete several credentials, pipe them from Get-WinCredential.

## EXAMPLES

### Example 1: Delete a credential
```powershell
PS C:\> Remove-WinCredential -Target backup01
```

### Example 2: Delete every credential that matches a pattern, after previewing
```powershell
PS C:\> Get-WinCredential -Target 'test.*' | Remove-WinCredential -WhatIf
PS C:\> Get-WinCredential -Target 'test.*' | Remove-WinCredential
```

## PARAMETERS

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Target
The target name of the credential to delete. Wildcard characters have no special meaning.

```yaml
Type: String
Parameter Sets: (All)
Aliases: TargetName

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Type
The type of the credential to delete.

```yaml
Type: CredType
Parameter Sets: (All)
Aliases: 
Accepted values: Generic, DomainPassword, DomainCertificate, DomainVisiblePassword, GenericCertificate, DomainExtended

Required: False
Position: Named
Default value: Generic
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs. The cmdlet does not run and nothing is written.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String
You can pipe a target name to this cmdlet.

### WinCred.WinCredential
You can pipe credentials from Get-WinCredential.

## OUTPUTS

### None

## NOTES


## RELATED LINKS

[Get-WinCredential](Get-WinCredential.md)

[Set-WinCredential](Set-WinCredential.md)

[about_WinCred](about_WinCred.md)
