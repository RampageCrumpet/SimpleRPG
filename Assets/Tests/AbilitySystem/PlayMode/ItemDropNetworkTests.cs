using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.HitDetection;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Dropping an item on a host with a real player character: it leaves the inventory, lies on the ground as the same instance, and picking
    /// it up gives that instance back. A dead player drops nothing.
    /// </summary>
    public class ItemDropNetworkTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TreasurePath = "Assets/Items/LootItems/Golden Bell.asset";

        private GameObject managerObject;
        private GameObject cameraObject;
        private GameObject floorObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void ClearLocalPlayerState() => LocalPlayer.Reset();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LocalPlayer.Reset();
            GameplayServices.Reset();
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

            foreach (WorldItemPickup pickup in Object.FindObjectsByType<WorldItemPickup>())
            {
                Object.Destroy(pickup.gameObject);
            }

            spawned.Clear();
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (floorObject != null) Object.Destroy(floorObject);
            if (managerObject != null) Object.Destroy(managerObject);
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
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

        private static Item GiveTreasure(Character player)
        {
            Item treasure = AssetDatabase.LoadAssetAtPath<Item>(TreasurePath);
            Assert.IsNotNull(treasure, $"Could not load {TreasurePath}.");
            Inventory inventory = player.GetComponent<Inventory>();
            Assert.IsTrue(inventory.AddItemInstance(treasure));
            return inventory.GetItems().Select(pair => pair.item).Single();
        }

        [UnityTest]
        public IEnumerator ADroppedItem_LiesOnTheGround_AndPicksUpAsTheSameInstance()
        {
            Character player = SpawnPlayer();
            yield return null;
            Inventory inventory = player.GetComponent<Inventory>();
            Item held = GiveTreasure(player);
            var instanceId = inventory.GetInstanceId(held);

            Assert.IsTrue(inventory.RequestDropItem(held));
            yield return null;

            Assert.AreEqual(0, inventory.GetItems().Count(), "It left the inventory.");
            WorldItemPickup pickup = Object.FindAnyObjectByType<WorldItemPickup>();
            Assert.IsNotNull(pickup, "It lies on the ground.");
            Assert.Less(Vector3.Distance(pickup.transform.position, player.transform.position), 2.5f, "Close by, where the player can take it back.");

            Assert.IsTrue(pickup.TryPickUp(player));
            Item back = inventory.GetItems().Select(pair => pair.item).Single();
            Assert.AreSame(held, back, "The same item comes back, not a new one.");
            Assert.AreEqual(instanceId, inventory.GetInstanceId(back), "Under the same instance id.");
        }

        [UnityTest]
        public IEnumerator ADeadPlayer_DropsNothing()
        {
            Character player = SpawnPlayer();
            yield return null;
            Inventory inventory = player.GetComponent<Inventory>();
            Item held = GiveTreasure(player);

            player.TakeDamage(new DamageInfo { Damage = player.MaxHealth }, BodyLocation.Torso, bypassesMitigation: true);
            yield return null;
            inventory.RequestDropItem(held);
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<WorldItemPickup>(), "Nothing was dropped.");
            Assert.IsTrue(inventory.Contains(held), "The item is still in the inventory.");
        }

        [UnityTest]
        public IEnumerator AnItemInAChest_CannotBeDroppedFromThere()
        {
            Character player = SpawnPlayer();
            yield return null;
            Item held = GiveTreasure(player);

            // A character's own inventory is the only place to drop from: a bare inventory with no character is not one.
            var chestObject = new GameObject("Chest");
            spawned.Add(chestObject);
            Inventory chest = chestObject.AddComponent<Inventory>();

            Assert.IsFalse(chest.RequestDropItem(held), "The item is not in that inventory at all.");
            Assert.IsFalse(chest.IsCarriedByOwnCharacter);
        }
    }
}
