using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
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
    /// An item is the same item wherever it goes. One armor instance that rolled an affix is carried through the whole chain on a host with a
    /// real player character: inventory, worn, inventory, the ground, inventory, worn again. At every step it is the same object under the same
    /// instance id with the same rolled affix, and the affix's effect is on the wearer exactly while it is worn. Armor with a world form will
    /// travel this way, so the guarantee is held here, with the real inventory, equipment, drop and pickup code.
    /// </summary>
    public class ItemTransferIdentityTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string PickupSource = "Assets/Items/LootItems/Golden Bell.asset";
        private const float Rolled = 2f;

        private GameObject managerObject;
        private GameObject cameraObject;
        private GameObject floorObject;
        private NetworkManager manager;
        private DefinitionRegistry previousRegistry;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void ClearLocalPlayerState() => LocalPlayer.Reset();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LocalPlayer.Reset();
            GameplayServices.Reset();
            AbilityServices.Reset();
            previousRegistry = DefinitionRegistry.Instance;

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

            foreach (WorldItemPickup pickup in Object.FindObjectsByType<WorldItemPickup>())
            {
                Object.Destroy(pickup.gameObject);
            }

            spawned.Clear();
            DefinitionRegistry.Instance = previousRegistry;
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (floorObject != null) Object.Destroy(floorObject);
            if (managerObject != null) Object.Destroy(managerObject);
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        private static void Set(object target, string field, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (info != null)
                {
                    info.SetValue(target, value);
                    return;
                }
            }

            throw new System.ArgumentException(field);
        }

        // A chest piece that always rolls one affix worth exactly +2 Armor Rating, and has the world form of an existing pickup.
        private ArmorData MakeArmor()
        {
            AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
            created.Add(affix);
            affix.EnsureId();
            Set(affix, "effects", new List<AffixEffect> { new StatModifierEffect("Armor Rating", DerivedStats.ArmorRating.Key, ModifierLayer.Add, Rolled, Rolled) });

            AffixPool pool = ScriptableObject.CreateInstance<AffixPool>();
            created.Add(pool);
            Set(pool, "affixes", new List<AffixDefinition> { affix });
            Set(pool, "minimumCount", 1);
            Set(pool, "maximumCount", 1);

            ArmorData armor = ScriptableObject.CreateInstance<ArmorData>();
            created.Add(armor);
            armor.EnsureId();
            Set(armor, "slot", EquipmentSlot.chest);
            Set(armor, "<ItemSize>k__BackingField", new Vector2Int(2, 3));
            Set(armor, "affixPool", pool);
            armor.WorldPrefab = AssetDatabase.LoadAssetAtPath<Item>(PickupSource).WorldPrefab;
            Assert.IsNotNull(armor.WorldPrefab, "The Golden Bell's world form is borrowed for the test.");

            // The live registry plus these, so clients could look them up and the affix can roll.
            DefinitionRegistry registry = previousRegistry != null ? Object.Instantiate(previousRegistry) : ScriptableObject.CreateInstance<DefinitionRegistry>();
            created.Add(registry);
            List<GameplayDefinition> all = previousRegistry != null ? previousRegistry.Definitions.ToList() : new List<GameplayDefinition>();
            all.Add(armor);
            all.Add(affix);
            RegistryReport report = registry.Rebuild(all);
            Assert.IsTrue(report.Success, string.Join("; ", report.Errors));
            DefinitionRegistry.Instance = registry;
            return armor;
        }

        private Character SpawnPlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(prefab, $"Could not load {PlayerPrefab}.");

            GameObject instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            spawned.Add(instance);
            foreach (System.Type type in new[] { typeof(LocalPlayerControls), typeof(AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            instance.tag = "Player";
            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private static double ArmorRating(Character player) => player.Stats.Attributes.Get(DerivedStats.ArmorRating);

        [UnityTest]
        public IEnumerator OneItem_IsTheSameItem_ThroughInventoryWornGroundAndBack()
        {
            ArmorData armor = MakeArmor();
            Character player = SpawnPlayer();
            _ = AbilityServices.Current;
            yield return null;
            Inventory inventory = player.GetComponent<Inventory>();
            Equipment equipment = player.GetComponent<Equipment>();

            // It enters the world as a new instance and rolls its affix once.
            Assert.IsTrue(inventory.AddItemInstance(armor));
            Item piece = inventory.GetItems().Select(pair => pair.item).Single();
            var instanceId = inventory.GetInstanceId(piece);
            ItemAffixes rolled = piece.Affixes;
            Assert.AreEqual(1, rolled.Count, "It rolled one affix.");
            Assert.AreEqual((int)(Rolled * AffixRoll.Scale), rolled[0].ValuesMilli[0]);
            Assert.AreEqual(0d, ArmorRating(player), "Carried, it protects nothing.");

            // Worn.
            Assert.IsTrue(equipment.RequestEquip(piece));
            yield return null;
            Assert.AreSame(piece, equipment.Get(EquipmentSlot.chest), "Worn: the same object.");
            Assert.AreEqual(instanceId.Value, equipment.GetInstanceId(EquipmentSlot.chest), "Under the same instance id.");
            Assert.AreEqual(rolled, equipment.Get(EquipmentSlot.chest).Affixes);
            Assert.IsFalse(inventory.Contains(piece), "An item is in one place.");
            Assert.AreEqual(Rolled, ArmorRating(player), 1e-6, "Its affix is on the wearer while it is worn.");

            // Back in the inventory.
            Assert.IsTrue(equipment.RequestUnequip(EquipmentSlot.chest));
            yield return null;
            Assert.IsTrue(inventory.Contains(piece));
            Assert.AreEqual(instanceId, inventory.GetInstanceId(piece));
            Assert.AreEqual(rolled, piece.Affixes);
            Assert.AreEqual(0d, ArmorRating(player), 1e-6, "And its effect is gone.");

            // On the ground.
            Assert.IsTrue(inventory.RequestDropItem(piece));
            yield return null;
            Assert.IsFalse(inventory.Contains(piece));
            WorldItemPickup pickup = Object.FindFirstObjectByType<WorldItemPickup>();
            Assert.IsNotNull(pickup);
            Assert.AreEqual(rolled, pickup.Affixes, "The pickup shows what the dropped item rolled.");

            // Picked up again: the very same item, not a new roll.
            Assert.IsTrue(pickup.TryPickUp(player));
            Item back = inventory.GetItems().Select(pair => pair.item).Single();
            Assert.AreSame(piece, back, "The ground gave back the same object.");
            Assert.AreEqual(instanceId, inventory.GetInstanceId(back), "Under the same instance id.");
            Assert.AreEqual(rolled, back.Affixes, "With the affix it rolled, not a fresh one.");

            // And worn again.
            Assert.IsTrue(equipment.RequestEquip(back));
            yield return null;
            Assert.AreSame(piece, equipment.Get(EquipmentSlot.chest));
            Assert.AreEqual(Rolled, ArmorRating(player), 1e-6);
        }

        [UnityTest]
        public IEnumerator TwoInstancesOfOneItem_KeepTheirOwnIdentities()
        {
            ArmorData armor = MakeArmor();
            Character player = SpawnPlayer();
            yield return null;
            Inventory inventory = player.GetComponent<Inventory>();

            Assert.IsTrue(inventory.AddItemInstance(armor));
            Assert.IsTrue(inventory.AddItemInstance(armor));
            List<Item> pieces = inventory.GetItems().Select(pair => pair.item).ToList();

            Assert.AreEqual(2, pieces.Count);
            Assert.AreNotSame(pieces[0], pieces[1], "Two copies of one asset are two items.");
            Assert.AreNotEqual(inventory.GetInstanceId(pieces[0]), inventory.GetInstanceId(pieces[1]));

            // Dropping one leaves the other alone.
            Assert.IsTrue(inventory.RequestDropItem(pieces[0]));
            yield return null;
            Assert.IsFalse(inventory.Contains(pieces[0]));
            Assert.IsTrue(inventory.Contains(pieces[1]));
        }
    }
}
