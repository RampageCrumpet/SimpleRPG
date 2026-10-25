using NUnit.Framework;
using SimpleRPG.Articulation;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// A host and a separate owning client with 200 ms each way. The client must show its own pelvis and torso at once, the server's and the
    /// client's answers must agree once the view is still, and a view the server was told wrong must be put right.
    /// </summary>
    public class OwnerArticulationPredictionTests : RemoteClientTestBase
    {
        protected override int SimulatedDelayMs => 200;

        private Character onServer;
        private Character onClient;
        private UpperBodyArticulation serverArticulation;
        private UpperBodyArticulation clientArticulation;

        private IEnumerator SpawnOwned()
        {
            onServer = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return ClientCopyOf(onServer, copy => onClient = copy);
            yield return Wait(1.0f);
            serverArticulation = onServer.GetComponent<UpperBodyArticulation>();
            clientArticulation = onClient.GetComponent<UpperBodyArticulation>();
            Assert.IsTrue(clientArticulation.IsOwner, "the client owns its player");
        }

        private static float Yaw(Component c) => c.transform.eulerAngles.y;

        [UnityTest]
        public IEnumerator TheOwnerTurnsItsOwnRootAtOnce_WhileTheServerHasNotHeardYet()
        {
            yield return SpawnOwned();

            clientArticulation.SubmitLook(100f, 0f);
            yield return Wait(0.12f);

            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0f, Yaw(onClient))), 10f, "the owner's pelvis has turned from its own prediction");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0f, Yaw(onServer))), 1f, "the server, a latency away, has not received the view yet");
            Assert.LessOrEqual(Mathf.Abs(clientArticulation.AppliedTwist), clientArticulation.Settings.maxTwist + 0.01f, "the owner's twist obeys the same limit");
        }

        [UnityTest]
        public IEnumerator OwnerAndServer_ArriveAtTheSamePelvisAndTorso_WithoutAResync()
        {
            yield return SpawnOwned();

            clientArticulation.SubmitLook(120f, 20f);
            yield return Wait(3.0f);

            Assert.AreEqual(0f, Mathf.DeltaAngle(Yaw(onServer), Yaw(onClient)), 1f, "the two roots end up facing the same way");
            Assert.AreEqual(0f, Mathf.DeltaAngle(serverArticulation.Solver.TorsoYaw, clientArticulation.Solver.TorsoYaw), 0.5f);
            Assert.AreEqual(serverArticulation.AppliedTwist, clientArticulation.AppliedTwist, 0.5f);
            Assert.AreEqual(serverArticulation.AppliedPitch, clientArticulation.AppliedPitch, 0.5f);
            Assert.AreEqual(0, clientArticulation.ViewResyncCount, "nothing needed correcting");
        }

        [UnityTest]
        public IEnumerator SpinningManyTimes_LeavesNoAccumulatedYawError()
        {
            yield return SpawnOwned();

            for (int i = 0; i < 100; i++)
            {
                clientArticulation.SubmitLook(18f, 0f);
                yield return null;
            }

            yield return Wait(3.0f);

            Assert.AreEqual(0f, Mathf.DeltaAngle(serverArticulation.ViewYaw, clientArticulation.ViewYaw), 0.5f, "five full turns later the server's view is the owner's");
            Assert.AreEqual(0f, Mathf.DeltaAngle(Yaw(onServer), Yaw(onClient)), 1f);
            Assert.AreEqual(0, clientArticulation.ViewResyncCount);
        }

        [UnityTest]
        public IEnumerator AViewTheServerWasToldWrong_IsPutRightByTheOwner()
        {
            yield return SpawnOwned();
            clientArticulation.SubmitLook(70f, 0f);
            yield return Wait(2.0f);

            // Pretend a message was lost: the server's idea of the view is 110 degrees off.
            FieldInfo viewYaw = typeof(UpperBodyArticulation).GetField("viewYaw", BindingFlags.Instance | BindingFlags.NonPublic);
            viewYaw.SetValue(serverArticulation, 70f - 110f);
            yield return Wait(0.2f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(serverArticulation.ViewYaw, clientArticulation.ViewYaw)), 90f, "set-up: the two disagree");

            yield return Wait(9.0f);

            Assert.AreEqual(0f, Mathf.DeltaAngle(serverArticulation.ViewYaw, clientArticulation.ViewYaw), 0.5f, "the server holds the owner's view again");
            Assert.AreEqual(0f, Mathf.DeltaAngle(Yaw(onServer), Yaw(onClient)), 1f, "and the roots agree");
            Assert.GreaterOrEqual(clientArticulation.ViewResyncCount, 1);
            Assert.LessOrEqual(clientArticulation.ViewResyncCount, 3, "it was said once, not nagged");
        }

        [UnityTest]
        public IEnumerator TheServersHitPose_IsTheServersOwn_NotTheOwnersPrediction()
        {
            yield return SpawnOwned();

            clientArticulation.SubmitLook(100f, 0f);
            yield return Wait(0.12f);

            Assert.AreEqual(0f, serverArticulation.AppliedTwist, 0.5f, "the server's pose, which hits are tested against, only moves on what the server received");
            Assert.AreNotEqual(0f, clientArticulation.AppliedTwist, "while the owner already shows the turn");
        }
    }
}
