using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using System;
using System.Runtime.InteropServices;

namespace PSCredentialManager.Api.Extensions
{
    public static class NativeCredentialExtensions
    {
        public static Credential ToCredential(this NativeCredential nativeCredential, bool includeClearPassword, bool includeSecurePassword)
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

                if (0 < nativeCredential.CredentialBlobSize)
                {
                    if(includeClearPassword)
                    {
                        credential.PasswordSize = nativeCredential.CredentialBlobSize;
                        credential.Password = Marshal.PtrToStringUni(nativeCredential.CredentialBlob, (int)nativeCredential.CredentialBlobSize / 2);
                    }

                    if(includeSecurePassword)
                    {
                        credential.SecurePassword = new System.Security.SecureString();
                        
                        for(int i=0, size = (int)nativeCredential.CredentialBlobSize / 2; i< size; i++)
                        {
                            credential.SecurePassword.AppendChar((char)Marshal.ReadInt16(nativeCredential.CredentialBlob, i * 2));
                        }
                    }
                }
                else
                {
                    if(includeClearPassword)
                    {
                        credential.PasswordSize = nativeCredential.CredentialBlobSize;
                        credential.Password = "";
                    }

                    if(includeSecurePassword)
                    {
                        credential.SecurePassword = new System.Security.SecureString();
                    }
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
