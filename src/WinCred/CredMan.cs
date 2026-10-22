using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using WinCred.Native;
using static WinCred.Native.NativeMethods;

namespace WinCred
{
    /// <summary>
    /// Thin wrapper over the CredMan API. Every buffer from Windows is scrubbed and released on every path,
    /// and every buffer this class allocates for a secret is zeroed before it is freed.
    /// </summary>
    internal sealed class CredMan
    {
        /// <summary>Every credential type, for exact lookups that do not name one.</summary>
        internal static readonly CredType[] AllTypes =
        {
            CredType.Generic, CredType.DomainPassword, CredType.DomainCertificate,
            CredType.DomainVisiblePassword, CredType.GenericCertificate, CredType.DomainExtended,
        };

        private readonly ICredentialApi api;
        private readonly Action<IntPtr, int> freeSecretMemory;

        internal CredMan() : this(NativeCredentialApi.Instance) { }

        internal CredMan(ICredentialApi api) : this(api, (memory, size) => Marshal.FreeCoTaskMem(memory)) { }

        /// <summary>Tests pass <paramref name="freeSecretMemory"/> to see an encoded secret just before it is freed.</summary>
        internal CredMan(ICredentialApi api, Action<IntPtr, int> freeSecretMemory)
        {
            this.api = api;
            this.freeSecretMemory = freeSecretMemory;
        }

        /// <summary>
        /// Reads one credential with CredReadW, which matches the exact name (ignoring case) and type.
        /// Returns null when it does not exist.
        /// </summary>
        /// <exception cref="Win32Exception">CredReadW failed for another reason.</exception>
        internal ReadResult? Read(string targetName, CredType type, bool includeSecret, bool includeAttributeValues, CredEncoding encoding)
        {
            CheckName(targetName);
            int error = api.Read(targetName, type, out SingleCredentialBuffer buffer);
            using (buffer)
            {
                if (error == ERROR_NOT_FOUND)
                {
                    return null;
                }
                if (error != 0)
                {
                    throw new Win32Exception(error);
                }

                return Convert(buffer.Read(), includeSecret, includeAttributeValues, encoding);
            }
        }

        /// <summary>
        /// Lists the credentials that pass <paramref name="filter"/> (see <see cref="ICredentialApi.Enumerate"/>)
        /// and <paramref name="match"/>. Windows decrypts every credential the filter passes; secrets are decoded
        /// only for those <paramref name="match"/> accepts.
        /// </summary>
        /// <exception cref="Win32Exception">CredEnumerateW failed.</exception>
        internal List<ReadResult> Enumerate(string? filter, Func<string, CredType, bool> match, bool includeSecret, bool includeAttributeValues, CredEncoding encoding)
        {
            CheckName(filter);
            var results = new List<ReadResult>();
            int error = api.Enumerate(filter, out CredentialArrayBuffer buffer);
            using (buffer)
            {
                if (error == ERROR_NOT_FOUND)
                {
                    return results;
                }
                if (error != 0)
                {
                    throw new Win32Exception(error);
                }

                for (int i = 0; i < buffer.Count; i++)
                {
                    CREDENTIALW native = Marshal.PtrToStructure<CREDENTIALW>(buffer[i]);
                    if (match(Marshal.PtrToStringUni(native.TargetName) ?? "", (CredType)native.Type))
                    {
                        results.Add(Convert(native, includeSecret, includeAttributeValues, encoding));
                    }
                }
            }
            return results;
        }

        /// <summary>Creates a credential. It MUST NOT exist yet.</summary>
        /// <exception cref="CredentialExistsException">A credential with this target and type exists.</exception>
        /// <exception cref="SecretNotEncodableException">The secret is not valid Unicode text.</exception>
        /// <exception cref="SecretTooLargeException">The encoded secret exceeds CRED_MAX_CREDENTIAL_BLOB_SIZE.</exception>
        /// <exception cref="Win32Exception">CredReadW or CredWriteW failed.</exception>
        internal void Create(string targetName, CredType type, string? userName, string? comment, CredPersist persist,
            SecureString secret, CredEncoding encoding)
        {
            CheckName(targetName);
            // Encode first, so an unusable secret fails before the store is touched
            var native = new CREDENTIALW { Type = (uint)type, Persist = (uint)persist };
            var strings = new List<IntPtr>();
            uint blobSize = 0;
            try
            {
                native.CredentialBlob = EncodeSecret(secret, encoding, out blobSize);
                native.CredentialBlobSize = blobSize;

                // Windows has no create-only write, so check first. Another program could create the
                // credential between this check and the write; that window cannot be closed from here.
                int error = api.Read(targetName, type, out SingleCredentialBuffer existing);
                using (existing)
                {
                    if (error == 0)
                    {
                        throw new CredentialExistsException(targetName, type);
                    }
                    if (error != ERROR_NOT_FOUND)
                    {
                        throw new CredentialReadException(error);
                    }
                }

                native.TargetName = AllocString(targetName, strings);
                native.UserName = AllocString(userName, strings);
                native.Comment = AllocString(comment, strings);

                error = api.Write(ref native, 0);
                if (error != 0)
                {
                    throw new Win32Exception(error);
                }
            }
            finally
            {
                FreeSecret(native.CredentialBlob, blobSize);
                strings.ForEach(Marshal.FreeCoTaskMem);
            }
        }

        /// <summary>
        /// Updates an existing credential. Fields that <paramref name="update"/> does not set keep their stored
        /// values, including persistence, the alias, the attributes, the flags Windows accepts back, and the
        /// secret: without a new secret, CRED_PRESERVE_CREDENTIAL_BLOB keeps it and it is never decoded. The read still makes
        /// Windows decrypt a Generic secret into this process; the buffer zeroes it before CredFree.
        /// </summary>
        /// <exception cref="CredentialNotFoundException">The credential does not exist.</exception>
        /// <exception cref="SecretNotEncodableException">The new secret is not valid Unicode text.</exception>
        /// <exception cref="SecretTooLargeException">The encoded new secret exceeds CRED_MAX_CREDENTIAL_BLOB_SIZE.</exception>
        /// <exception cref="Win32Exception">CredReadW or CredWriteW failed.</exception>
        internal void Update(string targetName, CredType type, CredentialUpdate update)
        {
            CheckName(targetName);
            // Encode first, so an unusable secret fails before the store is touched
            IntPtr blob = IntPtr.Zero;
            uint blobSize = 0;
            var strings = new List<IntPtr>();
            try
            {
                if (update.Secret != null)
                {
                    blob = EncodeSecret(update.Secret, update.Encoding, out blobSize);
                }

                int error = api.Read(targetName, type, out SingleCredentialBuffer existing);
                using (existing)
                {
                    if (error == ERROR_NOT_FOUND)
                    {
                        throw new CredentialNotFoundException(targetName, type);
                    }
                    if (error != 0)
                    {
                        throw new CredentialReadException(error);
                    }

                    // Start from the stored record. Its strings and attributes stay in Windows' buffer,
                    // which is alive until the end of this block; only changed fields are allocated here.
                    CREDENTIALW native = existing.Read();
                    native.Flags &= WritableFlags;
                    native.LastWritten = default;
                    native.CredentialBlob = blob;
                    native.CredentialBlobSize = blobSize;
                    if (update.SetUserName)
                    {
                        native.UserName = AllocString(update.UserName, strings);
                        // The flag says the target is the user name; that no longer holds for a new user name
                        native.Flags &= ~CRED_FLAGS_USERNAME_TARGET;
                    }
                    if (update.SetComment)
                    {
                        native.Comment = AllocString(update.Comment, strings);
                    }
                    if (update.Persist.HasValue)
                    {
                        native.Persist = (uint)update.Persist.Value;
                    }

                    // Without a new secret, keep the stored one
                    error = api.Write(ref native, update.Secret != null ? 0 : CRED_PRESERVE_CREDENTIAL_BLOB);
                    if (error == ERROR_NOT_FOUND)
                    {
                        // Removed by another program after the read; the preserving write creates nothing
                        throw new CredentialNotFoundException(targetName, type);
                    }
                    if (error != 0)
                    {
                        throw new Win32Exception(error);
                    }
                }
            }
            finally
            {
                FreeSecret(blob, blobSize);
                strings.ForEach(Marshal.FreeCoTaskMem);
            }
        }

        /// <summary>Returns false when the credential does not exist.</summary>
        /// <exception cref="Win32Exception">CredDeleteW failed for another reason.</exception>
        internal bool Delete(string targetName, CredType type)
        {
            CheckName(targetName);
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

        internal static bool ContainsNul(string value)
        {
#if NETFRAMEWORK
            return value.IndexOf('\0') >= 0;
#else
            return value.Contains('\0');
#endif
        }

        /// <summary>
        /// Windows reads a name only up to the first NUL, so a name with one would act on a different credential.
        /// The cmdlets report this first; this is the last check before the native call.
        /// </summary>
        /// <exception cref="ArgumentException">The name contains U+0000.</exception>
        private static void CheckName(string? name)
        {
            if (name != null && ContainsNul(name))
            {
                throw new ArgumentException("The target name contains U+0000, which ends a name in Windows.", nameof(name));
            }
        }

        private IntPtr EncodeSecret(SecureString secret, CredEncoding encoding, out uint size)
        {
            IntPtr blob = BlobEncoding.Encode(secret, encoding, out size);
            if (size > CRED_MAX_CREDENTIAL_BLOB_SIZE)
            {
                FreeSecret(blob, size);
                size = 0;
                throw new SecretTooLargeException();
            }
            return blob;
        }

        private void FreeSecret(IntPtr blob, uint size)
        {
            if (blob != IntPtr.Zero)
            {
                BlobEncoding.ZeroMemory(blob, (int)size);
                freeSecretMemory(blob, (int)size);
            }
        }

        // An empty string is stored as no value
        private static IntPtr AllocString(string? value, List<IntPtr> allocations)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }
            IntPtr p = Marshal.StringToCoTaskMemUni(value);
            allocations.Add(p);
            return p;
        }

        private static ReadResult Convert(CREDENTIALW native, bool includeSecret, bool includeAttributeValues, CredEncoding encoding)
        {
            string targetName = Marshal.PtrToStringUni(native.TargetName) ?? "";
            try
            {
                CredType type = (CredType)native.Type;
                SecureString? secret = null;
                Exception? secretError = null;
                if (includeSecret && WinCredential.IsSecretReadable(type))
                {
                    try
                    {
                        secret = BlobEncoding.DecodeToSecureString(native.CredentialBlob, native.CredentialBlobSize, encoding);
                    }
                    catch (SecretNotDecodableException exception)
                    {
                        secretError = exception;
                    }
                }

                var credential = new WinCredential(
                    targetName,
                    type,
                    (CredFlags)native.Flags,
                    (CredPersist)native.Persist,
                    Marshal.PtrToStringUni(native.UserName),
                    Marshal.PtrToStringUni(native.Comment),
                    Marshal.PtrToStringUni(native.TargetAlias),
                    ToDateTimeOffset(native.LastWritten),
                    ReadAttributes(native, includeAttributeValues),
                    secret);
                return new ReadResult(targetName, credential, secretError);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                // One malformed credential is reported, not allowed to end a listing
                return new ReadResult(targetName, null, exception);
            }
        }

        internal static DateTimeOffset ToDateTimeOffset(NativeMethods.FILETIME fileTime)
        {
            return DateTimeOffset.FromFileTime(((long)fileTime.dwHighDateTime << 32) | fileTime.dwLowDateTime);
        }

        private static WinCredentialAttribute[] ReadAttributes(CREDENTIALW native, bool includeValues)
        {
            var attributes = new WinCredentialAttribute[native.AttributeCount];
            int size = Marshal.SizeOf<CREDENTIAL_ATTRIBUTEW>();
            for (int i = 0; i < attributes.Length; i++)
            {
                CREDENTIAL_ATTRIBUTEW attribute = Marshal.PtrToStructure<CREDENTIAL_ATTRIBUTEW>(native.Attributes + i * size);
                byte[]? value = null;
                if (includeValues)
                {
                    value = new byte[attribute.ValueSize];
                    if (value.Length > 0)
                    {
                        Marshal.Copy(attribute.Value, value, 0, value.Length);
                    }
                }
                attributes[i] = new WinCredentialAttribute(
                    Marshal.PtrToStringUni(attribute.Keyword) ?? "", attribute.Flags, (int)attribute.ValueSize,
                    value);
            }
            return attributes;
        }
    }

    /// <summary>
    /// One credential from a read. <see cref="Credential"/> is null when it could not be converted;
    /// <see cref="Error"/> is set when it could not be converted or its secret could not be decoded.
    /// </summary>
    internal sealed class ReadResult
    {
        internal ReadResult(string targetName, WinCredential? credential, Exception? error)
        {
            TargetName = targetName;
            Credential = credential;
            Error = error;
        }

        internal string TargetName { get; }
        internal WinCredential? Credential { get; }
        internal Exception? Error { get; }
    }

    /// <summary>The fields an update changes. Unset fields keep their stored values.</summary>
    internal sealed class CredentialUpdate
    {
        internal bool SetUserName { get; set; }
        internal string? UserName { get; set; }
        internal bool SetComment { get; set; }
        internal string? Comment { get; set; }
        internal CredPersist? Persist { get; set; }
        internal SecureString? Secret { get; set; }
        internal CredEncoding Encoding { get; set; } = CredEncoding.Unicode;
    }

    /// <summary>
    /// Builds CredEnumerateW filters. Measured: Windows treats only a trailing "*" as a wildcard, ignores case,
    /// and treats "[" and "?" literally. Known gap (documented in Get-WinCredential's help): .NET folds a few
    /// non-ASCII characters onto ASCII letters, such as the Kelvin sign onto k, and Windows presumably does not,
    /// so the filter can leave out a name the pattern matches. Ending the prefix at every such letter would make
    /// most prefixes empty and decrypt the whole store instead.
    /// </summary>
    internal static class TargetFilter
    {
        /// <summary>
        /// Returns a filter that passes every name the PowerShell wildcard <paramref name="pattern"/> can match:
        /// the pattern's literal prefix followed by "*", or null (no filter) when it has no usable prefix.
        /// The prefix stops at the first wildcard character, at a literal "*", and at the first non-ASCII
        /// character, whose case folding in Windows and .NET could differ.
        /// </summary>
        internal static string? ForPattern(string pattern)
        {
            var prefix = new StringBuilder();
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '`' && i + 1 < pattern.Length)
                {
                    c = pattern[++i];
                }
                else if (c == '*' || c == '?' || c == '[')
                {
                    break;
                }

                if (c == '*' || c > 0x7F)
                {
                    break;
                }
                prefix.Append(c);
            }
            return prefix.Length == 0 ? null : prefix.Append('*').ToString();
        }
    }

    internal sealed class SecretTooLargeException : ArgumentException
    {
        // The message leaves out the secret's actual size, which would tell its length
        internal SecretTooLargeException()
            : base($"The encoded secret exceeds the {CRED_MAX_CREDENTIAL_BLOB_SIZE} bytes Windows Credential Manager allows.")
        {
        }
    }

    /// <summary>CredReadW failed while New- or Set-WinCredential prepared a write, so nothing was written.</summary>
    internal sealed class CredentialReadException : Win32Exception
    {
        internal CredentialReadException(int error) : base(error) { }
    }

    internal sealed class CredentialExistsException : InvalidOperationException
    {
        internal CredentialExistsException(string targetName, CredType type)
            : base($"A {type} credential with the target '{targetName}' already exists. Use Set-WinCredential to change it.")
        {
        }
    }

    internal sealed class CredentialNotFoundException : InvalidOperationException
    {
        internal CredentialNotFoundException(string targetName, CredType type)
            : base($"No {type} credential with the target '{targetName}' was found. Use New-WinCredential to create it.")
        {
        }
    }
}
