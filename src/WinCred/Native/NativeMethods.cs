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

        internal const int ERROR_NOT_FOUND = 1168;

        internal const int CRED_MAX_STRING_LENGTH = 256;
        internal const int CRED_MAX_USERNAME_LENGTH = 256 + 1 + 256;
        internal const int CRED_MAX_GENERIC_TARGET_NAME_LENGTH = 32767;

        // The limits CredWriteW actually enforces, measured on Windows. The wincred.h constants above
        // appear to count the terminating null: one more character fails with RPC_S_INVALID_BOUND (1734).
        internal const int MaxGenericTargetLength = CRED_MAX_GENERIC_TARGET_NAME_LENGTH - 1;   // 32766
        internal const int MaxUserNameLength = CRED_MAX_USERNAME_LENGTH - 1;                   // 512
        internal const int MaxCommentLength = CRED_MAX_STRING_LENGTH - 1;                      // 255

        // Domain targets are checked as DNS host names, not against CRED_MAX_DOMAIN_TARGET_NAME_LENGTH:
        // measured, 253 characters in total and 63 per dot-separated label (the RFC 1035 limits).
        // Longer ones fail with ERROR_INVALID_PARAMETER (87).
        internal const int MaxDomainTargetLength = 253;
        internal const int MaxDomainTargetLabelLength = 63;

        internal const int ERROR_INVALID_PARAMETER = 87;
        internal const int CRED_MAX_DOMAIN_TARGET_NAME_LENGTH = 256 + 1 + 80;
        internal const int CRED_MAX_CREDENTIAL_BLOB_SIZE = 5 * 512;

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
        internal static extern bool CredRead(string targetName, uint type, uint flags, out CredentialHandle credential);

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
        internal static extern bool CredEnumerate(string? filter, uint flags, out uint count, out CredentialHandle credentials);

        [DllImport(Advapi32, EntryPoint = "CredFree", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern void CredFree(IntPtr buffer);
    }
}
