using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Stats between a host and a separate client: the server resolves them, the client sees only the replicated maximum health, and it has no
    /// attribute set (and so no speed) of its own to change.
    /// </summary>
    public class StatsRemoteClientTests : RemoteClientTestBase
    {
        [UnityTest]
        public IEnumerator Client_SeesReplicatedMaximumHealth_ButHasNoStatsOfItsOwn()
        {
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return Until(() => clientPlayer.MaxHealth == 100, 5f, "the client to receive the maximum health");

            Assert.IsNull(clientPlayer.Attributes, "Only the server resolves stats.");
            Assert.AreEqual(0f, clientPlayer.MovementSpeed, "A client has no speed to set; it only ever sends a direction.");

            ModifierHandle handle = player.Attributes.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Constitution, 5, default));
            yield return Until(() => clientPlayer.MaxHealth == 150, 5f, "the client to see the raised maximum");
            Assert.AreEqual(100, clientPlayer.Health, "A higher maximum does not heal.");

            player.Attributes.RemoveModifier(handle);
            yield return Until(() => clientPlayer.MaxHealth == 100, 5f, "the client to see the maximum fall back");
        }
    }
}
