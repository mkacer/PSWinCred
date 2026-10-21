#Requires -Version 5.1
<#
.SYNOPSIS
    End-to-end checks of the built module against the real Windows Credential Manager.
.DESCRIPTION
    Run it with Windows PowerShell 5.1 and with PowerShell 7 after build.ps1.
    It creates credentials with Session persistence under a unique target prefix and deletes them again.
#>
[CmdletBinding()]
param(
    [string]$ModulePath = (Join-Path $PSScriptRoot '../out/StoredCredential/StoredCredential.psd1')
)

$ErrorActionPreference = 'Stop'
$failures = 0

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        Write-Host "[PASS] $Name"
    }
    catch {
        $script:failures++
        Write-Host "[FAIL] $Name`n       $($_.Exception.Message)"
    }
}

function Assert-Equal($Expected, $Actual, [string]$What) {
    if ($Expected -cne $Actual) { throw "$What`: expected '$Expected', got '$Actual'." }
}

function ConvertTo-PlainText([securestring]$Secure) {
    [System.Net.NetworkCredential]::new('', $Secure).Password
}

Write-Host "PowerShell $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition))"
Import-Module $ModulePath -Force

$prefix = "StoredCredential.Smoke.$([guid]::NewGuid())"
$target = "$prefix.main"
$secure = ConvertTo-SecureString 'P@ss word1' -AsPlainText -Force

try {
    Test-Case 'exports the four cmdlets' {
        $names = (Get-Command -Module StoredCredential).Name | Sort-Object
        Assert-Equal 'Get-StoredCredential,Get-StrongPassword,New-StoredCredential,Remove-StoredCredential' ($names -join ',') 'cmdlets'
    }

    Test-Case 'loads help' {
        $synopsis = (Get-Help Get-StoredCredential).Synopsis
        Assert-Equal 'Gets stored credentials from the Windows Credential Manager.' $synopsis 'synopsis'
    }

    Test-Case 'New-StoredCredential stores a SecureString password' {
        $result = New-StoredCredential -Target $target -UserName 'smoke-user' -SecurePassword $secure
        Assert-Equal $target $result.TargetName 'TargetName'
        Assert-Equal $null $result.Password 'Password'
    }

    Test-Case 'Get-StoredCredential returns a PSCredential' {
        $cred = Get-StoredCredential -Target $target
        Assert-Equal 'System.Management.Automation.PSCredential' $cred.GetType().FullName 'type'
        Assert-Equal 'smoke-user' $cred.UserName 'UserName'
        Assert-Equal 'P@ss word1' (ConvertTo-PlainText $cred.Password) 'password'
    }

    Test-Case 'Credential objects exclude the clear password by default' {
        $cred = Get-StoredCredential -Target $target -AsCredentialObject
        Assert-Equal $null $cred.Password 'Password'
        Assert-Equal 0 $cred.PasswordSize 'PasswordSize'
    }

    Test-Case '-IncludeClearPassword fills Password and PasswordSize' {
        $cred = Get-StoredCredential -Target $target -AsCredentialObject -IncludeClearPassword
        Assert-Equal 'P@ss word1' $cred.Password 'Password'
        Assert-Equal 20 $cred.PasswordSize 'PasswordSize'
    }

    Test-Case '-ExcludeClearPassword overrides -IncludeClearPassword' {
        $cred = Get-StoredCredential -Target $target -AsCredentialObject -IncludeClearPassword -ExcludeClearPassword
        Assert-Equal $null $cred.Password 'Password'
    }

    Test-Case '-IncludeSecurePassword fills SecurePassword' {
        $cred = Get-StoredCredential -Target $target -AsCredentialObject -IncludeSecurePassword
        Assert-Equal 'P@ss word1' (ConvertTo-PlainText $cred.SecurePassword) 'SecurePassword'
    }

    Test-Case 'enumeration writes one pipeline object per credential' {
        New-StoredCredential -Target "$prefix.second" -UserName 'smoke-user' -Password 'x' | Out-Null
        $mine = @(Get-StoredCredential -AsCredentialObject | Where-Object { $_.TargetName -like "$prefix*" })
        Assert-Equal 2 $mine.Count 'count'
    }

    Test-Case 'empty password becomes a PSCredential' {
        New-StoredCredential -Target "$prefix.empty" -UserName 'smoke-user' -Password '' | Out-Null
        $cred = Get-StoredCredential -Target "$prefix.empty"
        Assert-Equal 0 $cred.Password.Length 'password length'
    }

    Test-Case 'PSCredential from the pipeline' {
        $psCred = [pscredential]::new('pipe-user', $secure)
        New-StoredCredential -Target "$prefix.pipe" -Credential $psCred | Out-Null
        $psCred | New-StoredCredential -Target "$prefix.pipe" | Out-Null
        Assert-Equal 'pipe-user' (Get-StoredCredential -Target "$prefix.pipe").UserName 'UserName'
    }

    Test-Case 'too-long password stops before writing' {
        $threw = $false
        try { New-StoredCredential -Target "$prefix.long" -UserName 'u' -Password ('a' * 1281) | Out-Null }
        catch { $threw = $true }
        if (-not $threw) { throw 'No error.' }
        Assert-Equal $null (Get-StoredCredential -Target "$prefix.long") 'stored credential'
    }

    Test-Case 'New-StoredCredential -Password output holds no password' {
        $result = New-StoredCredential -Target "$prefix.plain" -UserName 'u' -Password 'plain'
        Assert-Equal $null $result.Password 'Password'
        Assert-Equal $null $result.SecurePassword 'SecurePassword'
        Assert-Equal 0 $result.PasswordSize 'PasswordSize'
    }

    Test-Case '-Encoding UTF8 round-trips non-ASCII passwords' {
        $text = "p$([char]0xE4)ss$([char]0xE9)"
        New-StoredCredential -Target "$prefix.utf8" -UserName 'u' -Password $text -Encoding UTF8 | Out-Null
        $cred = Get-StoredCredential -Target "$prefix.utf8" -AsCredentialObject -IncludeClearPassword -Encoding UTF8
        Assert-Equal $text $cred.Password 'Password'
        Assert-Equal 7 $cred.PasswordSize 'PasswordSize'
        Assert-Equal $text (ConvertTo-PlainText (Get-StoredCredential -Target "$prefix.utf8" -Encoding UTF8).Password) 'PSCredential password'
    }

    Test-Case 'the size limit applies to the encoded password' {
        # 1281 ASCII chars are 1281 bytes in UTF-8, so they fit
        New-StoredCredential -Target "$prefix.utf8long" -UserName 'u' -Password ('a' * 1281) -Encoding UTF8 | Out-Null
        $threw = $false
        try { New-StoredCredential -Target "$prefix.utf8toolong" -UserName 'u' -Password ([string][char]0xE9 * 1281) -Encoding UTF8 | Out-Null }
        catch { $threw = $true }
        if (-not $threw) { throw 'No error for 2562 bytes.' }
    }

    Test-Case '-WhatIf writes and deletes nothing' {
        New-StoredCredential -Target "$prefix.whatif" -UserName 'u' -SecurePassword $secure -WhatIf
        Assert-Equal $null (Get-StoredCredential -Target "$prefix.whatif") 'created credential'
        Remove-StoredCredential -Target "$prefix.pipe" -WhatIf
        Assert-Equal 'pipe-user' (Get-StoredCredential -Target "$prefix.pipe").UserName 'kept credential'
    }

    Test-Case 'missing target returns nothing without error' {
        Assert-Equal $null (Get-StoredCredential -Target "$prefix.missing") 'result'
    }

    Test-Case 'Remove-StoredCredential deletes' {
        Remove-StoredCredential -Target $target
        Assert-Equal $null (Get-StoredCredential -Target $target) 'result'
    }

    Test-Case 'Remove-StoredCredential reports a missing credential' {
        Remove-StoredCredential -Target "$prefix.missing" -ErrorAction SilentlyContinue -ErrorVariable removeErrors
        Assert-Equal 'CredentialNotFound,PSCredentialManager.Cmdlet.RemoveStoredCredential' $removeErrors[0].FullyQualifiedErrorId 'error id'
    }

    Test-Case 'Get-StrongPassword' {
        Assert-Equal 24 (Get-StrongPassword -Length 24).Length 'length'
    }
}
finally {
    Get-StoredCredential -AsCredentialObject |
        Where-Object { $_.TargetName -like "$prefix*" } |
        ForEach-Object { Remove-StoredCredential -Target $_.TargetName -Type $_.Type -ErrorAction SilentlyContinue }
}

if ($failures -gt 0) { throw "$failures smoke test(s) failed." }
Write-Host 'All smoke tests passed.'
