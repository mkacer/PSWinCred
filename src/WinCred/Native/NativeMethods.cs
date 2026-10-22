using System;
using System.Runtime.InteropServices;

namespace WinCred.Native
{
    /// <summary>
    /// Declarations from wincred.h. Only the Unicode (W) functions are used.
    /// </summary>
    internal static class NativeMethods
    {
        private const string Advapi32 = "advapi32.dll";

        internal const int ERROR_INVALID_PARAMETER = 87;
        internal const int ERROR_NOT_FOUND = 1168;
        internal const int ERROR_NO_SUCH_LOGON_SESSION = 1312;

        // Measured: CredReadW fails with this HRESULT ("The requested credential requires confirmation")
        // for a credential written with CRED_FLAGS_REQUIRE_CONFIRMATION.
        internal const int CredRequiresConfirmation = unchecked((int)0x80097019);

        // CredWriteW flag. Measured: keeps the stored secret while other fields change; fails with
        // ERROR_INVALID_PARAMETER when a new secret is passed too, and with ERROR_NOT_FOUND (creating
        // nothing) when the credential does not exist.
        internal const uint CRED_PRESERVE_CREDENTIAL_BLOB = 0x1;

        internal const uint CRED_FLAGS_PASSWORD_FOR_CERT = 0x1;
        internal const uint CRED_FLAGS_PROMPT_NOW = 0x2;
        internal const uint CRED_FLAGS_USERNAME_TARGET = 0x4;
        internal const uint CRED_FLAGS_OWF_CRED_BLOB = 0x8;
        internal const uint CRED_FLAGS_REQUIRE_CONFIRMATION = 0x10;
        internal const uint CRED_FLAGS_WILDCARD_MATCH = 0x20;
        internal const uint CRED_FLAGS_VSM_PROTECTED = 0x40;
        internal const uint CRED_FLAGS_NGC_CERT = 0x80;

        // Flags an update writes back. Measured: CredWriteW accepts 0x1, 0x2, 0x8, 0x10 and 0x40 and
        // rejects 0x20, 0x80 and above with ERROR_INVALID_PARAMETER. 0x4 was rejected for a target that
        // differs from the user name, which is presumably its rule; it is kept so an update of such a
        // credential stays as Windows wrote it (inferred, not measured).
        internal const uint WritableFlags = CRED_FLAGS_PASSWORD_FOR_CERT | CRED_FLAGS_PROMPT_NOW | CRED_FLAGS_USERNAME_TARGET
            | CRED_FLAGS_OWF_CRED_BLOB | CRED_FLAGS_REQUIRE_CONFIRMATION | CRED_FLAGS_VSM_PROTECTED;

        internal const int CRED_MAX_STRING_LENGTH = 256;
        internal const int CRED_MAX_USERNAME_LENGTH = 256 + 1 + 256;
        internal const int CRED_MAX_GENERIC_TARGET_NAME_LENGTH = 32767;
        internal const int CRED_MAX_DOMAIN_TARGET_NAME_LENGTH = 256 + 1 + 80;
        internal const int CRED_MAX_CREDENTIAL_BLOB_SIZE = 5 * 512;

        // The limits CredWriteW actually enforces, measured on Windows. The wincred.h constants above
        // appear to count the terminating null: one more character fails with RPC_S_INVALID_BOUND (1734).
        internal const int MaxGenericTargetLength = CRED_MAX_GENERIC_TARGET_NAME_LENGTH - 1;   // 32766
        internal const int MaxUserNameLength = CRED_MAX_USERNAME_LENGTH - 1;                   // 512
        internal const int MaxCommentLength = CRED_MAX_STRING_LENGTH - 1;                      // 255

        // Domain targets are checked as DNS host names, not against CRED_MAX_DOMAIN_TARGET_NAME_LENGTH.
        // Measured: 63 UTF-8 bytes per dot-separated label (31 x U+00E9, 21 x U+65E5, 15 x U+1F600),
        // and 253 in total for ASCII names; longer ones fail with ERROR_INVALID_PARAMETER (87).
        // The total is checked in UTF-8 bytes too, which is inferred for non-ASCII names.
        internal const int MaxDomainTargetBytes = 253;
        internal const int MaxDomainTargetLabelBytes = 63;

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        /// <summary>CREDENTIALW. Strings are IntPtr so this code controls every allocation.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct CREDENTIALW
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CREDENTIAL_ATTRIBUTEW
        {
            public IntPtr Keyword;
            public uint Flags;
            public uint ValueSize;
            public IntPtr Value;
        }

        // DefaultDllImportSearchPaths loads advapi32.dll only from System32.

        [DllImport(Advapi32, EntryPoint = "CredReadW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CredRead(string targetName, uint type, uint flags, out SingleCredentialBuffer credential);

        [DllImport(Advapi32, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CredWrite(ref CREDENTIALW credential, uint flags);

        [DllImport(Advapi32, EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CredDelete(string targetName, uint type, uint flags);

        [DllImport(Advapi32, EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CredEnumerate(string? filter, uint flags, out uint count, out CredentialArrayBuffer credentials);

        [DllImport(Advapi32, EntryPoint = "CredFree", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern void CredFree(IntPtr buffer);
    }
}
