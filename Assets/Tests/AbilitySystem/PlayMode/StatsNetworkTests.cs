using SimpleRPG.HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Stats on a real host: the first person controller moves at the server's resolved Movement Speed, Maximum Health comes from the stat sheet,
    /// a status modifier changes both for as long as it is active, and an enemy with a direct sheet registers and cleans up its stats.
    /// </summary>
    public class StatsNetworkTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";

        private GameObject managerObject;
        private GameObject cameraObject;
        private GameObject floorObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObject.transform.position = new Vector3(0f, -1.13f, 0f);
            floorObject.transform.localScale = new Vector3(200f, 1f, 200f);
            cameraObject = new GameObject("TestMainCamera", typeof(Camera)) { tag = "MainCamera" };

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            foreach (GameObject instance in spawned)
            {
                if (instance != null) Object.Destroy(instance);
            }

            spawned.Clear();
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (floorObject != null) Object.Destroy(floorObject);
            if (managerObject != null) Object.Destroy(managerObject);
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        private Character Spawn(string path, Vector3 position, bool player, StatProfile profile = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            foreach (System.Type type in new[] { typeof(RegisterWithUI), typeof(AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            if (!player)
            {
                Component controller = instance.GetComponent<FirstPersonCharacterController>();
                if (controller != null) Object.DestroyImmediate(controller);
            }

            Character character = instance.GetComponent<Character>();
            if (profile != null)
            {
                typeof(Character).GetField("statProfile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(character, profile);
            }

            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        /// <summary>
        /// Asks to move forward every frame for a while and reports the speed the server actually moved the character at, in units per second.
        /// The first frames are skipped: the controller starts a frame late and a request to a host's own server arrives a frame after it is sent.
        /// </summary>
        private static IEnumerator MeasureSpeed(Character player, System.Action<float> speed)
        {
            yield return null;
            FirstPersonCharacterController controller = player.GetComponent<FirstPersonCharacterController>();
            MethodInfo move = typeof(FirstPersonCharacterController).GetMethod("MoveCharacterServerRPC", BindingFlags.Instance | BindingFlags.NonPublic);

            for (int i = 0; i < 5; i++)
            {
                move.Invoke(controller, new object[] { player.transform.forward });
                yield return null;
            }

            Vector3 before = player.transform.position;
            float start = Time.time;
            for (int i = 0; i < 30; i++)
            {
                move.Invoke(controller, new object[] { player.transform.forward });
                yield return null;
            }

            Vector3 delta = player.transform.position - before;
            delta.y = 0f;
            speed(delta.magnitude / (Time.time - start));
        }

        private StatProfile DirectProfile(double health, double speed, double physicalPower)
        {
            StatProfile profile = ScriptableObject.CreateInstance<StatProfile>();
            profile.Configure(StatOwnership.Direct, new[]
            {
                new StatRuleData { statKey = DerivedStats.MaximumHealth.Key, baseValue = health },
                new StatRuleData { statKey = DerivedStats.MovementSpeed.Key, baseValue = speed },
                new StatRuleData { statKey = DerivedStats.PhysicalPower.Key, baseValue = physicalPower },
            });
            created.Add(profile);
            return profile;
        }

        private StatusDefinition Slow(double factor)
        {
            StatusDefinition status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.AssignNewId();
            status.Configure("Slow", 30f, 0f, statModifiers: new[] { new StatusStatModifier { attributeKey = DerivedStats.MovementSpeed.Key, layer = ModifierLayer.Multiply, value = factor } });
            created.Add(status);
            return status;
        }

        [UnityTest]
        public IEnumerator Player_ResolvesBaselineStatsFromItsProfile()
        {
            Character player = Spawn(PlayerPrefab, Vector3.zero, player: true);

            Assert.AreEqual(100, player.MaxHealth);
            Assert.AreEqual(100, player.Health);
            Assert.AreEqual(4f, player.MovementSpeed, 1e-4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Movement_UsesTheResolvedSpeed_AndAModifierChangesIt()
        {
            Character player = Spawn(PlayerPrefab, Vector3.zero, player: true);
            float normal = 0f;
            yield return MeasureSpeed(player, d => normal = d);
            Assert.AreEqual(4f, normal, 0.8f, "The player moves at the baseline speed.");

            ModifierHandle handle = player.Attributes.AddModifier(AttributeModifier.Factor(DerivedStats.MovementSpeed, 0.25, default));
            Assert.AreEqual(1f, player.MovementSpeed, 1e-4f);
            float slowed = 0f;
            yield return MeasureSpeed(player, d => slowed = d);
            Assert.AreEqual(1f, slowed, 0.3f, "A quarter of the speed.");

            player.Attributes.RemoveModifier(handle);
            float restored = 0f;
            yield return MeasureSpeed(player, d => restored = d);
            Assert.AreEqual(4f, restored, 0.8f);
        }

        [UnityTest]
        public IEnumerator LiveStatus_ChangesTheServerResolvedMovementSpeed_AndEndsWithTheStatus()
        {
            Character player = Spawn(PlayerPrefab, Vector3.zero, player: true);
            StatusDefinition slow = Slow(0.5);
            AbilityServices.Current.Statuses.Apply(new StatusApplication(player.Id, slow, default, default, 1d));

            Assert.AreEqual(2f, player.MovementSpeed, 1e-4f);
            float slowed = 0f;
            yield return MeasureSpeed(player, d => slowed = d);

            AbilityServices.Current.Statuses.Dispel(player.Id, slow);
            Assert.AreEqual(4f, player.MovementSpeed, 1e-4f);
            float normal = 0f;
            yield return MeasureSpeed(player, d => normal = d);

            Assert.AreEqual(2f, slowed, 0.5f, "Half the speed while the status is active.");
            Assert.AreEqual(4f, normal, 0.8f, "Full speed once it ends.");
        }

        [UnityTest]
        public IEnumerator MaximumHealth_ChangeClampsCurrentHealth_NeverRaisesIt_AndRespawnUsesIt()
        {
            Character player = Spawn(PlayerPrefab, Vector3.zero, player: true);
            player.Attributes.SetBase(PrimaryAttributes.Constitution, 5);
            Assert.AreEqual(50, player.MaxHealth);
            Assert.AreEqual(50, player.Health, "Current health comes down with a lower maximum.");

            player.Attributes.SetBase(PrimaryAttributes.Constitution, 20);
            Assert.AreEqual(200, player.MaxHealth);
            Assert.AreEqual(50, player.Health, "A higher maximum does not heal.");

            player.TakeDamage(new DamageInfo { Damage = 1000 }, BodyLocation.Torso);
            Assert.IsFalse(player.IsAlive);
            Assert.IsTrue(player.RespawnOnServer());
            Assert.AreEqual(200, player.Health, "A new life starts at the resolved maximum.");
            Assert.IsNull(typeof(AttributeSet).GetProperty("Health"), "Health is not stored in the attribute set.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Enemy_WithADirectSheet_RegistersItsStats_AndCleansUpOnDespawn()
        {
            StatProfile profile = DirectProfile(health: 75, speed: 2.5, physicalPower: 12);
            Character enemy = Spawn(TargetPrefab, new Vector3(0f, 0f, 3f), player: false, profile);
            SourceHandle source = SourceHandle.FromEntity(enemy.Id);

            Assert.AreEqual(75, enemy.MaxHealth);
            Assert.AreEqual(75, enemy.Health);
            Assert.IsTrue(services.Attributes.TryGetAttributes(source, out IAttributeReader reader), "The same registry the abilities read holds the enemy's stats.");
            Assert.AreSame(enemy.Attributes, reader);
            Assert.AreEqual(12d, reader.Get(DerivedStats.PhysicalPower));
            Assert.AreEqual(2.5d, reader.Get(DerivedStats.MovementSpeed));
            Assert.AreEqual(0d, reader.Get(PrimaryAttributes.Strength), "No core attributes were needed.");

            enemy.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Assert.IsFalse(services.Attributes.TryGetSet(source, out AttributeSet _), "The stats are removed with the enemy.");
        }

        [UnityTest]
        public IEnumerator Enemy_WithNoProfile_KeepsItsAuthoredHealthAndCoreAttributes()
        {
            Character enemy = Spawn(TargetPrefab, new Vector3(0f, 0f, 3f), player: false);

            Assert.AreEqual(100, enemy.MaxHealth);
            Assert.AreEqual(10d, enemy.Attributes.Get(PrimaryAttributes.Strength));
            Assert.AreEqual(0f, enemy.MovementSpeed);
            yield return null;
        }
    }
}
