using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PSCredentialManager.Cmdlet
{
    internal class Helpers
    {
        private static char[] punctuations = "!@#$%^&*()_-+=[{]};:>|./?".ToCharArray();

        internal static string GeneratePassword(int length, int numberOfNonAlphanumericCharacters)
        {
            if (length < 1 || length > 128)
            {
                throw new ArgumentException("password length incorrect");
            }

            if (numberOfNonAlphanumericCharacters > length || numberOfNonAlphanumericCharacters < 0)
            {
                throw new ArgumentException("Minimum number of non alphanumeric characters incorrect");
            }

            string password;
            int index;
            char[] cBuf;
            int count;

            using (RandomNumberGenerator rng = new RNGCryptoServiceProvider())
            {
                do
                {
                    cBuf = new char[length];
                    count = 0;

                    for (int iter = 0; iter < length; iter++)
                    {
                        int i = GetRandomInt(rng, 87);
                        if (i < 10)
                            cBuf[iter] = (char)('0' + i);
                        else if (i < 36)
                            cBuf[iter] = (char)('A' + i - 10);
                        else if (i < 62)
                            cBuf[iter] = (char)('a' + i - 36);
                        else
                        {
                            cBuf[iter] = punctuations[i - 62];
                            count++;
                        }
                    }

                    if (count < numberOfNonAlphanumericCharacters)
                    {
                        int j, k;

                        for (j = 0; j < numberOfNonAlphanumericCharacters - count; j++)
                        {
                            do
                            {
                                k = GetRandomInt(rng, length);
                            }
                            while (!Char.IsLetterOrDigit(cBuf[k]));

                            cBuf[k] = punctuations[GetRandomInt(rng, punctuations.Length)];
                        }
                    }

                    password = new string(cBuf);
                }
                while (IsDangerousString(password, out index));
            }

            return password;
        }

        // Returns a uniform random int in [0, maxExclusive) using rejection sampling to avoid modulo bias
        private static int GetRandomInt(RandomNumberGenerator rng, int maxExclusive)
        {
            byte[] buf = new byte[4];
            uint limit = uint.MaxValue - (uint.MaxValue % (uint)maxExclusive);
            uint value;

            do
            {
                rng.GetBytes(buf);
                value = BitConverter.ToUInt32(buf, 0);
            }
            while (value >= limit);

            return (int)(value % (uint)maxExclusive);
        }

        private static bool IsAtoZ(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        // Detect constructs that look like HTML tags
        private static char[] startingChars = new char[] { '<', '&' };

        // Only accepts http: and https: protocols, and protocolless urls.
        // Used by web parts to validate import and editor input on Url properties.
        // Review: is there a way to escape colon that will still be recognized by IE?
        // %3a does not work with IE.
        internal static bool IsDangerousUrl(string s)
        {
            if (String.IsNullOrEmpty(s))
            {
                return false;
            }

            // Trim the string inside this method, since a Url starting with whitespace
            // is not necessarily dangerous.  This saves the caller from having to pre-trim
            // the argument as well.
            s = s.Trim();

            int len = s.Length;

            if ((len > 4) &&
                ((s[0] == 'h') || (s[0] == 'H')) &&
                ((s[1] == 't') || (s[1] == 'T')) &&
                ((s[2] == 't') || (s[2] == 'T')) &&
                ((s[3] == 'p') || (s[3] == 'P')))
            {
                if ((s[4] == ':') ||
                    ((len > 5) && ((s[4] == 's') || (s[4] == 'S')) && (s[5] == ':')))
                {
                    return false;
                }
            }

            int colonPosition = s.IndexOf(':');
            if (colonPosition == -1)
            {
                return false;
            }
            return true;
        }

        internal static bool IsValidJavascriptId(string id)
        {
            return (String.IsNullOrEmpty(id) || System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier(id));
        }

        internal static bool IsDangerousString(string s, out int matchIndex)
        {
            //bool inComment = false;
            matchIndex = 0;

            for (int i = 0; ;)
            {

                // Look for the start of one of our patterns
                int n = s.IndexOfAny(startingChars, i);

                // If not found, the string is safe
                if (n < 0) return false;

                // If it's the last char, it's safe
                if (n == s.Length - 1) return false;

                matchIndex = n;

                switch (s[n])
                {
                    case '<':
                        // If the < is followed by a letter or '!', it's unsafe (looks like a tag or HTML comment)
                        if (IsAtoZ(s[n + 1]) || s[n + 1] == '!' || s[n + 1] == '/' || s[n + 1] == '?') return true;
                        break;
                    case '&':
                        // If the & is followed by a #, it's unsafe (e.g. &#83;)
                        if (s[n + 1] == '#') return true;
                        break;
                }

                // Continue searching
                i = n + 1;
            }
        }
    }
}
