using System.ComponentModel;
using System.Management.Automation;
using System.Security;
using static WinCred.Native.NativeMethods;

namespace WinCred.Commands
{
    /// <summary>
    /// Creates a credential in Windows Credential Manager, or replaces the one with the same target and type.
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "WinCredential", DefaultParameterSetName = SecretSet,
        SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
    [OutputType(typeof(WinCredential))]
    public sealed class SetWinCredentialCommand : PSCmdlet
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

            Validate(userName, secret);

            if (!ShouldProcess(Target, $"Create or replace {Type} credential with {Persist} persistence"))
            {
                return;
            }

            ErrorRecord? error = null;
            try
            {
                credMan.Write(Target, Type, userName, Comment, Persist, secret!, Encoding);
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

            WriteVerbose($"Wrote {Type} credential '{Target}' with {Persist} persistence.");

            if (PassThru)
            {
                WinCredential? written;
                try
                {
                    written = credMan.Read(Target, Type, includeSecret: false, Encoding);
                }
                catch (Win32Exception exception)
                {
                    WriteError(Errors.Native(exception, "ReadFailed", ErrorCategory.ReadError, Target));
                    return;
                }

                if (written != null)
                {
                    WriteObject(written);
                }
            }
        }

        private void Validate(string? userName, SecureString? secret)
        {
            if (secret == null)
            {
                ThrowTerminatingError(Errors.InvalidArgument(nameof(Secret),
                    "Specify the secret with -Secret or -Credential. Set-WinCredential never creates a credential without one.",
                    "MissingSecret", Target));
            }

            int maxTarget = Type == CredType.Generic ? MaxGenericTargetLength : CRED_MAX_DOMAIN_TARGET_NAME_LENGTH;
            if (Target.Length > maxTarget)
            {
                ThrowTerminatingError(Errors.InvalidArgument(nameof(Target),
                    $"The target is {Target.Length} characters; {Type} credentials allow at most {maxTarget}.",
                    "TargetTooLong", Target));
            }

            if (userName != null && userName.Length > CRED_MAX_USERNAME_LENGTH)
            {
                ThrowTerminatingError(Errors.InvalidArgument(nameof(UserName),
                    $"The user name is {userName.Length} characters; at most {CRED_MAX_USERNAME_LENGTH} are allowed.",
                    "UserNameTooLong", Target));
            }

            if (Comment != null && Comment.Length > CRED_MAX_STRING_LENGTH)
            {
                ThrowTerminatingError(Errors.InvalidArgument(nameof(Comment),
                    $"The comment is {Comment.Length} characters; at most {CRED_MAX_STRING_LENGTH} are allowed.",
                    "CommentTooLong", Target));
            }
        }
    }
}
