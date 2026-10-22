using System;

namespace WinCred
{
    /// <summary>CRED_TYPE_* values.</summary>
    public enum CredType : uint
    {
        Generic = 1,
        DomainPassword = 2,
        DomainCertificate = 3,
        DomainVisiblePassword = 4,
        GenericCertificate = 5,
        DomainExtended = 6,
    }

    /// <summary>CRED_PERSIST_* values.</summary>
    public enum CredPersist : uint
    {
        Session = 1,
        LocalMachine = 2,
        Enterprise = 3,
    }

    /// <summary>CRED_FLAGS_* values from wincred.h.</summary>
    [Flags]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "Mirrors CRED_FLAGS_* in wincred.h.")]
    public enum CredFlags : uint
    {
        None = 0,
        PasswordForCert = 0x1,
        PromptNow = 0x2,
        UsernameTarget = 0x4,
        OwfCredBlob = 0x8,
        RequireConfirmation = 0x10,
        WildcardMatch = 0x20,
        VsmProtected = 0x40,
        NgcCert = 0x80,
    }

    /// <summary>
    /// Text encoding of the secret. Windows stores the secret as opaque bytes;
    /// Unicode (UTF-16LE) is what Windows and most programs use.
    /// </summary>
    public enum CredEncoding
    {
        Unicode = 0,
        UTF8 = 1,
    }
}
