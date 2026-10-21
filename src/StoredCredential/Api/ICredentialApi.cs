using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using System;
using System.Runtime.InteropServices;

namespace PSCredentialManager.Api
{
    /// <summary>
    /// The native Credential Manager calls used by <see cref="CredentialManager"/>.
    /// Each call returns 0 on success or the Win32 error code on failure.
    /// Tests replace it with a fake.
    /// </summary>
    internal interface ICredentialApi
    {
        int CredRead(string target, CredType type, out IntPtr credentialPointer);
        int CredWrite(ref NativeCredential credential);
        int CredDelete(string target, CredType type);
        int CredEnumerate(string filter, int flags, out int count, out IntPtr credentialPointers);
        void CredFree(IntPtr buffer);
    }

    internal sealed class NativeCredentialApi : ICredentialApi
    {
        internal static readonly NativeCredentialApi Instance = new NativeCredentialApi();

        public int CredRead(string target, CredType type, out IntPtr credentialPointer)
        {
            return Imports.CredRead(target, type, 0, out credentialPointer) ? 0 : Marshal.GetLastWin32Error();
        }

        public int CredWrite(ref NativeCredential credential)
        {
            return Imports.CredWrite(ref credential, 0) ? 0 : Marshal.GetLastWin32Error();
        }

        public int CredDelete(string target, CredType type)
        {
            return Imports.CredDelete(target, type, 0) ? 0 : Marshal.GetLastWin32Error();
        }

        public int CredEnumerate(string filter, int flags, out int count, out IntPtr credentialPointers)
        {
            return Imports.CredEnumerate(filter, flags, out count, out credentialPointers) ? 0 : Marshal.GetLastWin32Error();
        }

        public void CredFree(IntPtr buffer)
        {
            Imports.CredFree(buffer);
        }
    }
}
