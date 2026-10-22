# Pinned build and test tools. Install-Tools.ps1 saves them to .tools; Import-Tool in Tools.psm1 verifies every
# file against the pin below each time a tool is imported. They are never shipped with the module.
# Update them by hand; Dependabot does not read this file.
#
# SignerThumbprint: every file MUST be signed by exactly this certificate.
# RootThumbprint:   every file MUST be signed by a certificate that chains to one of these roots.
# FileRoots:        extra roots for single files, by path relative to the module folder.
# FileSigners:      exact signing certificates for single files, in place of the root check.
@{
    # Thumbprint from the Pester README's signing-certificate table for 6.0.0-alpha4 and later
    'Pester'                       = @{ Version = '6.2.0'; SignerThumbprint = '147C2FD397677DC76DD198E83E7D9D234AA59D1A' }

    # Microsoft Root Certificate Authority 2011 and 2010
    'PSScriptAnalyzer'             = @{
        Version        = '1.25.0'
        RootThumbprint = @('8F43288AD272F3103B6FB1428485EA3014C0BCFE', '3B1EFD3A66EA28B16697394703A72CA340A05BD5')
        # Measured: signed by 'CN=Json.NET (.NET Foundation)' under DigiCert, not Microsoft. Pinned to that
        # certificate (valid until 3 Nov 2027) rather than trusting the DigiCert root for every file.
        FileSigners    = @{ 'Newtonsoft.Json.dll' = @('1A9FF7E5BDC4E3E5E2F14E4B2DA6029C5AE58DBF') }
    }
    'Microsoft.PowerShell.PlatyPS' = @{
        Version        = '1.0.3'
        RootThumbprint = @('8F43288AD272F3103B6FB1428485EA3014C0BCFE', '3B1EFD3A66EA28B16697394703A72CA340A05BD5')
        # Measured: this bundled .NET library is signed under an older Microsoft root. Allowed for this file only.
        FileRoots      = @{ 'Dependencies/System.Numerics.Vectors.dll' = @('CDD4EEAE6000AC7F40C3802C171E30148030C072') }
    }
}
