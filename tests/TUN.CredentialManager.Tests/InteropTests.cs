using System;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSCredentialManager.Api.Extensions;
using PSCredentialManager.Cmdlet.Extensions;
using PSCredentialManager.Common;

namespace PSCredentialManager.Tests
{
    [TestClass]
    public class InteropTests
    {
        [TestMethod]
        public void FileTimeWithHighBitInLowPartConverts()
        {
            // 0x01D0000080000000: the low 32 bits read as a negative int
            DateTime expected = DateTime.FromFileTime(0x01D0000080000000);
            FILETIME fileTime = new FILETIME { dwHighDateTime = 0x01D00000, dwLowDateTime = unchecked((int)0x80000000) };

            Assert.AreEqual(expected, fileTime.ToDateTime());
        }

        [TestMethod]
        public void FileTimeRoundTrips()
        {
            DateTime now = DateTime.Now;
            Assert.AreEqual(now, now.ToComFileTime().ToDateTime());
        }

        [TestMethod]
        public void BlobSizeComesFromPasswordNotPasswordSize()
        {
            Credential credential = new Credential
            {
                TargetName = "server01",
                UserName = "test-user",
                Password = "abc",
                PasswordSize = 1000,
                LastWritten = DateTime.Now,
            };

            NativeCredential native = credential.ToNativeCredential();
            try
            {
                Assert.AreEqual(6u, native.CredentialBlobSize);
            }
            finally
            {
                native.Release();
            }
        }

        [TestMethod]
        public void SecurePasswordBlobSizeIsTwoBytesPerChar()
        {
            Credential credential = new Credential
            {
                TargetName = "server01",
                UserName = "test-user",
                SecurePassword = "abcd".ToSecureString(),
                LastWritten = DateTime.Now,
            };

            NativeCredential native = credential.ToNativeCredential();
            try
            {
                Assert.AreEqual(8u, native.CredentialBlobSize);
                Assert.AreEqual("abcd", Marshal.PtrToStringUni(native.CredentialBlob, 4));
            }
            finally
            {
                native.Release();
            }
        }

        [TestMethod]
        public void ZeroMemoryClearsPastEmbeddedNull()
        {
            IntPtr blob = Marshal.StringToCoTaskMemUni("se\0cret");
            try
            {
                PSCredentialManager.Api.Extensions.CredentialExtensions.ZeroMemory(blob, 14);

                byte[] after = new byte[14];
                Marshal.Copy(blob, after, 0, 14);
                CollectionAssert.AreEqual(new byte[14], after);
            }
            finally
            {
                Marshal.FreeCoTaskMem(blob);
            }
        }
    }
}
