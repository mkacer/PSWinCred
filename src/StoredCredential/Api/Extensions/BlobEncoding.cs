using PSCredentialManager.Common.Enum;
using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace PSCredentialManager.Api.Extensions
{
    /// <summary>
    /// Converts between password characters and the credential blob in unmanaged memory,
    /// without creating managed copies of the password.
    /// </summary>
    internal static class BlobEncoding
    {
        private static Encoding Get(CredEncoding encoding)
        {
            switch (encoding)
            {
                case CredEncoding.Unicode:
                    return new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
                case CredEncoding.UTF8:
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unknown encoding.");
            }
        }

        /// <summary>
        /// Encodes the characters into a new CoTaskMem buffer. The caller owns the buffer.
        /// </summary>
        internal static unsafe IntPtr Encode(char* chars, int length, CredEncoding encoding, out uint size)
        {
            Encoding enc = Get(encoding);
            int byteCount = enc.GetByteCount(chars, length);
            IntPtr blob = Marshal.AllocCoTaskMem(Math.Max(byteCount, 1));
            enc.GetBytes(chars, length, (byte*)blob, byteCount);
            size = (uint)byteCount;
            return blob;
        }

        internal static unsafe string DecodeToString(IntPtr blob, uint size, CredEncoding encoding)
        {
            return size == 0 ? "" : Get(encoding).GetString((byte*)blob, (int)size);
        }

        internal static unsafe SecureString DecodeToSecureString(IntPtr blob, uint size, CredEncoding encoding)
        {
            if (size == 0)
            {
                return new SecureString();
            }

            Encoding enc = Get(encoding);
            int charCount = enc.GetCharCount((byte*)blob, (int)size);
            int bufferSize = Math.Max(charCount * 2, 2);
            IntPtr chars = Marshal.AllocCoTaskMem(bufferSize);
            try
            {
                enc.GetChars((byte*)blob, (int)size, (char*)chars, charCount);
                return new SecureString((char*)chars, charCount);
            }
            finally
            {
                CredentialExtensions.ZeroMemory(chars, bufferSize);
                Marshal.FreeCoTaskMem(chars);
            }
        }
    }
}
