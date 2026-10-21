using System;
using System.Management.Automation;
using System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSCredentialManager.Cmdlet.Extensions;
using PSCredentialManager.Common;

namespace PSCredentialManager.Tests
{
    [TestClass]
    public class CmdletCredentialExtensionsTests
    {
        [TestMethod]
        public void ToPSCredentialTest()
        {
            Credential credential = new Credential()
            {
                UserName = "test-user",
                Password = "Password1"
            };

            PSCredential psCredential = credential.ToPsCredential();

            Assert.IsNotNull(psCredential);
            Assert.IsInstanceOfType(psCredential, typeof(PSCredential));
            Assert.AreEqual(credential.UserName, psCredential.UserName);
            Assert.AreEqual(credential.Password, psCredential.Password.ToInsecureString());
        }

        [TestMethod]
        public void ToPSCredentialTestSecureString()
        {
            string pwd = "Password1";

            Credential credential = new Credential()
            {
                UserName = "test-user",
                SecurePassword = pwd.ToSecureString()
            };

            PSCredential psCredential = credential.ToPsCredential();

            Assert.IsNotNull(psCredential);
            Assert.IsInstanceOfType(psCredential, typeof(PSCredential));
            Assert.AreEqual(credential.UserName, psCredential.UserName);
            Assert.AreEqual(pwd, psCredential.Password.ToInsecureString());
        }

        [TestMethod]
        public void ToPSCredentialTestException()
        {
            Credential credential = new Credential()
            {
                UserName = "test-user",
            };

            Assert.ThrowsExactly<Exception>(() => credential.ToPsCredential());
        }

        [TestMethod]
        public void ToPSCredentialTestEmptyPassword()
        {
            Credential credential = new Credential()
            {
                UserName = "test-user",
                SecurePassword = new SecureString()
            };

            PSCredential psCredential = credential.ToPsCredential();

            Assert.AreEqual(credential.UserName, psCredential.UserName);
            Assert.AreEqual(0, psCredential.Password.Length);
        }
    }
}