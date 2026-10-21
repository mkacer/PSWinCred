using System;
using System.ComponentModel;
using System.Management.Automation;
using System.Text;
using static WinCred.Native.NativeMethods;

namespace WinCred.Commands
{
    /// <summary>
    /// Error records and input checks shared by the cmdlets. Messages name targets but never secrets.
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

        /// <summary>A failed CredMan call, with a hint for the causes measured or documented for it.</summary>
        internal static ErrorRecord Native(Win32Exception exception, string errorId, ErrorCategory category, object? target)
        {
            if (exception.NativeErrorCode == CredRequiresConfirmation)
            {
                var confirmation = new ErrorRecord(exception, "RequiresConfirmation", ErrorCategory.PermissionDenied, target);
                confirmation.ErrorDetails = new ErrorDetails(
                    $"'{target}' requires confirmation (CRED_FLAGS_REQUIRE_CONFIRMATION), so Windows does not return it to programs.");
                return confirmation;
            }

            var record = new ErrorRecord(exception, errorId, category, target);
            if (exception.NativeErrorCode == ERROR_NO_SUCH_LOGON_SESSION)
            {
                record.ErrorDetails = new ErrorDetails(
                    $"{exception.Message} This logon session has no credential store, which happens in network logons such as PowerShell remoting and in scheduled tasks that do not load the user profile.");
            }
            else if (exception.NativeErrorCode == ERROR_INVALID_PARAMETER && errorId == "ReadFailed")
            {
                record.ErrorDetails = new ErrorDetails(
                    $"Windows rejected the target name '{target}' for the requested credential type: {exception.Message}");
            }
            else if (exception.NativeErrorCode == ERROR_INVALID_PARAMETER && errorId == "WriteFailed")
            {
                record.ErrorDetails = new ErrorDetails(
                    $"Windows rejected the credential '{target}': {exception.Message} Domain targets MUST be host names, such as server.example.com or *.example.com, and domain user names MUST be DOMAIN\\user or user@domain.");
            }
            return record;
        }

        internal static ErrorRecord InvalidArgument(string parameterName, string message, string errorId, object? target)
        {
            return new ErrorRecord(new ArgumentException(message, parameterName), errorId, ErrorCategory.InvalidArgument, target);
        }

        /// <summary>Checks a target for New-WinCredential. Returns null when it is valid.</summary>
        internal static ErrorRecord? CheckTarget(string target, CredType type)
        {
            ErrorRecord? control = CheckCharacters("Target", target, "target", target);
            if (control != null)
            {
                return control;
            }

            if (type == CredType.Generic)
            {
                return target.Length > MaxGenericTargetLength
                    ? InvalidArgument("Target", $"The target is {target.Length} characters; Generic credentials allow at most {MaxGenericTargetLength}.", "TargetTooLong", target)
                    : null;
            }

            // Domain targets are host names, measured to be limited in UTF-8 bytes
            int bytes = Encoding.UTF8.GetByteCount(target);
            if (bytes > MaxDomainTargetBytes)
            {
                return InvalidArgument("Target", $"The target is {bytes} UTF-8 bytes; {type} targets are host names of at most {MaxDomainTargetBytes}.", "TargetTooLong", target);
            }
            foreach (string label in target.Split('.'))
            {
                int labelBytes = Encoding.UTF8.GetByteCount(label);
                if (labelBytes > MaxDomainTargetLabelBytes)
                {
                    return InvalidArgument("Target",
                        $"The target has a label of {labelBytes} UTF-8 bytes; {type} targets are host names, whose dot-separated labels allow at most {MaxDomainTargetLabelBytes}.",
                        "TargetLabelTooLong", target);
                }
            }
            return null;
        }

        /// <summary>Checks a user name that will be written. Returns null when it is valid.</summary>
        internal static ErrorRecord? CheckUserName(string? userName, CredType type, string target)
        {
            if (type == CredType.DomainPassword && !IsDomainUserName(userName))
            {
                return InvalidArgument("UserName",
                    "DomainPassword credentials need a user name in the form DOMAIN\\user or user@domain.", "InvalidUserName", target);
            }
            if (userName == null)
            {
                return null;
            }
            if (userName.Length > MaxUserNameLength)
            {
                return InvalidArgument("UserName", $"The user name is {userName.Length} characters; at most {MaxUserNameLength} are allowed.", "UserNameTooLong", target);
            }
            return CheckCharacters("UserName", userName, "user name", target);
        }

        /// <summary>Checks a comment that will be written. Returns null when it is valid.</summary>
        internal static ErrorRecord? CheckComment(string? comment, string target)
        {
            if (comment == null)
            {
                return null;
            }
            if (comment.Length > MaxCommentLength)
            {
                return InvalidArgument("Comment", $"The comment is {comment.Length} characters; at most {MaxCommentLength} are allowed.", "CommentTooLong", target);
            }
            return CheckCharacters("Comment", comment, "comment", target);
        }

        private static bool IsDomainUserName(string? userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return false;
            }
            int backslash = userName!.IndexOf('\\');
            if (backslash > 0 && backslash < userName.Length - 1)
            {
                return true;
            }
            int at = userName.IndexOf('@');
            return at > 0 && at < userName.Length - 1;
        }

        // Control characters such as line breaks and terminal escape sequences make names misleading in output
        private static ErrorRecord? CheckCharacters(string parameterName, string value, string what, string target)
        {
            foreach (char c in value)
            {
                if (char.IsControl(c))
                {
                    return InvalidArgument(parameterName, $"The {what} contains the control character U+{(int)c:X4}.", "InvalidCharacters", target);
                }
            }
            return null;
        }
    }
}
