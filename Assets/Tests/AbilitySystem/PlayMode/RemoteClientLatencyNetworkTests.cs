using HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// The owning player's melee prediction and a late-arriving swing, over a connection that delays every packet by 100 ms each way (about a
    /// fifth of a second round trip) with 20 ms of jitter. These are the cases where prediction earns its keep: the swing must show on the press,
    /// the server must still be the one that decides, and a late chain press made in time must not be lost.
    /// </summary>
    public class RemoteClientLatencyNetworkTests : RemoteClientTestBase
    {
        private const string MeleeAttack = "Assets/Abilitities/Abillities/MeleeAttack.asset";

        protected override int SimulatedDelayMs => 100;

        protected override int SimulatedJitterMs => 20;

        [UnityTest]
        public IEnumerator TheSimulatedConnection_DelaysWhatTheClientSends()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, melee);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return Wait(0.5f);
            ComboMeleeDelivery serverMelee = player.GetComponent<ComboMeleeDelivery>();

            float pressedAt = Time.realtimeSinceStartup;
            clientPlayer.GetComponent<AbilityUser>().RequestActivate(melee);

            Assert.AreEqual(0, serverMelee.CurrentAttack, "The press cannot have reached the server in the same frame.");
            yield return Until(() => serverMelee.CurrentAttack == 1, 5f, "the server to hear the press");
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - pressedAt, 0.08f, "The press should take about 100 ms to arrive.");
        }

        [UnityTest]
        public IEnumerator AnOwnersSwing_ShowsOnThePress_AndTheLateConfirmationDoesNotReplayIt()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, melee);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return Wait(0.5f);
            AttackLog log = new AttackLog(clientPlayer.GetComponent<ComboMeleeDelivery>());

            clientPlayer.GetComponent<AbilityUser>().RequestActivate(melee);

            Assert.AreEqual(1, log.Started.Count, "The swing shows on the press, a round trip before the server has answered.");
            yield return Until(() => player.GetComponent<ComboMeleeDelivery>().CurrentAttack == 1, 5f, "the server to start the attack");
            yield return Wait(0.8f);
            Assert.AreEqual(1, log.Started.Count, "The confirmation, arriving 100 ms later, must not start the swing again.");
            Assert.AreEqual(0, log.Cancelled);
        }

        [UnityTest]
        public IEnumerator AChainPressMadeInTheWindow_IsAcceptedByTheServerDespiteTheDelay()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, melee);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return Wait(0.5f);
            ComboMeleeDelivery serverMelee = player.GetComponent<ComboMeleeDelivery>();
            AttackLog log = new AttackLog(clientPlayer.GetComponent<ComboMeleeDelivery>());
            AbilityUser user = clientPlayer.GetComponent<AbilityUser>();

            user.RequestActivate(melee);
            yield return Wait(1.7f); // the first attack's chain window runs from 1.45 s to 2.0 s
            user.RequestActivate(melee);

            Assert.AreEqual(2, log.Started.Count, "The chained swing shows on the second press.");
            Assert.AreEqual(2, log.Started[1].Attack);
            yield return Until(() => serverMelee.CurrentAttack == 2, 5f, "the server to chain into the second attack");
            yield return Wait(0.8f);
            Assert.AreEqual(2, log.Started.Count, "The server's confirmations must not replay either swing.");
            Assert.AreEqual(0, log.Cancelled, "The server agreed with both guesses.");
        }

        [UnityTest]
        public IEnumerator AChainPressBeforeTheWindow_IsDiscardedByEveryone()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, melee);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return Wait(0.5f);
            ComboMeleeDelivery serverMelee = player.GetComponent<ComboMeleeDelivery>();
            AttackLog log = new AttackLog(clientPlayer.GetComponent<ComboMeleeDelivery>());
            AbilityUser user = clientPlayer.GetComponent<AbilityUser>();

            user.RequestActivate(melee);
            yield return Wait(0.6f);
            user.RequestActivate(melee); // far too early: the window opens at 1.45 s
            yield return Wait(1.0f);

            Assert.AreEqual(1, log.Started.Count, "The owner predicted nothing for a press outside the window.");
            Assert.AreEqual(1, serverMelee.CurrentAttack, "And the server did not chain either.");
            Assert.AreEqual(0, log.Cancelled);
        }

        [UnityTest]
        public IEnumerator APressTheServerRefuses_IsTakenBack_OnlyAfterTheRoundTrip()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, melee);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return Wait(0.5f);
            AttackLog log = new AttackLog(clientPlayer.GetComponent<ComboMeleeDelivery>());

            // The owner will not hear of the death for 100 ms, so it predicts a swing the server will refuse.
            player.TakeDamage(new DamageInfo { Damage = 1000 }, BodyLocation.Torso);
            float pressedAt = Time.realtimeSinceStartup;
            clientPlayer.GetComponent<AbilityUser>().RequestActivate(melee);
            Assert.AreEqual(1, log.Started.Count, "The owner showed the swing it guessed.");
            Assert.AreEqual(0, log.Cancelled);

            yield return Until(() => log.Cancelled == 1, 5f, "the server's refusal to take the swing back");
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - pressedAt, 0.15f, "The refusal cannot arrive before the press has made the round trip.");
        }

        [UnityTest]
        public IEnumerator AnotherPlayersSwing_ReachesTheClientOnce_AfterTheDelay()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character hostsPlayer = SpawnOnServer(PlayerPrefab, Vector3.zero, host.LocalClientId, melee);
            Character clientCopyOfHosts = null;
            yield return ClientCopyOf(hostsPlayer, c => clientCopyOfHosts = c);
            yield return Wait(0.5f);
            AttackLog log = new AttackLog(clientCopyOfHosts.GetComponent<ComboMeleeDelivery>());

            float startedAt = Time.realtimeSinceStartup;
            Assert.IsTrue(hostsPlayer.GetComponent<AbilityUser>().TryActivateOnServer(melee));

            yield return Until(() => log.Started.Count == 1, 5f, "the client to hear about the swing");
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - startedAt, 0.08f, "The news takes about 100 ms to arrive.");
            Assert.AreEqual(1, log.Started[0].Attack);
            Assert.GreaterOrEqual(log.Started[0].SecondsIn, 0f);
            Assert.Less(log.Started[0].SecondsIn, 0.6f);
            yield return Wait(0.5f);
            Assert.AreEqual(1, log.Started.Count);
            Assert.AreEqual(0, log.Cancelled);
        }
    }
}
