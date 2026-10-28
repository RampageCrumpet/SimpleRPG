using SimpleRPG.HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// The cursed amulet on a real host: while worn it raises Strength and Constitution, and when its wearer dies it spawns one creature where they
    /// died. The grants, the death rule, the spawn, the equipment and the combat pipeline are all the real ones; the creature is a test prefab.
    /// </summary>
    public class CursedAmuletNetworkTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string DemonPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;
        private const int BaseStat = 10;

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;
        private DefinitionRegistry previousRegistry;
        private Item amulet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            // Summon: spawn a creature where the ability started. A death rule starts its ability at the place of death.
            AbilityDefinition summon = ScriptableObject.CreateInstance<AbilityDefinition>();
            created.Add(summon);
            summon.name = "SummonDemon";
            summon.SetDetails("Summon Demon", 1f);
            if (summon.Nodes.Count == 0) summon.AddNode(NodeTypes.Activate, null, "activate");
            string spawn = summon.AddNode(NodeTypes.SpawnEntity, new SpawnEntityConfig { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemonPrefab) }, "spawn");
            summon.Connect("activate", BuiltInNodes.Next, spawn, BuiltInNodes.In);
            Assert.IsTrue(summon.Compile().Success, summon.Compile().Report.ToString());

            GameplayRuleDefinition curse = ScriptableObject.CreateInstance<GameplayRuleDefinition>();
            created.Add(curse);
            curse.name = "Curse";
            curse.Configure(summon, 1f, RuleTrigger.OwnerDeath);

            amulet = ScriptableObject.CreateInstance<Item>();
            created.Add(amulet);
            typeof(Item).GetField("rules", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(amulet, new List<GameplayRuleDefinition> { curse });
            typeof(Item).GetField("grants", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(amulet, new List<GrantSpec>
            {
                new GrantSpec { AttributeKey = PrimaryAttributes.Strength.Key, Layer = ModifierLayer.Add, Value = 3 },
                new GrantSpec { AttributeKey = PrimaryAttributes.Constitution.Key, Layer = ModifierLayer.Add, Value = 2 },
            });
            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = ItemTestRegistry.With(new[] { amulet });

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
            foreach (GameObject instance in spawned)
            {
                if (instance != null) Object.Destroy(instance);
            }

            spawned.Clear();

            // Creatures the amulet spawned belong to the network, not to this test.
            foreach (Character character in Object.FindObjectsByType<Character>())
            {
                if (character != null) Object.Destroy(character.gameObject);
            }

            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (managerObject != null) Object.Destroy(managerObject);
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

        private Character SpawnWearer(Vector3 position)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), position, Quaternion.identity);
            spawned.Add(instance);
            foreach (System.Type type in new[] { typeof(LocalPlayerControls), typeof(FirstPersonCharacterController), typeof(AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private static Equipment EquipmentOf(Character character) => character.GetComponent<Equipment>();

        private static void Kill(Character character) =>
            character.TakeDamage(new DamageInfo { Damage = StartingHealth * 5 }, BodyLocation.Torso);

        /// <summary>
        /// The characters in the world that are not among those given.
        /// </summary>
        private static List<Character> Others(params Character[] known) =>
            Object.FindObjectsByType<Character>().Where(c => !known.Contains(c)).ToList();

        // ----- The grants -----

        [UnityTest]
        public IEnumerator WearingTheAmulet_RaisesStrengthAndConstitution()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            Assert.AreEqual(BaseStat, wearer.Stats.Attributes.Get(PrimaryAttributes.Strength));

            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);

            Assert.AreEqual(BaseStat + 3, wearer.Stats.Attributes.Get(PrimaryAttributes.Strength));
            Assert.AreEqual(BaseStat + 2, wearer.Stats.Attributes.Get(PrimaryAttributes.Constitution));
            Assert.AreEqual(BaseStat, wearer.Stats.Attributes.Get(PrimaryAttributes.Dexterity), "Other stats are untouched.");
        }

        [UnityTest]
        public IEnumerator TakingTheAmuletOff_RestoresTheStats()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);

            EquipmentOf(wearer).Unequip(EquipmentSlot.neck);

            Assert.AreEqual(BaseStat, wearer.Stats.Attributes.Get(PrimaryAttributes.Strength));
            Assert.AreEqual(BaseStat, wearer.Stats.Attributes.Get(PrimaryAttributes.Constitution));
            Assert.AreEqual(0, wearer.Stats.Attributes.ModifierCount);
        }

        [UnityTest]
        public IEnumerator TheCharactersAttributes_AreWhatAbilitiesRead()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);

            Assert.IsTrue(services.Attributes.TryGetAttributes(SourceHandle.FromEntity(wearer.Id), out IAttributeReader reader));

            Assert.AreEqual(BaseStat + 3, reader.Get(PrimaryAttributes.Strength));
        }

        // ----- The curse -----

        [UnityTest]
        public IEnumerator TheWearersDeath_SpawnsExactlyOneCreature_WhereTheyDied()
        {
            Character wearer = SpawnWearer(new Vector3(4f, 0f, 6f));
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);
            List<EntitySpawnedEvent> spawns = new List<EntitySpawnedEvent>();
            services.Events.Subscribe<EntitySpawnedEvent>(spawns.Add);
            Vector3 deathPosition = wearer.transform.position;

            Kill(wearer);
            yield return null;

            Assert.IsFalse(wearer.IsAlive);
            List<Character> creatures = Others(wearer);
            Assert.AreEqual(1, creatures.Count, "Exactly one creature appears.");
            Assert.AreEqual(1, spawns.Count);
            Assert.Less(Vector3.Distance(creatures[0].transform.position, deathPosition), 0.01f, "It appears at the death position.");
            Assert.IsTrue(creatures[0].IsAlive);
            Assert.AreEqual(SourceHandle.FromItem(new ItemInstanceId(900)), spawns[0].Source);
            Assert.AreEqual(wearer.Id, spawns[0].Instigator);
            Assert.AreEqual(creatures[0].Id, spawns[0].Entity);
        }

        [UnityTest]
        public IEnumerator HittingTheCorpse_SpawnsNothingMore()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);
            Kill(wearer);
            yield return null;

            for (int i = 0; i < 5; i++)
            {
                Kill(wearer);
                yield return null;
            }

            Assert.AreEqual(1, Others(wearer).Count);
        }

        [UnityTest]
        public IEnumerator WithoutTheAmulet_NothingAppears()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;

            Kill(wearer);
            yield return null;

            Assert.IsEmpty(Others(wearer));
        }

        [UnityTest]
        public IEnumerator AnAmuletTakenOffBeforeDeath_SpawnsNothing()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);
            EquipmentOf(wearer).Unequip(EquipmentSlot.neck);

            Kill(wearer);
            yield return null;

            Assert.IsEmpty(Others(wearer));
        }

        [UnityTest]
        public IEnumerator AnotherCharactersDeath_DoesNotSpringTheCurse()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            Character bystander = SpawnWearer(new Vector3(10f, 0f, 0f));
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);

            Kill(bystander);
            yield return null;

            Assert.IsEmpty(Others(wearer, bystander));
        }

        [UnityTest]
        public IEnumerator TheCreature_KeepsLivingWhenTheAmuletComesOff()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);
            Kill(wearer);
            yield return null;
            Character creature = Others(wearer).Single();

            EquipmentOf(wearer).Unequip(EquipmentSlot.neck);
            yield return null;
            yield return null;

            Assert.IsTrue(creature != null && creature.gameObject.activeInHierarchy, "Nothing ties the creature to the amulet.");
            Assert.IsTrue(creature.IsAlive);
            Assert.AreEqual(1, Others(wearer).Count);
        }

        [UnityTest]
        public IEnumerator AfterARevival_ASecondDeathSpawnsASecondCreature()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);
            Kill(wearer);
            yield return null;
            Assert.AreEqual(1, Others(wearer).Count);

            Assert.IsTrue(services.Combat.Revive(wearer.Id, 50));
            yield return null;
            Kill(wearer);
            yield return null;

            Assert.AreEqual(2, Others(wearer).Count, "Each death spawns its own creature.");
        }

        [UnityTest]
        public IEnumerator TheStatsAreStillGranted_ForAsLongAsTheAmuletIsWorn_EvenOnTheCorpse()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);

            Kill(wearer);
            yield return null;

            Assert.AreEqual(BaseStat + 3, wearer.Stats.Attributes.Get(PrimaryAttributes.Strength), "A grant lasts as long as the item is worn, not as long as the wearer lives.");
        }

        [UnityTest]
        public IEnumerator ACharacterThatLeaves_LetsGoOfItsAttributes()
        {
            Character wearer = SpawnWearer(Vector3.zero);
            yield return null;
            EquipmentOf(wearer).Equip(EquipmentSlot.neck, amulet, 900);
            SourceHandle source = SourceHandle.FromEntity(wearer.Id);
            Assert.IsTrue(services.Attributes.TryGetAttributes(source, out _));

            wearer.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Assert.IsFalse(services.Attributes.TryGetAttributes(source, out _));
        }
    }
}
