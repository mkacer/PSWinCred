using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using PSCredentialManager.Common.Exceptions;
using System;
using System.Collections.Generic;

namespace PSCredentialManager.Api
{
    public class CredentialManager
    {
        private const int ErrorNotFound = 1168;
        private const int CredEnumerateAllCredentials = 0x1;

        private readonly ICredentialApi api;

        public CredentialManager() : this(NativeCredentialApi.Instance) { }

        internal CredentialManager(ICredentialApi api)
        {
            this.api = api;
        }

        public void WriteCred(NativeCredential credential)
        {
            // Write the info into the CredMan storage.
            int error = api.CredWrite(ref credential);
            if (error != 0)
            {
                throw new Exception($"CredWrite failed with the error code {error}.");
            }
        }

        public Credential ReadCred(string target, CredType type, bool includeClearPassword, bool includeSecurePassword, CredEncoding encoding = CredEncoding.Unicode)
        {
            int error = api.CredRead(target, type, out IntPtr nativeCredentialPointer);
            if (error == 0)
            {
                using (CriticalCredentialHandle critCred = new CriticalCredentialHandle(nativeCredentialPointer, api))
                {
                    return critCred.GetCredential(includeClearPassword, includeSecurePassword, encoding);
                }
            }
            else if (error == ErrorNotFound)
            {
                throw new CredentialNotFoundException($"Requested credential with target {target} was not found, error code {error}");
            }
            else
            {
                throw new Exception($"CredRead failed with the error code {error}.");
            }
        }

        public void DeleteCred(string target, CredType type)
        {
            int error = api.CredDelete(target, type);
            if (error == ErrorNotFound)
            {
                throw new CredentialNotFoundException($"DeleteCred failed with the error code {error} (credential not found).");
            }
            else if (error != 0)
            {
                throw new Exception($"DeleteCred failed with the error code {error}.");
            }
        }

        public IEnumerable<Credential> ReadCred(bool includeClearPassword, bool includeSecurePassword, CredEncoding encoding = CredEncoding.Unicode)
        {
            int error = api.CredEnumerate(null, CredEnumerateAllCredentials, out int count, out IntPtr pCredentials);
            if (error == 0)
            {
                using (CriticalCredentialHandle credHandle = new CriticalCredentialHandle(pCredentials, api))
                {
                    return credHandle.GetCredentials(count, includeClearPassword, includeSecurePassword, encoding);
                }
            }
            else if (error == ErrorNotFound)
            {
                // The store holds no credentials
                return new Credential[0];
            }
            else
            {
                throw new Exception($"CredEnumerate failed with the error code {error}.");
            }
        }

    }
}
