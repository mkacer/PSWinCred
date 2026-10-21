using PSCredentialManager.Common;
using PSCredentialManager.Common.Enum;
using System;
using System.Runtime.InteropServices;

namespace PSCredentialManager.Api
{
    public class Imports
    {
        public static bool CredRead(string target, CredType type, int reservedFlag, out IntPtr credentialPointer)
        {
            return NativeMethods.CredRead(target, type, reservedFlag, out credentialPointer);
        }

        public static bool CredWrite(ref NativeCredential userCredential, UInt32 flags)
        {
            return NativeMethods.CredWrite(ref userCredential, flags);
        }

        public static void CredFree(IntPtr credentialPointer)
        {
            NativeMethods.CredFree(credentialPointer);
        }

        public static bool CredDelete(string target, CredType type, int reservedFlag)
        {
            return NativeMethods.CredDelete(target, type, reservedFlag);
        }

        public static bool CredEnumerate(string filter, int flags, out int count, out IntPtr credentialPointers)
        {
            return NativeMethods.CredEnumerate(filter, flags, out count, out credentialPointers);
        }

        // DefaultDllImportSearchPaths loads advapi32.dll only from System32,
        // never from the current or application directory.
        private static class NativeMethods
        {
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CredRead([In] string target, [In] CredType type, [In] int reservedFlag, out IntPtr credentialPtr);

            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CredWrite([In] ref NativeCredential userCredential, [In] UInt32 flags);

            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [DllImport("Advapi32.dll", EntryPoint = "CredFree")]
            public static extern void CredFree([In] IntPtr credentialPointer);

            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CredDelete([In] string target, [In] CredType type, [In] int reservedFlag);

            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [DllImport("Advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CredEnumerate([In] string filter, [In] int flags, out int count, out IntPtr credentialPtrs);
        }
    }
}
