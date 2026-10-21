---
external help file: StoredCredential.dll-Help.xml
Module Name: StoredCredential
online version:
schema: 2.0.0
---

# Get-StrongPassword

## SYNOPSIS
Generates a random password.

## SYNTAX

```
Get-StrongPassword [-Length <Int32>] [-NumberOfSpecialCharacters <Int32>] [<CommonParameters>]
```

## DESCRIPTION
Generates a random password from letters, digits and the special characters !@#$%^&*()_-+=[{]};:>|./? with a cryptographic random number generator.
The password never contains sequences that look like HTML, such as <a or &#.

## EXAMPLES

### Example 1: Generate a password with the defaults
```powershell
PS C:\> Get-StrongPassword
QTJ(T?wwe)
```

Generates a password 10 characters long with at least 3 special characters.

### Example 2: Generate a longer password
```powershell
PS C:\> Get-StrongPassword -Length 20 -NumberOfSpecialCharacters 5
zPN>C%.f/(l1aGq)n3Ze
```

Generates a password 20 characters long with at least 5 special characters.

## PARAMETERS

### -Length
The length of the password, from 1 to 128 characters.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 10
Accept pipeline input: False
Accept wildcard characters: False
```

### -NumberOfSpecialCharacters
The minimum number of special characters. It MUST NOT be greater than -Length.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 3
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### System.String

## NOTES

## RELATED LINKS

[New-StoredCredential](New-StoredCredential.md)
