using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSCredentialManager.Api;
using PSCredentialManager.Api.Extensions;
using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using PSCredentialManager.Common.Exceptions;

namespace PSCredentialManager.Tests
{
    [TestClass]
    public class CredentialManagerTests
    {
        private const int ErrorNotFound = 1168;
        private const int ErrorAccessDenied = 5;

        [TestMethod]
        public void WriteCredTest()
        {
            FakeCredentialApi api = new FakeCredentialApi();
            new CredentialManager(api).WriteCred(new NativeCredential());
            Assert.AreEqual(1, api.WriteCount);
        }

        [TestMethod]
        public void WriteCredFailureThrows()
        {
            FakeCredentialApi api = new FakeCredentialApi { Error = ErrorAccessDenied };
            Exception ex = Assert.ThrowsExactly<Exception>(() => new CredentialManager(api).WriteCred(new NativeCredential()));
            StringAssert.Contains(ex.Message, "5");
        }

        [TestMethod]
        public void ReadCredTest()
        {
            FakeCredentialApi api = new FakeCredentialApi();
            api.Add(Sample("server01", "test-user", "Password1"));

            Credential credential = new CredentialManager(api).ReadCred("server01", CredType.Generic, true, false);

            Assert.AreEqual("server01", credential.TargetName);
            Assert.AreEqual("test-user", credential.UserName);
            Assert.AreEqual("Password1", credential.Password);
            Assert.AreEqual(1, api.FreeCount, "The native buffer must be freed after reading.");
        }

        [TestMethod]
        public void ReadCredNotFoundThrows()
        {
            FakeCredentialApi api = new FakeCredentialApi { Error = ErrorNotFound };
            Assert.ThrowsExactly<CredentialNotFoundException>(() => new CredentialManager(api).ReadCred("server01", CredType.Generic, true, true));
        }

        [TestMethod]
        public void ReadCredFailureThrows()
        {
            FakeCredentialApi api = new FakeCredentialApi { Error = ErrorAccessDenied };
            Assert.ThrowsExactly<Exception>(() => new CredentialManager(api).ReadCred("server01", CredType.Generic, true, true));
        }

        [TestMethod]
        public void DeleteCredTest()
        {
            FakeCredentialApi api = new FakeCredentialApi();
            new CredentialManager(api).DeleteCred("server01", CredType.Generic);
            Assert.AreEqual(1, api.DeleteCount);
        }

        [TestMethod]
        public void DeleteCredNotFoundThrows()
        {
            FakeCredentialApi api = new FakeCredentialApi { Error = ErrorNotFound };
            Assert.ThrowsExactly<CredentialNotFoundException>(() => new CredentialManager(api).DeleteCred("server01", CredType.Generic));
        }

        [TestMethod]
        public void ReadAllCredsTest()
        {
            FakeCredentialApi api = new FakeCredentialApi();
            api.Add(Sample("server01", "user1", "Password1"));
            api.Add(Sample("server02", "user2", "Password2"));

            Credential[] credentials = new CredentialManager(api).ReadCred(false, true).ToArray();

            CollectionAssert.AreEqual(new[] { "server01", "server02" }, credentials.Select(c => c.TargetName).ToArray());
            Assert.IsNull(credentials[0].Password);
            Assert.IsNotNull(credentials[0].SecurePassword);
            Assert.AreEqual(1, api.FreeCount, "The native buffer must be freed after enumerating.");
        }

        [TestMethod]
        public void ReadAllCredsEmptyStoreReturnsEmpty()
        {
            FakeCredentialApi api = new FakeCredentialApi { Error = ErrorNotFound };
            Assert.AreEqual(0, new CredentialManager(api).ReadCred(true, true).Count());
        }

        [TestMethod]
        public void ReadAllCredsFailureThrows()
        {
            FakeCredentialApi api = new FakeCredentialApi { Error = ErrorAccessDenied };
            Assert.ThrowsExactly<Exception>(() => new CredentialManager(api).ReadCred(true, true));
        }

        private static Credential Sample(string target, string userName, string password)
        {
            return new Credential
            {
                TargetName = target,
                UserName = userName,
                Password = password,
                Type = CredType.Generic,
                Persist = CredPersist.Session,
                LastWritten = DateTime.Now,
            };
        }

        /// <summary>
        /// Stands in for advapi32. Read and enumerate return buffers laid out like the real ones.
        /// </summary>
        private sealed class FakeCredentialApi : ICredentialApi
        {
            private readonly List<Credential> store = new List<Credential>();
            private readonly List<NativeCredential> allocated = new List<NativeCredential>();
            private readonly List<IntPtr> buffers = new List<IntPtr>();

            public int Error { get; set; }
            public int WriteCount { get; private set; }
            public int DeleteCount { get; private set; }
            public int FreeCount { get; private set; }

            public void Add(Credential credential) => store.Add(credential);

            public int CredWrite(ref NativeCredential credential)
            {
                WriteCount++;
                return Error;
            }

            public int CredDelete(string target, CredType type)
            {
                DeleteCount++;
                return Error;
            }

            public int CredRead(string target, CredType type, out IntPtr credentialPointer)
            {
                credentialPointer = IntPtr.Zero;
                if (Error != 0) return Error;

                credentialPointer = ToBuffer(store.Single(c => c.TargetName == target));
                return 0;
            }

            public int CredEnumerate(string filter, int flags, out int count, out IntPtr credentialPointers)
            {
                count = 0;
                credentialPointers = IntPtr.Zero;
                if (Error != 0) return Error;

                count = store.Count;
                credentialPointers = Marshal.AllocHGlobal(IntPtr.Size * count);
                buffers.Add(credentialPointers);
                for (int i = 0; i < count; i++)
                {
                    Marshal.WriteIntPtr(credentialPointers, i * IntPtr.Size, ToBuffer(store[i]));
                }
                return 0;
            }

            public void CredFree(IntPtr buffer)
            {
                FreeCount++;
                // The real CredFree releases the whole result in one call, so release every part here
                buffers.ForEach(Marshal.FreeHGlobal);
                buffers.Clear();
                allocated.ForEach(n => n.Release());
                allocated.Clear();
            }

            private IntPtr ToBuffer(Credential credential)
            {
                NativeCredential native = credential.ToNativeCredential();
                allocated.Add(native);
                IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeCredential)));
                Marshal.StructureToPtr(native, buffer, false);
                buffers.Add(buffer);
                return buffer;
            }
        }
    }
}
