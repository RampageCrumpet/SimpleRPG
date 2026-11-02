using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Delve;
using SimpleRPG.Gameplay;
using SimpleRPG.HitDetection;
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
    /// A delve on a host with real player characters: dying drops everything into a loot bag, a dead player waits for the delve to end, the
    /// party extracts together, and an all-dead party ends it.
    /// </summary>
    public class DelveControllerTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string LootBagPrefab = "Assets/Prefabs/Interactables/LootBag.prefab";
        private const string TreasurePath = "Assets/Items/LootItems/Golden Bell.asset";

        private GameObject managerObject;
        private GameObject cameraObject;
        private GameObject floorObject;
        private NetworkManager manager;
        private DelveController delve;
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

            delve = SpawnDelve();
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

            foreach (LootBag bag in Object.FindObjectsByType<LootBag>())
            {
                Object.Destroy(bag.gameObject);
            }

            spawned.Clear();
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (floorObject != null) Object.Destroy(floorObject);
            if (managerObject != null) Object.Destroy(managerObject);
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        private DelveController SpawnDelve()
        {
            var host = new GameObject("Delve");
            spawned.Add(host);
            host.AddComponent<NetworkObject>();
            DelveController controller = host.AddComponent<DelveController>();

            GameObject bagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootBagPrefab);
            Assert.IsNotNull(bagPrefab, $"Could not load {LootBagPrefab}.");
            Set(controller, "lootBagPrefab", bagPrefab.GetComponent<NetworkObject>());
            Set(controller, "extractionSeconds", 1f);
            Set(controller, "extractionRadius", 5f);

            host.GetComponent<NetworkObject>().Spawn();
            return controller;
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private Character SpawnPlayer(Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(prefab, $"Could not load {PlayerPrefab}.");

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
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

        private static void Kill(Character target) => target.TakeDamage(new DamageInfo { Damage = target.MaxHealth }, BodyLocation.Torso);

        private static int CarriedBy(Character character) => character.GetComponent<Inventory>().GetItems().Count();

        [UnityTest]
        public IEnumerator ADeadPlayerLosesEverything_ToALootBagWhereTheyFell()
        {
            Character player = SpawnPlayer(Vector3.zero);
            Character partner = SpawnPlayer(new Vector3(3f, 0f, 0f));
            yield return null;

            Item treasure = AssetDatabase.LoadAssetAtPath<Item>(TreasurePath);
            Assert.IsNotNull(treasure, $"Could not load {TreasurePath}.");
            Assert.IsTrue(player.GetComponent<Inventory>().AddItemInstance(treasure), "The player starts with something to lose.");
            Kill(player);
            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(0, CarriedBy(player), "The dead player carries nothing.");
            LootBag bag = Object.FindFirstObjectByType<LootBag>();
            Assert.IsNotNull(bag, "A loot bag was left behind.");
            Assert.AreEqual(1, bag.GetComponent<Inventory>().GetItems().Count(), "It holds what they carried.");
            Assert.Less(Vector3.Distance(bag.transform.position, player.transform.position), 1f, "It lies where they fell.");
            Assert.IsTrue(partner.IsAlive);
        }

        [UnityTest]
        public IEnumerator ADeadPlayerCannotRespawnWhileTheDelveRuns_ButCanOnceItEnds()
        {
            Character player = SpawnPlayer(Vector3.zero);
            Character partner = SpawnPlayer(new Vector3(3f, 0f, 0f));
            yield return null;

            Kill(player);
            yield return new WaitForSeconds(0.5f);
            player.RequestRespawn();
            yield return null;

            Assert.IsFalse(player.IsAlive, "The delve is still running, so the dead stay dead.");
            Assert.AreEqual(DelvePhase.Delving, delve.Phase);

            Kill(partner);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(DelvePhase.Wiped, delve.Phase, "Everyone is dead.");
            player.RequestRespawn();
            yield return null;
            Assert.IsTrue(player.IsAlive, "Once the delve is over a respawn is accepted.");
        }

        [UnityTest]
        public IEnumerator ThePartyExtractsTogether_OnlyWhenEveryLivingPlayerIsInTheZone()
        {
            Character player = SpawnPlayer(Vector3.zero);
            Character partner = SpawnPlayer(new Vector3(40f, 0f, 0f));
            yield return null;
            delve.ConfigureExtraction(Vector3.zero);

            yield return new WaitForSeconds(2f);
            Assert.AreEqual(DelvePhase.Delving, delve.Phase, "One player is far from the zone, so nobody gets out.");

            partner.GetComponent<CharacterController>().enabled = false;
            partner.transform.position = new Vector3(1f, 0f, 0f);
            partner.GetComponent<CharacterController>().enabled = true;
            yield return new WaitForSeconds(2f);

            Assert.AreEqual(DelvePhase.Extracted, delve.Phase, "Both are in the zone for the whole channel.");
            Assert.IsTrue(player.IsAlive);
            Assert.IsTrue(delve.StartNextDelve(), "The next delve can start.");
            Assert.AreEqual(DelvePhase.Delving, delve.Phase);
        }
    }
}
