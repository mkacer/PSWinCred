using System.ComponentModel;
using System.Management.Automation;
using System.Security;
using WinCred.Native;

namespace WinCred.Commands
{
    /// <summary>
    /// Creates a credential in Windows Credential Manager. It fails if the credential already exists.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "WinCredential", DefaultParameterSetName = SecretSet,
        SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(WinCredential))]
    public sealed class NewWinCredentialCommand : PSCmdlet
    {
        private const string SecretSet = "Secret";
        private const string PSCredentialSet = "PSCredential";

        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Target { get; set; } = null!;

        [Parameter(ParameterSetName = SecretSet)]
        public string? UserName { get; set; }

        /// <summary>
        /// Not mandatory on purpose: a missing secret MUST fail, not prompt.
        /// </summary>
        [Parameter(ParameterSetName = SecretSet)]
        public SecureString? Secret { get; set; }

        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = PSCredentialSet)]
        public PSCredential Credential { get; set; } = null!;

        [Parameter]
        [ValidateSet(nameof(CredType.Generic), nameof(CredType.DomainPassword))]
        public CredType Type { get; set; } = CredType.Generic;

        [Parameter]
        public CredPersist Persist { get; set; } = CredPersist.Session;

        [Parameter]
        public string? Comment { get; set; }

        [Parameter]
        public CredEncoding Encoding { get; set; } = CredEncoding.Unicode;

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        private readonly CredMan credMan = new CredMan();

        protected override void ProcessRecord()
        {
            string? userName = ParameterSetName == PSCredentialSet ? Credential.UserName : UserName;
            SecureString? secret = ParameterSetName == PSCredentialSet ? Credential.Password : Secret;

            if (secret == null)
            {
                ThrowTerminatingError(ParameterSetName == PSCredentialSet
                    ? Errors.InvalidArgument(nameof(Credential), "The PSCredential has no password, so there is no secret to store.", "CredentialHasNoSecret", Target)
                    : Errors.InvalidArgument(nameof(Secret), "Specify the secret with -Secret or -Credential. New-WinCredential never creates a credential without one.", "MissingSecret", Target));
            }

            ThrowIfInvalid(Errors.CheckTarget(Target, Type));
            ThrowIfInvalid(Errors.CheckUserName(userName, Type, Target));
            ThrowIfInvalid(Errors.CheckComment(Comment, Target));

            if (!ShouldProcess(Target, $"Create {Type} credential with {Persist} persistence"))
            {
                return;
            }

            ErrorRecord? error = null;
            try
            {
                credMan.Create(Target, Type, userName, Comment, Persist, secret!, Encoding);
            }
            catch (CredentialExistsException exception)
            {
                error = new ErrorRecord(exception, "CredentialExists", ErrorCategory.ResourceExists, Target);
            }
            catch (SecretNotEncodableException exception)
            {
                error = new ErrorRecord(exception, "SecretNotEncodable", ErrorCategory.InvalidArgument, Target);
            }
            catch (SecretTooLargeException exception)
            {
                error = new ErrorRecord(exception, "SecretTooLarge", ErrorCategory.InvalidArgument, Target);
            }
            catch (Win32Exception exception)
            {
                error = Errors.Native(exception, "WriteFailed", ErrorCategory.WriteError, Target);
            }

            if (error != null)
            {
                // Nothing was written; stop rather than continue with a different result than requested
                ThrowTerminatingError(error);
            }

            WriteVerbose($"Created {Type} credential '{Target}' with {Persist} persistence.");
            if (PassThru)
            {
                WriteStored(this, credMan, Target, Type);
            }
        }

        private void ThrowIfInvalid(ErrorRecord? error)
        {
            if (error != null)
            {
                ThrowTerminatingError(error);
            }
        }

        /// <summary>Writes the stored credential, without its secret, for -PassThru.</summary>
        internal static void WriteStored(PSCmdlet cmdlet, CredMan credMan, string target, CredType type)
        {
            try
            {
                ReadResult? stored = credMan.Read(target, type, includeSecret: false, CredEncoding.Unicode);
                if (stored?.Credential != null)
                {
                    cmdlet.WriteObject(stored.Credential);
                }
            }
            catch (Win32Exception exception)
            {
                cmdlet.WriteError(Errors.Native(exception, "ReadFailed", ErrorCategory.ReadError, target));
            }
        }
    }
}
