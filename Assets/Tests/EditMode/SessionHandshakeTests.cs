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

        [Test]
        public void SameBuildWithRoomIsApproved()
        {
            Assert.IsTrue(SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build), Build, 1, 4).Approved);
        }

        [Test]
        public void ADifferentBuildIsRefusedWithAReason()
        {
            SessionHandshake.Decision decision = SessionHandshake.Evaluate(SessionHandshake.BuildRequest("0.0.9"), Build, 1, 4);

            Assert.IsFalse(decision.Approved);
            StringAssert.Contains("Version mismatch", decision.Reason);
        }

        [Test]
        public void AFullServerRefusesTheNextClient()
        {
            SessionHandshake.Decision decision = SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build), Build, 4, 4);

            Assert.IsFalse(decision.Approved);
            StringAssert.Contains("full", decision.Reason);
        }

        [Test]
        public void TheLastFreePlaceIsStillGiven()
        {
            Assert.IsTrue(SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build), Build, 3, 4).Approved);
        }

        [Test]
        public void ZeroMaximumMeansNoLimit()
        {
            Assert.IsTrue(SessionHandshake.Evaluate(SessionHandshake.BuildRequest(Build), Build, 500, 0).Approved);
        }

        [Test]
        public void EmptyMalformedAndOversizedRequestsAreRefused()
        {
            Assert.IsFalse(SessionHandshake.Evaluate(null, Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(new byte[0], Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("hello"), Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("Other|1|" + Build), Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(Encoding.UTF8.GetBytes("SimpleRPG|x|" + Build), Build, 0, 4).Approved);
            Assert.IsFalse(SessionHandshake.Evaluate(new byte[1000], Build, 0, 4).Approved);
        }

        [Test]
        public void ADifferentProtocolIsRefusedEvenWithTheSameBuild()
        {
            byte[] request = Encoding.UTF8.GetBytes("SimpleRPG|" + (SessionHandshake.ProtocolVersion + 1) + "|" + Build);

            SessionHandshake.Decision decision = SessionHandshake.Evaluate(request, Build, 0, 4);

            Assert.IsFalse(decision.Approved);
            StringAssert.Contains("Protocol mismatch", decision.Reason);
        }
    }
}
