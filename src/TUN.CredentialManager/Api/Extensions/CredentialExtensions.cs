using PSCredentialManager.Common;
using System;
using System.Runtime.InteropServices;

namespace PSCredentialManager.Api.Extensions
{
    public static class CredentialExtensions
    {
        /// <summary>
        /// Copies the credential into unmanaged memory. The caller MUST call
        /// <see cref="Release"/> on the result once it is no longer needed.
        /// </summary>
        public static NativeCredential ToNativeCredential(this Credential credential)
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
                if (credential.SecurePassword != null)
                {
                    nativeCredential.CredentialBlobSize = (uint)credential.SecurePassword.Length * 2;
                    nativeCredential.CredentialBlob = Marshal.SecureStringToCoTaskMemUnicode(credential.SecurePassword);
                }
                else
                {
                    // Size comes from the string itself, so a wrong PasswordSize cannot overrun the buffer
                    nativeCredential.CredentialBlobSize = (uint)(credential.Password?.Length ?? 0) * 2;
                    nativeCredential.CredentialBlob = Marshal.StringToCoTaskMemUni(credential.Password);
                }
            }
            catch (Exception ex)
            {
                nativeCredential.Release();
                throw new Exception("PSCredentialManager.Api.CredentialUtility.ConvertToNativeCredential Unable to convert credential to native credential.", ex);
            }

            return nativeCredential;
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
