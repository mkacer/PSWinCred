using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WinCred.Native.Tests
{
    [TestClass]
    public class BlobEncodingTests
    {
        // ASCII, two-byte UTF-8 characters, and a surrogate pair
        private const string Secret = "päéss\U0001F600";

        private static byte[] Read(IntPtr blob, uint size)
        {
            byte[] bytes = new byte[size];
            Marshal.Copy(blob, bytes, 0, bytes.Length);
            return bytes;
        }

        private static void Free(IntPtr blob, uint size)
        {
            BlobEncoding.ZeroMemory(blob, (int)size);
            Marshal.FreeCoTaskMem(blob);
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode)]
        [DataRow(CredEncoding.UTF8)]
        public void RoundTrips(CredEncoding encoding)
        {
            IntPtr blob = BlobEncoding.Encode(Secret.ToSecureString(), encoding, out uint size);
            try
            {
                SecureString decoded = BlobEncoding.DecodeToSecureString(blob, size, encoding);
                Assert.AreEqual(Secret, decoded.ToPlainText());
            }
            finally
            {
                Free(blob, size);
            }
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode, 14u)]
        [DataRow(CredEncoding.UTF8, 11u)]
        public void SizeIsEncodedByteCount(CredEncoding encoding, uint expected)
        {
            // 7 UTF-16 code units = 14 bytes; UTF-8: p(1) ä(2) é(2) s(1) s(1) 😀(4) = 11 bytes
            IntPtr blob = BlobEncoding.Encode(Secret.ToSecureString(), encoding, out uint size);
            try
            {
                Assert.AreEqual(expected, size);
            }
            finally
            {
                Free(blob, size);
            }
        }

        [TestMethod]
        public void Utf8BlobIsPlainUtf8WithoutBom()
        {
            IntPtr blob = BlobEncoding.Encode("abc".ToSecureString(), CredEncoding.UTF8, out uint size);
            try
            {
                CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63 }, Read(blob, size));
            }
            finally
            {
                Free(blob, size);
            }
        }

        [TestMethod]
        [DataRow(CredEncoding.Unicode)]
        [DataRow(CredEncoding.UTF8)]
        public void EmptySecretRoundTrips(CredEncoding encoding)
        {
            IntPtr blob = BlobEncoding.Encode(new SecureString(), encoding, out uint size);
            try
            {
                Assert.AreEqual(0u, size);
                Assert.AreEqual(0, BlobEncoding.DecodeToSecureString(blob, size, encoding).Length);
            }
            finally
            {
                Free(blob, size);
            }
        }

        [TestMethod]
        public void OddLengthBlobDecodesAsUnicodeWithoutThrowing()
        {
            byte[] utf8 = Encoding.UTF8.GetBytes("abc");
            IntPtr blob = Marshal.AllocCoTaskMem(utf8.Length);
            try
            {
                Marshal.Copy(utf8, 0, blob, utf8.Length);
                SecureString decoded = BlobEncoding.DecodeToSecureString(blob, (uint)utf8.Length, CredEncoding.Unicode);
                Assert.AreEqual(2, decoded.Length, "One UTF-16 unit plus one replacement character.");
            }
            finally
            {
                Marshal.FreeCoTaskMem(blob);
            }
        }

        [TestMethod]
        public void ZeroMemoryClearsPastEmbeddedNull()
        {
            IntPtr blob = Marshal.StringToCoTaskMemUni("se\0cret");
            try
            {
                BlobEncoding.ZeroMemory(blob, 14);
                CollectionAssert.AreEqual(new byte[14], Read(blob, 14));
            }
            finally
            {
                Marshal.FreeCoTaskMem(blob);
            }
        }
    }
}
