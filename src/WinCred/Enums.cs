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

    /// <summary>CRED_FLAGS_* values that Windows can return.</summary>
    [Flags]
    public enum CredFlags : uint
    {
        None = 0,
        PromptNow = 0x2,
        UsernameTarget = 0x4,
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
