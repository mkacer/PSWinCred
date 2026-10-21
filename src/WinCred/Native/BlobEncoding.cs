using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace WinCred.Native
{
    /// <summary>
    /// Converts between a SecureString and the credential blob in unmanaged memory,
    /// without creating managed copies of the secret. Invalid text is an error in both directions,
    /// never silently replaced.
    /// </summary>
    internal static class BlobEncoding
    {
        private static Encoding Get(CredEncoding encoding)
        {
            switch (encoding)
            {
                case CredEncoding.Unicode:
                    return new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
                case CredEncoding.UTF8:
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unknown encoding.");
            }
        }

        /// <summary>
        /// Encodes the secret into a new CoTaskMem buffer. The caller owns the buffer and
        /// MUST zero <paramref name="size"/> bytes and free it.
        /// </summary>
        /// <exception cref="SecretNotEncodableException">The secret is not valid UTF-16, such as a lone surrogate.</exception>
        internal static unsafe IntPtr Encode(SecureString secret, CredEncoding encoding, out uint size)
        {
            int length = secret.Length;
            IntPtr chars = Marshal.SecureStringToCoTaskMemUnicode(secret);
            bool charsAreBlob = false;
            try
            {
                Encoding enc = Get(encoding);
                int byteCount;
                try
                {
                    byteCount = enc.GetByteCount((char*)chars, length);
                }
                catch (EncoderFallbackException)
                {
                    // The inner exception would describe the secret's characters; leave it out
                    throw new SecretNotEncodableException(encoding);
                }

                if (encoding == CredEncoding.Unicode)
                {
                    // Validated above; the decrypted UTF-16 buffer already is the blob, so no second copy
                    charsAreBlob = true;
                    size = (uint)byteCount;
                    return chars;
                }

                IntPtr blob = Marshal.AllocCoTaskMem(Math.Max(byteCount, 1));
                enc.GetBytes((char*)chars, length, (byte*)blob, byteCount);
                size = (uint)byteCount;
                return blob;
            }
            finally
            {
                if (!charsAreBlob)
                {
                    ZeroMemory(chars, length * 2);
                    Marshal.FreeCoTaskMem(chars);
                }
            }
        }

        /// <summary>Decodes a blob into a new read-only SecureString.</summary>
        /// <exception cref="SecretNotDecodableException">The blob is not valid text in <paramref name="encoding"/>.</exception>
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
                int charCount;
                try
                {
                    charCount = enc.GetCharCount((byte*)blob, (int)size);
                }
                catch (DecoderFallbackException)
                {
                    // The inner exception would contain the secret's bytes; leave it out
                    throw new SecretNotDecodableException(encoding);
                }

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

    /// <summary>The secret cannot be encoded. The message never describes the secret.</summary>
    internal sealed class SecretNotEncodableException : ArgumentException
    {
        internal SecretNotEncodableException(CredEncoding encoding)
            : base($"The secret is not valid Unicode text (for example, it contains an unpaired surrogate), so it cannot be stored as {encoding}. Nothing was written.")
        {
        }
    }

    /// <summary>The stored secret is not valid text in the requested encoding. The message never describes the secret.</summary>
    internal sealed class SecretNotDecodableException : FormatException
    {
        internal SecretNotDecodableException(CredEncoding encoding)
            : base($"The stored secret is not valid {encoding} text. Another program may have stored it in a different encoding; try the other -Encoding value.")
        {
        }
    }
}
