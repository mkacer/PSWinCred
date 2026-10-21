using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static WinCred.Native.NativeMethods;

namespace WinCred.Native.Tests
{
    /// <summary>
    /// Stands in for advapi32, following the behavior measured on Windows with tools/Measure-CredMan.ps1:
    /// exact, case-insensitive reads; a filter that is an exact name or a prefix with a trailing "*";
    /// CRED_PRESERVE_CREDENTIAL_BLOB keeping the secret and failing with ERROR_NOT_FOUND for a missing credential.
    /// Buffers it returns record whether every secret was zeroed before they were freed.
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
            public byte[] Blob = Array.Empty<byte>();
            public long LastWritten;
            public uint Flags;
            public List<(string Keyword, uint Flags, byte[] Value)> Attributes = new List<(string, uint, byte[])>();
        }

        private readonly List<IntPtr> allocations = new List<IntPtr>();
        private readonly List<(IntPtr Blob, int Size)> secrets = new List<(IntPtr, int)>();

        public List<Stored> Store { get; } = new List<Stored>();
        public int Error { get; set; }
        public int ReadError { get; set; }
        public int FreeCount { get; private set; }
        public bool AllSecretsZeroedBeforeFree { get; private set; } = true;
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public uint LastWriteFlags { get; private set; }
        public string? LastFilter { get; private set; }
        public int OpenAllocations => allocations.Count;

        public Stored? Find(string targetName, CredType type) =>
            Store.FirstOrDefault(s => s.Type == type && string.Equals(s.TargetName, targetName, StringComparison.OrdinalIgnoreCase));

        public int Read(string targetName, CredType type, out SingleCredentialBuffer credential)
        {
            ReadCount++;
            credential = new FakeSingle(this, IntPtr.Zero);
            if (ReadError != 0) return ReadError;

            Stored? stored = Find(targetName, type);
            if (stored == null) return ERROR_NOT_FOUND;

            credential = new FakeSingle(this, ToBuffer(stored));
            return 0;
        }

        public int Enumerate(string? filter, out CredentialArrayBuffer credentials)
        {
            LastFilter = filter;
            credentials = new FakeArray(this, IntPtr.Zero, 0);
            if (Error != 0) return Error;

            List<Stored> matching = Store.Where(s => Passes(filter, s.TargetName)).ToList();
            if (matching.Count == 0) return ERROR_NOT_FOUND;

            IntPtr array = Alloc(IntPtr.Size * matching.Count);
            for (int i = 0; i < matching.Count; i++)
            {
                Marshal.WriteIntPtr(array, i * IntPtr.Size, ToBuffer(matching[i]));
            }
            credentials = new FakeArray(this, array, matching.Count);
            return 0;
        }

        private static bool Passes(string? filter, string name)
        {
            if (filter == null) return true;
            // String.EndsWith(char) does not exist on .NET Framework 4.6.2
            return filter.Length > 0 && filter[filter.Length - 1] == '*'
                ? name.StartsWith(filter.Substring(0, filter.Length - 1), StringComparison.OrdinalIgnoreCase)
                : string.Equals(name, filter, StringComparison.OrdinalIgnoreCase);
        }

        public int Write(ref CREDENTIALW credential, uint flags)
        {
            WriteCount++;
            LastWriteFlags = flags;
            if (Error != 0) return Error;

            string target = Marshal.PtrToStringUni(credential.TargetName) ?? "";
            Stored? existing = Find(target, (CredType)credential.Type);
            bool preserve = (flags & CRED_PRESERVE_CREDENTIAL_BLOB) != 0;
            if (preserve && credential.CredentialBlob != IntPtr.Zero) return ERROR_INVALID_PARAMETER;
            if (preserve && existing == null) return ERROR_NOT_FOUND;

            byte[] blob = new byte[credential.CredentialBlobSize];
            if (blob.Length > 0) Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);

            var attributes = new List<(string, uint, byte[])>();
            int attributeSize = Marshal.SizeOf<CREDENTIAL_ATTRIBUTEW>();
            for (int i = 0; i < credential.AttributeCount; i++)
            {
                var a = Marshal.PtrToStructure<CREDENTIAL_ATTRIBUTEW>(credential.Attributes + i * attributeSize);
                byte[] value = new byte[a.ValueSize];
                if (value.Length > 0) Marshal.Copy(a.Value, value, 0, value.Length);
                attributes.Add((Marshal.PtrToStringUni(a.Keyword) ?? "", a.Flags, value));
            }

            var written = new Stored
            {
                TargetName = target,
                Type = (CredType)credential.Type,
                UserName = Marshal.PtrToStringUni(credential.UserName),
                Comment = Marshal.PtrToStringUni(credential.Comment),
                TargetAlias = Marshal.PtrToStringUni(credential.TargetAlias),
                Persist = (CredPersist)credential.Persist,
                Blob = preserve ? existing!.Blob : blob,
                // Measured: of the accepted flags, only these two are stored
                Flags = credential.Flags & (CRED_FLAGS_REQUIRE_CONFIRMATION | CRED_FLAGS_VSM_PROTECTED),
                Attributes = attributes,
            };
            if (existing != null) Store.Remove(existing);
            Store.Add(written);
            return 0;
        }

        public int Delete(string targetName, CredType type)
        {
            if (Error != 0) return Error;
            Stored? stored = Find(targetName, type);
            if (stored == null) return ERROR_NOT_FOUND;
            Store.Remove(stored);
            return 0;
        }

        private void FreeAll()
        {
            FreeCount++;
            foreach (var (blob, size) in secrets)
            {
                byte[] bytes = new byte[size];
                Marshal.Copy(blob, bytes, 0, size);
                if (bytes.Any(b => b != 0)) AllSecretsZeroedBeforeFree = false;
            }
            secrets.Clear();
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
                    IntPtr value = Bytes(a.Value);
                    // Attribute values can hold secrets too, so they MUST be zeroed before CredFree as well
                    secrets.Add((value, a.Value.Length));
                    Marshal.StructureToPtr(new CREDENTIAL_ATTRIBUTEW
                    {
                        Keyword = String(a.Keyword),
                        Flags = a.Flags,
                        ValueSize = (uint)a.Value.Length,
                        Value = value,
                    }, attributes + i * attributeSize, false);
                }
            }

            IntPtr blob = Bytes(stored.Blob);
            secrets.Add((blob, stored.Blob.Length));

            var native = new CREDENTIALW
            {
                Flags = stored.Flags,
                Type = (uint)stored.Type,
                TargetName = String(stored.TargetName),
                Comment = String(stored.Comment),
                LastWritten = new NativeMethods.FILETIME { dwLowDateTime = (uint)stored.LastWritten, dwHighDateTime = (uint)(stored.LastWritten >> 32) },
                CredentialBlobSize = (uint)stored.Blob.Length,
                CredentialBlob = blob,
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

        private sealed class FakeSingle : SingleCredentialBuffer
        {
            private readonly FakeCredentialApi api;

            internal FakeSingle(FakeCredentialApi api, IntPtr buffer) : base(buffer)
            {
                this.api = api;
            }

            protected override void Free(IntPtr buffer) => api.FreeAll();
        }

        private sealed class FakeArray : CredentialArrayBuffer
        {
            private readonly FakeCredentialApi api;

            internal FakeArray(FakeCredentialApi api, IntPtr buffer, int count) : base(buffer, count)
            {
                this.api = api;
            }

            protected override void Free(IntPtr buffer) => api.FreeAll();
        }
    }
}
