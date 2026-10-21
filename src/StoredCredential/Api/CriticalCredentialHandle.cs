using Microsoft.Win32.SafeHandles;
using PSCredentialManager.Api.Extensions;
using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using System;
using System.Runtime.InteropServices;

namespace PSCredentialManager.Api
{
    public class CriticalCredentialHandle : CriticalHandleZeroOrMinusOneIsInvalid
    {
        private readonly ICredentialApi api;

        internal CriticalCredentialHandle(IntPtr preexistingHandle, ICredentialApi api)
        {
            this.api = api;
            SetHandle(preexistingHandle);
        }

        internal Credential GetCredential(bool includeClearPassword, bool includeSecurePassword, CredEncoding encoding = CredEncoding.Unicode)
        {
            if (IsInvalid)
            {
                throw new InvalidOperationException("Invalid CriticalHandle!");
            }

            // Get the Credential from the mem location
            NativeCredential nativeCredential = (NativeCredential)Marshal.PtrToStructure(handle, typeof(NativeCredential));

            // Create a managed Credential type and fill it with data from the native counterpart.
            return nativeCredential.ToCredential(includeClearPassword, includeSecurePassword, encoding);
        }

        protected override bool ReleaseHandle()
        {
            api.CredFree(handle);
            return true;
        }

        public Credential[] GetCredentials(int count, bool includeClearPassword, bool includeSecurePassword, CredEncoding encoding = CredEncoding.Unicode)
        {
            if (IsInvalid)
            {
                throw new InvalidOperationException("Invalid CriticalHandle!");
            }

            Credential[] credentials = new Credential[count];
            for (int inx = 0; inx < count; inx++)
            {
                IntPtr pCred = Marshal.ReadIntPtr(handle, inx * IntPtr.Size);
                NativeCredential nativeCredential = (NativeCredential)Marshal.PtrToStructure(pCred, typeof(NativeCredential));
                credentials[inx] = nativeCredential.ToCredential(includeClearPassword, includeSecurePassword, encoding);
            }
            return credentials;
        }

    }
}
