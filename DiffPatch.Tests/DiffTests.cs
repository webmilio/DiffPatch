using NUnit.Framework;

namespace DiffPatch.Tests
{
    [TestFixture]
    public class DiffTests
    {
        [Test]
        public void Addition_ToString()
        {
            const string str = "TestString";

            var diff = new Diff(Operation.Insert, str);
            Assert.AreEqual($"+{str}", diff.ToString());
        }

        [Test]
        public void Equal_ToString()
        {
            const string str = "TestString";

            var diff = new Diff(Operation.Equal, str);
            Assert.AreEqual($" {str}", diff.ToString());
        }

        [Test]
        public void Delete_ToString()
        {
            const string str = "TestString";

            var diff = new Diff(Operation.Delete, str);
            Assert.AreEqual($"-{str}", diff.ToString());
        }
    }
}