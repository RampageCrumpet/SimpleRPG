using HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// A host and a genuinely separate client, connected over the loopback network in one process. These prove the networking from both sides:
    /// what a client asks for is decided by the server, what the server decides reaches the client, and a client cannot make anything
    /// authoritative happen by itself.
    /// </summary>
    public class RemoteClientNetworkTests : RemoteClientTestBase
    {
        // ----- The client asks, the server decides, the result comes back -----

        [UnityTest]
        public IEnumerator ARemoteClientsCast_RunsOnTheServer_AndTheResultReachesTheClient()
        {
            AbilityDefinition firebolt = Load(Firebolt);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, firebolt);
            Character target = SpawnOnServer(TargetPrefab, new Vector3(0f, 0f, 5f));
            Character clientPlayer = null;
            Character clientTarget = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return ClientCopyOf(target, c => clientTarget = c);
            Assert.IsTrue(clientPlayer.IsOwner, "The client controls its character.");
            Assert.IsFalse(clientPlayer.IsServer);

            Assert.IsTrue(clientPlayer.GetComponent<AbilityUser>().RequestActivate(firebolt), "The request was sent.");
            yield return Until(() => ServerProjectile() != null, 5f, "the server to launch the projectile");

            Assert.AreEqual(StartingHealth, target.Health, "Nothing is hurt until the projectile hits.");
            ServerProjectile().TargetHit(target, BodyLocation.Torso);
            int expected = StartingHealth - (int)((ConstantConfig)firebolt.Nodes.Single(n => n.TypeId == NodeTypes.Constant).Config).Value;

            Assert.AreEqual(expected, target.Health, "The server applied the damage the graph says.");
            yield return Until(() => clientTarget.Health == expected, 5f, "the client to see the new health");
        }

        [UnityTest]
        public IEnumerator ARemoteClient_CannotCastAnAbilityItDoesNotHave()
        {
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, Load(Firebolt));
            Character target = SpawnOnServer(TargetPrefab, new Vector3(0f, 0f, 5f));
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("which it does not have"));
            clientPlayer.GetComponent<AbilityUser>().RequestActivate(Load(Icebolt));
            yield return Wait(0.5f);

            Assert.AreEqual(0, ServerProjectileCount(), "The server does not launch what the character does not have.");
            Assert.AreEqual(StartingHealth, target.Health);
        }

        [UnityTest]
        public IEnumerator ARemoteClient_CannotCastAnotherPlayersCharacter()
        {
            AbilityDefinition firebolt = Load(Firebolt);
            Character hostsPlayer = SpawnOnServer(PlayerPrefab, Vector3.zero, host.LocalClientId, firebolt);
            Character clientCopyOfHosts = null;
            yield return ClientCopyOf(hostsPlayer, c => clientCopyOfHosts = c);

            bool sent = clientCopyOfHosts.GetComponent<AbilityUser>().RequestActivate(firebolt);
            yield return Wait(0.5f);

            Assert.IsFalse(sent, "A client has no say over a character it does not control.");
            Assert.AreEqual(0, ServerProjectileCount());
        }

        [UnityTest]
        public IEnumerator ARemoteClient_CannotChangeHealthEquipmentOrRunAbilitiesItself()
        {
            AbilityDefinition firebolt = Load(Firebolt);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, firebolt);
            Character target = SpawnOnServer(TargetPrefab, new Vector3(0f, 0f, 5f));
            Character clientPlayer = null;
            Character clientTarget = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return ClientCopyOf(target, c => clientTarget = c);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ignored a damage request on a client"));
            clientTarget.TakeDamage(new DamageInfo { Damage = 50 }, BodyLocation.Torso);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ignored an equip request"));
            bool equipped = clientPlayer.GetComponent<Equipment>().Equip(EquipmentSlot.mainHand, staff, 900);
            bool ran = clientPlayer.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);
            ItemCastResult itemCast = clientPlayer.GetComponent<AbilityUser>().TryActivateItemOnServer(EquipmentSlot.mainHand, 0);
            yield return Wait(0.5f);

            Assert.AreEqual(StartingHealth, target.Health, "The server's health is unchanged.");
            Assert.AreEqual(StartingHealth, clientTarget.Health, "And so is the client's view of it.");
            Assert.IsFalse(equipped);
            Assert.IsNull(player.GetComponent<Equipment>().Get(EquipmentSlot.mainHand), "The server's equipment is unchanged.");
            Assert.IsFalse(ran);
            Assert.AreEqual(ItemCastResult.NotAuthority, itemCast);
            Assert.AreEqual(0, ServerProjectileCount());
        }

        [UnityTest]
        public IEnumerator AnInvalidItemRequest_FromTheClient_IsIgnoredByTheServer()
        {
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, Load(Firebolt));
            player.GetComponent<Equipment>().Equip(EquipmentSlot.mainHand, staff, 800);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            MethodInfo rpc = typeof(AbilityUser).GetMethod("ActivateItemServerRpc", BindingFlags.Instance | BindingFlags.NonPublic);

            rpc.Invoke(clientPlayer.GetComponent<AbilityUser>(), new object[] { 9999, 0 });
            rpc.Invoke(clientPlayer.GetComponent<AbilityUser>(), new object[] { (int)EquipmentSlot.mainHand, 7 });
            rpc.Invoke(clientPlayer.GetComponent<AbilityUser>(), new object[] { (int)EquipmentSlot.mainHand, -1 });
            yield return Wait(0.5f);

            Assert.AreEqual(0, ServerProjectileCount(), "Nonsense slots and positions launch nothing.");
            Assert.AreEqual(3, player.GetComponent<Equipment>().GetCharges(EquipmentSlot.mainHand), "And cost nothing.");
        }

        // ----- Charged item, from the client's side -----

        [UnityTest]
        public IEnumerator AClientsStaff_SpendsServerCharges_AndTheOwnerSeesTheCountFall()
        {
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Equipment serverEquipment = player.GetComponent<Equipment>();
            serverEquipment.Equip(EquipmentSlot.mainHand, staff, 800);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            Equipment clientEquipment = clientPlayer.GetComponent<Equipment>();
            yield return Until(() => clientEquipment.GetCharges(EquipmentSlot.mainHand) == 3, 5f, "the owner to be told the staff has three charges");

            for (int cast = 1; cast <= 3; cast++)
            {
                Assert.IsTrue(clientPlayer.GetComponent<AbilityUser>().RequestActivateItem(EquipmentSlot.mainHand, 0));
                int remaining = 3 - cast;
                yield return Until(() => serverEquipment.GetCharges(EquipmentSlot.mainHand) == remaining, 5f, "the server to spend charge " + cast);
                yield return Wait(0.4f);
            }

            yield return Until(() => clientEquipment.GetCharges(EquipmentSlot.mainHand) == 0, 5f, "the owner to see the staff empty");
            int before = ServerProjectileCount();

            clientPlayer.GetComponent<AbilityUser>().RequestActivateItem(EquipmentSlot.mainHand, 0);
            yield return Wait(0.6f);

            Assert.AreEqual(0, serverEquipment.GetCharges(EquipmentSlot.mainHand), "An empty staff stays empty.");
            Assert.AreEqual(before, ServerProjectileCount(), "A fourth request launches nothing.");
        }

        [UnityTest]
        public IEnumerator AStaffsCharges_AreVisibleToItsOwnerOnly()
        {
            Character hostsPlayer = SpawnOnServer(PlayerPrefab, Vector3.zero, host.LocalClientId);
            hostsPlayer.GetComponent<Equipment>().Equip(EquipmentSlot.mainHand, staff, 800);
            Character clientsPlayer = SpawnOnServer(PlayerPrefab, new Vector3(5f, 0f, 0f), ClientId);
            clientsPlayer.GetComponent<Equipment>().Equip(EquipmentSlot.mainHand, staff, 801);
            Character clientCopyOfHosts = null;
            Character clientCopyOfOwn = null;
            yield return ClientCopyOf(hostsPlayer, c => clientCopyOfHosts = c);
            yield return ClientCopyOf(clientsPlayer, c => clientCopyOfOwn = c);
            yield return Until(() => clientCopyOfOwn.GetComponent<Equipment>().GetCharges(EquipmentSlot.mainHand) == 3, 5f, "the owner to be told");
            yield return Until(() => clientCopyOfHosts.GetComponent<Equipment>().Get(EquipmentSlot.mainHand) == staff, 5f, "the client to see the host's staff equipped");
            yield return Wait(0.3f);

            Assert.AreEqual(3, hostsPlayer.GetComponent<Equipment>().GetCharges(EquipmentSlot.mainHand), "The server knows.");
            Assert.AreEqual(-1, clientCopyOfHosts.GetComponent<Equipment>().GetCharges(EquipmentSlot.mainHand), "A client that does not control the staff is not told its charges.");
        }

        // ----- A melee combo, from the owning client's side -----

        [UnityTest]
        public IEnumerator AnOwnersMeleePress_ShowsTheSwingAtOnce_AndTheServerConfirmsItWithoutReplaying()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, melee);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            AttackLog log = new AttackLog(clientPlayer.GetComponent<ComboMeleeDelivery>());

            Assert.IsTrue(clientPlayer.GetComponent<AbilityUser>().RequestActivate(melee));

            Assert.AreEqual(1, log.Started.Count, "The swing shows on the press, before the server has answered.");
            Assert.AreEqual(1, log.Started[0].Attack);
            Assert.AreEqual(0f, log.Started[0].SecondsIn);

            yield return Until(() => player.GetComponent<ComboMeleeDelivery>().CurrentAttack == 1, 5f, "the server to start the attack");
            yield return Wait(0.5f);
            Assert.AreEqual(1, log.Started.Count, "The server's confirmation must not start the swing a second time.");
            Assert.AreEqual(0, log.Cancelled);
        }

        [UnityTest]
        public IEnumerator APressTheServerRefuses_IsTakenBackByTheOwner()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character player = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId, melee);
            Character clientPlayer = null;
            yield return ClientCopyOf(player, c => clientPlayer = c);
            yield return Wait(0.3f);
            AttackLog log = new AttackLog(clientPlayer.GetComponent<ComboMeleeDelivery>());

            // The owner has not heard of the death yet, so it predicts a swing that the server will refuse.
            player.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            clientPlayer.GetComponent<AbilityUser>().RequestActivate(melee);
            Assert.AreEqual(1, log.Started.Count, "The owner showed the swing it guessed.");

            yield return Until(() => log.Cancelled == 1, 5f, "the server's refusal to take the swing back");
            Assert.AreEqual(0, player.GetComponent<ComboMeleeDelivery>().CurrentAttack);
        }

        [UnityTest]
        public IEnumerator AnotherPlayersSwing_ReachesTheClient_WithTheSecondsAlreadyElapsed()
        {
            AbilityDefinition melee = Load(MeleeAttack);
            Character hostsPlayer = SpawnOnServer(PlayerPrefab, Vector3.zero, host.LocalClientId, melee);
            Character clientCopyOfHosts = null;
            yield return ClientCopyOf(hostsPlayer, c => clientCopyOfHosts = c);
            yield return Wait(0.3f);
            AttackLog log = new AttackLog(clientCopyOfHosts.GetComponent<ComboMeleeDelivery>());

            Assert.IsTrue(hostsPlayer.GetComponent<AbilityUser>().TryActivateOnServer(melee));

            yield return Until(() => log.Started.Count == 1, 5f, "the client to hear about the swing");
            Assert.AreEqual(1, log.Started[0].Attack);
            Assert.GreaterOrEqual(log.Started[0].SecondsIn, 0f);
            Assert.Less(log.Started[0].SecondsIn, 1f, "The news is a little late, not a whole swing late.");
            Assert.AreEqual(0, log.Cancelled);
        }
    }
}
