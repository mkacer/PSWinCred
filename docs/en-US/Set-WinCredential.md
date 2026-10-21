---
external help file: WinCred.dll-Help.xml
Module Name: WinCred
online version:
schema: 2.0.0
---

# Set-WinCredential

## SYNOPSIS
Creates or replaces a credential in Windows Credential Manager.

## SYNTAX

### Secret (Default)
```
Set-WinCredential [-Target] <String> [-UserName <String>] [-Secret <SecureString>] [-Type <CredType>]
 [-Persist <CredPersist>] [-Comment <String>] [-Encoding <CredEncoding>] [-PassThru] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### PSCredential
```
Set-WinCredential [-Target] <String> -Credential <PSCredential> [-Type <CredType>] [-Persist <CredPersist>]
 [-Comment <String>] [-Encoding <CredEncoding>] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

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
A comment stored with the credential. At most 256 characters.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 

Required: False
Position: Named
Default value: None
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

### -Credential
The user name and secret as a PSCredential object.

```yaml
Type: PSCredential
Parameter Sets: PSCredential
Aliases: 

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Encoding
The encoding to store the secret in. Windows and most programs expect Unicode (UTF-16LE). Use UTF8 only when the program that reads the credential expects UTF-8 bytes. The 2560-byte limit applies to the encoded secret.

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

### -PassThru
Returns the stored credential, without its secret.

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

### -Persist
How long the credential persists: Session (until logoff), LocalMachine (on this computer), or Enterprise (roams with the user profile). Longer persistence MUST be requested explicitly.

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

### -Secret
The secret as a SecureString. Required in this parameter set, but not marked mandatory, so that a missing secret fails instead of prompting.

```yaml
Type: SecureString
Parameter Sets: Secret
Aliases: 

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Target
The target name of the credential. At most 32767 characters for Generic and 337 for DomainPassword credentials. Wildcard characters have no special meaning.

```yaml
Type: String
Parameter Sets: (All)
Aliases: 

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
The credential type. Only Generic and DomainPassword can be written.

```yaml
Type: CredType
Parameter Sets: (All)
Aliases: 
Accepted values: Generic, DomainPassword

Required: False
Position: Named
Default value: Generic
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserName
The user name of the credential. At most 513 characters. Without it, the credential has no user name.

```yaml
Type: String
Parameter Sets: Secret
Aliases: 

Required: False
Position: Named
Default value: None
Accept pipeline input: False
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

[Get-WinCredential](Get-WinCredential.md)

[Remove-WinCredential](Remove-WinCredential.md)

[about_WinCred](about_WinCred.md)
