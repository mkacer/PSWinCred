---
external help file: StoredCredential.dll-Help.xml
Module Name: StoredCredential
online version:
schema: 2.0.0
---

# Remove-StoredCredential

## SYNOPSIS
Deletes a credential from the Windows Credential Manager.

## SYNTAX

```
Remove-StoredCredential -Target <String> [-Type <CredType>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Deletes a credential from the Windows Credential Manager. If no credential matches, the cmdlet writes an ObjectNotFound error.

## EXAMPLES

### Example 1: Delete a credential
```powershell
PS C:\> Remove-StoredCredential -Target Server01 -Type Generic
```

Deletes the generic credential with the target Server01.

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
The target name of the credential to delete.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Type
Specifies the type of the credential. Possible values are Generic, DomainPassword, DomainCertificate, DomainVisiblePassword, GenericCertificate, DomainExtended, Maximum and MaximumEx.

```yaml
Type: CredType
Parameter Sets: (All)
Aliases:
Accepted values: Generic, DomainPassword, DomainCertificate, DomainVisiblePassword, GenericCertificate, DomainExtended, Maximum, MaximumEx

Required: False
Position: Named
Default value: Generic
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs. The cmdlet is not run.

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
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String
You can pipe a target name to this cmdlet.

## OUTPUTS

### None

## NOTES

## RELATED LINKS

[Get-StoredCredential](Get-StoredCredential.md)

[New-StoredCredential](New-StoredCredential.md)
