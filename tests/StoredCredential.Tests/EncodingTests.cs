using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSCredentialManager.Api.Extensions;
using PSCredentialManager.Cmdlet.Extensions;
using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;

namespace PSCredentialManager.Tests
{
    [TestClass]
    public class EncodingTests
    {
        // ASCII, a two-byte UTF-8 char (é) and a surrogate pair (😀)
        private const string Password = "päéss\U0001F600";

        private static Credential Sample(string password, bool secure)
        {
            return new Credential
            {
                TargetName = "server01",
                UserName = "test-user",
                Password = secure ? null : password,
                SecurePassword = secure ? password.ToSecureString() : null,
                LastWritten = DateTime.Now,
            };
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode, false)]
        [DataRow(CredEncoding.Unicode, true)]
        [DataRow(CredEncoding.UTF8, false)]
        [DataRow(CredEncoding.UTF8, true)]
        public void RoundTrips(CredEncoding encoding, bool secure)
        {
            NativeCredential native = Sample(Password, secure).ToNativeCredential(encoding);
            try
            {
                Credential read = native.ToCredential(true, true, encoding);
                Assert.AreEqual(Password, read.Password);
                Assert.AreEqual(Password, read.SecurePassword.ToInsecureString());
                Assert.AreEqual(native.CredentialBlobSize, read.PasswordSize);
            }
            finally
            {
                native.Release();
            }
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode, 14u)]
        [DataRow(CredEncoding.UTF8, 11u)]
        public void BlobSizeIsEncodedByteCount(CredEncoding encoding, uint expected)
        {
            // p(1) ä(2) é(2) s(1) s(1) 😀(4) = 11 bytes in UTF-8; 7 UTF-16 code units (😀 is a surrogate pair) = 14 bytes
            NativeCredential native = Sample(Password, false).ToNativeCredential(encoding);
            try
            {
                Assert.AreEqual(expected, native.CredentialBlobSize);
            }
            finally
            {
                native.Release();
            }
        }

        [TestMethod]
        public void Utf8BlobIsPlainUtf8()
        {
            NativeCredential native = Sample("abc", false).ToNativeCredential(CredEncoding.UTF8);
            try
            {
                byte[] blob = new byte[native.CredentialBlobSize];
                Marshal.Copy(native.CredentialBlob, blob, 0, blob.Length);
                CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63 }, blob);
            }
            finally
            {
                native.Release();
            }
        }

        [TestMethod]
        public void OddLengthBlobDecodesAsUnicodeWithoutThrowing()
        {
            // Another program stored "abc" as UTF-8; reading it as Unicode must not fail an enumeration
            NativeCredential native = Sample("abc", false).ToNativeCredential(CredEncoding.UTF8);
            try
            {
                Credential read = native.ToCredential(true, true, CredEncoding.Unicode);
                Assert.AreEqual(3u, read.PasswordSize);
                Assert.AreEqual(read.Password.Length, read.SecurePassword.Length);
            }
            finally
            {
                native.Release();
            }
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode)]
        [DataRow(CredEncoding.UTF8)]
        public void EmptyPasswordRoundTrips(CredEncoding encoding)
        {
            NativeCredential native = Sample("", true).ToNativeCredential(encoding);
            try
            {
                Assert.AreEqual(0u, native.CredentialBlobSize);
                Credential read = native.ToCredential(true, true, encoding);
                Assert.AreEqual("", read.Password);
                Assert.AreEqual(0, read.SecurePassword.Length);
            }
            finally
            {
                native.Release();
            }
        }
    }
}
