using HitDetection;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Death and respawn between a host and a genuinely separate client: the server alone decides who is alive, every machine sees it, and a
    /// client can only ask for its own next life once it is dead.
    /// </summary>
    public class PlayerDeathRemoteClientTests : RemoteClientTestBase
    {
        private static void Kill(Character target) => target.TakeDamage(new DamageInfo { Damage = StartingHealth }, BodyLocation.Torso);

        [UnityTest]
        public IEnumerator ADeath_ReachesTheOwningClient_AndTheClientCanAskForItsNextLife()
        {
            Character player = SpawnOnServer(PlayerPrefab, new Vector3(2f, 0f, 3f), ClientId);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            int clientSawDeath = 0;
            int clientSawRevive = 0;
            clientPlayer.OnDeath += () => clientSawDeath++;
            clientPlayer.OnRevive += () => clientSawRevive++;

            Kill(player);
            yield return Until(() => !clientPlayer.IsAlive, 5f, "the client to see the death");
            Assert.AreEqual(1, clientSawDeath);
            Assert.AreEqual(0, clientPlayer.Health);

            clientPlayer.RequestRespawn();
            yield return Until(() => clientPlayer.IsAlive, 5f, "the client to see the new life");

            Assert.IsTrue(player.IsAlive, "The server revived the character.");
            Assert.AreEqual(StartingHealth, clientPlayer.Health);
            Assert.AreEqual(1, clientSawRevive);
            Assert.AreEqual(2, clientPlayer.LifeId);
        }

        [UnityTest]
        public IEnumerator AClient_CannotRespawnACharacterThatIsAlive()
        {
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            player.TakeDamage(new DamageInfo { Damage = 30 }, BodyLocation.Torso);
            yield return Until(() => clientPlayer.Health == StartingHealth - 30, 5f, "the client to see the damage");

            clientPlayer.RequestRespawn();
            yield return Wait(0.5f);

            Assert.AreEqual(StartingHealth - 30, player.Health, "A respawn is not a heal.");
            Assert.AreEqual(1, player.LifeId);
        }

        [UnityTest]
        public IEnumerator AClient_CannotRespawnAnotherPlayersCharacter()
        {
            Character hostsPlayer = SpawnOnServer(PlayerPrefab, Vector3.zero, host.LocalClientId);
            Character copyOfHosts = null;
            yield return ClientCopyOf(hostsPlayer, c => copyOfHosts = c);
            Kill(hostsPlayer);
            yield return Until(() => !copyOfHosts.IsAlive, 5f, "the client to see the death");

            Assert.IsFalse(copyOfHosts.IsOwner);
            copyOfHosts.RequestRespawn();
            yield return Wait(0.5f);

            Assert.IsFalse(hostsPlayer.IsAlive, "Only a character's own player can ask for its next life.");
        }

        [UnityTest]
        public IEnumerator ADeadPlayer_IsDeadForEveryoneWatching()
        {
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, host.LocalClientId);
            Character copyOnClient = null;
            yield return ClientCopyOf(player, c => copyOnClient = c);

            Kill(player);

            yield return Until(() => !copyOnClient.IsAlive, 5f, "the other client to see the death");
            Assert.AreEqual(0, copyOnClient.Health);
        }
    }
}
