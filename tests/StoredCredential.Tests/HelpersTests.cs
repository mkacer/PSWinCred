using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSCredentialManager.Cmdlet;

namespace PSCredentialManager.Tests
{
    [TestClass]
    public class HelpersTests
    {
        private const string Punctuation = "!@#$%^&*()_-+=[{]};:>|./?";

        [TestMethod]
        [DataRow(1, 0)]
        [DataRow(10, 3)]
        [DataRow(20, 20)]
        [DataRow(128, 5)]
        public void GeneratePasswordHasLengthAndSpecialCharacters(int length, int special)
        {
            for (int i = 0; i < 50; i++)
            {
                string password = Helpers.GeneratePassword(length, special);

                Assert.AreEqual(length, password.Length);
                Assert.IsTrue(password.Count(c => Punctuation.IndexOf(c) >= 0) >= special);
                Assert.IsTrue(password.All(c => char.IsLetterOrDigit(c) || Punctuation.IndexOf(c) >= 0));
            }
        }

        [TestMethod]
        public void GeneratePasswordUsesWholeAlphabet()
        {
            string all = string.Concat(Enumerable.Range(0, 200).Select(_ => Helpers.GeneratePassword(128, 0)));
            Assert.AreEqual(87, all.Distinct().Count());
        }
    }
}
