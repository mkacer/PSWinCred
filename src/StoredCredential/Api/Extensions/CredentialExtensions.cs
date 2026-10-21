using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using System;
using System.Runtime.InteropServices;

namespace PSCredentialManager.Api.Extensions
{
    public static class CredentialExtensions
    {
        /// <summary>
        /// Copies the credential into unmanaged memory, with the password in the given encoding.
        /// The caller MUST call <see cref="Release"/> on the result once it is no longer needed.
        /// </summary>
        public static NativeCredential ToNativeCredential(this Credential credential, CredEncoding encoding = CredEncoding.Unicode)
        {
            NativeCredential nativeCredential = new NativeCredential()
            {
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = IntPtr.Zero,
                Type = credential.Type,
                Persist = (uint)credential.Persist,
            };

            try
            {
                nativeCredential.LastWritten = credential.LastWritten.ToComFileTime();
                nativeCredential.Comment = Marshal.StringToCoTaskMemUni(credential.Comment);
                nativeCredential.TargetName = Marshal.StringToCoTaskMemUni(credential.TargetName);
                nativeCredential.UserName = Marshal.StringToCoTaskMemUni(credential.UserName);
                nativeCredential.CredentialBlob = EncodePassword(credential, encoding, out uint blobSize);
                nativeCredential.CredentialBlobSize = blobSize;
            }
            catch (Exception ex)
            {
                nativeCredential.Release();
                throw new Exception("PSCredentialManager.Api.CredentialUtility.ConvertToNativeCredential Unable to convert credential to native credential.", ex);
            }

            return nativeCredential;
        }

        // Prefers SecurePassword; the blob size always comes from the encoded password, never from PasswordSize
        private static unsafe IntPtr EncodePassword(Credential credential, CredEncoding encoding, out uint size)
        {
            if (credential.SecurePassword != null)
            {
                int length = credential.SecurePassword.Length;
                IntPtr chars = Marshal.SecureStringToCoTaskMemUnicode(credential.SecurePassword);
                try
                {
                    return BlobEncoding.Encode((char*)chars, length, encoding, out size);
                }
                finally
                {
                    ZeroMemory(chars, length * 2);
                    Marshal.FreeCoTaskMem(chars);
                }
            }

            string password = credential.Password ?? "";
            fixed (char* chars = password)
            {
                return BlobEncoding.Encode(chars, password.Length, encoding, out size);
            }
        }

        /// <summary>
        /// Zeroes the password copy and frees the unmanaged memory allocated by <see cref="ToNativeCredential"/>.
        /// </summary>
        public static void Release(this NativeCredential nativeCredential)
        {
            if (nativeCredential.CredentialBlob != IntPtr.Zero)
            {
                ZeroMemory(nativeCredential.CredentialBlob, (int)nativeCredential.CredentialBlobSize);
                Marshal.FreeCoTaskMem(nativeCredential.CredentialBlob);
            }
            Marshal.FreeCoTaskMem(nativeCredential.TargetName);
            Marshal.FreeCoTaskMem(nativeCredential.UserName);
            Marshal.FreeCoTaskMem(nativeCredential.Comment);
        }

        // Zeroes by the known size, so a password with an embedded null is fully cleared
        internal static void ZeroMemory(IntPtr buffer, int size)
        {
            Marshal.Copy(new byte[size], 0, buffer, size);
        }
    }
}
