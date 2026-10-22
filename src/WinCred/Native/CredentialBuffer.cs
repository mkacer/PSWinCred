using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using static WinCred.Native.NativeMethods;

namespace WinCred.Native
{
    /// <summary>
    /// Owns memory returned by CredReadW or CredEnumerateW. Windows decrypts every returned secret into
    /// this memory, so release zeroes each secret and each attribute value, which can hold secrets too,
    /// before CredFree, on every path. The P/Invoke marshaller
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

        // Field offsets, computed once when the type is first used, so scrubbing reads fields without allocating:
        // ReleaseHandle can run under memory pressure, where an allocation that fails would skip the zeroing.
        private static readonly int BlobSizeOffset = Marshal.OffsetOf<CREDENTIALW>(nameof(CREDENTIALW.CredentialBlobSize)).ToInt32();
        private static readonly int BlobOffset = Marshal.OffsetOf<CREDENTIALW>(nameof(CREDENTIALW.CredentialBlob)).ToInt32();
        private static readonly int AttributeCountOffset = Marshal.OffsetOf<CREDENTIALW>(nameof(CREDENTIALW.AttributeCount)).ToInt32();
        private static readonly int AttributesOffset = Marshal.OffsetOf<CREDENTIALW>(nameof(CREDENTIALW.Attributes)).ToInt32();
        private static readonly int AttributeSize = Marshal.SizeOf<CREDENTIAL_ATTRIBUTEW>();
        private static readonly int ValueSizeOffset = Marshal.OffsetOf<CREDENTIAL_ATTRIBUTEW>(nameof(CREDENTIAL_ATTRIBUTEW.ValueSize)).ToInt32();
        private static readonly int ValueOffset = Marshal.OffsetOf<CREDENTIAL_ATTRIBUTEW>(nameof(CREDENTIAL_ATTRIBUTEW.Value)).ToInt32();

        protected static void ScrubSecret(IntPtr credential)
        {
            ZeroField(Marshal.ReadIntPtr(credential, BlobOffset), Marshal.ReadInt32(credential, BlobSizeOffset));

            uint count = (uint)Marshal.ReadInt32(credential, AttributeCountOffset);
            IntPtr attributes = Marshal.ReadIntPtr(credential, AttributesOffset);
            for (uint i = 0; i < count && attributes != IntPtr.Zero; i++)
            {
                IntPtr attribute = attributes + (int)i * AttributeSize;
                ZeroField(Marshal.ReadIntPtr(attribute, ValueOffset), Marshal.ReadInt32(attribute, ValueSizeOffset));
            }
        }

        private static void ZeroField(IntPtr value, int size)
        {
            if (value != IntPtr.Zero && size > 0)
            {
                BlobEncoding.ZeroMemory(value, size);
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
