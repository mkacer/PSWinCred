using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using System;
using System.Runtime.InteropServices;

namespace PSCredentialManager.Api.Extensions
{
    public static class NativeCredentialExtensions
    {
        public static Credential ToCredential(this NativeCredential nativeCredential, bool includeClearPassword, bool includeSecurePassword, CredEncoding encoding = CredEncoding.Unicode)
        {
            Credential credential;

            try
            {
                credential = new Credential()
                {
                    Type = nativeCredential.Type,
                    Flags = nativeCredential.Flags,
                    Persist = (CredPersist)nativeCredential.Persist,
                    UserName = Marshal.PtrToStringUni(nativeCredential.UserName),
                    TargetName = Marshal.PtrToStringUni(nativeCredential.TargetName),
                    TargetAlias = Marshal.PtrToStringUni(nativeCredential.TargetAlias),
                    Comment = Marshal.PtrToStringUni(nativeCredential.Comment),
                    LastWritten = nativeCredential.LastWritten.ToDateTime(),
                    SecurePassword = null,
                    PasswordSize = uint.MinValue,
                    Password = null,
                };

                if (includeClearPassword)
                {
                    credential.PasswordSize = nativeCredential.CredentialBlobSize;
                    credential.Password = BlobEncoding.DecodeToString(nativeCredential.CredentialBlob, nativeCredential.CredentialBlobSize, encoding);
                }

                if (includeSecurePassword)
                {
                    credential.SecurePassword = BlobEncoding.DecodeToSecureString(nativeCredential.CredentialBlob, nativeCredential.CredentialBlobSize, encoding);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("PSCredentialManager.Api.CredentialUtility.ConvertToCredential Unable to convert native credential to credential.", ex);
            }

            return credential;
        }
    }
}
