using System;
using System.Collections.Generic;
using System.Security;

namespace WinCred
{
    /// <summary>
    /// A credential read from Windows Credential Manager. Secret and attribute values are filled only on request.
    /// </summary>
    public sealed class WinCredential
    {
        internal WinCredential(string targetName, CredType type, CredFlags flags, CredPersist persist, string? userName,
            string? comment, string? targetAlias, DateTimeOffset lastWritten, IReadOnlyList<WinCredentialAttribute> attributes, SecureString? secret)
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
        public DateTimeOffset LastWritten { get; }
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

    /// <summary>
    /// A CREDENTIAL_ATTRIBUTEW entry. Its value belongs to the program that wrote it and may be secret,
    /// so Value is filled only with -IncludeSecret. Value is a copy that the caller owns: clear it with
    /// [Array]::Clear($attribute.Value, 0, $attribute.Value.Length) when done. Unlike the secret, it is
    /// plain bytes, so formatting or serializing the attribute shows it.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "Mirrors CREDENTIAL_ATTRIBUTEW in wincred.h.")]
    public sealed class WinCredentialAttribute
    {
        internal WinCredentialAttribute(string keyword, uint flags, int valueLength, byte[]? value)
        {
            Keyword = keyword;
            Flags = flags;
            ValueLength = valueLength;
            Value = value;
        }

        public string Keyword { get; }
        public uint Flags { get; }
        public int ValueLength { get; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819", Justification = "A mutable copy, so the caller can clear a secret value.")]
        public byte[]? Value { get; }

        public override string ToString() => Keyword;
    }
}
