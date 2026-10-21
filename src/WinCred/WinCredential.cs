using System;
using System.Collections.Generic;
using System.Security;

namespace WinCred
{
    /// <summary>
    /// A credential read from Windows Credential Manager. Secret is filled only on request.
    /// </summary>
    public sealed class WinCredential
    {
        internal WinCredential(string targetName, CredType type, CredFlags flags, CredPersist persist, string? userName,
            string? comment, string? targetAlias, DateTime lastWritten, IReadOnlyList<WinCredentialAttribute> attributes, SecureString? secret)
        {
            TargetName = targetName;
            Type = type;
            Flags = flags;
            Persist = persist;
            UserName = userName;
            Comment = comment;
            TargetAlias = targetAlias;
            LastWritten = lastWritten;
            Attributes = attributes;
            Secret = secret;
        }

        public string TargetName { get; }
        public CredType Type { get; }
        public string? UserName { get; }
        public CredPersist Persist { get; }
        public string? Comment { get; }
        public string? TargetAlias { get; }
        public DateTime LastWritten { get; }
        public CredFlags Flags { get; }
        public IReadOnlyList<WinCredentialAttribute> Attributes { get; }
        public SecureString? Secret { get; }

        public override string ToString() => TargetName;

        /// <summary>
        /// Windows returns the secret of these types only to the Local Security Authority.
        /// </summary>
        internal static bool IsSecretReadable(CredType type)
        {
            return type != CredType.DomainPassword && type != CredType.DomainCertificate;
        }
    }

    /// <summary>A CREDENTIAL_ATTRIBUTEW entry.</summary>
    public sealed class WinCredentialAttribute
    {
        internal WinCredentialAttribute(string keyword, uint flags, byte[] value)
        {
            Keyword = keyword;
            Flags = flags;
            Value = value;
        }

        public string Keyword { get; }
        public uint Flags { get; }
        public byte[] Value { get; }

        public override string ToString() => Keyword;
    }
}
