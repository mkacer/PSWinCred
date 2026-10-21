using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static WinCred.Native.NativeMethods;

namespace WinCred.Native.Tests
{
    [TestClass]
    public class CredManTests
    {
        private const int ErrorAccessDenied = 5;

        private static FakeCredentialApi.Stored Sample(string target, string secret = "s3cret", CredType type = CredType.Generic)
        {
            return new FakeCredentialApi.Stored
            {
                TargetName = target,
                Type = type,
                UserName = "user",
                Blob = Encoding.Unicode.GetBytes(secret),
                LastWritten = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToFileTimeUtc(),
            };
        }

        [TestMethod]
        public void ReadReturnsFieldsAndFreesBuffer()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01");
            stored.Comment = "note";
            stored.TargetAlias = "alias";
            stored.Persist = CredPersist.LocalMachine;
            stored.Flags = (uint)CredFlags.UsernameTarget;
            api.Store.Add(stored);

            WinCredential? credential = new CredMan(api).Read("server01", CredType.Generic, includeSecret: false, CredEncoding.Unicode);

            Assert.IsNotNull(credential);
            Assert.AreEqual("server01", credential.TargetName);
            Assert.AreEqual(CredType.Generic, credential.Type);
            Assert.AreEqual("user", credential.UserName);
            Assert.AreEqual("note", credential.Comment);
            Assert.AreEqual("alias", credential.TargetAlias);
            Assert.AreEqual(CredPersist.LocalMachine, credential.Persist);
            Assert.AreEqual(CredFlags.UsernameTarget, credential.Flags);
            Assert.AreEqual(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), credential.LastWritten.ToUniversalTime());
            Assert.IsNull(credential.Secret, "The secret MUST NOT be read unless requested.");
            Assert.AreEqual(1, api.FreeCount);
            Assert.AreEqual(0, api.OpenAllocations);
        }

        [TestMethod]
        public void ReadIncludesSecretOnRequest()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01", "pä\U0001F600ss"));

            WinCredential? credential = new CredMan(api).Read("server01", CredType.Generic, includeSecret: true, CredEncoding.Unicode);

            Assert.AreEqual("pä\U0001F600ss", credential!.Secret!.ToPlainText());
            Assert.IsTrue(credential.Secret!.IsReadOnly());
        }

        [TestMethod]
        public void ReadDomainPasswordNeverHasSecret()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01", "", CredType.DomainPassword));

            WinCredential? credential = new CredMan(api).Read("server01", CredType.DomainPassword, includeSecret: true, CredEncoding.Unicode);

            Assert.IsNull(credential!.Secret);
        }

        [TestMethod]
        public void ReadNotFoundReturnsNull()
        {
            var api = new FakeCredentialApi();
            Assert.IsNull(new CredMan(api).Read("missing", CredType.Generic, true, CredEncoding.Unicode));
        }

        [TestMethod]
        public void ReadFailureThrowsWin32Exception()
        {
            var api = new FakeCredentialApi { Error = ErrorAccessDenied };
            var ex = Assert.ThrowsExactly<Win32Exception>(() => new CredMan(api).Read("x", CredType.Generic, true, CredEncoding.Unicode));
            Assert.AreEqual(ErrorAccessDenied, ex.NativeErrorCode);
        }

        [TestMethod]
        public void ReadAttributes()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01");
            stored.Attributes.Add(("key1", 0u, new byte[] { 1, 2, 3 }));
            stored.Attributes.Add(("key2", 0u, Array.Empty<byte>()));
            api.Store.Add(stored);

            WinCredential? credential = new CredMan(api).Read("server01", CredType.Generic, false, CredEncoding.Unicode);

            Assert.HasCount(2, credential!.Attributes);
            Assert.AreEqual("key1", credential.Attributes[0].Keyword);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, credential.Attributes[0].Value);
            Assert.IsEmpty(credential.Attributes[1].Value);
        }

        [TestMethod]
        public void EnumerateFiltersAndFreesBuffer()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("a.one"));
            api.Store.Add(Sample("b.two"));
            api.Store.Add(Sample("a.three", type: CredType.DomainPassword));

            var credentials = new CredMan(api).Enumerate((name, type) => name.StartsWith("a.", StringComparison.Ordinal) && type == CredType.Generic, false, CredEncoding.Unicode);

            CollectionAssert.AreEqual(new[] { "a.one" }, credentials.Select(c => c.TargetName).ToArray());
            Assert.AreEqual(1, api.FreeCount);
            Assert.AreEqual(0, api.OpenAllocations);
        }

        [TestMethod]
        public void EnumerateFreesBufferWhenConversionFails()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("a"));
            api.Store.Add(Sample("b"));

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                new CredMan(api).Enumerate((name, type) => name == "b" ? throw new InvalidOperationException() : true, false, CredEncoding.Unicode));

            Assert.AreEqual(1, api.FreeCount);
            Assert.AreEqual(0, api.OpenAllocations);
        }

        [TestMethod]
        public void EnumerateEmptyStoreReturnsEmpty()
        {
            var api = new FakeCredentialApi();
            Assert.IsEmpty(new CredMan(api).Enumerate((n, t) => true, true, CredEncoding.Unicode));
        }

        [TestMethod]
        public void EnumerateFailureThrowsWin32Exception()
        {
            var api = new FakeCredentialApi { Error = ErrorAccessDenied };
            Assert.ThrowsExactly<Win32Exception>(() => new CredMan(api).Enumerate((n, t) => true, true, CredEncoding.Unicode));
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode)]
        [DataRow(CredEncoding.UTF8)]
        public void WritePassesFieldsAndEncodedSecret(CredEncoding encoding)
        {
            var api = new FakeCredentialApi();
            new CredMan(api).Write("server01", CredType.Generic, "user", "note", CredPersist.Enterprise, "pä\U0001F600".ToSecureString(), encoding);

            FakeCredentialApi.Stored written = api.LastWrite!;
            Assert.AreEqual("server01", written.TargetName);
            Assert.AreEqual(CredType.Generic, written.Type);
            Assert.AreEqual("user", written.UserName);
            Assert.AreEqual("note", written.Comment);
            Assert.AreEqual(CredPersist.Enterprise, written.Persist);
            Encoding expected = encoding == CredEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
            CollectionAssert.AreEqual(expected.GetBytes("pä\U0001F600"), written.Blob);
        }

        [TestMethod]
        public void WriteNullUserNameAndCommentPassNull()
        {
            var api = new FakeCredentialApi();
            new CredMan(api).Write("server01", CredType.Generic, null, null, CredPersist.Session, "x".ToSecureString(), CredEncoding.Unicode);

            Assert.IsNull(api.LastWrite!.UserName);
            Assert.IsNull(api.LastWrite.Comment);
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode, 1280, false)]
        [DataRow(CredEncoding.Unicode, 1281, true)]
        [DataRow(CredEncoding.UTF8, 2560, false)]
        [DataRow(CredEncoding.UTF8, 2561, true)]
        public void WriteEnforcesBlobLimit(CredEncoding encoding, int characters, bool tooLarge)
        {
            var api = new FakeCredentialApi();
            var credMan = new CredMan(api);
            Action write = () => credMan.Write("t", CredType.Generic, null, null, CredPersist.Session, new string('a', characters).ToSecureString(), encoding);

            if (tooLarge)
            {
                Assert.ThrowsExactly<SecretTooLargeException>(write);
                Assert.AreEqual(0, api.WriteCount, "An oversized secret MUST NOT reach CredWrite.");
            }
            else
            {
                write();
                Assert.HasCount(CRED_MAX_CREDENTIAL_BLOB_SIZE, api.LastWrite!.Blob);
            }
        }

        [TestMethod]
        public void WriteFailureThrowsWin32Exception()
        {
            var api = new FakeCredentialApi { Error = ErrorAccessDenied };
            Assert.ThrowsExactly<Win32Exception>(() =>
                new CredMan(api).Write("t", CredType.Generic, null, null, CredPersist.Session, "x".ToSecureString(), CredEncoding.Unicode));
        }

        [TestMethod]
        public void DeleteReportsWhetherCredentialExisted()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01"));
            var credMan = new CredMan(api);

            Assert.IsTrue(credMan.Delete("server01", CredType.Generic));
            Assert.IsFalse(credMan.Delete("server01", CredType.Generic));
        }

        [TestMethod]
        public void DeleteFailureThrowsWin32Exception()
        {
            var api = new FakeCredentialApi { Error = ErrorAccessDenied };
            Assert.ThrowsExactly<Win32Exception>(() => new CredMan(api).Delete("t", CredType.Generic));
        }

        [TestMethod]
        public void FileTimeWithHighBitInLowPartConverts()
        {
            var fileTime = new NativeMethods.FILETIME { dwHighDateTime = 0x01D00000, dwLowDateTime = 0x80000000 };
            Assert.AreEqual(DateTime.FromFileTime(0x01D0000080000000), CredMan.ToDateTime(fileTime));
        }
    }
}
