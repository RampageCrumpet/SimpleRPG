using System.Text;
using NUnit.Framework;
using SimpleRPG.Networking;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Who the server lets in: the same build, with room to spare, and nothing that isn't a SimpleRPG client.
    /// </summary>
    public class SessionHandshakeTests
    {
        private const string Build = "0.1.0";
        private const string Player = "player-0001";

        [Test]
        public void SameBuildWithRoomIsApproved()
        {
            Assert.IsTrue(SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build, Player), Build, 1, 4).Approved);
        }

        [Test]
        public void ADifferentBuildIsRefusedWithAReason()
        {
            SessionHandshake.Decision decision = SessionHandshake.Evaluate(SessionHandshake.BuildRequest("0.0.9", Player), Build, 1, 4);

            Assert.IsFalse(decision.Approved);
            StringAssert.Contains("Version mismatch", decision.Reason);
        }

        [Test]
        public void AFullServerRefusesTheNextClient()
        {
            SessionHandshake.Decision decision = SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build, Player), Build, 4, 4);

            Assert.IsFalse(decision.Approved);
            StringAssert.Contains("full", decision.Reason);
        }

        [Test]
        public void TheLastFreePlaceIsStillGiven()
        {
            Assert.IsTrue(SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build, Player), Build, 3, 4).Approved);
        }

        [Test]
        public void ZeroMaximumMeansNoLimit()
        {
            Assert.IsTrue(SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build, Player), Build, 500, 0).Approved);
        }

        [Test]
        public void EmptyMalformedAndOversizedRequestsAreRefused()
        {
            Assert.IsFalse(SessionHandshake.Evaluate(null, Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(new byte[0], Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("hello"), Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("Other|2|" + Build + "|" + Player), Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("SimpleRPG|x|" + Build + "|" + Player), Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(new byte[1000], Build, 0, 4).Approved);
        }

        [Test]
        public void ADifferentProtocolIsRefusedEvenWithTheSameBuild()
        {
            byte[] request = Encoding.UTF8.GetBytes("SimpleRPG|" + (SessionHandshake.ProtocolVersion + 1) + "|" + Build + "|" + Player);

            SessionHandshake.Decision decision = SessionHandshake.Evaluate(request, Build, 0, 4);

            Assert.IsFalse(decision.Approved);
            StringAssert.Contains("Protocol mismatch", decision.Reason);
        }

        [Test]
        public void TheApprovedDecisionCarriesThePlayerId()
        {
            SessionHandshake.Decision decision = SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build, Player), Build, 0, 4);

            Assert.IsTrue(decision.Approved);
            Assert.AreEqual(Player, decision.PlayerId);
        }

        [Test]
        public void ARequestWithoutAValidPlayerIdIsRefused()
        {
            foreach (string bad in new[] { "", "short", "has space in it", "bad|pipe|chars", new string('x', 65) })
            {
                Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("SimpleRPG|" + SessionHandshake.ProtocolVersion + "|" + Build + "|" + bad), Build, 0, 4).Approved, "'" + bad + "'");
            }

            Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("SimpleRPG|" + SessionHandshake.ProtocolVersion + "|" + Build), Build, 0, 4).Approved, "No player id at all.");
        }

        [Test]
        public void PlayerIdsAreLettersDigitsDashesAndUnderscores()
        {
            Assert.IsTrue(SessionHandshake.IsValidPlayerId("0123456789abcdef0123456789abcdef"));
            Assert.IsTrue(SessionHandshake.IsValidPlayerId("Player_One-01"));
            Assert.IsFalse(SessionHandshake.IsValidPlayerId(null));
            Assert.IsFalse(SessionHandshake.IsValidPlayerId("a/b/c/d/e/f"));
        }
    }
}
