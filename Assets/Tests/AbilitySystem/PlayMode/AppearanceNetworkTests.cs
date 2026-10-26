using NUnit.Framework;
using SimpleRPG.Appearance;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// A host and a genuinely separate client: the parts the server dresses a character in appear on the client, and a server-side change follows.
    /// </summary>
    public class AppearanceNetworkTests : RemoteClientTestBase
    {
        private static int PartCount(Character body) => body.GetComponentsInChildren<Renderer>(true).Count(r => (r is SkinnedMeshRenderer || r is MeshRenderer) && r.GetComponentInParent<Weapon>() == null);

        [UnityTest]
        public IEnumerator AClient_SeesEveryDefaultPartTheServerWears()
        {
            Character onServer = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character onClient = null;
            yield return ClientCopyOf(onServer, copy => onClient = copy);
            yield return Wait(0.5f);

            int expected = onServer.GetComponent<CharacterAppearance>().DefaultParts.Length;
            Assert.AreEqual(expected, PartCount(onServer), "the server shows its own parts");
            Assert.AreEqual(expected, PartCount(onClient), "the client shows the same parts");
        }

        [UnityTest]
        public IEnumerator ARemovedPart_DisappearsOnTheClient()
        {
            Character onServer = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character onClient = null;
            yield return ClientCopyOf(onServer, copy => onClient = copy);
            yield return Wait(0.5f);
            int before = PartCount(onClient);

            onServer.GetComponent<CharacterAppearance>().ServerClear(BodyPartSlot.HandLeft);
            yield return Wait(0.5f);

            Assert.AreEqual(before - 1, PartCount(onClient));
        }
    }
}
