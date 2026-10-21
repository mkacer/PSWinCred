using System.Runtime.InteropServices;

namespace WinCred.Native
{
    /// <summary>
    /// The CredMan calls used by <see cref="CredMan"/>. Each returns 0 on success or the Win32 error code.
    /// Tests replace it with a fake.
    /// </summary>
    internal interface ICredentialApi
    {
        int Read(string targetName, CredType type, out SingleCredentialBuffer credential);

        /// <summary>
        /// <paramref name="filter"/> is null for all credentials, a name for an exact match, or a prefix
        /// followed by "*". Measured: matching ignores case, and "[", "?" and a non-trailing "*" are literal.
        /// </summary>
        int Enumerate(string? filter, out CredentialArrayBuffer credentials);

        int Write(ref NativeMethods.CREDENTIALW credential, uint flags);
        int Delete(string targetName, CredType type);
    }

    internal sealed class NativeCredentialApi : ICredentialApi
    {
        internal static readonly NativeCredentialApi Instance = new NativeCredentialApi();

        public int Read(string targetName, CredType type, out SingleCredentialBuffer credential)
        {
            return NativeMethods.CredRead(targetName, (uint)type, 0, out credential) ? 0 : Marshal.GetLastWin32Error();
        }

        public int Enumerate(string? filter, out CredentialArrayBuffer credentials)
        {
            // No flags: CRED_ENUMERATE_ALL_CREDENTIALS would return "namespace:attribute=target" names.
            bool ok = NativeMethods.CredEnumerate(filter, 0, out uint count, out credentials);
            int error = ok ? 0 : Marshal.GetLastWin32Error();
            credentials.Count = ok ? (int)count : 0;
            return error;
        }

        public int Write(ref NativeMethods.CREDENTIALW credential, uint flags)
        {
            return NativeMethods.CredWrite(ref credential, flags) ? 0 : Marshal.GetLastWin32Error();
        }

        public int Delete(string targetName, CredType type)
        {
            return NativeMethods.CredDelete(targetName, (uint)type, 0) ? 0 : Marshal.GetLastWin32Error();
        }
    }
}
