using System.Collections.Generic;
using System.ComponentModel;
using System.Management.Automation;
using System.Security;
using WinCred.Native;

namespace WinCred.Commands
{
    /// <summary>
    /// Changes an existing credential in Windows Credential Manager. Every field that is not specified keeps
    /// its stored value, including the secret, persistence, alias, attributes and flags.
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "WinCredential", DefaultParameterSetName = SecretSet,
        SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(WinCredential))]
    public sealed class SetWinCredentialCommand : PSCmdlet
    {
        private const string SecretSet = "Secret";
        private const string PSCredentialSet = "PSCredential";

        /// <summary>Always literal. Credential objects piped in bind here through their TargetName property.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("TargetName")]
        [ValidateNotNullOrEmpty]
        public string Target { get; set; } = null!;

        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateSet(nameof(CredType.Generic), nameof(CredType.DomainPassword))]
        public CredType Type { get; set; } = CredType.Generic;

        /// <summary>An empty string removes the user name.</summary>
        [Parameter(ParameterSetName = SecretSet)]
        public string? UserName { get; set; }

        [Parameter(ParameterSetName = SecretSet)]
        public SecureString? Secret { get; set; }

        /// <summary>Sets both the user name and the secret.</summary>
        [Parameter(Mandatory = true, ParameterSetName = PSCredentialSet)]
        public PSCredential Credential { get; set; } = null!;

        [Parameter]
        public CredPersist Persist { get; set; }

        /// <summary>An empty string removes the comment.</summary>
        [Parameter]
        public string? Comment { get; set; }

        /// <summary>Encoding of the new secret; only valid together with one.</summary>
        [Parameter]
        public CredEncoding Encoding { get; set; } = CredEncoding.Unicode;

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        private readonly CredMan credMan = new CredMan();

        protected override void ProcessRecord()
        {
            Dictionary<string, object> bound = MyInvocation.BoundParameters;
            var update = new CredentialUpdate { Encoding = Encoding };
            var changes = new List<string>();

            if (ParameterSetName == PSCredentialSet)
            {
                if (Credential.Password == null)
                {
                    ThrowTerminatingError(Errors.InvalidArgument(nameof(Credential), "The PSCredential has no password, so there is no secret to store.", "CredentialHasNoSecret", Target));
                }
                update.SetUserName = true;
                update.UserName = Credential.UserName;
                update.Secret = Credential.Password;
                changes.Add("user name");
                changes.Add("secret");
            }
            else
            {
                if (bound.ContainsKey(nameof(UserName)))
                {
                    update.SetUserName = true;
                    update.UserName = UserName;
                    changes.Add("user name");
                }
                if (bound.ContainsKey(nameof(Secret)))
                {
                    if (Secret == null)
                    {
                        ThrowTerminatingError(Errors.InvalidArgument(nameof(Secret), "-Secret is null. Omit it to keep the stored secret.", "MissingSecret", Target));
                    }
                    update.Secret = Secret;
                    changes.Add("secret");
                }
            }

            if (bound.ContainsKey(nameof(Comment)))
            {
                update.SetComment = true;
                update.Comment = Comment;
                changes.Add("comment");
            }
            if (bound.ContainsKey(nameof(Persist)))
            {
                update.Persist = Persist;
                changes.Add($"persistence to {Persist}");
            }

            if (bound.ContainsKey(nameof(Encoding)) && update.Secret == null)
            {
                ThrowTerminatingError(Errors.InvalidArgument(nameof(Encoding), "-Encoding applies to a new secret; specify -Secret or -Credential too.", "EncodingWithoutSecret", Target));
            }
            if (changes.Count == 0)
            {
                ThrowTerminatingError(Errors.InvalidArgument(nameof(Target), "Specify at least one field to change: -UserName, -Secret, -Credential, -Persist or -Comment.", "NothingToSet", Target));
            }

            ThrowIfInvalid(Errors.CheckTargetName(nameof(Target), Target));
            if (update.SetUserName)
            {
                ThrowIfInvalid(Errors.CheckUserName(update.UserName, Type, Target));
            }
            ThrowIfInvalid(Errors.CheckComment(update.Comment, Target));
            if (update.Secret != null)
            {
                ThrowIfInvalid(Errors.CheckEncoding(Type, Encoding, Target));
            }

            if (!ShouldProcess(Target, $"Change {string.Join(", ", changes)} of {Type} credential"))
            {
                return;
            }

            ErrorRecord? error = null;
            try
            {
                credMan.Update(Target, Type, update);
            }
            catch (CredentialNotFoundException exception)
            {
                // Like Remove-WinCredential: reported per item, nothing was written
                WriteError(new ErrorRecord(exception, "CredentialNotFound", ErrorCategory.ObjectNotFound, Target));
                return;
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

            WriteVerbose($"Changed {string.Join(", ", changes)} of {Type} credential '{Target}'.");
            if (PassThru)
            {
                NewWinCredentialCommand.WriteStored(this, credMan, Target, Type);
            }
        }

        private void ThrowIfInvalid(ErrorRecord? error)
        {
            if (error != null)
            {
                ThrowTerminatingError(error);
            }
        }
    }
}
