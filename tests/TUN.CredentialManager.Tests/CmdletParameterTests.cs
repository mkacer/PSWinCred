using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSCredentialManager.Cmdlet;

namespace PSCredentialManager.Tests
{
    /// <summary>
    /// Checks parameter binding in a real runspace. These tests never reach the credential store.
    /// </summary>
    [TestClass]
    public class CmdletParameterTests
    {
        private static Collection<PSObject> Invoke(string script)
        {
            Collection<PSObject> output = Invoke(script, out ErrorRecord[] errors);
            Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
            return output;
        }

        private static Collection<PSObject> Invoke(string script, out ErrorRecord[] errors)
        {
            InitialSessionState state = InitialSessionState.CreateDefault2();
            state.ImportPSModule(new[] { typeof(GetStoredCredential).Assembly.Location });
            using (PowerShell ps = PowerShell.Create(state))
            {
                Collection<PSObject> output = ps.AddScript(script).Invoke();
                errors = ps.Streams.Error.ToArray();
                return output;
            }
        }

        [TestMethod]
        public void NewStoredCredentialRequiresPassword()
        {
            // With no host to prompt, a missing mandatory parameter fails binding
            Invoke("New-StoredCredential -Target 'x' -UserName 'u'", out ErrorRecord[] errors);
            Assert.HasCount(1, errors);
            StringAssert.StartsWith(errors[0].FullyQualifiedErrorId, "MissingMandatoryParameter");
        }

        [TestMethod]
        public void NewStoredCredentialAcceptsEmptyPlainPassword()
        {
            CommandInfo command = Invoke("Get-Command New-StoredCredential").Single().BaseObject as CommandInfo;
            ParameterMetadata password = command.Parameters["Password"];
            Assert.IsTrue(password.Attributes.OfType<AllowEmptyStringAttribute>().Any());
        }

        [TestMethod]
        public void NewStoredCredentialDefaultsToSecureString()
        {
            CmdletInfo command = Invoke("Get-Command New-StoredCredential").Single().BaseObject as CmdletInfo;
            Assert.AreEqual("Secure String", command.DefaultParameterSet);
        }

        [TestMethod]
        public void GetStoredCredentialHasIncludeClearPassword()
        {
            CommandInfo command = Invoke("Get-Command Get-StoredCredential").Single().BaseObject as CommandInfo;
            Assert.IsTrue(command.Parameters.ContainsKey("IncludeClearPassword"));
            Assert.IsTrue(command.Parameters.ContainsKey("ExcludeClearPassword"));
        }

        [TestMethod]
        public void GetStrongPasswordRejectsTooManySpecialCharacters()
        {
            Invoke("Get-StrongPassword -Length 5 -NumberOfSpecialCharacters 6", out ErrorRecord[] errors);
            Assert.HasCount(1, errors);
            StringAssert.StartsWith(errors[0].FullyQualifiedErrorId, "TooManySpecialCharacters");
        }

        [TestMethod]
        public void GetStrongPasswordReturnsPassword()
        {
            string password = (string)Invoke("Get-StrongPassword -Length 24").Single().BaseObject;
            Assert.AreEqual(24, password.Length);
        }
    }
}
