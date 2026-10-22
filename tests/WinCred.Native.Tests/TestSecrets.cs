using System;
using System.Runtime.InteropServices;
using System.Security;

namespace WinCred.Native.Tests
{
    internal static class TestSecrets
    {
        internal static SecureString ToSecureString(this string text)
        {
            var secure = new SecureString();
            foreach (char c in text)
            {
                secure.AppendChar(c);
            }
            return secure;
        }

        internal static string ToPlainText(this SecureString secure)
        {
            IntPtr chars = Marshal.SecureStringToGlobalAllocUnicode(secure);
            try
            {
                return Marshal.PtrToStringUni(chars, secure.Length);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(chars);
            }
        }
    }
}
