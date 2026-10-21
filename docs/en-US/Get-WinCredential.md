---
external help file: WinCred.dll-Help.xml
Module Name: WinCred
online version:
schema: 2.0.0
---

# Get-WinCredential

## SYNOPSIS
Gets credentials from Windows Credential Manager.

## SYNTAX

### WinCredential (Default)
```
Get-WinCredential [[-Target] <String[]>] [-Type <CredType[]>] [-IncludeSecret] [-Encoding <CredEncoding>]
 [<CommonParameters>]
```

### PSCredential
```
Get-WinCredential [[-Target] <String[]>] -AsPSCredential [-Type <CredType[]>] [-Encoding <CredEncoding>]
 [<CommonParameters>]
```

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
Type: SwitchParameter
Parameter Sets: PSCredential
Aliases: 

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Encoding
The encoding of the stored secret. Windows and most programs use Unicode (UTF-16LE). Use UTF8 for secrets that another program stored as UTF-8 bytes. With the wrong encoding the secret comes back garbled; the cmdlet cannot detect this.

```yaml
Type: CredEncoding
Parameter Sets: (All)
Aliases: 
Accepted values: Unicode, UTF8

Required: False
Position: Named
Default value: Unicode
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeSecret
Fills the Secret property with the secret as a SecureString. Without it, the cmdlet does not decode any secret.

```yaml
Type: SwitchParameter
Parameter Sets: WinCredential
Aliases: 

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Target
The target names to get. Wildcards are supported; escape wildcard characters with a backtick to match them literally. Target names are not case-sensitive. Without -Target, the cmdlet returns all credentials.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: TargetName

Required: False
Position: 0
Default value: All credentials
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: True
```

### -Type
Returns only credentials of these types. Without it, credentials of every type match.

```yaml
Type: CredType[]
Parameter Sets: (All)
Aliases: 
Accepted values: Generic, DomainPassword, DomainCertificate, DomainVisiblePassword, GenericCertificate, DomainExtended

Required: False
Position: Named
Default value: All types
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String[]
You can pipe target names to this cmdlet.

## OUTPUTS

### WinCred.WinCredential
By default, and with -IncludeSecret.

### System.Management.Automation.PSCredential
With -AsPSCredential.

## NOTES
Windows returns the secret of DomainPassword and DomainCertificate credentials only to the Local Security Authority, so their Secret property is always empty.

## RELATED LINKS

[Set-WinCredential](Set-WinCredential.md)

[Remove-WinCredential](Remove-WinCredential.md)

[about_WinCred](about_WinCred.md)
