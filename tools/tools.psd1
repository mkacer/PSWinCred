# Pinned build and test tools. Install-Tools.ps1 saves them to .tools and verifies their signatures.
# They are never shipped with the module. Update them by hand; Dependabot does not read this file.
@{
    # Thumbprint from the Pester README's signing-certificate table for 6.0.0-alpha4 and later
    'Pester'                       = @{ Version = '6.2.0'; SignerThumbprint = '147C2FD397677DC76DD198E83E7D9D234AA59D1A' }
    'PSScriptAnalyzer'             = @{ Version = '1.25.0'; SignerSubject = 'CN=Microsoft Corporation, *' }
    'Microsoft.PowerShell.PlatyPS' = @{ Version = '1.0.3'; SignerSubject = 'CN=Microsoft Corporation, *' }
}
