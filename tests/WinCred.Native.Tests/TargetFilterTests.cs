using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WinCred.Native.Tests
{
    [TestClass]
    public class TargetFilterTests
    {
        [TestMethod]
        [DataRow("WinCred.Test.*", "WinCred.Test.*")]
        [DataRow("server?", "server*")]
        [DataRow("server[12]", "server*")]
        [DataRow("lit`[1`]*", "lit[1]*")]
        [DataRow("a``b*", "a`b*")]
        [DataRow("abc`*d*", "abc*")]
        [DataRow("café*", "caf*")]
        public void UsesTheLiteralPrefix(string pattern, string expected)
        {
            Assert.AreEqual(expected, TargetFilter.ForPattern(pattern));
        }

        [TestMethod]
        [DataRow("*")]
        [DataRow("*.example.com")]
        [DataRow("?x")]
        [DataRow("[ab]x")]
        [DataRow("é*")]
        public void ReturnsNoFilterWithoutAPrefix(string pattern)
        {
            Assert.IsNull(TargetFilter.ForPattern(pattern));
        }
    }
}
