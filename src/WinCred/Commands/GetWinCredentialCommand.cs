using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using WinCred.Native;

namespace WinCred.Commands
{
    /// <summary>
    /// Gets credentials from Windows Credential Manager. Secrets and attribute values are included only on request.
    /// </summary>
    // No default parameter set on purpose. With one, the binder converts a piped credential object to a string for
    // the default set's wildcard -Target before it tries -LiteralTarget by property name, so 'lit[1]' matched 'lit1'.
    // Without one, it binds by property name first. Calls without -LiteralTarget still resolve to the Target sets.
    [Cmdlet(VerbsCommon.Get, "WinCredential")]
    [OutputType(typeof(WinCredential), ParameterSetName = new[] { Pattern, Literal })]
    [OutputType(typeof(PSCredential), ParameterSetName = new[] { PatternAsPSCredential, LiteralAsPSCredential })]
    public sealed class GetWinCredentialCommand : PSCmdlet
    {
        private const string Pattern = "Target";
        private const string Literal = "LiteralTarget";
        private const string PatternAsPSCredential = "TargetAsPSCredential";
        private const string LiteralAsPSCredential = "LiteralTargetAsPSCredential";

        [Parameter(Position = 0, ValueFromPipeline = true, ParameterSetName = Pattern)]
        [Parameter(Position = 0, ValueFromPipeline = true, ParameterSetName = PatternAsPSCredential)]
        [SupportsWildcards]
        [ValidateNotNullOrEmpty]
        public string[]? Target { get; set; }

        /// <summary>Exact names. Credential objects piped in bind here through their TargetName property.</summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = Literal)]
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = LiteralAsPSCredential)]
        [Alias("TargetName")]
        [ValidateNotNullOrEmpty]
        public string[] LiteralTarget { get; set; } = null!;

        [Parameter(ValueFromPipelineByPropertyName = true)]
        public CredType[]? Type { get; set; }

        [Parameter(ParameterSetName = Pattern)]
        [Parameter(ParameterSetName = Literal)]
        public SwitchParameter IncludeSecret { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = PatternAsPSCredential)]
        [Parameter(Mandatory = true, ParameterSetName = LiteralAsPSCredential)]
        public SwitchParameter AsPSCredential { get; set; }

        [Parameter]
        public CredEncoding Encoding { get; set; } = CredEncoding.Unicode;

        private readonly CredMan credMan = new CredMan();

        private bool IncludeSecretValues => IncludeSecret || AsPSCredential;

        protected override void ProcessRecord()
        {
            HashSet<CredType>? types = Type == null ? null : new HashSet<CredType>(Type);

            if (ParameterSetName == Literal || ParameterSetName == LiteralAsPSCredential)
            {
                foreach (string name in LiteralTarget)
                {
                    WriteExact(name, types);
                }
                return;
            }

            if (Target == null)
            {
                // Lists the whole store: Windows decrypts every secret, and each is scrubbed after reading
                WriteMatches(null, name => true, types);
                return;
            }

            foreach (string target in Target)
            {
                if (WildcardPattern.ContainsWildcardCharacters(target))
                {
                    var pattern = new WildcardPattern(target, WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
                    WriteMatches(TargetFilter.ForPattern(target), pattern.IsMatch, types);
                }
                else
                {
                    WriteExact(WildcardPattern.Unescape(target), types);
                }
            }
        }

        // CredReadW per type: Windows decrypts only the requested credential, never the whole store
        private void WriteExact(string target, HashSet<CredType>? types)
        {
            bool found = false;
            bool failed = false;
            Win32Exception? rejected = null;
            int rejectedCount = 0;
            CredType[] candidates = types?.ToArray() ?? CredMan.AllTypes;
            foreach (CredType type in candidates)
            {
                ReadResult? result;
                try
                {
                    result = credMan.Read(target, type, IncludeSecretValues, Encoding);
                }
                catch (Win32Exception exception) when (types == null && exception.NativeErrorCode == NativeMethods.ERROR_INVALID_PARAMETER)
                {
                    // Measured: CredReadW returns ERROR_INVALID_PARAMETER for DomainExtended with a plain name
                    // such as 'WinCred.Probe.none'; the other types return ERROR_NOT_FOUND. A type that rejects the
                    // name cannot hold a credential with it. Without -Type, skip that type; with -Type, report it.
                    rejected = exception;
                    rejectedCount++;
                    continue;
                }
                catch (Win32Exception exception)
                {
                    WriteError(Errors.Native(exception, "ReadFailed", ErrorCategory.ReadError, target));
                    failed = true;
                    continue;
                }

                if (result != null)
                {
                    found = true;
                    Emit(result);
                }
            }

            if (!found && !failed)
            {
                if (rejected != null && rejectedCount == candidates.Length)
                {
                    // No type accepted the name, so report Windows' answer instead of hiding it
                    WriteError(Errors.Native(rejected, "ReadFailed", ErrorCategory.ReadError, target));
                }
                else
                {
                    WriteError(Errors.NotFound(target, types?.Count == 1 ? types.Single() : (CredType?)null));
                }
            }
        }

        private void WriteMatches(string? filter, Func<string, bool> matchTarget, HashSet<CredType>? types)
        {
            List<ReadResult> results;
            try
            {
                results = credMan.Enumerate(filter, (name, type) => (types == null || types.Contains(type)) && matchTarget(name), IncludeSecretValues, Encoding);
            }
            catch (Win32Exception exception)
            {
                WriteError(Errors.Native(exception, "EnumerateFailed", ErrorCategory.ReadError, null));
                return;
            }

            foreach (ReadResult result in results)
            {
                Emit(result);
            }
        }

        private void Emit(ReadResult result)
        {
            WinCredential? credential = result.Credential;
            if (credential == null)
            {
                WriteError(new ErrorRecord(result.Error, "ConversionFailed", ErrorCategory.ReadError, result.TargetName));
                return;
            }

            if (result.Error != null)
            {
                // The secret could not be decoded; the credential is still reported, without it
                WriteError(new ErrorRecord(result.Error, "SecretNotDecodable", ErrorCategory.InvalidData, credential.TargetName));
                if (AsPSCredential)
                {
                    return;
                }
            }

            if (!AsPSCredential)
            {
                WriteObject(credential);
            }
            else if (credential.Secret == null)
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
