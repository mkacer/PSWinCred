using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using WinCred.Native;
using static WinCred.Native.NativeMethods;

namespace WinCred
{
    /// <summary>
    /// Thin wrapper over the CredMan API. Every native buffer is released on every path.
    /// </summary>
    internal sealed class CredMan
    {
        private readonly ICredentialApi api;

        internal CredMan() : this(NativeCredentialApi.Instance) { }

        internal CredMan(ICredentialApi api)
        {
            this.api = api;
        }

        /// <summary>Returns null when the credential does not exist.</summary>
        /// <exception cref="Win32Exception">CredReadW failed for another reason.</exception>
        internal WinCredential? Read(string targetName, CredType type, bool includeSecret, CredEncoding encoding)
        {
            int error = api.Read(targetName, type, out CredentialHandle handle);
            using (handle)
            {
                if (error == ERROR_NOT_FOUND)
                {
                    return null;
                }
                if (error != 0)
                {
                    throw new Win32Exception(error);
                }

                return Convert(Marshal.PtrToStructure<CREDENTIALW>(handle.DangerousGetHandle()), includeSecret, encoding);
            }
        }

        /// <summary>
        /// Returns the credentials for which <paramref name="match"/> is true. Secrets are decoded only for those.
        /// </summary>
        /// <exception cref="Win32Exception">CredEnumerateW failed.</exception>
        internal List<WinCredential> Enumerate(Func<string, CredType, bool> match, bool includeSecret, CredEncoding encoding)
        {
            var result = new List<WinCredential>();
            int error = api.Enumerate(out int count, out CredentialHandle handle);
            using (handle)
            {
                if (error == ERROR_NOT_FOUND)
                {
                    // The store holds no credentials
                    return result;
                }
                if (error != 0)
                {
                    throw new Win32Exception(error);
                }

                IntPtr array = handle.DangerousGetHandle();
                for (int i = 0; i < count; i++)
                {
                    CREDENTIALW native = Marshal.PtrToStructure<CREDENTIALW>(Marshal.ReadIntPtr(array, i * IntPtr.Size));
                    if (match(Marshal.PtrToStringUni(native.TargetName) ?? "", (CredType)native.Type))
                    {
                        result.Add(Convert(native, includeSecret, encoding));
                    }
                }
            }
            return result;
        }

        /// <summary>Creates the credential, or replaces an existing one with the same target and type.</summary>
        /// <exception cref="SecretTooLargeException">The encoded secret exceeds CRED_MAX_CREDENTIAL_BLOB_SIZE.</exception>
        /// <exception cref="Win32Exception">CredWriteW failed.</exception>
        internal void Write(string targetName, CredType type, string? userName, string? comment, CredPersist persist,
            SecureString secret, CredEncoding encoding)
        {
            var native = new CREDENTIALW { Type = (uint)type, Persist = (uint)persist };
            uint blobSize = 0;
            try
            {
                native.TargetName = Marshal.StringToCoTaskMemUni(targetName);
                native.UserName = Marshal.StringToCoTaskMemUni(userName);
                native.Comment = Marshal.StringToCoTaskMemUni(comment);
                native.CredentialBlob = BlobEncoding.Encode(secret, encoding, out blobSize);
                native.CredentialBlobSize = blobSize;

                if (blobSize > CRED_MAX_CREDENTIAL_BLOB_SIZE)
                {
                    throw new SecretTooLargeException(blobSize);
                }

                int error = api.Write(ref native);
                if (error != 0)
                {
                    throw new Win32Exception(error);
                }
            }
            finally
            {
                if (native.CredentialBlob != IntPtr.Zero)
                {
                    BlobEncoding.ZeroMemory(native.CredentialBlob, (int)blobSize);
                    Marshal.FreeCoTaskMem(native.CredentialBlob);
                }
                Marshal.FreeCoTaskMem(native.TargetName);
                Marshal.FreeCoTaskMem(native.UserName);
                Marshal.FreeCoTaskMem(native.Comment);
            }
        }

        /// <summary>Returns false when the credential does not exist.</summary>
        /// <exception cref="Win32Exception">CredDeleteW failed for another reason.</exception>
        internal bool Delete(string targetName, CredType type)
        {
            int error = api.Delete(targetName, type);
            if (error == ERROR_NOT_FOUND)
            {
                return false;
            }
            if (error != 0)
            {
                throw new Win32Exception(error);
            }
            return true;
        }

        private static WinCredential Convert(CREDENTIALW native, bool includeSecret, CredEncoding encoding)
        {
            CredType type = (CredType)native.Type;
            SecureString? secret = includeSecret && WinCredential.IsSecretReadable(type)
                ? BlobEncoding.DecodeToSecureString(native.CredentialBlob, native.CredentialBlobSize, encoding)
                : null;

            return new WinCredential(
                Marshal.PtrToStringUni(native.TargetName) ?? "",
                type,
                (CredFlags)native.Flags,
                (CredPersist)native.Persist,
                Marshal.PtrToStringUni(native.UserName),
                Marshal.PtrToStringUni(native.Comment),
                Marshal.PtrToStringUni(native.TargetAlias),
                ToDateTime(native.LastWritten),
                ReadAttributes(native),
                secret);
        }

        internal static DateTime ToDateTime(NativeMethods.FILETIME fileTime)
        {
            return DateTime.FromFileTime(((long)fileTime.dwHighDateTime << 32) | fileTime.dwLowDateTime);
        }

        private static IReadOnlyList<WinCredentialAttribute> ReadAttributes(CREDENTIALW native)
        {
            var attributes = new WinCredentialAttribute[native.AttributeCount];
            int size = Marshal.SizeOf<CREDENTIAL_ATTRIBUTEW>();
            for (int i = 0; i < attributes.Length; i++)
            {
                CREDENTIAL_ATTRIBUTEW attribute = Marshal.PtrToStructure<CREDENTIAL_ATTRIBUTEW>(native.Attributes + i * size);
                byte[] value = new byte[attribute.ValueSize];
                if (value.Length > 0)
                {
                    Marshal.Copy(attribute.Value, value, 0, value.Length);
                }
                attributes[i] = new WinCredentialAttribute(Marshal.PtrToStringUni(attribute.Keyword) ?? "", attribute.Flags, value);
            }
            return attributes;
        }
    }

    internal sealed class SecretTooLargeException : ArgumentException
    {
        internal SecretTooLargeException(uint size)
            : base($"The encoded secret is {size} bytes; Windows Credential Manager allows at most {CRED_MAX_CREDENTIAL_BLOB_SIZE} bytes.")
        {
        }
    }
}
