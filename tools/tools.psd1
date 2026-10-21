# Pinned build and test tools. Install-Tools.ps1 saves them to .tools; Import-Tool in Tools.psm1 verifies every
# file against the pin below each time a tool is imported. They are never shipped with the module.
# Update them by hand; Dependabot does not read this file.
#
# SignerThumbprint: every file MUST be signed by exactly this certificate.
# RootThumbprint:   every file MUST be signed by a certificate that chains to one of these roots.
@{
    # Thumbprint from the Pester README's signing-certificate table for 6.0.0-alpha4 and later
    'Pester'                       = @{ Version = '6.2.0'; SignerThumbprint = '147C2FD397677DC76DD198E83E7D9D234AA59D1A' }

    # Microsoft Root Certificate Authority 2011 and 2010
    'PSScriptAnalyzer'             = @{ Version = '1.25.0'; RootThumbprint = @('8F43288AD272F3103B6FB1428485EA3014C0BCFE', '3B1EFD3A66EA28B16697394703A72CA340A05BD5') }
    'Microsoft.PowerShell.PlatyPS' = @{ Version = '1.0.3'; RootThumbprint = @('8F43288AD272F3103B6FB1428485EA3014C0BCFE', '3B1EFD3A66EA28B16697394703A72CA340A05BD5') }
}
