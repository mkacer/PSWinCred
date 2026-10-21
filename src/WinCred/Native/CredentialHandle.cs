using Microsoft.Win32.SafeHandles;
using System;

namespace WinCred.Native
{
    /// <summary>
    /// Owns a buffer returned by CredReadW or CredEnumerateW and frees it with CredFree.
    /// The P/Invoke marshaller creates it directly, so no raw pointer exists without an owner.
    /// </summary>
    internal class CredentialHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal CredentialHandle() : base(ownsHandle: true) { }

        internal CredentialHandle(IntPtr buffer) : base(ownsHandle: true)
        {
            SetHandle(buffer);
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.CredFree(handle);
            return true;
        }
    }
}
