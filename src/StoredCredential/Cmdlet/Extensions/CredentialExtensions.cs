using PSCredentialManager.Common;
using System;
using System.Management.Automation;
using System.Security;

namespace PSCredentialManager.Cmdlet.Extensions
{
    public static class CredentialExtensions
    {
        public static PSCredential ToPsCredential(this Credential credential)
        {
            // An empty password is valid; only a missing one is not
            SecureString password = credential.SecurePassword ?? credential.Password?.ToSecureString();

            if (credential.UserName == null || password == null)
            {
                throw new Exception("Unable to convert Credential object without username or password to PSCredential object");
            }

            try
            {
                return new PSCredential(credential.UserName, password);
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to convert credential object", ex);
            }
        }
    }
}
