---
external help file: StoredCredential.dll-Help.xml
Module Name: StoredCredential
online version:
schema: 2.0.0
---

# New-StoredCredential

## SYNOPSIS
Creates a credential in the Windows Credential Manager.

## SYNTAX

### Secure String (Default)
```
New-StoredCredential [-Target <String>] [-UserName <String>] -SecurePassword <SecureString> [-Comment <String>]
 [-Type <CredType>] [-Persist <CredPersist>]
 [-Encoding <CredEncoding>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Plain Text
```
New-StoredCredential [-Target <String>] [-UserName <String>] -Password <String> [-Comment <String>]
 [-Type <CredType>] [-Persist <CredPersist>]
 [-Encoding <CredEncoding>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PSCredentialObject
```
New-StoredCredential [-Target <String>] [-Comment <String>] [-Type <CredType>] [-Persist <CredPersist>]
 -Credentials <PSCredential>
 [-Encoding <CredEncoding>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates a credential in the Windows Credential Manager. If a credential with the same target and type exists, it is replaced.

You MUST give a password through -SecurePassword, -Password or -Credentials.
If you give none of them in an interactive session, PowerShell prompts for -SecurePassword.
The encoded password can be up to 2560 bytes long: 1280 characters in Unicode, or fewer for non-ASCII characters in UTF-8.

The returned object never contains the password, the same as Get-StoredCredential -AsCredentialObject.

## EXAMPLES

### Example 1: Store a credential from Get-Credential
```powershell
PS C:\> Get-Credential -UserName test-user -Message "Password please" | New-StoredCredential -Target Server01 -Persist LocalMachine
```

Creates a credential for Server01 with the user name and password from Get-Credential.

### Example 2: Store a credential with a plain-text password
```powershell
PS C:\> New-StoredCredential -Target server01 -UserName test-user -Password Password1

Flags          : 0
Type           : Generic
TargetName     : server01
Comment        : Updated by: Dave on: 23/04/2016
LastWritten    : 23/04/2016 10:48:56
PasswordSize   : 0
Password       :
SecurePassword :
Persist        : Session
AttributeCount : 0
Attributes     : 0
TargetAlias    :
UserName       : test-user
```

Creates a credential for server01 with the user name test-user and the password Password1.
A plain-text password can end up in your command history. Prefer -SecurePassword or -Credentials.

### Example 3: Store a generated password
```powershell
PS C:\> New-StoredCredential -Target server01 -UserName test-user -Password (Get-StrongPassword -Length 20)
```

Creates a credential with a random 20-character password.

## PARAMETERS

### -Comment
A comment to identify the credential in the store.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: Updated by: <user> on: <date>
Accept pipeline input: False
Accept wildcard characters: False
```

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

### -Credentials
The user name and password as a PSCredential object.

```yaml
Type: PSCredential
Parameter Sets: PSCredentialObject
Aliases: Credential

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Encoding
The encoding to store the password in. Windows and most programs expect Unicode (UTF-16LE).
Use UTF8 only when the program that reads the credential expects UTF-8 bytes.
The 2560-byte size limit applies to the encoded password.

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

### -Password
The password as plain text. It can be empty.

```yaml
Type: String
Parameter Sets: Plain Text
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Persist
How long the credential persists. Possible values are Session (until logoff), LocalMachine (this computer) and Enterprise (roams with the user profile).

```yaml
Type: CredPersist
Parameter Sets: (All)
Aliases:
Accepted values: Session, LocalMachine, Enterprise

Required: False
Position: Named
Default value: Session
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecurePassword
The password as a SecureString.

```yaml
Type: SecureString
Parameter Sets: Secure String
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Target
The target name of the credential.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: The computer name
Accept pipeline input: False
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

### -UserName
The user name of the credential.

```yaml
Type: String
Parameter Sets: Secure String, Plain Text
Aliases:

Required: False
Position: Named
Default value: The current user name
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

### System.Management.Automation.PSCredential
You can pipe a PSCredential object to this cmdlet.

## OUTPUTS

### PSCredentialManager.Common.Credential
Returns the stored credential without its password.

## NOTES

## RELATED LINKS

[Get-StoredCredential](Get-StoredCredential.md)

[Get-StrongPassword](Get-StrongPassword.md)
