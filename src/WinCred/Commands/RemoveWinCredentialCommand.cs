using System.ComponentModel;
using System.Management.Automation;

namespace WinCred.Commands
{
    /// <summary>
    /// Deletes a credential from Windows Credential Manager. Target is always literal. Deleting cannot be
    /// undone, so it asks for confirmation by default.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "WinCredential", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveWinCredentialCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("TargetName")]
        [ValidateNotNullOrEmpty]
        public string Target { get; set; } = null!;

        [Parameter(ValueFromPipelineByPropertyName = true)]
        public CredType Type { get; set; } = CredType.Generic;

        private readonly CredMan credMan = new CredMan();

        protected override void ProcessRecord()
        {
            if (!ShouldProcess(Target, $"Remove {Type} credential"))
            {
                return;
            }

            try
            {
                if (!credMan.Delete(Target, Type))
                {
                    WriteError(Errors.NotFound(Target, Type));
                }
            }
            catch (Win32Exception exception)
            {
                WriteError(Errors.Native(exception, "DeleteFailed", ErrorCategory.WriteError, Target));
            }
        }
    }
}
