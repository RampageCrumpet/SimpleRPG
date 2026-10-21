using NUnit.Framework;
using SimpleRPG.Keys;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The server's rules for opening locked doors and picking up keys.
    /// </summary>
    public class KeyRulesTests
    {
        private const float Range = 3f;

        [Test]
        public void CheckOpen_HoldingTheKeyNearby_Opens()
        {
            Assert.AreEqual(KeyUseResult.Opened, KeyRules.CheckOpen(new[] { 2 }, new int[0], 2, 1f, Range));
        }

        [Test]
        public void CheckOpen_HoldingADifferentKey_StaysShut()
        {
            Assert.AreEqual(KeyUseResult.NotHeld, KeyRules.CheckOpen(new[] { 1 }, new int[0], 2, 1f, Range));
        }

        [Test]
        public void CheckOpen_NoKeys_StaysShut()
        {
            Assert.AreEqual(KeyUseResult.NotHeld, KeyRules.CheckOpen(new int[0], new int[0], 1, 0f, Range));
        }

        [Test]
        public void CheckOpen_AlreadyOpen_IsReportedAsSuch()
        {
            Assert.AreEqual(KeyUseResult.AlreadyOpen, KeyRules.CheckOpen(new[] { 1 }, new[] { 1 }, 1, 0f, Range));
        }

        [Test]
        public void CheckOpen_HoldingTheKeyButFarAway_IsRefused()
        {
            Assert.AreEqual(KeyUseResult.TooFar, KeyRules.CheckOpen(new[] { 1 }, new int[0], 1, Range + KeyRules.ServerSlack + 0.1f, Range));
        }

        [Test]
        public void InRange_AllowsTheServerSlackButNoMore()
        {
            Assert.IsTrue(KeyRules.InRange(Range + KeyRules.ServerSlack, Range));
            Assert.IsFalse(KeyRules.InRange(Range + KeyRules.ServerSlack + 0.01f, Range));
        }
    }
}
