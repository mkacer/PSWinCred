#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; RequiredVersion = '6.2.0' }
<#
    Tests of the built WinCred module. Run through tests/Invoke-Tests.ps1.

    Tests tagged 'Store' read and write the current user's Windows Credential Manager. They only touch
    targets named WinCred.Test.*: they first remove leftovers of earlier runs, then remove their own
    credentials and fail if any remain.
    Tests tagged 'Persistent' write LocalMachine or Enterprise credentials, which outlive the logon session;
    'Roaming' ones write Enterprise credentials, which can roam to other computers with the user profile.
    Invoke-Tests.ps1 runs them only with -IncludePersistent.
    Tests tagged 'Help' need the module built with help (build.ps1 without -SkipHelp).
    Set- and Remove-WinCredential ask for confirmation by default, so every call here passes -Confirm:$false.
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
    $controlCharacters = @(
        @{ Field = 'Target' }
        @{ Field = 'UserName' }
        @{ Field = 'Comment' }
    )
}

BeforeAll {
    Import-Module $ModulePath -Force

    $prefix = "WinCred.Test.$([guid]::NewGuid().ToString('N'))"
    $domainUser = "$env:COMPUTERNAME\wincred-test"

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

    function Get-ConfirmImpact([string]$Command) {
        (Get-Command $Command).ImplementingType.GetCustomAttributes([System.Management.Automation.CmdletAttribute], $false)[0].ConfirmImpact
    }

    # a-umlaut, e-acute, a surrogate pair (U+1F600) and two CJK characters
    $unicodeText = -join @('p', [char]0xE4, [char]0xE9, 's', [char]::ConvertFromUtf32(0x1F600), [char]0x65E5, [char]0x672C)
}

AfterAll {
    Remove-Module WinCred -ErrorAction SilentlyContinue
}

Describe 'Module surface' {
    It 'exports exactly Get-, New-, Set- and Remove-WinCredential' {
        $names = @((Get-Command -Module WinCred).Name | Sort-Object)
        Should-BeCollection -Actual $names -Expected @('Get-WinCredential', 'New-WinCredential', 'Remove-WinCredential', 'Set-WinCredential')
    }

    It 'has no parameter that takes or returns a plain-text secret' {
        foreach ($command in 'New-WinCredential', 'Set-WinCredential') {
            foreach ($name in 'Password', 'PlainText') { (Get-Command $command).Parameters.ContainsKey($name) | Should-BeFalse }
            (Get-Command $command).Parameters['Secret'].ParameterType | Should-Be ([securestring])
        }
        foreach ($name in 'IncludeClearPassword', 'AsPlainText') { (Get-Command Get-WinCredential).Parameters.ContainsKey($name) | Should-BeFalse }
    }

    It 'requires -Target for New-, Set- and Remove-WinCredential' {
        foreach ($command in 'New-WinCredential', 'Set-WinCredential', 'Remove-WinCredential') {
            $mandatory = (Get-Command $command).Parameters['Target'].Attributes | Where-Object { $_ -is [Parameter] -and $_.Mandatory }
            Should-BeCollection -Actual @($mandatory) -Count 1
        }
    }

    It 'supports -WhatIf and -Confirm on every command that writes' {
        foreach ($command in 'New-WinCredential', 'Set-WinCredential', 'Remove-WinCredential') {
            (Get-Command $command).Parameters.ContainsKey('WhatIf') | Should-BeTrue
            (Get-Command $command).Parameters.ContainsKey('Confirm') | Should-BeTrue
        }
    }

    It 'asks for confirmation by default before overwriting or deleting' {
        Get-ConfirmImpact 'Set-WinCredential' | Should-Be ([System.Management.Automation.ConfirmImpact]::High)
        Get-ConfirmImpact 'Remove-WinCredential' | Should-Be ([System.Management.Automation.ConfirmImpact]::High)
        Get-ConfirmImpact 'New-WinCredential' | Should-Be ([System.Management.Automation.ConfirmImpact]::Medium)
    }

    It 'binds piped credential objects to -LiteralTarget, never to the wildcard -Target' {
        $get = Get-Command Get-WinCredential
        @($get.Parameters['Target'].Attributes | Where-Object { $_ -is [Parameter] -and $_.ValueFromPipelineByPropertyName }).Count | Should-Be 0
        @($get.Parameters['LiteralTarget'].Attributes | Where-Object { $_ -is [Parameter] -and $_.ValueFromPipelineByPropertyName }).Count | Should-BeGreaterThan 0
        $get.Parameters['LiteralTarget'].Aliases | Should-Be 'TargetName'
        # A default parameter set makes the binder convert piped objects to strings for -Target first
        $get.DefaultParameterSet | Should-BeFalsy
    }

    It 'loads cmdlet help' -Tag 'Help' {
        (Get-Help Get-WinCredential).Synopsis | Should-Be 'Gets credentials from Windows Credential Manager.'
        (Get-Help New-WinCredential).Synopsis | Should-Be 'Creates a credential in Windows Credential Manager.'
        (Get-Help Set-WinCredential).Synopsis | Should-Be 'Changes an existing credential in Windows Credential Manager.'
        (Get-Help Remove-WinCredential).Synopsis | Should-Be 'Deletes a credential from Windows Credential Manager.'
    }

    It 'loads the about_WinCred topic' -Tag 'Help' {
        Get-Help about_WinCred | Out-String | Should-BeLikeString '*Windows Credential Manager*'
    }
}

Describe 'Input validation' {
    # These cases fail before any call to Windows, so nothing can be written

    It 'fails without a secret instead of prompting or generating one' {
        { New-WinCredential -Target (Get-TestTarget 'nosecret') -UserName 'u' } | Should-Throw -FullyQualifiedErrorId 'MissingSecret,*'
    }

    It 'rejects -Secret together with -Credential' {
        $credential = [pscredential]::new('u', (ConvertTo-TestSecret 'x'))
        { New-WinCredential -Target (Get-TestTarget 'both') -Secret (ConvertTo-TestSecret 'y') -Credential $credential } |
            Should-Throw -FullyQualifiedErrorId 'AmbiguousParameterSet,*'
    }

    It 'rejects credential types it cannot write' {
        { New-WinCredential -Target (Get-TestTarget 'cert') -Secret (ConvertTo-TestSecret 'x') -Type DomainCertificate } |
            Should-Throw -FullyQualifiedErrorId 'ParameterArgumentValidationError,*'
    }

    It 'rejects a generic target over 32766 characters' {
        # Windows accepts 32766, one fewer than CRED_MAX_GENERIC_TARGET_NAME_LENGTH
        { New-WinCredential -Target ('t' * 32767) -Secret (ConvertTo-TestSecret 'x') } | Should-Throw -FullyQualifiedErrorId 'TargetTooLong,*'
    }

    It 'rejects a domain target over 253 bytes' {
        { New-WinCredential -Target (Get-DomainTestTarget 254) -Secret (ConvertTo-TestSecret 'x') -Type DomainPassword -UserName 'd\u' } |
            Should-Throw -FullyQualifiedErrorId 'TargetTooLong,*'
    }

    It 'rejects a domain target label over 63 bytes' {
        { New-WinCredential -Target (Get-TestTarget ('a' * 64)) -Secret (ConvertTo-TestSecret 'x') -Type DomainPassword -UserName 'd\u' } |
            Should-Throw -FullyQualifiedErrorId 'TargetLabelTooLong,*'
    }

    It 'counts domain labels in UTF-8 bytes' {
        # 32 x e-acute is 32 characters but 64 UTF-8 bytes; Windows accepts at most 31
        $label = [string][char]0xE9 * 32
        { New-WinCredential -Target (Get-TestTarget $label) -Secret (ConvertTo-TestSecret 'x') -Type DomainPassword -UserName 'd\u' } |
            Should-Throw -FullyQualifiedErrorId 'TargetLabelTooLong,*'
    }

    It 'allows long labels in generic targets' {
        # Only domain targets are host names; this fails later, on the missing secret, not on the label
        { New-WinCredential -Target (Get-TestTarget ('a' * 64)) } | Should-Throw -FullyQualifiedErrorId 'MissingSecret,*'
    }

    It 'rejects a user name over 512 characters' {
        { New-WinCredential -Target (Get-TestTarget 'longuser') -Secret (ConvertTo-TestSecret 'x') -UserName ('u' * 513) } |
            Should-Throw -FullyQualifiedErrorId 'UserNameTooLong,*'
    }

    It 'rejects a comment over 255 characters' {
        { New-WinCredential -Target (Get-TestTarget 'longcomment') -Secret (ConvertTo-TestSecret 'x') -Comment ('c' * 256) } |
            Should-Throw -FullyQualifiedErrorId 'CommentTooLong,*'
    }

    It 'rejects a control character in the <Field>' -ForEach $controlCharacters {
        $values = @{ Target = (Get-TestTarget 'control'); UserName = 'u'; Comment = 'c' }
        $values[$Field] = "line`nbreak"
        { New-WinCredential @values -Secret (ConvertTo-TestSecret 'x') } | Should-Throw -FullyQualifiedErrorId 'InvalidCharacters,*'
    }

    It 'requires a DOMAIN\user or user@domain user name for DomainPassword credentials' {
        foreach ($userName in $null, 'plain', '\user', 'DOMAIN\', '@domain', 'user@') {
            $parameters = @{ Target = (Get-TestTarget 'domainuser'); Type = 'DomainPassword'; Secret = (ConvertTo-TestSecret 'x') }
            if ($null -ne $userName) { $parameters.UserName = $userName }
            { New-WinCredential @parameters } | Should-Throw -FullyQualifiedErrorId 'InvalidUserName,*'
        }
        { Set-WinCredential -Target (Get-TestTarget 'domainuser') -Type DomainPassword -UserName 'plain' -Confirm:$false } |
            Should-Throw -FullyQualifiedErrorId 'InvalidUserName,*'
    }

    It 'rejects <Length> x ''<Char>'' as a <Encoding> secret' -ForEach ($blobSizes | Where-Object { -not $_.Fits }) {
        { New-WinCredential -Target (Get-TestTarget 'toolarge') -Secret (ConvertTo-TestSecret ($Char * $Length)) -Encoding $Encoding } |
            Should-Throw -FullyQualifiedErrorId 'SecretTooLarge,*'
    }

    It 'rejects a secret that is not valid Unicode as <Encoding> instead of altering it' -ForEach $encodings {
        $loneSurrogate = 'a' + [char]0xD800 + 'b'
        { New-WinCredential -Target (Get-TestTarget 'surrogate') -Secret (ConvertTo-TestSecret $loneSurrogate) -Encoding $Encoding } |
            Should-Throw -FullyQualifiedErrorId 'SecretNotEncodable,*'
    }

    It 'rejects Set-WinCredential without anything to change' {
        { Set-WinCredential -Target (Get-TestTarget 'nothing') -Confirm:$false } | Should-Throw -FullyQualifiedErrorId 'NothingToSet,*'
    }

    It 'rejects -Encoding on Set-WinCredential without a new secret' {
        { Set-WinCredential -Target (Get-TestTarget 'encoding') -Comment 'c' -Encoding UTF8 -Confirm:$false } |
            Should-Throw -FullyQualifiedErrorId 'EncodingWithoutSecret,*'
    }

    It 'rejects a UTF8 secret for a DomainPassword credential with <Command>' -ForEach @(
        @{ Command = 'New-WinCredential' }
        @{ Command = 'Set-WinCredential' }
    ) {
        # Windows uses a DomainPassword secret as the Unicode sign-in password
        $parameters = @{ Target = 'server.example.com'; Type = 'DomainPassword'; UserName = 'EXAMPLE\user'; Secret = (ConvertTo-TestSecret 'p'); Encoding = 'UTF8' }
        if ($Command -eq 'Set-WinCredential') { $parameters.Confirm = $false }
        { & $Command @parameters } | Should-Throw -FullyQualifiedErrorId 'EncodingNotSupported,*'
    }

    It 'rejects a malformed wildcard before reading the store' {
        { Get-WinCredential -Target 'WinCred.Test.x[1' } | Should-Throw -FullyQualifiedErrorId 'InvalidWildcard,*'
    }

    # Windows reads a name only up to the first NUL, so 'name<NUL>x' would act on 'name'
    It 'rejects a NUL in the target with <Name>' -ForEach @(
        @{ Name = 'Get-WinCredential -Target'; Call = { Get-WinCredential -Target "WinCred.Test.nul`0x" } }
        @{ Name = 'Get-WinCredential -Target with a wildcard'; Call = { Get-WinCredential -Target "WinCred.Test.nul`0x*" } }
        @{ Name = 'Get-WinCredential -LiteralTarget'; Call = { Get-WinCredential -LiteralTarget "WinCred.Test.nul`0x" } }
        @{ Name = 'Set-WinCredential'; Call = { Set-WinCredential -Target "WinCred.Test.nul`0x" -Comment 'c' -Confirm:$false } }
        @{ Name = 'Remove-WinCredential'; Call = { Remove-WinCredential -Target "WinCred.Test.nul`0x" -Confirm:$false } }
    ) {
        $Call | Should-Throw -FullyQualifiedErrorId 'InvalidCharacters,*'
    }
}

Describe 'Credential store' -Tag 'Store' {
    BeforeAll {
        # Leftovers of interrupted runs; every WinCred.Test.* credential belongs to this suite
        Get-WinCredential -Target 'WinCred.Test.*' | Remove-WinCredential -Confirm:$false
        $secret = ConvertTo-TestSecret 'P@ss word 1'
    }

    AfterAll {
        Get-WinCredential -Target "$prefix*" | Remove-WinCredential -Confirm:$false
        $left = @(Get-WinCredential -Target "$prefix*")
        if ($left.Count -gt 0) {
            throw "Cleanup left $($left.Count) test credential(s): $($left.TargetName -join ', ')"
        }
    }

    Context 'Create and read' {
        BeforeAll {
            $target = Get-TestTarget 'main'
            $result = New-WinCredential -Target $target -UserName 'test-user' -Secret $secret
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
            $credential.LastWritten | Should-BeGreaterThan ([DateTimeOffset]::Now.AddMinutes(-10))
        }

        It 'reads the secret with -IncludeSecret' {
            ConvertTo-PlainText (Get-WinCredential -Target $target -IncludeSecret).Secret | Should-Be 'P@ss word 1'
        }

        It 'stores the secret as Unicode by default' {
            ConvertTo-PlainText (Get-WinCredential -Target $target -IncludeSecret -Encoding Unicode).Secret | Should-Be 'P@ss word 1'
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

        It 'returns the created credential without its secret with -PassThru' {
            $created = New-WinCredential -Target (Get-TestTarget 'passthru') -UserName 'u' -Secret $secret -PassThru
            $created.TargetName | Should-Be (Get-TestTarget 'passthru')
            Should-BeNull -Actual $created.Secret
        }

        It 'stores a PSCredential from the pipeline' {
            [pscredential]::new('pipe-user', $secret) | New-WinCredential -Target (Get-TestTarget 'pipe')
            (Get-WinCredential -Target (Get-TestTarget 'pipe')).UserName | Should-Be 'pipe-user'
        }

        It 'refuses to replace an existing credential' {
            { New-WinCredential -Target $target -UserName 'other' -Secret (ConvertTo-TestSecret 'other') } |
                Should-Throw -FullyQualifiedErrorId 'CredentialExists,*'
            $credential = Get-WinCredential -Target $target -IncludeSecret
            $credential.UserName | Should-Be 'test-user'
            ConvertTo-PlainText $credential.Secret | Should-Be 'P@ss word 1'
        }
    }

    Context 'Update' {
        BeforeEach {
            $target = Get-TestTarget "update.$([guid]::NewGuid().ToString('N'))"
            New-WinCredential -Target $target -UserName 'before' -Secret (ConvertTo-TestSecret 'old') -Comment 'old comment'
        }

        It 'changes only the comment and keeps the secret, user name and persistence' {
            Set-WinCredential -Target $target -Comment 'new comment' -Confirm:$false
            $credential = Get-WinCredential -Target $target -IncludeSecret
            $credential.Comment | Should-Be 'new comment'
            $credential.UserName | Should-Be 'before'
            $credential.Persist | Should-Be ([WinCred.CredPersist]::Session)
            ConvertTo-PlainText $credential.Secret | Should-Be 'old'
        }

        It 'replaces the secret and keeps the other fields' {
            Set-WinCredential -Target $target -Secret (ConvertTo-TestSecret 'new') -Confirm:$false
            $credential = Get-WinCredential -Target $target -IncludeSecret
            ConvertTo-PlainText $credential.Secret | Should-Be 'new'
            $credential.UserName | Should-Be 'before'
            $credential.Comment | Should-Be 'old comment'
        }

        It 'clears a field set to an empty string' {
            Set-WinCredential -Target $target -UserName '' -Comment '' -Confirm:$false
            $credential = Get-WinCredential -Target $target
            [string]::IsNullOrEmpty($credential.UserName) | Should-BeTrue
            [string]::IsNullOrEmpty($credential.Comment) | Should-BeTrue
        }

        It 'sets the user name and secret from a PSCredential' {
            Set-WinCredential -Target $target -Credential ([pscredential]::new('after', (ConvertTo-TestSecret 'pscred'))) -Confirm:$false
            $credential = Get-WinCredential -Target $target -IncludeSecret
            $credential.UserName | Should-Be 'after'
            ConvertTo-PlainText $credential.Secret | Should-Be 'pscred'
        }

        It 'updates the credentials piped from Get-WinCredential' {
            Get-WinCredential -Target $target | Set-WinCredential -Comment 'piped' -Confirm:$false
            (Get-WinCredential -Target $target).Comment | Should-Be 'piped'
        }

        It 'changes nothing with -WhatIf' {
            Set-WinCredential -Target $target -Comment 'whatif' -WhatIf
            (Get-WinCredential -Target $target).Comment | Should-Be 'old comment'
        }

        It 'keeps exactly one credential for the target' {
            Set-WinCredential -Target $target -Secret (ConvertTo-TestSecret 'again') -Confirm:$false
            Should-BeCollection -Actual @(Get-WinCredential -Target $target) -Count 1
        }

        It 'keeps LocalMachine persistence when only the secret changes' -Tag 'Persistent' {
            Set-WinCredential -Target $target -Persist LocalMachine -Confirm:$false
            Set-WinCredential -Target $target -Secret (ConvertTo-TestSecret 'rotated') -Confirm:$false
            $credential = Get-WinCredential -Target $target -IncludeSecret
            $credential.Persist | Should-Be ([WinCred.CredPersist]::LocalMachine)
            ConvertTo-PlainText $credential.Secret | Should-Be 'rotated'
        }

        It 'reports a missing credential and creates nothing' {
            $missing = Get-TestTarget 'update.missing'
            { Set-WinCredential -Target $missing -Secret (ConvertTo-TestSecret 'x') -Confirm:$false } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
            { Get-WinCredential -Target $missing } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }
    }

    Context 'Delete' {
        BeforeAll {
            $target = Get-TestTarget 'delete'
            New-WinCredential -Target $target -Secret $secret
            Remove-WinCredential -Target $target -Confirm:$false
        }

        It 'removes the credential' {
            { Get-WinCredential -Target $target } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'reports a repeated delete as not found' {
            { Remove-WinCredential -Target $target -Confirm:$false } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'removes the credentials piped from Get-WinCredential' {
            New-WinCredential -Target (Get-TestTarget 'bulk.1') -Secret $secret
            New-WinCredential -Target (Get-TestTarget 'bulk.2') -Secret $secret
            Should-BeCollection -Actual @(Get-WinCredential -Target (Get-TestTarget 'bulk.*')) -Count 2
            Get-WinCredential -Target (Get-TestTarget 'bulk.*') | Remove-WinCredential -Confirm:$false
            Should-BeCollection -Actual @(Get-WinCredential -Target (Get-TestTarget 'bulk.*')) -Count 0
        }

        It 'removes nothing with -WhatIf' {
            New-WinCredential -Target (Get-TestTarget 'keep') -Secret $secret
            Remove-WinCredential -Target (Get-TestTarget 'keep') -WhatIf
            (Get-WinCredential -Target (Get-TestTarget 'keep')).TargetName | Should-Be (Get-TestTarget 'keep')
        }
    }

    Context 'Lookup' {
        BeforeAll {
            $bracketTarget = Get-TestTarget 'lit[1]'
            New-WinCredential -Target $bracketTarget -Secret $secret
            New-WinCredential -Target (Get-TestTarget 'lit1') -Secret $secret
        }

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
            { Get-WinCredential -Target (Get-TestTarget 'lit1') -Type DomainPassword } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'treats an escaped wildcard character literally' {
            (Get-WinCredential -Target (Get-TestTarget 'lit`[1`]')).TargetName | Should-Be $bracketTarget
        }

        It 'matches -LiteralTarget exactly' {
            $found = @(Get-WinCredential -LiteralTarget $bracketTarget)
            Should-BeCollection -Actual $found -Count 1
            $found[0].TargetName | Should-Be $bracketTarget
        }

        It 'round-trips piped credential objects literally' {
            # As a wildcard, 'lit[1]' would match 'lit1' instead
            $found = @(Get-WinCredential -LiteralTarget $bracketTarget | Get-WinCredential)
            Should-BeCollection -Actual $found -Count 1
            $found[0].TargetName | Should-Be $bracketTarget
        }

        It 'reads a piped object with a TargetName property as a literal name' {
            # As a wildcard, the name would match nothing and write no error
            $missing = [pscustomobject]@{ TargetName = (Get-TestTarget 'missing[1]') }
            { $missing | Get-WinCredential } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }

        It 'reads piped strings as wildcards' {
            $found = @((Get-TestTarget 'lit[1]') | Get-WinCredential)
            Should-BeCollection -Actual $found -Count 1
            $found[0].TargetName | Should-Be (Get-TestTarget 'lit1')
        }

        It 'returns one PSCredential when Windows also saved a DomainPassword credential under the name' {
            # What 'Remember my credentials' leaves behind: Windows never returns the domain secret, so
            # -AsPSCredential without -Type uses the credential it can read and reports no error
            $shared = Get-TestTarget 'shared'
            New-WinCredential -Target $shared -UserName 'test-user' -Secret $secret
            New-WinCredential -Target $shared -Type DomainPassword -UserName $domainUser -Secret $secret
            $found = @(Get-WinCredential -Target $shared -AsPSCredential -ErrorVariable errors)
            Should-BeCollection -Actual $found -Count 1
            $found[0].UserName | Should-Be 'test-user'
            Should-BeCollection -Actual @($errors) -Count 0
            $found = @(Get-WinCredential -Target "$shared*" -AsPSCredential -ErrorVariable errors)
            Should-BeCollection -Actual $found -Count 1
            Should-BeCollection -Actual @($errors) -Count 0
            # Asked for by type, the domain credential still explains why it cannot be a PSCredential
            { Get-WinCredential -Target $shared -Type DomainPassword -AsPSCredential } | Should-Throw -FullyQualifiedErrorId 'SecretNotReadable,*'
        }

        It 'finds a DomainPassword credential by exact name without -Type' {
            $domainTarget = Get-TestTarget 'lookup-domain'
            New-WinCredential -Target $domainTarget -Type DomainPassword -UserName $domainUser -Secret $secret
            (Get-WinCredential -Target $domainTarget).Type | Should-Be ([WinCred.CredType]::DomainPassword)
        }
    }

    Context 'Empty user name' {
        It 'stores a credential without a user name' {
            New-WinCredential -Target (Get-TestTarget 'nouser') -Secret $secret
            $credential = Get-WinCredential -Target (Get-TestTarget 'nouser') -IncludeSecret
            [string]::IsNullOrEmpty($credential.UserName) | Should-BeTrue
            ConvertTo-PlainText $credential.Secret | Should-Be 'P@ss word 1'
        }

        It 'stores an explicitly empty user name' {
            New-WinCredential -Target (Get-TestTarget 'emptyuser') -UserName '' -Secret $secret
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
            New-WinCredential -Target $target -UserName $userName -Secret (ConvertTo-TestSecret $unicodeText) -Encoding $Encoding
        }

        It 'round-trips the target, user name and secret' {
            $credential = Get-WinCredential -Target $target -IncludeSecret -Encoding $Encoding
            $credential.TargetName | Should-Be $target
            $credential.UserName | Should-Be $userName
            ConvertTo-PlainText $credential.Secret | Should-Be $unicodeText
        }
    }

    Context 'Encoding mismatch' {
        It 'reports a secret that is not valid in the requested encoding and still returns the credential' {
            $target = Get-TestTarget 'mismatch'
            # 3 bytes of UTF-8 cannot be UTF-16
            New-WinCredential -Target $target -Secret (ConvertTo-TestSecret 'abc') -Encoding UTF8
            $credential = Get-WinCredential -Target $target -IncludeSecret -ErrorAction SilentlyContinue -ErrorVariable errors
            $credential.TargetName | Should-Be $target
            Should-BeNull -Actual $credential.Secret
            $errors[0].FullyQualifiedErrorId | Should-BeLikeString 'SecretNotDecodable,*'
        }
    }

    Context 'Persistence' {
        It 'stores and reads Session persistence' {
            New-WinCredential -Target (Get-TestTarget 'persist.Session') -Secret $secret -Persist Session
            (Get-WinCredential -Target (Get-TestTarget 'persist.Session')).Persist | Should-Be ([WinCred.CredPersist]::Session)
        }

        It 'stores and reads LocalMachine persistence' -Tag 'Persistent' {
            New-WinCredential -Target (Get-TestTarget 'persist.LocalMachine') -Secret $secret -Persist LocalMachine
            (Get-WinCredential -Target (Get-TestTarget 'persist.LocalMachine')).Persist | Should-Be ([WinCred.CredPersist]::LocalMachine)
        }

        It 'stores and reads Enterprise persistence' -Tag 'Persistent', 'Roaming' {
            New-WinCredential -Target (Get-TestTarget 'persist.Enterprise') -Secret $secret -Persist Enterprise
            (Get-WinCredential -Target (Get-TestTarget 'persist.Enterprise')).Persist | Should-Be ([WinCred.CredPersist]::Enterprise)
        }
    }

    Context 'Secret size limits' {
        It 'stores and reads <Length> x ''<Char>'' as a <Encoding> secret' -ForEach ($blobSizes | Where-Object { $_.Fits }) {
            $target = Get-TestTarget "size.$Encoding.$Length.$([int][char]$Char)"
            New-WinCredential -Target $target -Secret (ConvertTo-TestSecret ($Char * $Length)) -Encoding $Encoding
            (Get-WinCredential -Target $target -IncludeSecret -Encoding $Encoding).Secret.Length | Should-Be $Length
        }

        It 'stores nothing for <Length> x ''<Char>'' as a <Encoding> secret' -ForEach ($blobSizes | Where-Object { -not $_.Fits }) {
            $target = Get-TestTarget "size.$Encoding.$Length.$([int][char]$Char)"
            { New-WinCredential -Target $target -Secret (ConvertTo-TestSecret ($Char * $Length)) -Encoding $Encoding } | Should-Throw -FullyQualifiedErrorId 'SecretTooLarge,*'
            { Get-WinCredential -Target $target } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }
    }

    Context 'Field length limits' {
        # Each case writes exactly at the limit WinCred enforces, so Windows MUST accept it

        It 'stores a generic target of 32766 characters' {
            $target = (Get-TestTarget 'longtarget.').PadRight(32766, 't')
            New-WinCredential -Target $target -Secret $secret
            (Get-WinCredential -Target $target).TargetName.Length | Should-Be 32766
        }

        It 'stores a domain target of 253 bytes' {
            $target = Get-DomainTestTarget 253
            New-WinCredential -Target $target -Type DomainPassword -UserName $domainUser -Secret $secret
            (Get-WinCredential -Target $target -Type DomainPassword).TargetName.Length | Should-Be 253
        }

        It 'stores a domain target label of 63 bytes' {
            $target = Get-TestTarget ('a' * 63)
            New-WinCredential -Target $target -Type DomainPassword -UserName $domainUser -Secret $secret
            (Get-WinCredential -Target $target -Type DomainPassword).TargetName | Should-Be $target
        }

        It 'stores a non-ASCII domain target label of 62 UTF-8 bytes' {
            $target = Get-TestTarget ([string][char]0xE9 * 31)
            New-WinCredential -Target $target -Type DomainPassword -UserName $domainUser -Secret $secret
            (Get-WinCredential -Target $target -Type DomainPassword).TargetName | Should-Be $target
        }

        It 'stores a user name of 512 characters' {
            New-WinCredential -Target (Get-TestTarget 'longuser') -UserName ('u' * 512) -Secret $secret
            (Get-WinCredential -Target (Get-TestTarget 'longuser')).UserName.Length | Should-Be 512
        }

        It 'stores a comment of 255 characters' {
            New-WinCredential -Target (Get-TestTarget 'longcomment') -Comment ('c' * 255) -Secret $secret
            (Get-WinCredential -Target (Get-TestTarget 'longcomment')).Comment.Length | Should-Be 255
        }
    }

    Context 'Repeated operations' {
        It 'returns the same result for repeated reads' {
            $target = Get-TestTarget 'reread'
            New-WinCredential -Target $target -Secret $secret
            $reads = 1..3 | ForEach-Object { Get-WinCredential -Target $target -IncludeSecret }
            foreach ($read in $reads) {
                ConvertTo-PlainText $read.Secret | Should-Be 'P@ss word 1'
                $read.LastWritten | Should-Be $reads[0].LastWritten
            }
        }

        It 'keeps the last of repeated updates' {
            $target = Get-TestTarget 'rewrite'
            New-WinCredential -Target $target -Secret $secret
            foreach ($i in 1..3) { Set-WinCredential -Target $target -Secret (ConvertTo-TestSecret "secret $i") -Confirm:$false }
            Should-BeCollection -Actual @(Get-WinCredential -Target $target) -Count 1
            ConvertTo-PlainText (Get-WinCredential -Target $target -IncludeSecret).Secret | Should-Be 'secret 3'
        }
    }

    Context 'WhatIf' {
        It 'writes nothing with New-WinCredential -WhatIf' {
            New-WinCredential -Target (Get-TestTarget 'whatif') -Secret $secret -WhatIf
            { Get-WinCredential -Target (Get-TestTarget 'whatif') } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }
    }

    Context 'Domain password' {
        BeforeAll {
            $target = Get-TestTarget 'domain'
            New-WinCredential -Target $target -Type DomainPassword -UserName $domainUser -Secret $secret
        }

        It 'never returns the secret' {
            $credential = Get-WinCredential -Target $target -Type DomainPassword -IncludeSecret
            $credential.Type | Should-Be ([WinCred.CredType]::DomainPassword)
            Should-BeNull -Actual $credential.Secret
        }

        It 'refuses -AsPSCredential' {
            { Get-WinCredential -Target $target -Type DomainPassword -AsPSCredential } | Should-Throw -FullyQualifiedErrorId 'SecretNotReadable,*'
        }

        It 'refuses -AsPSCredential without -Type when no readable credential has the name' {
            { Get-WinCredential -Target $target -AsPSCredential } | Should-Throw -FullyQualifiedErrorId 'SecretNotReadable,*'
        }

        It 'accepts a comment-only change' {
            # Proves only that the write with CRED_PRESERVE_CREDENTIAL_BLOB succeeds. No program can read a
            # DomainPassword secret back, so this test cannot show that the secret was kept.
            Set-WinCredential -Target $target -Type DomainPassword -Comment 'changed' -Confirm:$false
            (Get-WinCredential -Target $target -Type DomainPassword).Comment | Should-Be 'changed'
        }

        It 'is removed by type' {
            Remove-WinCredential -Target $target -Type DomainPassword -Confirm:$false
            { Get-WinCredential -Target $target -Type DomainPassword } | Should-Throw -FullyQualifiedErrorId 'CredentialNotFound,*'
        }
    }
}
