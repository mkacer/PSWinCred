using System;

namespace PSCredentialManager.Api.Extensions
{
    public static class FileTimeExtension
    {
        public static DateTime ToDateTime(this System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
        {
            long highFileTime = (((long)fileTime.dwHighDateTime) << 32) + (uint)fileTime.dwLowDateTime;

            return DateTime.FromFileTime(highFileTime);
        }
    }
}
