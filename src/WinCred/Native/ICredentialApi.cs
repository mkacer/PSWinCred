using System.Runtime.InteropServices;

namespace WinCred.Native
{
    /// <summary>
    /// The CredMan calls used by <see cref="CredMan"/>. Each returns 0 on success or the Win32 error code.
    /// Tests replace it with a fake.
    /// </summary>
    internal interface ICredentialApi
    {
        int Read(string targetName, CredType type, out CredentialHandle credential);
        int Write(ref NativeMethods.CREDENTIALW credential);
        int Delete(string targetName, CredType type);
        int Enumerate(out int count, out CredentialHandle credentials);
    }

    internal sealed class NativeCredentialApi : ICredentialApi
    {
        internal static readonly NativeCredentialApi Instance = new NativeCredentialApi();

        public int Read(string targetName, CredType type, out CredentialHandle credential)
        {
            return NativeMethods.CredRead(targetName, (uint)type, 0, out credential) ? 0 : Marshal.GetLastWin32Error();
        }

        public int Write(ref NativeMethods.CREDENTIALW credential)
        {
            return NativeMethods.CredWrite(ref credential, 0) ? 0 : Marshal.GetLastWin32Error();
        }

        public int Delete(string targetName, CredType type)
        {
            return NativeMethods.CredDelete(targetName, (uint)type, 0) ? 0 : Marshal.GetLastWin32Error();
        }

        public int Enumerate(out int count, out CredentialHandle credentials)
        {
            bool ok = NativeMethods.CredEnumerate(null, NativeMethods.CRED_ENUMERATE_ALL_CREDENTIALS, out uint nativeCount, out credentials);
            int error = ok ? 0 : Marshal.GetLastWin32Error();
            count = (int)nativeCount;
            return error;
        }
    }
}
