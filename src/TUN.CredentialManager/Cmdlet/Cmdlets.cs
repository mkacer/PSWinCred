using System;
using System.Text;
using System.Management.Automation;
using PSCredentialManager.Common.Enum;
using PSCredentialManager.Api;
using PSCredentialManager.Common;
using PSCredentialManager.Api.Extensions;
using PSCredentialManager.Common.Exceptions;
using System.Collections.Generic;
using System.Security;
using PSCredentialManager.Cmdlet.Extensions;

// User-facing help for these cmdlets lives in docs/en-US/*.md and is compiled to MAML by build.ps1.
namespace PSCredentialManager.Cmdlet
{
    /// <summary>
    /// Gets stored credentials from the Windows Credential Manager.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "StoredCredential")]
    [OutputType(typeof(PSCredential))]
    [OutputType(typeof(Credential), ParameterSetName = new[]{"CredentialObject Output"})]
    public class GetStoredCredential : PSCmdlet
    {
        [Parameter(ValueFromPipeline = true)]
        [ValidateLength(1, 337)]
        public string Target;

        [Parameter()]
        public CredType Type = CredType.Generic;

        /// <summary>
        /// Fills Password and PasswordSize on Credential objects. Only applies with AsCredentialObject.
        /// </summary>
        [Parameter()]
        public SwitchParameter IncludeClearPassword;

        /// <summary>
        /// Obsolete: the clear password is excluded by default. Overrides IncludeClearPassword.
        /// </summary>
        [Parameter()]
        public SwitchParameter ExcludeClearPassword;

        /// <summary>
        /// Fills SecurePassword on Credential objects. Only applies with AsCredentialObject.
        /// </summary>
        [Parameter()]
        public SwitchParameter IncludeSecurePassword;

        [Parameter(ParameterSetName = "CredentialObject Output")]
        public SwitchParameter AsCredentialObject;

        protected override void ProcessRecord()
        {
            CredentialManager credentialManager = new CredentialManager();

            // A PSCredential only needs the secure password, so never read the clear one for it
            bool includeClearPassword = AsCredentialObject && IncludeClearPassword && !ExcludeClearPassword;
            bool includeSecurePassword = !AsCredentialObject || IncludeSecurePassword;

            if (Target == null)
            {
                //If no target is specified get all available credentials from the store
                IEnumerable<Credential> credential;
                try
                {
                    credential = credentialManager.ReadCred(includeClearPassword, includeSecurePassword);
                }
                catch (Exception exception)
                {
                    WriteError(new ErrorRecord(exception, "EnumerateFailed", ErrorCategory.InvalidOperation, null));
                    return;
                }

                if (!AsCredentialObject)
                {
                    WriteVerbose("Converting returned credentials to PSCredential Object");

                    foreach (Credential cred in credential)
                    {
                        WritePsCredential(cred);
                    }
                }
                else
                {
                    //write each credential object to the pipeline
                    WriteObject(credential, true);
                }
            }
            else
            {
                Credential credential;
                try
                {
                    //Retrieve credential from Cred Store
                    WriteVerbose("Retrieving requested credential from Windows Credential Manager");
                    credential = credentialManager.ReadCred(Target, Type, includeClearPassword, includeSecurePassword);
                }
                catch (CredentialNotFoundException exception)
                {
                    WriteDebug(exception.Message);
                    return;
                }
                catch (Exception exception)
                {
                    WriteError(new ErrorRecord(exception, "ReadFailed", ErrorCategory.InvalidOperation, Target));
                    return;
                }

                if (!AsCredentialObject)
                {
                    WriteVerbose("Converting returned credential blob to PSCredential Object");
                    WritePsCredential(credential);
                }
                else
                {
                    //write credential object to pipeline
                    WriteObject(credential);
                }
            }
        }

        private void WritePsCredential(Credential credential)
        {
            try
            {
                WriteObject(credential.ToPsCredential());
            }
            catch (Exception ex)
            {
                WriteWarning($"{credential.TargetName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Creates a new credential in the Windows Credential Manager.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "StoredCredential", DefaultParameterSetName = "Secure String")]
    [OutputType(typeof(Credential))]
    public class NewStoredCredential : PSCmdlet
    {
        private const uint c_maxPasswordBytes = 2560; // CRED_MAX_CREDENTIAL_BLOB_SIZE (5 * 512) => 2560

        [Parameter()]
        [ValidateLength(1, 337)]
        public string Target = Environment.MachineName;

        [Parameter(ParameterSetName = "Plain Text")]
        [Parameter(ParameterSetName = "Secure String")]
        public string UserName = Environment.UserName;

        [Parameter(Mandatory = true, ParameterSetName = "Plain Text")]
        [AllowEmptyString()]
        public string Password;

        [Parameter(Mandatory = true, ParameterSetName = "Secure String")]
        public SecureString SecurePassword;

        [Parameter()]
        public string Comment = "Updated by: " + Environment.UserName + " on: " + DateTime.Now.ToShortDateString();

        [Parameter()]
        public CredType Type = CredType.Generic;

        [Parameter()]
        public CredPersist Persist = CredPersist.Session;

        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "PSCredentialObject")]
        public PSCredential Credentials;

        protected override void BeginProcessing()
        {
            //Check password does not exceed 2560 bytes
            if (MyInvocation.BoundParameters.ContainsKey("Password") && Encoding.Unicode.GetByteCount(Password) > c_maxPasswordBytes)
            {
                ThrowTerminatingError(PasswordTooLong("Password"));
            }

            if (MyInvocation.BoundParameters.ContainsKey("SecurePassword") && SecurePassword.Length * 2 > c_maxPasswordBytes)
            {
                ThrowTerminatingError(PasswordTooLong("SecurePassword"));
            }
        }

        protected override void ProcessRecord()
        {
            string userName = UserName;
            string password = Password;
            SecureString securePassword = SecurePassword;

            if (ParameterSetName == "PSCredentialObject")
            {
                userName = Credentials.UserName;
                securePassword = Credentials.Password;
                password = null;

                if (securePassword.Length * 2 > c_maxPasswordBytes)
                {
                    WriteError(PasswordTooLong("Credentials"));
                    return;
                }
            }

            //Create credential object
            Credential credential = new Credential()
            {
                TargetName = Target,
                Password = password,
                SecurePassword = securePassword,
                PasswordSize = password != null ? (UInt32)Encoding.Unicode.GetByteCount(password) : uint.MinValue,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                Comment = Comment,
                TargetAlias = null,
                Type = Type,
                Persist = Persist,
                UserName = userName,
                LastWritten = DateTime.Now,
            };

            //Convert credential to native credential
            NativeCredential nativeCredential = credential.ToNativeCredential();
            CredentialManager credentialManager = new CredentialManager();

            try
            {
                //Write credential to Windows Credential manager
                WriteVerbose("Writing credential to Windows Credential Manager");
                credentialManager.WriteCred(nativeCredential);
                WriteObject(credential);
            }
            catch (Exception exception)
            {
                WriteError(new ErrorRecord(exception, "WriteFailed", ErrorCategory.InvalidOperation, Target));
            }
            finally
            {
                //Zero the unmanaged password copy and free all unmanaged strings
                nativeCredential.Release();
            }
        }

        private static ErrorRecord PasswordTooLong(string parameterName)
        {
            Exception exception = new ArgumentOutOfRangeException(parameterName, $"The specified password has exceeded {c_maxPasswordBytes} bytes");
            return new ErrorRecord(exception, "PasswordTooLong", ErrorCategory.InvalidArgument, parameterName);
        }
    }

    /// <summary>
    /// Deletes a credential from the Windows Credential Manager.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "StoredCredential")]
    public class RemoveStoredCredential : PSCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public string Target;

        [Parameter()]
        public CredType Type = CredType.Generic;

        protected override void ProcessRecord()
        {
            CredentialManager credentialManager = new CredentialManager();

            //Delete credential from store
            try
            {
                WriteVerbose("Deleting requested credential from Windows Credential Manager");
                credentialManager.DeleteCred(Target, Type);
            }
            catch (CredentialNotFoundException exception)
            {
                WriteError(new ErrorRecord(exception, "CredentialNotFound", ErrorCategory.ObjectNotFound, Target));
            }
            catch (Exception exception)
            {
                WriteError(new ErrorRecord(exception, "DeleteFailed", ErrorCategory.InvalidOperation, Target));
            }
        }
    }

    /// <summary>
    /// Generates a random password.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "StrongPassword")]
    [OutputType(typeof(string))]
    public class GetStrongPassword : PSCmdlet
    {
        [Parameter()]
        [ValidateRange(1, 128)]
        public int Length = 10;

        [Parameter()]
        [ValidateRange(0, 128)]
        public int NumberOfSpecialCharacters = 3;

        protected override void ProcessRecord()
        {
            if (NumberOfSpecialCharacters > Length)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("NumberOfSpecialCharacters must not be greater than Length.", "NumberOfSpecialCharacters"),
                    "TooManySpecialCharacters", ErrorCategory.InvalidArgument, NumberOfSpecialCharacters));
            }

            WriteObject(Helpers.GeneratePassword(Length, NumberOfSpecialCharacters));
        }
    }
}
