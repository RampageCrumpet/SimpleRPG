using SimpleRPG.HitDetection;
using NUnit.Framework;
using SimpleRPG.Articulation;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// A host and a genuinely separate client. A remote player's regions on the server must be where that player's view puts them, with no bone
    /// poses sent: the client only reports where it looks, and the server's own rig does the rest.
    /// </summary>
    public class AnatomicalRemotePoseTests : RemoteClientTestBase
    {
        private static Vector3 HeadInBody(Character body, HurtBox head)
        {
            Physics.SyncTransforms();
            return body.transform.InverseTransformPoint(head.Shapes[0].bounds.center);
        }

        [UnityTest]
        public IEnumerator ARemotePlayersLean_MovesItsHeadRegionOnTheServer()
        {
            Character onServer = SpawnOnServer(PlayerPrefab, new Vector3(0f, 0f, 0f), ClientId);
            Character onClient = null;
            yield return ClientCopyOf(onServer, copy => onClient = copy);
            yield return Wait(0.5f);

            HurtBox head = onServer.GetComponentsInChildren<HurtBox>(true).First(h => h.BodyLocation == BodyLocation.Head);
            Vector3 upright = HeadInBody(onServer, head);

            // The owning client says only where it looks; the server's rig does the bending.
            UpperBodyArticulation articulation = onClient.GetComponent<UpperBodyArticulation>();
            Assert.IsTrue(articulation.IsOwner, "the client owns its player");
            articulation.SubmitLook(0f, 70f);
            yield return Wait(1.2f);
            Vector3 leaning = HeadInBody(onServer, head);

            Assert.Greater(leaning.z - upright.z, 0.3f, "the server's head region has moved forward");
            Assert.Less(leaning.y - upright.y, -0.2f, "and down");
        }

        [UnityTest]
        public IEnumerator ARemotePlayersHeadRegion_ReturnsWhenTheViewIsLevelAgain()
        {
            Character onServer = SpawnOnServer(PlayerPrefab, new Vector3(0f, 0f, 0f), ClientId);
            Character onClient = null;
            yield return ClientCopyOf(onServer, copy => onClient = copy);
            yield return Wait(0.5f);

            HurtBox head = onServer.GetComponentsInChildren<HurtBox>(true).First(h => h.BodyLocation == BodyLocation.Head);
            UpperBodyArticulation articulation = onClient.GetComponent<UpperBodyArticulation>();
            articulation.SubmitLook(0f, 0f);
            yield return Wait(0.3f);
            Vector3 level = HeadInBody(onServer, head);

            articulation.SubmitLook(0f, 70f);
            yield return Wait(1.0f);
            articulation.SubmitLook(0f, 0f);
            yield return Wait(1.0f);

            Assert.AreEqual(0f, Vector3.Distance(level, HeadInBody(onServer, head)), 0.08f, "no lean is left behind in the server's regions");
        }
    }
}
