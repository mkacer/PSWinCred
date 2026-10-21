---
external help file: TUN.CredentialManager.dll-Help.xml
Module Name: TUN.CredentialManager
online version:
schema: 2.0.0
---

# Get-StoredCredential

## SYNOPSIS
Gets stored credentials from the Windows Credential Manager.

## SYNTAX

```
Get-StoredCredential [-Target <String>] [-Type <CredType>] [-IncludeClearPassword] [-ExcludeClearPassword]
 [-IncludeSecurePassword] [-AsCredentialObject] [<CommonParameters>]
```

## DESCRIPTION
Gets stored credentials from the Windows Credential Manager. By default, it returns PSCredential objects.
With -AsCredentialObject, it returns Credential objects that hold all stored properties.

Without -Target, it returns every credential that the current user can read.
If no credential matches -Target, it returns nothing and writes no error.

Credential objects do not contain the clear-text password unless you add -IncludeClearPassword.

## EXAMPLES

### Example 1: Get a credential as a PSCredential
```powershell
PS C:\> Get-StoredCredential -Target Server01

UserName                      Password
--------                      --------
test-user System.Security.SecureString
```

Returns the credential for Server01 as a PSCredential object.

### Example 2: Get a credential as a Credential object
```powershell
PS C:\> Get-StoredCredential -Target Server01 -AsCredentialObject -IncludeSecurePassword

Flags          : 0
Type           : Generic
TargetName     : server01
Comment        :
LastWritten    : 23/04/2016 10:01:37
PasswordSize   : 0
Password       :
SecurePassword : System.Security.SecureString
Persist        : Enterprise
AttributeCount : 0
Attributes     : 0
TargetAlias    :
UserName       : test-user
```

Returns the credential for Server01 as a Credential object, with the password as a SecureString only.

### Example 3: Get the clear-text password
```powershell
PS C:\> (Get-StoredCredential -Target Server01 -AsCredentialObject -IncludeClearPassword).Password
Password1
```

Returns the clear-text password. Use this only when a program needs the password as plain text.

## PARAMETERS

### -AsCredentialObject
Returns Credential objects instead of PSCredential objects.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExcludeClearPassword
Obsolete. Credential objects exclude the clear-text password by default since version 4.0.
If you add both this parameter and -IncludeClearPassword, the password stays excluded.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeClearPassword
Fills the Password and PasswordSize properties of Credential objects with the clear-text password and its size in bytes.
Only takes effect with -AsCredentialObject.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeSecurePassword
Fills the SecurePassword property of Credential objects with the password as a SecureString.
Only takes effect with -AsCredentialObject. PSCredential objects always contain the password.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Target
Returns only the credential with this target name. Without it, the cmdlet returns all credentials.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String
You can pipe a target name to this cmdlet.

## OUTPUTS

### System.Management.Automation.PSCredential
By default, this cmdlet returns PSCredential objects.

### PSCredentialManager.Common.Credential
With -AsCredentialObject, this cmdlet returns Credential objects.

## NOTES
Credentials without a user name or password cannot become PSCredential objects. The cmdlet writes a warning for each of them.

## RELATED LINKS

[New-StoredCredential](New-StoredCredential.md)

[Remove-StoredCredential](Remove-StoredCredential.md)
