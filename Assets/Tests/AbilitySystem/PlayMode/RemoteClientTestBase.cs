using SimpleRPG.HitDetection;
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
    public abstract class RemoteClientTestBase
    {
        /// <summary>
        /// One-way packet delay the loopback connection simulates, in milliseconds. Zero leaves the connection as it is.
        /// </summary>
        protected virtual int SimulatedDelayMs => 0;

        /// <summary>
        /// Random variation added to or taken from the delay on each packet, in milliseconds.
        /// </summary>
        protected virtual int SimulatedJitterMs => 0;

        protected Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulator Simulator { get; private set; }

        /// <summary>
        /// Delays every packet either side sends, so a host and client in one process behave like machines a real network apart.
        /// </summary>
        private void SimulateLatency()
        {
            if (SimulatedDelayMs <= 0)
            {
                return;
            }

            Simulator = new GameObject("TestNetworkSimulator").AddComponent<Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulator>();
            Simulator.ConnectionPreset = Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulatorPreset.Create(
                "Test latency", "Fixed test delay.", SimulatedDelayMs, SimulatedJitterMs, 0, 0);
        }

        protected sealed class AttackLog
        {
            public readonly List<(int Attack, float SecondsIn)> Started = new List<(int, float)>();
            public int Cancelled;

            public AttackLog(MeleeDeliveryBase melee)
            {
                melee.MeleeAttackStarted += (attack, seconds) => Started.Add((attack, seconds));
                melee.MeleeAttackCancelled += () => Cancelled++;
            }
        }
        protected const string Firebolt = "Assets/Abilities/Abilities/Firebolt.asset";
        protected const string Icebolt = "Assets/Abilities/Abilities/Icebolt.asset";
        protected const string MeleeAttack = "Assets/Abilities/Abilities/MeleeAttack.asset";
        protected const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        protected const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        protected const int StartingHealth = 100;

        protected GameObject hostObject;
        protected GameObject clientObject;
        protected NetworkManager host;
        protected NetworkManager client;
        protected readonly List<GameObject> spawned = new List<GameObject>();
        protected readonly List<Object> created = new List<Object>();
        protected DefinitionRegistry previousRegistry;
        protected Item staff;
        protected AbilityDefinition staffBolt;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameplayServices.Reset();
            AbilityServices.Reset();

            staffBolt = BuildStaffBolt();
            staff = BuildStaff(staffBolt);
            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = ItemTestRegistry.With(new[] { staff });

            SimulateLatency();

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

            if (Simulator != null) Object.Destroy(Simulator.gameObject);
            if (clientObject != null) Object.Destroy(clientObject);
            if (hostObject != null) Object.Destroy(hostObject);
            DefinitionRegistry.Instance = previousRegistry;
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

        protected static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "Timed out waiting for " + what + ".");
        }

        protected static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>The prefabs both sides need to know. The client builds characters without the player-only parts that want a camera and a HUD.</summary>
        protected void RegisterPrefabs(NetworkManager manager, bool strip)
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

        protected static void StripPlayerOnlyParts(GameObject instance)
        {
            foreach (Type type in new[] { typeof(RegisterWithUI), typeof(FirstPersonCharacterController), typeof(AnimatableCharacter) })
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

        protected static AbilityDefinition Load(string path)
        {
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            Assert.IsNotNull(ability, path);
            return ability;
        }

        protected static void Set(Item item, string field, object value) =>
            typeof(Item).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, value);

        /// <summary>A bolt with a short cooldown and a fixed 10 damage, for the staff to hold.</summary>
        protected AbilityDefinition BuildStaffBolt()
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

        protected Item BuildStaff(AbilityDefinition bolt)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            created.Add(item);
            ItemAbilityGrant grant = new ItemAbilityGrant { Ability = bolt, ChargeCost = 1 };
            Set(item, "startingCharges", 3);
            Set(item, "grantedAbilities", new List<ItemAbilityGrant> { grant });
            return item;
        }

        /// <summary>Creates a character on the server. When an owner is given, that client controls it.</summary>
        protected Character SpawnOnServer(string path, Vector3 position, ulong? owner = null, params AbilityDefinition[] graphAbilities)
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
        protected IEnumerator ClientCopyOf(Character serverCharacter, Action<Character> found)
        {
            ulong id = serverCharacter.NetworkObjectId;
            yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(id), 10f, "the client to receive object " + id);
            found(client.SpawnManager.SpawnedObjects[id].GetComponent<Character>());
        }

        protected static Projectile ServerProjectile() => Object.FindObjectsByType<Projectile>().FirstOrDefault(p => p.IsServer);

        protected static int ServerProjectileCount() => Object.FindObjectsByType<Projectile>().Count(p => p.IsServer);

        protected ulong ClientId => client.LocalClientId;
    }
}
