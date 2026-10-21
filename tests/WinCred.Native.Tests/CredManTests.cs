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

        // Reading

        [TestMethod]
        public void ReadReturnsFieldsWithoutSecretAndScrubsBuffer()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01");
            stored.Comment = "note";
            stored.TargetAlias = "alias";
            stored.Persist = CredPersist.LocalMachine;
            stored.Flags = (uint)CredFlags.VsmProtected;
            api.Store.Add(stored);

            WinCredential credential = new CredMan(api).Read("server01", CredType.Generic, includeSecret: false, CredEncoding.Unicode)!.Credential!;

            Assert.AreEqual("server01", credential.TargetName);
            Assert.AreEqual("user", credential.UserName);
            Assert.AreEqual("note", credential.Comment);
            Assert.AreEqual("alias", credential.TargetAlias);
            Assert.AreEqual(CredPersist.LocalMachine, credential.Persist);
            Assert.AreEqual(CredFlags.VsmProtected, credential.Flags);
            Assert.AreEqual(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), credential.LastWritten);
            Assert.IsNull(credential.Secret, "The secret MUST NOT be decoded unless requested.");
            Assert.AreEqual(1, api.FreeCount);
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree, "Windows' copy of the secret MUST be zeroed before CredFree.");
            Assert.AreEqual(0, api.OpenAllocations);
        }

        [TestMethod]
        public void ReadIncludesSecretOnRequest()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01", "pä\U0001F600ss"));

            WinCredential credential = new CredMan(api).Read("server01", CredType.Generic, includeSecret: true, CredEncoding.Unicode)!.Credential!;

            Assert.AreEqual("pä\U0001F600ss", credential.Secret!.ToPlainText());
            Assert.IsTrue(credential.Secret!.IsReadOnly());
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree);
        }

        [TestMethod]
        public void ReadDomainPasswordNeverHasSecret()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01", "", CredType.DomainPassword));

            Assert.IsNull(new CredMan(api).Read("server01", CredType.DomainPassword, true, CredEncoding.Unicode)!.Credential!.Secret);
        }

        [TestMethod]
        public void ReadNotFoundReturnsNull()
        {
            Assert.IsNull(new CredMan(new FakeCredentialApi()).Read("missing", CredType.Generic, true, CredEncoding.Unicode));
        }

        [TestMethod]
        public void ReadFailureThrowsWin32Exception()
        {
            var api = new FakeCredentialApi { ReadError = CredRequiresConfirmation };
            var ex = Assert.ThrowsExactly<Win32Exception>(() => new CredMan(api).Read("x", CredType.Generic, true, CredEncoding.Unicode));
            Assert.AreEqual(CredRequiresConfirmation, ex.NativeErrorCode);
        }

        [TestMethod]
        public void UndecodableSecretIsReportedWithTheCredential()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01");
            stored.Blob = Encoding.UTF8.GetBytes("abc");   // 3 bytes: not UTF-16
            api.Store.Add(stored);

            ReadResult result = new CredMan(api).Read("server01", CredType.Generic, true, CredEncoding.Unicode)!;

            Assert.IsNotNull(result.Credential);
            Assert.IsNull(result.Credential.Secret);
            Assert.IsInstanceOfType<SecretNotDecodableException>(result.Error);
            Assert.DoesNotContain("abc", result.Error!.Message);
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree);
        }

        [TestMethod]
        public void AttributeValuesOnlyOnRequest()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01");
            stored.Attributes.Add(("key1", 0u, new byte[] { 1, 2, 3 }));
            stored.Attributes.Add(("key2", 0u, Array.Empty<byte>()));
            api.Store.Add(stored);
            var credMan = new CredMan(api);

            WinCredential withoutValues = credMan.Read("server01", CredType.Generic, false, CredEncoding.Unicode)!.Credential!;
            Assert.HasCount(2, withoutValues.Attributes);
            Assert.AreEqual("key1", withoutValues.Attributes[0].Keyword);
            Assert.AreEqual(3, withoutValues.Attributes[0].ValueLength);
            Assert.IsNull(withoutValues.Attributes[0].Value);

            WinCredential withValues = credMan.Read("server01", CredType.Generic, true, CredEncoding.Unicode)!.Credential!;
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, withValues.Attributes[0].Value!.ToArray());
            Assert.IsEmpty(withValues.Attributes[1].Value!);
        }

        // Listing

        [TestMethod]
        public void EnumeratePassesFilterAndScrubsEveryCredential()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("a.one"));
            api.Store.Add(Sample("A.two"));
            api.Store.Add(Sample("b.three"));

            var results = new CredMan(api).Enumerate("a.*", (name, type) => name.EndsWith("one", StringComparison.Ordinal), false, CredEncoding.Unicode);

            Assert.AreEqual("a.*", api.LastFilter);
            CollectionAssert.AreEqual(new[] { "a.one" }, results.Select(r => r.Credential!.TargetName).ToArray());
            Assert.AreEqual(1, api.FreeCount);
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree, "Secrets of credentials that did not match MUST be zeroed too.");
            Assert.AreEqual(0, api.OpenAllocations);
        }

        [TestMethod]
        public void EnumerateScrubsAndFreesWhenMatchThrows()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("a"));
            api.Store.Add(Sample("b"));

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                new CredMan(api).Enumerate(null, (name, type) => name == "b" ? throw new InvalidOperationException() : true, false, CredEncoding.Unicode));

            Assert.AreEqual(1, api.FreeCount);
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree);
            Assert.AreEqual(0, api.OpenAllocations);
        }

        [TestMethod]
        public void EnumerateEmptyStoreReturnsEmpty()
        {
            Assert.IsEmpty(new CredMan(new FakeCredentialApi()).Enumerate(null, (n, t) => true, true, CredEncoding.Unicode));
        }

        [TestMethod]
        public void EnumerateFailureThrowsWin32Exception()
        {
            var api = new FakeCredentialApi { Error = ErrorAccessDenied };
            Assert.ThrowsExactly<Win32Exception>(() => new CredMan(api).Enumerate(null, (n, t) => true, true, CredEncoding.Unicode));
        }

        // Creating

        [TestMethod]
        [DataRow(CredEncoding.Unicode)]
        [DataRow(CredEncoding.UTF8)]
        public void CreateWritesFieldsAndEncodedSecret(CredEncoding encoding)
        {
            var api = new FakeCredentialApi();
            new CredMan(api).Create("server01", CredType.Generic, "user", "note", CredPersist.Enterprise, "pä\U0001F600".ToSecureString(), encoding);

            FakeCredentialApi.Stored written = api.Find("server01", CredType.Generic)!;
            Assert.AreEqual("user", written.UserName);
            Assert.AreEqual("note", written.Comment);
            Assert.AreEqual(CredPersist.Enterprise, written.Persist);
            Assert.AreEqual(0u, api.LastWriteFlags);
            Encoding expected = encoding == CredEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
            CollectionAssert.AreEqual(expected.GetBytes("pä\U0001F600"), written.Blob);
        }

        [TestMethod]
        public void CreateStoresEmptyUserNameAndCommentAsNone()
        {
            var api = new FakeCredentialApi();
            new CredMan(api).Create("server01", CredType.Generic, "", null, CredPersist.Session, "x".ToSecureString(), CredEncoding.Unicode);

            Assert.IsNull(api.Find("server01", CredType.Generic)!.UserName);
            Assert.IsNull(api.Find("server01", CredType.Generic)!.Comment);
        }

        [TestMethod]
        public void CreateRefusesExistingCredential()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01", "old"));

            Assert.ThrowsExactly<CredentialExistsException>(() =>
                new CredMan(api).Create("SERVER01", CredType.Generic, null, null, CredPersist.Session, "new".ToSecureString(), CredEncoding.Unicode));

            Assert.AreEqual(0, api.WriteCount);
            CollectionAssert.AreEqual(Encoding.Unicode.GetBytes("old"), api.Find("server01", CredType.Generic)!.Blob);
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree);
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode, 1280, false)]
        [DataRow(CredEncoding.Unicode, 1281, true)]
        [DataRow(CredEncoding.UTF8, 2560, false)]
        [DataRow(CredEncoding.UTF8, 2561, true)]
        public void CreateEnforcesBlobLimit(CredEncoding encoding, int characters, bool tooLarge)
        {
            var api = new FakeCredentialApi();
            Action create = () => new CredMan(api).Create("t", CredType.Generic, null, null, CredPersist.Session, new string('a', characters).ToSecureString(), encoding);

            if (tooLarge)
            {
                Assert.ThrowsExactly<SecretTooLargeException>(create);
                Assert.AreEqual(0, api.WriteCount, "An oversized secret MUST NOT reach CredWrite.");
                Assert.AreEqual(0, api.ReadCount, "An oversized secret MUST fail before the store is touched.");
            }
            else
            {
                create();
                Assert.HasCount(CRED_MAX_CREDENTIAL_BLOB_SIZE, api.Find("t", CredType.Generic)!.Blob);
            }
        }

        [TestMethod]
        public void CreateRefusesInvalidUnicodeInsteadOfReplacingIt()
        {
            var api = new FakeCredentialApi();
            foreach (CredEncoding encoding in new[] { CredEncoding.Unicode, CredEncoding.UTF8 })
            {
                Assert.ThrowsExactly<SecretNotEncodableException>(() =>
                    new CredMan(api).Create("t", CredType.Generic, null, null, CredPersist.Session, "a\uD800b".ToSecureString(), encoding));
            }
            Assert.AreEqual(0, api.WriteCount);
            Assert.AreEqual(0, api.ReadCount);
        }

        [TestMethod]
        public void UpdateRejectsUnusableSecretBeforeReadingTheStore()
        {
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01", "old"));

            Assert.ThrowsExactly<SecretTooLargeException>(() => new CredMan(api).Update("server01", CredType.Generic,
                new CredentialUpdate { Secret = new string('a', 1281).ToSecureString() }));
            Assert.ThrowsExactly<SecretNotEncodableException>(() => new CredMan(api).Update("missing", CredType.Generic,
                new CredentialUpdate { Secret = "a\uD800b".ToSecureString() }));

            Assert.AreEqual(0, api.ReadCount);
            Assert.AreEqual(0, api.WriteCount);
        }

        [TestMethod]
        public void ReadZeroesAttributeValuesBeforeFreeEvenWithoutIncludeSecret()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01", "secret");
            stored.Attributes.Add(("token", 0u, new byte[] { 1, 2, 3 }));
            stored.Attributes.Add(("other", 0u, new byte[] { 4 }));
            api.Store.Add(stored);

            new CredMan(api).Read("server01", CredType.Generic, includeSecret: false, CredEncoding.Unicode);
            new CredMan(api).Enumerate(null, (name, type) => false, includeSecret: false, CredEncoding.Unicode);

            Assert.AreEqual(2, api.FreeCount);
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree, "Attribute values MUST be zeroed before CredFree.");
        }

        [TestMethod]
        public void RejectsNulInNamesBeforeCallingWindows()
        {
            // Windows would read 'server01\0x' as 'server01' and act on that credential
            var api = new FakeCredentialApi();
            api.Store.Add(Sample("server01", "secret"));
            var credMan = new CredMan(api);
            const string name = "server01\0x";

            Assert.ThrowsExactly<ArgumentException>(() => credMan.Read(name, CredType.Generic, includeSecret: true, CredEncoding.Unicode));
            Assert.ThrowsExactly<ArgumentException>(() => credMan.Enumerate(name + "*", (n, t) => true, includeSecret: true, CredEncoding.Unicode));
            Assert.ThrowsExactly<ArgumentException>(() => credMan.Create(name, CredType.Generic, null, null, CredPersist.Session, "new".ToSecureString(), CredEncoding.Unicode));
            Assert.ThrowsExactly<ArgumentException>(() => credMan.Update(name, CredType.Generic, new CredentialUpdate { SetComment = true, Comment = "c" }));
            Assert.ThrowsExactly<ArgumentException>(() => credMan.Delete(name, CredType.Generic));

            Assert.AreEqual(0, api.ReadCount);
            Assert.AreEqual(0, api.WriteCount);
            Assert.IsNull(api.LastFilter);
            Assert.HasCount(1, api.Store);
        }

        // Updating

        [TestMethod]
        public void UpdateWithoutSecretPreservesItAndEveryUnsetField()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01", "keep");
            stored.Comment = "old comment";
            stored.TargetAlias = "alias";
            stored.Persist = CredPersist.LocalMachine;
            stored.Flags = (uint)CredFlags.VsmProtected;
            stored.Attributes.Add(("key", 0u, new byte[] { 9 }));
            api.Store.Add(stored);

            new CredMan(api).Update("server01", CredType.Generic, new CredentialUpdate { SetComment = true, Comment = "new comment" });

            FakeCredentialApi.Stored written = api.Find("server01", CredType.Generic)!;
            Assert.AreEqual(CRED_PRESERVE_CREDENTIAL_BLOB, api.LastWriteFlags);
            CollectionAssert.AreEqual(Encoding.Unicode.GetBytes("keep"), written.Blob);
            Assert.AreEqual("new comment", written.Comment);
            Assert.AreEqual("user", written.UserName);
            Assert.AreEqual("alias", written.TargetAlias);
            Assert.AreEqual(CredPersist.LocalMachine, written.Persist, "Persistence MUST NOT change unless requested.");
            Assert.AreEqual((uint)CredFlags.VsmProtected, written.Flags);
            Assert.AreEqual("key", written.Attributes.Single().Keyword);
            CollectionAssert.AreEqual(new byte[] { 9 }, written.Attributes.Single().Value);
            Assert.IsTrue(api.AllSecretsZeroedBeforeFree);
        }

        [TestMethod]
        public void UpdateWithSecretReplacesItAndKeepsPersistence()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01", "old");
            stored.Persist = CredPersist.Enterprise;
            api.Store.Add(stored);

            new CredMan(api).Update("server01", CredType.Generic, new CredentialUpdate { Secret = "new".ToSecureString() });

            FakeCredentialApi.Stored written = api.Find("server01", CredType.Generic)!;
            Assert.AreEqual(0u, api.LastWriteFlags);
            CollectionAssert.AreEqual(Encoding.Unicode.GetBytes("new"), written.Blob);
            Assert.AreEqual(CredPersist.Enterprise, written.Persist);
        }

        [TestMethod]
        public void UpdateClearsFieldsSetToEmpty()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01");
            stored.Comment = "note";
            api.Store.Add(stored);

            new CredMan(api).Update("server01", CredType.Generic,
                new CredentialUpdate { SetUserName = true, UserName = "", SetComment = true, Comment = "", Persist = CredPersist.Session });

            FakeCredentialApi.Stored written = api.Find("server01", CredType.Generic)!;
            Assert.IsNull(written.UserName);
            Assert.IsNull(written.Comment);
            Assert.AreEqual(CredPersist.Session, written.Persist);
        }

        [TestMethod]
        public void UpdateWritesBackOnlyFlagsWindowsAccepts()
        {
            var api = new FakeCredentialApi();
            FakeCredentialApi.Stored stored = Sample("server01");
            stored.Flags = CRED_FLAGS_VSM_PROTECTED | CRED_FLAGS_WILDCARD_MATCH | CRED_FLAGS_NGC_CERT;
            api.Store.Add(stored);
            uint written = 0;
            var spy = new SpyApi(api, flags => written = flags);

            new CredMan(spy).Update("server01", CredType.Generic, new CredentialUpdate { SetComment = true, Comment = "x" });

            Assert.AreEqual(CRED_FLAGS_VSM_PROTECTED, written);
        }

        [TestMethod]
        public void UpdateOfMissingCredentialCreatesNothing()
        {
            var api = new FakeCredentialApi();
            Assert.ThrowsExactly<CredentialNotFoundException>(() =>
                new CredMan(api).Update("missing", CredType.Generic, new CredentialUpdate { Secret = "x".ToSecureString() }));
            Assert.AreEqual(0, api.WriteCount);
            Assert.IsEmpty(api.Store);
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
        public void FileTimeWithHighBitInLowPartConverts()
        {
            var fileTime = new NativeMethods.FILETIME { dwHighDateTime = 0x01D00000, dwLowDateTime = 0x80000000 };
            Assert.AreEqual(DateTimeOffset.FromFileTime(0x01D0000080000000), CredMan.ToDateTimeOffset(fileTime));
        }

        /// <summary>Records the CREDENTIALW flags passed to CredWrite, then forwards to the fake.</summary>
        private sealed class SpyApi : ICredentialApi
        {
            private readonly FakeCredentialApi inner;
            private readonly Action<uint> onWrite;

            internal SpyApi(FakeCredentialApi inner, Action<uint> onWrite)
            {
                this.inner = inner;
                this.onWrite = onWrite;
            }

            public int Read(string targetName, CredType type, out SingleCredentialBuffer credential) => inner.Read(targetName, type, out credential);
            public int Enumerate(string? filter, out CredentialArrayBuffer credentials) => inner.Enumerate(filter, out credentials);
            public int Delete(string targetName, CredType type) => inner.Delete(targetName, type);

            public int Write(ref CREDENTIALW credential, uint flags)
            {
                onWrite(credential.Flags);
                return inner.Write(ref credential, flags);
            }
        }
    }
}
