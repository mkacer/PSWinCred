using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;

namespace WinCred.Commands
{
    /// <summary>
    /// Gets credentials from Windows Credential Manager. Secrets are included only on request.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "WinCredential", DefaultParameterSetName = InfoSet)]
    [OutputType(typeof(WinCredential), ParameterSetName = new[] { InfoSet })]
    [OutputType(typeof(PSCredential), ParameterSetName = new[] { PSCredentialSet })]
    public sealed class GetWinCredentialCommand : PSCmdlet
    {
        private const string InfoSet = "WinCredential";
        private const string PSCredentialSet = "PSCredential";

        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("TargetName")]
        [SupportsWildcards]
        [ValidateNotNullOrEmpty]
        public string[]? Target { get; set; }

        [Parameter(ValueFromPipelineByPropertyName = true)]
        public CredType[]? Type { get; set; }

        [Parameter(ParameterSetName = InfoSet)]
        public SwitchParameter IncludeSecret { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = PSCredentialSet)]
        public SwitchParameter AsPSCredential { get; set; }

        [Parameter]
        public CredEncoding Encoding { get; set; } = CredEncoding.Unicode;

        private readonly CredMan credMan = new CredMan();

        protected override void ProcessRecord()
        {
            HashSet<CredType>? types = Type == null ? null : new HashSet<CredType>(Type);
            bool includeSecret = IncludeSecret || AsPSCredential;

            if (Target == null)
            {
                WriteMatches(target => true, types, includeSecret);
                return;
            }

            foreach (string target in Target)
            {
                if (WildcardPattern.ContainsWildcardCharacters(target))
                {
                    var pattern = new WildcardPattern(target, WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
                    WriteMatches(pattern.IsMatch, types, includeSecret);
                }
                else
                {
                    string literal = WildcardPattern.Unescape(target);
                    if (!WriteExact(literal, types, includeSecret))
                    {
                        WriteError(Errors.NotFound(literal, types?.Count == 1 ? types.Single() : (CredType?)null));
                    }
                }
            }
        }

        // Target names are case-insensitive in Credential Manager
        private bool WriteExact(string target, HashSet<CredType>? types, bool includeSecret)
        {
            if (types == null)
            {
                return WriteMatches(name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase), null, includeSecret);
            }

            bool found = false;
            foreach (CredType type in types)
            {
                WinCredential? credential;
                try
                {
                    credential = credMan.Read(target, type, includeSecret, Encoding);
                }
                catch (Win32Exception exception)
                {
                    WriteError(Errors.Native(exception, "ReadFailed", ErrorCategory.ReadError, target));
                    continue;
                }

                if (credential != null)
                {
                    found = true;
                    Emit(credential);
                }
            }
            return found;
        }

        private bool WriteMatches(Func<string, bool> matchTarget, HashSet<CredType>? types, bool includeSecret)
        {
            List<WinCredential> credentials;
            try
            {
                credentials = credMan.Enumerate((name, type) => (types == null || types.Contains(type)) && matchTarget(name), includeSecret, Encoding);
            }
            catch (Win32Exception exception)
            {
                WriteError(Errors.Native(exception, "EnumerateFailed", ErrorCategory.ReadError, null));
                // Not "not found": the caller already sees why
                return true;
            }

            foreach (WinCredential credential in credentials)
            {
                Emit(credential);
            }
            return credentials.Count > 0;
        }

        private void Emit(WinCredential credential)
        {
            if (!AsPSCredential)
            {
                WriteObject(credential);
                return;
            }

            if (credential.Secret == null)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"Windows does not return the secret of {credential.Type} credentials, so '{credential.TargetName}' cannot become a PSCredential."),
                    "SecretNotReadable", ErrorCategory.InvalidData, credential.TargetName));
            }
            else if (string.IsNullOrEmpty(credential.UserName))
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"The credential '{credential.TargetName}' has no user name, so it cannot become a PSCredential. Use -IncludeSecret instead."),
                    "NoUserName", ErrorCategory.InvalidData, credential.TargetName));
            }
            else
            {
                WriteObject(new PSCredential(credential.UserName, credential.Secret));
            }
        }
    }
}
