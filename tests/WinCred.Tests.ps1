#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; RequiredVersion = '6.2.0' }
<#
    Tests of the built WinCred module. Run through tests/Invoke-Tests.ps1.

    Tests tagged 'Store' read and write the current user's Windows Credential Manager. They only touch
    targets under 'WinCred.Test.<run guid>.' and remove them again, even after failures.
    Tests tagged 'Help' need the module built with help (build.ps1 without -SkipHelp).
    This file is ASCII only, because Windows PowerShell 5.1 reads BOM-less scripts as ANSI.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '', Justification = 'Pester shares variables between blocks.')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'ModulePath', Justification = 'Used in BeforeAll, which the analyzer cannot follow.')]
param(
    [Parameter(Mandatory)]
    [string]$ModulePath
)

BeforeDiscovery {
    $encodings = @(
        @{ Encoding = 'Unicode' }
        @{ Encoding = 'UTF8' }
    )
    $persistModes = @(
        @{ Persist = 'Session' }
        @{ Persist = 'LocalMachine' }
        @{ Persist = 'Enterprise' }
    )
    # 2560 bytes is CRED_MAX_CREDENTIAL_BLOB_SIZE. [char]0xE9 is 2 bytes in UTF-8.
    $blobSizes = @(
        @{ Encoding = 'Unicode'; Char = 'a'; Length = 0; Fits = $true }
        @{ Encoding = 'Unicode'; Char = 'a'; Length = 1280; Fits = $true }
        @{ Encoding = 'Unicode'; Char = 'a'; Length = 1281; Fits = $false }
        @{ Encoding = 'UTF8'; Char = 'a'; Length = 2560; Fits = $true }
        @{ Encoding = 'UTF8'; Char = 'a'; Length = 2561; Fits = $false }
        @{ Encoding = 'UTF8'; Char = [string][char]0xE9; Length = 1280; Fits = $true }
        @{ Encoding = 'UTF8'; Char = [string][char]0xE9; Length = 1281; Fits = $false }
    )
}

BeforeAll {
    Import-Module $ModulePath -Force

    $prefix = "WinCred.Test.$([guid]::NewGuid().ToString('N'))"

    function Get-TestTarget([string]$Name) { "$prefix.$Name" }

    function ConvertTo-TestSecret([string]$Text) {
        $secret = [securestring]::new()
        foreach ($c in $Text.ToCharArray()) { $secret.AppendChar($c) }
        $secret.MakeReadOnly()
        $secret
    }

    # A host name of exactly $Length characters under this run's prefix, with labels of at most 63
    function Get-DomainTestTarget([int]$Length) {
        $name = Get-TestTarget 'domain'
        while ($Length - $name.Length -gt 64) { $name += '.' + ('a' * 63) }
        $rest = $Length - $name.Length
        if ($rest -ge 2) { $name += '.' + ('a' * ($rest - 1)) }
        $name
    }

    function ConvertTo-PlainText([securestring]$Secret) {
        [System.Net.NetworkCredential]::new('', $Secret).Password
    }

    # a-umlaut, e-acute, a surrogate pair (U+1F600) and two CJK characters
    $unicodeText = -join @('p', [char]0xE4, [char]0xE9, 's', [char]::ConvertFromUtf32(0x1F600), [char]0x65E5, [char]0x672C)
}

AfterAll {
    Remove-Module WinCred -ErrorAction SilentlyContinue
}

Describe 'Module surface' {
    It 'exports exactly Get-, Set- and Remove-WinCredential' {
        $names = @((Get-Command -Module WinCred).Name | Sort-Object)
        Should-BeCollection -Actual $names -Expected @('Get-WinCredential', 'Remove-WinCredential', 'Set-WinCredential')
    }

    It 'has no parameter that takes or returns a plain-text secret' {
        $set = Get-Command Set-WinCredential
        $get = Get-Command Get-WinCredential
        foreach ($name in 'Password', 'PlainText') { $set.Parameters.ContainsKey($name) | Should-BeFalse }
        foreach ($name in 'IncludeClearPassword', 'AsPlainText') { $get.Parameters.ContainsKey($name) | Should-BeFalse }
        $set.Parameters['Secret'].ParameterType | Should-Be ([securestring])
    }

    It 'requires -Target for Set- and Remove-WinCredential' {
        foreach ($command in 'Set-WinCredential', 'Remove-WinCredential') {
            $mandatory = (Get-Command $command).Parameters['Target'].Attributes | Where-Object { $_ -is [Parameter] -and $_.Mandatory }
            Should-BeCollection -Actual @($mandatory) -Count 1
        }
    }

    It 'supports -WhatIf and -Confirm on Set- and Remove-WinCredential' {
        foreach ($command in 'Set-WinCredential', 'Remove-WinCredential') {
            (Get-Command $command).Parameters.ContainsKey('WhatIf') | Should-BeTrue
            (Get-Command $command).Parameters.ContainsKey('Confirm') | Should-BeTrue
        }
    }

    It 'loads cmdlet help' -Tag 'Help' {
        (Get-Help Get-WinCredential).Synopsis | Should-Be 'Gets credentials from Windows Credential Manager.'
        (Get-Help Set-WinCredential).Synopsis | Should-Be 'Creates or replaces a credential in Windows Credential Manager.'
        (Get-Help Remove-WinCredential).Synopsis | Should-Be 'Deletes a credential from Windows Credential Manager.'
    }

    It 'loads the about_WinCred topic' -Tag 'Help' {
        Get-Help about_WinCred | Out-String | Should-BeLikeString '*Windows Credential Manager*'
    }
}

Describe 'Set-WinCredential input validation' {
    # These cases fail before any call to Windows, so nothing can be written

    It 'fails without a secret instead of prompting or generating one' {
        { Set-WinCredential -Target (Get-TestTarget 'nosecret') -UserName 'u' } | Should-Throw -FullyQualifiedErrorId 'MissingSecret,*'
    }

    It 'rejects -Secret together with -Credential' {
        $credential = [pscredential]::new('u', (ConvertTo-TestSecret 'x'))
        { Set-WinCredential -Target (Get-TestTarget 'both') -Secret (ConvertTo-TestSecret 'y') -Credential $credential } |
            Should-Throw -FullyQualifiedErrorId 'AmbiguousParameterSet,*'
    }

    It 'rejects credential types it cannot write' {
        { Set-WinCredential -Target (Get-TestTarget 'cert') -Secret (ConvertTo-TestSecret 'x') -Type DomainCertificate } |
            Should-Throw -FullyQualifiedErrorId 'ParameterArgumentValidationError,*'
    }

    It 'rejects a generic target over 32766 characters' {
        # Windows accepts 32766, one fewer than CRED_MAX_GENERIC_TARGET_NAME_LENGTH
        { Set-WinCredential -Target ('t' * 32767) -Secret (ConvertTo-TestSecret 'x') } | Should-Throw -FullyQualifiedErrorId 'TargetTooLong,*'
    }

    It 'rejects a domain target over 253 characters' {
        { Set-WinCredential -Target (Get-DomainTestTarget 254) -Secret (ConvertTo-TestSecret 'x') -Type DomainPassword -UserName 'd\u' } |
            Should-Throw -FullyQualifiedErrorId 'TargetTooLong,*'
    }

    It 'rejects a domain target label over 63 characters' {
        { Set-WinCredential -Target (Get-TestTarget ('a' * 64)) -Secret (ConvertTo-TestSecret 'x') -Type DomainPassword -UserName 'd\u' } |
            Should-Throw -FullyQualifiedErrorId 'TargetLabelTooLong,*'
    }

    It 'allows long labels in generic targets' {
        # Only domain targets are host names; this fails later, on the missing secret, not on the label
        { Set-WinCredential -Target (Get-TestTarget ('a' * 64)) } | Should-Throw -FullyQualifiedErrorId 'MissingSecret,*'
    }

    It 'rejects a user name over 512 characters' {
        { Set-WinCredential -Target (Get-TestTarget 'longuser') -Secret (ConvertTo-TestSecret 'x') -UserName ('u' * 513) } |
            Should-Throw -FullyQualifiedErrorId 'UserNameTooLong,*'
    }

    It 'rejects a comment over 255 characters' {
        { Set-WinCredential -Target (Get-TestTarget 'longcomment') -Secret (ConvertTo-TestSecret 'x') -Comment ('c' * 256) } |
            Should-Throw -FullyQualifiedErrorId 'CommentTooLong,*'
    }

    It 'rejects <Length> x ''<Char>'' as a <Encoding> secret' -ForEach ($blobSizes | Where-Object { -not $_.Fits }) {
        { Set-WinCredential -Target (Get-TestTarget 'toolarge') -Secret (ConvertTo-TestSecret ($Char * $Length)) -Encoding $Encoding } |
            Should-Throw -FullyQualifiedErrorId 'SecretTooLarge,*'
    }
}

Describe 'Credential store' -Tag 'Store' {
    BeforeAll {
        $secret = ConvertTo-TestSecret 'P@ss word 1'
    }

    AfterAll {
        # Remove everything this run created, including after failures
        Get-WinCredential -Target "$prefix*" | Remove-WinCredential -Confirm:$false
    }

    Context 'Create and read' {
        BeforeAll {
            $target = Get-TestTarget 'main'
            $result = Set-WinCredential -Target $target -UserName 'test-user' -Secret $secret
        }

        It 'returns nothing without -PassThru' {
            Should-BeNull -Actual $result
        }

        It 'reads the fields without the secret by default' {
            $credential = Get-WinCredential -Target $target
            $credential.TargetName | Should-Be $target
            $credential.Type | Should-Be ([WinCred.CredType]::Generic)
            $credential.UserName | Should-Be 'test-user'
            $credential.Persist | Should-Be ([WinCred.CredPersist]::Session)
            Should-BeNull -Actual $credential.Comment
            Should-BeNull -Actual $credential.Secret
            $credential.LastWritten | Should-BeGreaterThan ((Get-Date).AddMinutes(-10))
        }

        It 'reads the secret with -IncludeSecret' {
            $credential = Get-WinCredential -Target $target -IncludeSecret
            ConvertTo-PlainText $credential.Secret | Should-Be 'P@ss word 1'
        }

        It 'stores the secret as Unicode by default' {
            $credential = Get-WinCredential -Target $target -IncludeSecret -Encoding Unicode
            ConvertTo-PlainText $credential.Secret | Should-Be 'P@ss word 1'
        }

        It 'reads a PSCredential with -AsPSCredential' {
            $credential = Get-WinCredential -Target $target -AsPSCredential
            $credential | Should-HaveType ([pscredential])
            $credential.UserName | Should-Be 'test-user'
            ConvertTo-PlainText $credential.Password | Should-Be 'P@ss word 1'
        }

        It 'never shows the secret in formatted output' {
            Get-WinCredential -Target $target -IncludeSecret | Format-List * | Out-String | Should-NotMatchString 'P@ss word 1'
        }

        It 'lists the credential under its own target name' {
            # Guards against "LegacyGeneric:target=..." names from CRED_ENUMERATE_ALL_CREDENTIALS
            $listed = @(Get-WinCredential -Target "$prefix.ma*")
            Should-BeCollection -Actual $listed -Count 1
            $listed[0].TargetName | Should-Be $target
        }

        It 'finds the target case-insensitively' {
            (Get-WinCredential -Target $target.ToUpperInvariant()).TargetName | Should-Be $target
        }

        It 'returns the written credential without its secret with -PassThru' {
            $written = Set-WinCredential -Target (Get-TestTarget 'passthru') -UserName 'u' -Secret $secret -PassThru
            $written.TargetName | Should-Be (Get-TestTarget 'passthru')
            Should-BeNull -Actual $written.Secret
        }

        It 'stores a PSCredential from the pipeline' {
            [pscredential]::new('pipe-user', $secret) | Set-WinCredential -Target (Get-TestTarget 'pipe')
            (Get-WinCredential -Target (Get-TestTarget 'pipe')).UserName | Should-Be 'pipe-user'
        }
    }

    Context 'Update' {
        BeforeAll {
            $target = Get-TestTarget 'update'
            Set-WinCredential -Target $target -UserName 'before' -Secret (ConvertTo-TestSecret 'old')
            Set-WinCredential -Target $target -UserName 'after' -Secret (ConvertTo-TestSecret 'new') -Comment 'updated' -Persist LocalMachine
        }

        It 'replaces every field' {
            $credential = Get-WinCredential -Target $target -IncludeSecret
            $credential.UserName | Should-Be 'after'
            $credential.Comment | Should-Be 'updated'
            $credential.Persist | Should-Be ([WinCred.CredPersist]::LocalMachine)
            ConvertTo-PlainText $credential.Secret | Should-Be 'new'
        }

        It 'keeps exactly one credential for the target' {
            Should-BeCollection -Actual @(Get-WinCredential -Target $target) -Count 1
        }
    }

    Context 'Delete' {
        BeforeAll {
            $target = Get-TestTarget 'delete'
            Set-WinCredential -Target $target -Secret $secret
            Remove-WinCredential -Target $target
        }

        It 'removes the credential' {
            { Get-WinCredential -Target $target } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'reports a repeated delete as not found' {
            { Remove-WinCredential -Target $target } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'removes the credentials piped from Get-WinCredential' {
            Set-WinCredential -Target (Get-TestTarget 'bulk.1') -Secret $secret
            Set-WinCredential -Target (Get-TestTarget 'bulk.2') -Secret $secret
            Should-BeCollection -Actual @(Get-WinCredential -Target (Get-TestTarget 'bulk.*')) -Count 2
            Get-WinCredential -Target (Get-TestTarget 'bulk.*') | Remove-WinCredential
            Should-BeCollection -Actual @(Get-WinCredential -Target (Get-TestTarget 'bulk.*')) -Count 0
        }
    }

    Context 'Missing target' {
        It 'writes CredentialNotFound for an exact target' {
            { Get-WinCredential -Target (Get-TestTarget 'missing') } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'returns nothing and no error for a wildcard without matches' {
            # The prefix exists, so an empty result means the wildcard matched nothing, not that listing failed
            @(Get-WinCredential -Target "$prefix*").Count | Should-BeGreaterThan 0
            $found = @(Get-WinCredential -Target (Get-TestTarget 'missing*') -ErrorVariable errors)
            Should-BeCollection -Actual $found -Count 0
            Should-BeCollection -Actual @($errors) -Count 0
        }

        It 'respects -Type' {
            Set-WinCredential -Target (Get-TestTarget 'typed') -Secret $secret
            { Get-WinCredential -Target (Get-TestTarget 'typed') -Type DomainPassword } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'treats an escaped wildcard character literally' {
            Set-WinCredential -Target (Get-TestTarget 'lit[1]') -Secret $secret
            (Get-WinCredential -Target (Get-TestTarget 'lit`[1`]')).TargetName | Should-Be (Get-TestTarget 'lit[1]')
        }
    }

    Context 'Missing secret' {
        It 'writes nothing' {
            { Set-WinCredential -Target (Get-TestTarget 'nosecret') -UserName 'u' } | Should-Throw -FullyQualifiedErrorId 'MissingSecret,*'
            { Get-WinCredential -Target (Get-TestTarget 'nosecret') } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }
    }

    Context 'Empty user name' {
        It 'stores a credential without a user name' {
            Set-WinCredential -Target (Get-TestTarget 'nouser') -Secret $secret
            $credential = Get-WinCredential -Target (Get-TestTarget 'nouser') -IncludeSecret
            [string]::IsNullOrEmpty($credential.UserName) | Should-BeTrue
            ConvertTo-PlainText $credential.Secret | Should-Be 'P@ss word 1'
        }

        It 'stores an explicitly empty user name' {
            Set-WinCredential -Target (Get-TestTarget 'emptyuser') -UserName '' -Secret $secret
            [string]::IsNullOrEmpty((Get-WinCredential -Target (Get-TestTarget 'emptyuser')).UserName) | Should-BeTrue
        }

        It 'refuses -AsPSCredential without a user name' {
            { Get-WinCredential -Target (Get-TestTarget 'nouser') -AsPSCredential } | Should-Throw -FullyQualifiedErrorId 'NoUserName,*'
        }
    }

    Context 'Unicode with <Encoding> encoding' -ForEach $encodings {
        BeforeAll {
            $target = Get-TestTarget "unicode.$Encoding.$unicodeText"
            $userName = "user.$unicodeText"
            Set-WinCredential -Target $target -UserName $userName -Secret (ConvertTo-TestSecret $unicodeText) -Encoding $Encoding
        }

        It 'round-trips the target, user name and secret' {
            $credential = Get-WinCredential -Target $target -IncludeSecret -Encoding $Encoding
            $credential.TargetName | Should-Be $target
            $credential.UserName | Should-Be $userName
            ConvertTo-PlainText $credential.Secret | Should-Be $unicodeText
        }
    }

    Context 'Persistence <Persist>' -ForEach $persistModes {
        It 'stores and reads <Persist> persistence' {
            $target = Get-TestTarget "persist.$Persist"
            Set-WinCredential -Target $target -Secret $secret -Persist $Persist
            (Get-WinCredential -Target $target).Persist | Should-Be ([WinCred.CredPersist]$Persist)
        }
    }

    Context 'Secret size limits' {
        It 'stores and reads <Length> x ''<Char>'' as a <Encoding> secret' -ForEach ($blobSizes | Where-Object { $_.Fits }) {
            $target = Get-TestTarget "size.$Encoding.$Length.$([int][char]$Char)"
            Set-WinCredential -Target $target -Secret (ConvertTo-TestSecret ($Char * $Length)) -Encoding $Encoding
            (Get-WinCredential -Target $target -IncludeSecret -Encoding $Encoding).Secret.Length | Should-Be $Length
        }

        It 'stores nothing for <Length> x ''<Char>'' as a <Encoding> secret' -ForEach ($blobSizes | Where-Object { -not $_.Fits }) {
            $target = Get-TestTarget "size.$Encoding.$Length.$([int][char]$Char)"
            { Set-WinCredential -Target $target -Secret (ConvertTo-TestSecret ($Char * $Length)) -Encoding $Encoding } | Should-Throw -FullyQualifiedErrorId 'SecretTooLarge,*'
            { Get-WinCredential -Target $target } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }
    }

    Context 'Field length limits' {
        # Each case writes exactly at the limit Set-WinCredential enforces, so Windows MUST accept it

        It 'stores a generic target of 32766 characters' {
            $target = (Get-TestTarget 'longtarget.').PadRight(32766, 't')
            Set-WinCredential -Target $target -Secret $secret
            (Get-WinCredential -Target $target).TargetName.Length | Should-Be 32766
        }

        It 'stores a domain target of 253 characters' {
            $target = Get-DomainTestTarget 253
            Set-WinCredential -Target $target -Type DomainPassword -UserName "$env:COMPUTERNAME\wincred-test" -Secret $secret
            (Get-WinCredential -Target $target -Type DomainPassword).TargetName.Length | Should-Be 253
        }

        It 'stores a domain target label of 63 characters' {
            $target = Get-TestTarget ('a' * 63)
            Set-WinCredential -Target $target -Type DomainPassword -UserName "$env:COMPUTERNAME\wincred-test" -Secret $secret
            (Get-WinCredential -Target $target -Type DomainPassword).TargetName | Should-Be $target
        }

        It 'stores a user name of 512 characters' {
            Set-WinCredential -Target (Get-TestTarget 'longuser') -UserName ('u' * 512) -Secret $secret
            (Get-WinCredential -Target (Get-TestTarget 'longuser')).UserName.Length | Should-Be 512
        }

        It 'stores a comment of 255 characters' {
            Set-WinCredential -Target (Get-TestTarget 'longcomment') -Comment ('c' * 255) -Secret $secret
            (Get-WinCredential -Target (Get-TestTarget 'longcomment')).Comment.Length | Should-Be 255
        }
    }

    Context 'Repeated operations' {
        It 'returns the same result for repeated reads' {
            $target = Get-TestTarget 'reread'
            Set-WinCredential -Target $target -Secret $secret
            $reads = 1..3 | ForEach-Object { Get-WinCredential -Target $target -IncludeSecret }
            foreach ($read in $reads) {
                ConvertTo-PlainText $read.Secret | Should-Be 'P@ss word 1'
                $read.LastWritten | Should-Be $reads[0].LastWritten
            }
        }

        It 'keeps the last of repeated writes' {
            $target = Get-TestTarget 'rewrite'
            foreach ($i in 1..3) { Set-WinCredential -Target $target -Secret (ConvertTo-TestSecret "secret $i") }
            Should-BeCollection -Actual @(Get-WinCredential -Target $target) -Count 1
            ConvertTo-PlainText (Get-WinCredential -Target $target -IncludeSecret).Secret | Should-Be 'secret 3'
        }
    }

    Context 'WhatIf' {
        It 'writes nothing with Set-WinCredential -WhatIf' {
            Set-WinCredential -Target (Get-TestTarget 'whatif') -Secret $secret -WhatIf
            { Get-WinCredential -Target (Get-TestTarget 'whatif') } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'removes nothing with Remove-WinCredential -WhatIf' {
            Set-WinCredential -Target (Get-TestTarget 'keep') -Secret $secret
            Remove-WinCredential -Target (Get-TestTarget 'keep') -WhatIf
            (Get-WinCredential -Target (Get-TestTarget 'keep')).TargetName | Should-Be (Get-TestTarget 'keep')
        }
    }

    Context 'Domain password' {
        BeforeAll {
            $target = Get-TestTarget 'domain'
            Set-WinCredential -Target $target -Type DomainPassword -UserName "$env:COMPUTERNAME\wincred-test" -Secret $secret
        }

        It 'never returns the secret' {
            $credential = Get-WinCredential -Target $target -Type DomainPassword -IncludeSecret
            $credential.Type | Should-Be ([WinCred.CredType]::DomainPassword)
            Should-BeNull -Actual $credential.Secret
        }

        It 'refuses -AsPSCredential' {
            { Get-WinCredential -Target $target -Type DomainPassword -AsPSCredential } | Should-Throw -FullyQualifiedErrorId 'SecretNotReadable,*'
        }

        It 'is removed by type' {
            Remove-WinCredential -Target $target -Type DomainPassword
            { Get-WinCredential -Target $target -Type DomainPassword } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }
    }
}
