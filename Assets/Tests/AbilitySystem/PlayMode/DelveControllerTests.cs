using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Delve;
using SimpleRPG.Gameplay;
using SimpleRPG.HitDetection;
using SimpleRPG.InventorySystem;
using SimpleRPG.Networking;
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
            // The real controller is placed in the scene, which gives its NetworkObject an id; a runtime one has none, and Netcode refuses to spawn it.
            NetworkObject networkObject = host.AddComponent<NetworkObject>();
            typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(networkObject, 0xDE17E001u);
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

        // ----- A player who drops out -----

        private const string PlayerId = "player-7777";
        private const ulong ClientId = 7;

        private Character SpawnAwayPlayer(Vector3 position)
        {
            Character character = SpawnPlayer(position);
            PlayerSessions.For(manager).Set(ClientId, PlayerId);
            delve.RegisterCharacter(PlayerId, ClientId, character);
            return character;
        }

        private static Item Treasure()
        {
            Item treasure = AssetDatabase.LoadAssetAtPath<Item>(TreasurePath);
            Assert.IsNotNull(treasure, $"Could not load {TreasurePath}.");
            return treasure;
        }

        [UnityTest]
        public IEnumerator ACharacterWhosePlayerDropsOut_IsKept_AndComesBackWithItsItems()
        {
            Character away = SpawnAwayPlayer(new Vector3(2f, 0f, 0f));
            Character partner = SpawnPlayer(new Vector3(4f, 0f, 0f));
            yield return null;
            Assert.IsTrue(away.GetComponent<Inventory>().AddItemInstance(Treasure()));

            Assert.IsTrue(delve.ParkDisconnected(ClientId), "The character is put aside for its player.");
            yield return null;

            Assert.IsTrue(away == null, "While waiting it is not in the world.");
            Assert.AreEqual(1, delve.AwayCount);
            Assert.AreEqual(0, Object.FindObjectsByType<LootBag>().Length, "Nothing was dropped: this is not a death.");

            ParkedCharacter waiting = delve.TakeParked(PlayerId);
            Assert.IsNotNull(waiting);
            Assert.AreEqual(0, delve.AwayCount);
            Assert.AreEqual(1, waiting.Carried.Count);
            Assert.AreEqual(2f, waiting.Position.x, 0.5f, "It waits where it stood.");

            Character returned = SpawnPlayer(waiting.Position);
            yield return null;
            Assert.AreEqual(0, waiting.RestoreInto(returned));
            Assert.AreEqual(1, CarriedBy(returned), "Everything it carried is back.");
            Assert.IsTrue(partner.IsAlive);
        }

        [UnityTest]
        public IEnumerator ADelveCannotBeWiped_WhileAPlayerIsAway()
        {
            Character away = SpawnAwayPlayer(Vector3.zero);
            Character partner = SpawnPlayer(new Vector3(3f, 0f, 0f));
            yield return null;
            delve.ParkDisconnected(ClientId);
            yield return null;

            Kill(partner);
            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(DelvePhase.Delving, delve.Phase, "Everyone present is dead, but one player may still come back.");
        }

        [UnityTest]
        public IEnumerator APlayerWhoDoesNotReturnInTime_LeavesTheirBelongingsInALootBag()
        {
            Set(delve, "reconnectGraceSeconds", 0.3f);
            Character away = SpawnAwayPlayer(new Vector3(2f, 0f, 0f));
            Character partner = SpawnPlayer(new Vector3(4f, 0f, 0f));
            yield return null;
            away.GetComponent<Inventory>().AddItemInstance(Treasure());
            delve.ParkDisconnected(ClientId);

            yield return new WaitForSeconds(1f);

            Assert.AreEqual(0, delve.AwayCount, "They are no longer waited for.");
            LootBag bag = Object.FindFirstObjectByType<LootBag>();
            Assert.IsNotNull(bag, "What they carried was left behind.");
            Assert.AreEqual(1, bag.GetComponent<Inventory>().GetItems().Count());
            Assert.IsNull(delve.TakeParked(PlayerId), "There is nothing left to come back to.");
        }

        [UnityTest]
        public IEnumerator APlayerWhoReturnsBeforeTheServerNoticed_FindsTheirCharacterAnyway()
        {
            Character away = SpawnAwayPlayer(Vector3.zero);
            yield return null;
            away.GetComponent<Inventory>().AddItemInstance(Treasure());

            // No disconnect was seen; the same player id arrives again.
            ParkedCharacter waiting = delve.TakeParked(PlayerId);

            Assert.IsNotNull(waiting, "The old character is put aside and handed back.");
            Assert.AreEqual(1, waiting.Carried.Count);
        }

        [UnityTest]
        public IEnumerator ALateWordThatAnOldConnectionDied_DoesNotTakeTheReturnedPlayersCharacter()
        {
            Character first = SpawnAwayPlayer(Vector3.zero);
            yield return null;
            // Every test character is owned by the host, so the first one took the only camera as a child, and parking despawns it with the camera.
            // A real returning client has a camera of its own.
            cameraObject.transform.SetParent(null);
            delve.ParkDisconnected(ClientId);
            yield return null;

            // The player returns as a different client and gets a character.
            const ulong secondClient = 8;
            PlayerSessions.For(manager).Set(secondClient, PlayerId);
            Character second = SpawnPlayer(Vector3.zero);
            delve.RegisterCharacter(PlayerId, secondClient, second);
            yield return null;

            // The server still knows the first client by name; it just no longer owns the character.
            PlayerSessions.For(manager).Set(ClientId, PlayerId);
            Assert.IsFalse(delve.ParkDisconnected(ClientId), "The first connection no longer owns the character.");
            yield return null;
            Assert.IsTrue(second != null && second.IsSpawned, "The returned player's character is untouched.");
        }
    }
}
