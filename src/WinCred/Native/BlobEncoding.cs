using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace WinCred.Native
{
    /// <summary>
    /// Converts between a SecureString and the credential blob in unmanaged memory,
    /// without creating managed copies of the secret.
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
        /// Encodes the secret into a new CoTaskMem buffer. The caller owns the buffer and
        /// MUST zero <paramref name="size"/> bytes and free it.
        /// </summary>
        internal static unsafe IntPtr Encode(SecureString secret, CredEncoding encoding, out uint size)
        {
            int length = secret.Length;
            IntPtr chars = Marshal.SecureStringToCoTaskMemUnicode(secret);

            if (encoding == CredEncoding.Unicode)
            {
                // The decrypted UTF-16 buffer already is the blob; no second copy
                size = (uint)length * 2;
                return chars;
            }

            try
            {
                Encoding enc = Get(encoding);
                int byteCount = enc.GetByteCount((char*)chars, length);
                IntPtr blob = Marshal.AllocCoTaskMem(Math.Max(byteCount, 1));
                enc.GetBytes((char*)chars, length, (byte*)blob, byteCount);
                size = (uint)byteCount;
                return blob;
            }
            finally
            {
                ZeroMemory(chars, length * 2);
                Marshal.FreeCoTaskMem(chars);
            }
        }

        /// <summary>
        /// Decodes a blob into a new SecureString. Invalid bytes become U+FFFD, so one
        /// credential stored by another program cannot fail a whole enumeration.
        /// </summary>
        internal static unsafe SecureString DecodeToSecureString(IntPtr blob, uint size, CredEncoding encoding)
        {
            SecureString secret;
            if (size == 0)
            {
                secret = new SecureString();
            }
            else
            {
                Encoding enc = Get(encoding);
                int charCount = enc.GetCharCount((byte*)blob, (int)size);
                int bufferSize = Math.Max(charCount * 2, 2);
                IntPtr chars = Marshal.AllocCoTaskMem(bufferSize);
                try
                {
                    enc.GetChars((byte*)blob, (int)size, (char*)chars, charCount);
                    secret = new SecureString((char*)chars, charCount);
                }
                finally
                {
                    ZeroMemory(chars, bufferSize);
                    Marshal.FreeCoTaskMem(chars);
                }
            }

            secret.MakeReadOnly();
            return secret;
        }

        /// <summary>Zeroes by the known size, so a secret with an embedded null is fully cleared.</summary>
        internal static void ZeroMemory(IntPtr buffer, int size)
        {
            Marshal.Copy(new byte[size], 0, buffer, size);
        }
    }
}
