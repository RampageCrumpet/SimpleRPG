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
    public class RemoteClientNetworkTests
    {
        private const string Firebolt = "Assets/Abilitities/Abillities/Firebolt.asset";
        private const string Icebolt = "Assets/Abilitities/Abillities/Icebolt.asset";
        private const string MeleeAttack = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;

        private GameObject hostObject;
        private GameObject clientObject;
        private NetworkManager host;
        private NetworkManager client;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private ItemRegistry previousRegistry;
        private Item staff;
        private AbilityDefinition staffBolt;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameplayServices.Reset();
            AbilityServices.Reset();

            staffBolt = BuildStaffBolt();
            staff = BuildStaff(staffBolt);
            previousRegistry = ItemRegistry.Instance;
            ItemRegistry.Instance = ItemRegistry.Create(new[] { staff });

            hostObject = new GameObject("TestHost");
            host = hostObject.AddComponent<NetworkManager>();
            UnityTransport hostTransport = hostObject.AddComponent<UnityTransport>();
            host.NetworkConfig = new NetworkConfig { NetworkTransport = hostTransport };
            RegisterPrefabs(host, strip: false);
            Assert.IsTrue(host.StartHost(), "The host should start.");
            yield return null;

            clientObject = new GameObject("TestClient");
            client = clientObject.AddComponent<NetworkManager>();
            UnityTransport clientTransport = clientObject.AddComponent<UnityTransport>();
            client.NetworkConfig = new NetworkConfig { NetworkTransport = clientTransport };
            RegisterPrefabs(client, strip: true);
            Assert.AreSame(host, NetworkManager.Singleton, "The host stays the game's network manager.");
            Assert.IsTrue(client.StartClient(), "The client should start.");

            yield return Until(() => client.IsConnectedClient, 10f, "the client to connect");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (client != null && client.IsListening)
            {
                client.Shutdown();
                yield return Until(() => !client.IsListening, 5f, "the client to stop");
            }

            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
            {
                if (projectile != null) Object.Destroy(projectile.gameObject);
            }

            foreach (GameObject instance in spawned)
            {
                if (instance != null) Object.Destroy(instance);
            }

            spawned.Clear();

            if (host != null && host.IsListening)
            {
                host.Shutdown();
                yield return Until(() => !host.IsListening, 5f, "the host to stop");
            }

            if (clientObject != null) Object.Destroy(clientObject);
            if (hostObject != null) Object.Destroy(hostObject);
            ItemRegistry.Instance = previousRegistry;
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        // ----- Setting up the world -----

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "Timed out waiting for " + what + ".");
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>The prefabs both sides need to know. The client builds characters without the player-only parts that want a camera and a HUD.</summary>
        private void RegisterPrefabs(NetworkManager manager, bool strip)
        {
            GameObject projectile = ((LaunchProjectileConfig)AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt).Nodes
                .Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            foreach (string path in new[] { PlayerPrefab, TargetPrefab })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                manager.AddNetworkPrefab(prefab);
                if (strip) manager.PrefabHandler.AddHandler(prefab, new StrippedCharacterHandler(prefab, spawned));
            }

            manager.AddNetworkPrefab(projectile);
        }

        private static void StripPlayerOnlyParts(GameObject instance)
        {
            foreach (Type type in new[] { typeof(RegisterWithUI), typeof(FirstPersonCharacterController), typeof(ThirdPersonCharacterController), typeof(AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }
        }

        private sealed class StrippedCharacterHandler : INetworkPrefabInstanceHandler
        {
            private readonly GameObject prefab;
            private readonly List<GameObject> tracked;

            public StrippedCharacterHandler(GameObject prefab, List<GameObject> tracked)
            {
                this.prefab = prefab;
                this.tracked = tracked;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                GameObject instance = Object.Instantiate(prefab, position, rotation);
                StripPlayerOnlyParts(instance);
                tracked.Add(instance);
                return instance.GetComponent<NetworkObject>();
            }

            public void Destroy(NetworkObject networkObject)
            {
                if (networkObject != null) Object.Destroy(networkObject.gameObject);
            }
        }

        private static AbilityDefinition Load(string path)
        {
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            Assert.IsNotNull(ability, path);
            return ability;
        }

        private static void Set(Item item, string field, object value) =>
            typeof(Item).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, value);

        /// <summary>A bolt with a short cooldown and a fixed 10 damage, for the staff to hold.</summary>
        private AbilityDefinition BuildStaffBolt()
        {
            GameObject projectile = ((LaunchProjectileConfig)Load(Firebolt).Nodes.Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            created.Add(ability);
            ability.name = "RemoteStaffBolt";
            ability.SetDetails("Staff Bolt", 0.2f);
            if (ability.Nodes.Count == 0) ability.AddNode(NodeTypes.Activate, null, "activate");
            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = projectile }, "launch");
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string amount = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 10f }, "amount");
            ability.Connect("activate", BuiltInNodes.Next, launch, BuiltInNodes.In);
            ability.Connect(launch, BuiltInNodes.OnImpact, damage, BuiltInNodes.In);
            ability.Connect(victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            Assert.IsTrue(ability.Compile().Success, ability.Compile().Report.ToString());
            return ability;
        }

        private Item BuildStaff(AbilityDefinition bolt)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            created.Add(item);
            ItemAbilityGrant grant = new ItemAbilityGrant { Ability = bolt, ChargeCost = 1 };
            Set(item, "startingCharges", 3);
            Set(item, "grantedAbilities", new List<ItemAbilityGrant> { grant });
            return item;
        }

        /// <summary>Creates a character on the server. When an owner is given, that client controls it.</summary>
        private Character SpawnOnServer(string path, Vector3 position, ulong? owner = null, params AbilityDefinition[] graphAbilities)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);
            StripPlayerOnlyParts(instance);

            Character character = instance.GetComponent<Character>();
            if (graphAbilities.Length > 0)
            {
                typeof(Character).GetField("graphAbilities", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(character, graphAbilities.ToList());
            }

            character.Initialize();
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            if (owner.HasValue) networkObject.SpawnWithOwnership(owner.Value);
            else networkObject.Spawn();
            return character;
        }

        /// <summary>The client's own copy of a server object, once it has arrived.</summary>
        private IEnumerator ClientCopyOf(Character serverCharacter, Action<Character> found)
        {
            ulong id = serverCharacter.NetworkObjectId;
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(id), 10f, "the client to receive object " + id);
            found(client.SpawnManager.SpawnedObjects[id].GetComponent<Character>());
        }

        private static Projectile ServerProjectile() => Object.FindObjectsByType<Projectile>().FirstOrDefault(p => p.IsServer);

        private static int ServerProjectileCount() => Object.FindObjectsByType<Projectile>().Count(p => p.IsServer);

        private ulong ClientId => client.LocalClientId;

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

            clientTarget.TakeDamage(new DamageInfo { Damage = 50 }, BodyLocation.Torso);
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

        private sealed class AttackLog
        {
            public readonly List<(int Attack, float SecondsIn)> Started = new List<(int, float)>();
            public int Cancelled;

            public AttackLog(MeleeDeliveryBase melee)
            {
                melee.MeleeAttackStarted += (attack, seconds) => Started.Add((attack, seconds));
                melee.MeleeAttackCancelled += () => Cancelled++;
            }
        }

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
