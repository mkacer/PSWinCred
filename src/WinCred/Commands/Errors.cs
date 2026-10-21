using System;
using System.ComponentModel;
using System.Management.Automation;

namespace WinCred.Commands
{
    /// <summary>
    /// Error records shared by the cmdlets. Messages name targets but never secrets.
    /// </summary>
    internal static class Errors
    {
        internal static ErrorRecord NotFound(string target, CredType? type)
        {
            string what = type.HasValue ? $"{type.Value} credential" : "credential";
            return new ErrorRecord(
                new ItemNotFoundException($"No {what} with the target '{target}' was found."),
                "CredentialNotFound", ErrorCategory.ObjectNotFound, target);
        }

        internal static ErrorRecord Native(Win32Exception exception, string errorId, ErrorCategory category, object? target)
        {
            return new ErrorRecord(exception, errorId, category, target);
        }

        internal static ErrorRecord InvalidArgument(string parameterName, string message, string errorId, object? target)
        {
            return new ErrorRecord(new ArgumentException(message, parameterName), errorId, ErrorCategory.InvalidArgument, target);
        }
    }
}
