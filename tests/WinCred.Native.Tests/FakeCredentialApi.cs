using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static WinCred.Native.NativeMethods;

namespace WinCred.Native.Tests
{
    /// <summary>
    /// Stands in for advapi32. Read and Enumerate return buffers laid out like the real ones;
    /// the handle frees them all, like CredFree, and counts how often that happens.
    /// </summary>
    internal sealed class FakeCredentialApi : ICredentialApi
    {
        internal sealed class Stored
        {
            public string TargetName = "";
            public CredType Type = CredType.Generic;
            public string? UserName;
            public string? Comment;
            public string? TargetAlias;
            public CredPersist Persist = CredPersist.Session;
            public byte[] Blob = new byte[0];
            public long LastWritten;
            public uint Flags;
            public List<(string Keyword, uint Flags, byte[] Value)> Attributes = new List<(string, uint, byte[])>();
        }

        private readonly List<IntPtr> allocations = new List<IntPtr>();

        public List<Stored> Store { get; } = new List<Stored>();
        public int Error { get; set; }
        public int FreeCount { get; private set; }
        public int WriteCount { get; private set; }
        public Stored? LastWrite { get; private set; }
        public int OpenAllocations => allocations.Count;

        public int Read(string targetName, CredType type, out CredentialHandle credential)
        {
            credential = new FakeHandle(this, IntPtr.Zero);
            if (Error != 0) return Error;

            Stored? stored = Store.FirstOrDefault(s => s.Type == type && string.Equals(s.TargetName, targetName, StringComparison.OrdinalIgnoreCase));
            if (stored == null) return ERROR_NOT_FOUND;

            credential = new FakeHandle(this, ToBuffer(stored));
            return 0;
        }

        public int Enumerate(out int count, out CredentialHandle credentials)
        {
            count = 0;
            credentials = new FakeHandle(this, IntPtr.Zero);
            if (Error != 0) return Error;
            if (Store.Count == 0) return ERROR_NOT_FOUND;

            IntPtr array = Alloc(IntPtr.Size * Store.Count);
            for (int i = 0; i < Store.Count; i++)
            {
                Marshal.WriteIntPtr(array, i * IntPtr.Size, ToBuffer(Store[i]));
            }
            count = Store.Count;
            credentials = new FakeHandle(this, array);
            return 0;
        }

        public int Write(ref CREDENTIALW credential)
        {
            WriteCount++;
            byte[] blob = new byte[credential.CredentialBlobSize];
            if (blob.Length > 0) Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            LastWrite = new Stored
            {
                TargetName = Marshal.PtrToStringUni(credential.TargetName) ?? "",
                Type = (CredType)credential.Type,
                UserName = Marshal.PtrToStringUni(credential.UserName),
                Comment = Marshal.PtrToStringUni(credential.Comment),
                Persist = (CredPersist)credential.Persist,
                Blob = blob,
            };
            return Error;
        }

        public int Delete(string targetName, CredType type)
        {
            if (Error != 0) return Error;
            return Store.RemoveAll(s => s.Type == type && string.Equals(s.TargetName, targetName, StringComparison.OrdinalIgnoreCase)) > 0 ? 0 : ERROR_NOT_FOUND;
        }

        private void FreeAll()
        {
            FreeCount++;
            allocations.ForEach(Marshal.FreeHGlobal);
            allocations.Clear();
        }

        private IntPtr Alloc(int size)
        {
            IntPtr p = Marshal.AllocHGlobal(Math.Max(size, 1));
            allocations.Add(p);
            return p;
        }

        private IntPtr String(string? s)
        {
            if (s == null) return IntPtr.Zero;
            IntPtr p = Marshal.StringToHGlobalUni(s);
            allocations.Add(p);
            return p;
        }

        private IntPtr Bytes(byte[] bytes)
        {
            IntPtr p = Alloc(bytes.Length);
            Marshal.Copy(bytes, 0, p, bytes.Length);
            return p;
        }

        private IntPtr ToBuffer(Stored stored)
        {
            IntPtr attributes = IntPtr.Zero;
            int attributeSize = Marshal.SizeOf<CREDENTIAL_ATTRIBUTEW>();
            if (stored.Attributes.Count > 0)
            {
                attributes = Alloc(attributeSize * stored.Attributes.Count);
                for (int i = 0; i < stored.Attributes.Count; i++)
                {
                    var a = stored.Attributes[i];
                    Marshal.StructureToPtr(new CREDENTIAL_ATTRIBUTEW
                    {
                        Keyword = String(a.Keyword),
                        Flags = a.Flags,
                        ValueSize = (uint)a.Value.Length,
                        Value = Bytes(a.Value),
                    }, attributes + i * attributeSize, false);
                }
            }

            var native = new CREDENTIALW
            {
                Flags = stored.Flags,
                Type = (uint)stored.Type,
                TargetName = String(stored.TargetName),
                Comment = String(stored.Comment),
                LastWritten = new NativeMethods.FILETIME { dwLowDateTime = (uint)stored.LastWritten, dwHighDateTime = (uint)(stored.LastWritten >> 32) },
                CredentialBlobSize = (uint)stored.Blob.Length,
                CredentialBlob = Bytes(stored.Blob),
                Persist = (uint)stored.Persist,
                AttributeCount = (uint)stored.Attributes.Count,
                Attributes = attributes,
                TargetAlias = String(stored.TargetAlias),
                UserName = String(stored.UserName),
            };
            IntPtr buffer = Alloc(Marshal.SizeOf<CREDENTIALW>());
            Marshal.StructureToPtr(native, buffer, false);
            return buffer;
        }

        private sealed class FakeHandle : CredentialHandle
        {
            private readonly FakeCredentialApi api;

            internal FakeHandle(FakeCredentialApi api, IntPtr buffer) : base(buffer)
            {
                this.api = api;
            }

            protected override bool ReleaseHandle()
            {
                api.FreeAll();
                return true;
            }
        }
    }
}
