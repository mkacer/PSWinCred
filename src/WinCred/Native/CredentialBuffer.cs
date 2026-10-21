using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using static WinCred.Native.NativeMethods;

namespace WinCred.Native
{
    /// <summary>
    /// Owns memory returned by CredReadW or CredEnumerateW. Windows decrypts every returned secret into
    /// this memory, so release zeroes each secret before CredFree, on every path. The P/Invoke marshaller
    /// creates these objects directly, so no raw pointer exists without an owner.
    /// </summary>
    internal abstract class CredentialBuffer : SafeHandleZeroOrMinusOneIsInvalid
    {
        protected CredentialBuffer() : base(ownsHandle: true) { }

        protected CredentialBuffer(IntPtr buffer) : base(ownsHandle: true)
        {
            SetHandle(buffer);
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                ScrubSecrets();
            }
            finally
            {
                Free(handle);
            }
            return true;
        }

        protected abstract void ScrubSecrets();

        /// <summary>Tests override this to observe the memory before it is freed.</summary>
        protected virtual void Free(IntPtr buffer)
        {
            CredFree(buffer);
        }

        protected static void ScrubSecret(IntPtr credential)
        {
            CREDENTIALW native = Marshal.PtrToStructure<CREDENTIALW>(credential);
            if (native.CredentialBlob != IntPtr.Zero && native.CredentialBlobSize > 0)
            {
                BlobEncoding.ZeroMemory(native.CredentialBlob, (int)native.CredentialBlobSize);
            }
        }
    }

    /// <summary>One CREDENTIALW, from CredReadW.</summary>
    internal class SingleCredentialBuffer : CredentialBuffer
    {
        internal SingleCredentialBuffer() { }

        internal SingleCredentialBuffer(IntPtr buffer) : base(buffer) { }

        internal CREDENTIALW Read() => Marshal.PtrToStructure<CREDENTIALW>(handle);

        protected override void ScrubSecrets() => ScrubSecret(handle);
    }

    /// <summary>An array of CREDENTIALW pointers, from CredEnumerateW.</summary>
    internal class CredentialArrayBuffer : CredentialBuffer
    {
        internal CredentialArrayBuffer() { }

        internal CredentialArrayBuffer(IntPtr buffer, int count) : base(buffer)
        {
            Count = count;
        }

        /// <summary>Set right after CredEnumerateW returns; scrubbing needs it.</summary>
        internal int Count { get; set; }

        internal IntPtr this[int index] => Marshal.ReadIntPtr(handle, index * IntPtr.Size);

        protected override void ScrubSecrets()
        {
            for (int i = 0; i < Count; i++)
            {
                ScrubSecret(this[i]);
            }
        }
    }
}
