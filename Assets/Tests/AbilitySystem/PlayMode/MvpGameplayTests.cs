using AI;
using HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.LevelGeneration;
using SimpleRPG.ObjectInteractions;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>Exercises the shipped scene, room geometry, navigation and catalog on a real host.</summary>
    public class MvpGameplayTests
    {
        private NetworkManager manager;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null)
            {
                manager.Shutdown();
                yield return null;
                Object.Destroy(manager.gameObject);
            }
            yield return null;
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            // Netcode moves its manager into DontDestroyOnLoad during Awake, outside this scene's roots.
            foreach (var leftover in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)) Object.Destroy(leftover.gameObject);
            // Keep other test fixtures free of the demo scene's camera and objects.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }

        private IEnumerator StartDungeon(int seed, int rooms, int floors, int maximumRooms = -1)
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var game = Object.FindFirstObjectByType<GameController>();
            game.UseRandomSeed = false;
            game.Seed = seed;
            var generator = Object.FindFirstObjectByType<LevelGeneratorController>();
            generator.MinimumNumberOfRooms = rooms;
            generator.MaximumNumberOfRooms = maximumRooms < 0 ? rooms + 3 : maximumRooms;
            generator.FloorCount = floors;
            manager = NetworkManager.Singleton;
            Assert.IsTrue(manager.StartHost());
            yield return null;
            yield return null;
            var player = Object.FindFirstObjectByType<Player>().character;
            Assert.IsNotNull(player, "The player waits for dungeon population and must then spawn.");
            player.GetComponent<FirstPersonCharacterController>().enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
        }

        [UnityTest]
        public IEnumerator GeneratedDungeons_AreConnectedPopulatedAndLootable()
        {
            foreach (var config in new[] { (seed: 42, rooms: 8, floors: 1, maximum: 11), (seed: 1337, rooms: 25, floors: 3, maximum: 28), (seed: 9001, rooms: 40, floors: 3, maximum: 43), (seed: 42, rooms: 25, floors: 3, maximum: 0) })
            {
                yield return StartDungeon(config.seed, config.rooms, config.floors, config.maximum);
                var generator = Object.FindFirstObjectByType<LevelGeneratorController>();
                var population = Object.FindFirstObjectByType<DungeonPopulation>();
                var player = Object.FindFirstObjectByType<Player>().character;
                Assert.IsTrue(generator.IsGenerated);
                Assert.IsTrue(population.IsReady);
                Assert.GreaterOrEqual(generator.GeneratedRooms.Count, config.rooms);
                Assert.Less(Vector3.Distance(player.transform.position, population.PlayerSpawnPosition), 2);
                Assert.GreaterOrEqual(population.EnemyCount, 2);
                Assert.GreaterOrEqual(population.TreasureCount, config.rooms);
                foreach (var room in generator.GeneratedRooms)
                    Assert.IsTrue(population.TryFindPosition(room, generator.Roomset.cellSize, new System.Random(12), true, out _, avoidOccupied: false), $"Seed {config.seed}: no connected walkable position in {room.name}");
                foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    Assert.IsTrue(enemy.GetComponent<NavMeshAgent>().isOnNavMesh);
                var loot = Object.FindObjectsByType<WorldItemPickup>(FindObjectsSortMode.None);
                Assert.IsTrue(loot.Any(p => p.Item.ItemName.StartsWith("Golden")));
                Assert.IsTrue(loot.Any(p => p.Item.ItemName.StartsWith("Brilliant")));
                var inventory = player.GetComponent<Inventory>();
                bool testedInteraction = false;
                foreach (var pickup in loot.GroupBy(p => p.Item).Select(g => g.First()).Take(4))
                {
                    Assert.IsNotNull(pickup.Item.Icon, pickup.Item.ItemName + " needs an inventory icon.");
                    var source = pickup.Item;
                    player.transform.position = pickup.transform.position + Vector3.right * 10;
                    Assert.IsFalse(pickup.TryPickUp(player), "The server rejects out-of-range pickup.");
                    player.transform.position = pickup.transform.position + Vector3.up * 0.2f;
                    if (!testedInteraction)
                    {
                        player.GetComponent<Interactor>().StartInteraction(pickup);
                        yield return new WaitForSeconds(0.3f);
                        Assert.IsTrue(pickup == null || !pickup.IsSpawned, "The timed interaction and pickup RPC must transfer the item.");
                        testedInteraction = true;
                    }
                    else Assert.IsTrue(pickup.TryPickUp(player));
                    var copy = inventory.GetItems().Select(e => e.item).Single(i => i.SourceAsset == source);
                    Assert.AreNotSame(source, copy);
                    Assert.Greater(ItemRegistry.Instance.GetId(copy), 0);
                    if (pickup != null) Assert.IsFalse(pickup.TryPickUp(player), "A pickup cannot be claimed twice.");
                }
                var menu = Object.FindFirstObjectByType<SimpleRPG.UI.LootMenuUI>();
                Assert.AreNotSame(menu.PlayerInventoryUI, menu.LootInventoryUI, "Player and container inventories need distinct views.");
                menu.PlayerInventoryUI.gameObject.SetActive(true);
                yield return null;
                Assert.AreEqual(inventory.GetItems().Count(), menu.PlayerInventoryUI.GetComponentsInChildren<SimpleRPG.UI.ItemIcon>().Length);
                foreach (var icon in menu.PlayerInventoryUI.GetComponentsInChildren<SimpleRPG.UI.ItemIcon>())
                {
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, icon.transform.position);
                    Assert.That(screen.x, Is.InRange(0f, (float)Screen.width));
                    Assert.That(screen.y, Is.InRange(0f, (float)Screen.height));
                    var local = icon.transform.localPosition;
                    Assert.AreEqual(inventory.GetItems().Single(i => i.item == icon.Item).position,
                        menu.PlayerInventoryUI.ScreenToItemOrigin(screen, icon.Item.ItemSize), "Grid coordinates must round-trip through the displayed icon.");
                }
                menu.PlayerInventoryUI.gameObject.SetActive(false);
                var remaining = Object.FindObjectsByType<WorldItemPickup>(FindObjectsSortMode.None);
                var small = remaining.First(p => p.Item.ItemSize == Vector2Int.one).Item;
                int capacityGuard = 0;
                while (inventory.AddItemInstance(small) && capacityGuard++ < 1000) { }
                Assert.Less(capacityGuard, 1000);
                var blocked = remaining[0];
                player.transform.position = blocked.transform.position;
                Assert.IsFalse(blocked.TryPickUp(player), "A full inventory must leave the item in the world.");
                Assert.IsTrue(blocked.IsSpawned);
                manager.Shutdown();
                yield return null;
                Object.Destroy(manager.gameObject);
                yield return null;
                GameplayServices.Reset();
                AbilityServices.Reset();
            }
        }

        [UnityTest]
        public IEnumerator Skeletons_UsePhysicalHitsAndEnterTerminalDeath()
        {
            yield return StartDungeon(42, 8, 1);
            var player = Object.FindFirstObjectByType<Player>().character;
            var population = Object.FindFirstObjectByType<DungeonPopulation>();
            var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            // Place the combatants on a real generated floor; the rest of the host and combat systems stay active.
            var enemy = enemies[0];
            foreach (var other in enemies.Where(e => e != enemy)) other.GetComponent<NavMeshAgent>().enabled = false;
            var agent = enemy.GetComponent<NavMeshAgent>();
            agent.Warp(population.PlayerSpawnPosition);
            enemy.transform.rotation = Quaternion.identity;
            player.transform.position = enemy.transform.position + Vector3.forward * 1.1f + Vector3.up * 0.65f;
            player.transform.rotation = Quaternion.Euler(0, 180, 0);
            Physics.SyncTransforms();
            int before = player.Health;
            float deadline = Time.time + 15;
            while (player.Health == before && Time.time < deadline) yield return null;
            Assert.Less(player.Health, before, "An animated blade must physically contact the player's hurtbox.");
            Assert.GreaterOrEqual(player.Health, before - 30, "A swing cannot damage the same target repeatedly.");

            // Kill with the player's real melee ability and animation-driven weapon, with the enemy planted in reach.
            enemy.enabled = false;
            agent.enabled = false;
            var melee = player.AbilitySlots.OfType<GraphAbilitySlot>().First(s => s.Definition.HasNode(NodeTypes.MeleeSwing));
            deadline = Time.time + 20;
            float closest = float.PositiveInfinity;
            bool opened = false;
            while (enemy.Character.IsAlive && Time.time < deadline)
            {
                player.transform.position = enemy.transform.position - enemy.transform.forward * 1.1f + Vector3.up * 0.65f;
                player.transform.rotation = enemy.transform.rotation;
                melee.Activate();
                float end = Time.time + 1.8f;
                while (Time.time < end)
                {
                    var blade = player.GetComponentInChildren<Hitbox>();
                    opened |= blade.enabled;
                    if (blade.enabled)
                        closest = Mathf.Min(closest, Vector3.Distance(blade.transform.position, enemy.GetComponentInChildren<HurtBox>().transform.position));
                    yield return null;
                }
            }
            Assert.IsFalse(enemy.Character.IsAlive, $"The player's animated sword should kill the skeleton. Health={enemy.Character.Health}, window={opened}, closest={closest}.");
            Assert.IsTrue(enemy.IsDead);
            Assert.IsFalse(agent.enabled);
            yield return new WaitForSeconds(2);
            Assert.IsTrue(enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Die"));
            Assert.IsTrue(Physics.Raycast(enemy.transform.position + Vector3.up * .5f, Vector3.down,
                out RaycastHit floor, 1, LayerMask.GetMask("World", "Walkable"), QueryTriggerInteraction.Ignore));
            var renderer = enemy.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            renderer.BakeMesh(mesh);
            var vertices = mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            Object.Destroy(mesh);
            Assert.That(vertices.Min(v => v.y) - floor.point.y, Is.InRange(-.015f, .025f), "The pile must rest on the visible dungeon floor, not the elevated navmesh.");
            Assert.Less(vertices.Max(v => v.y) - floor.point.y, .25f, "All bones must land.");
            var delivery = enemy.GetComponent<MeleeDelivery>();
            delivery.ActivateWeapon(1);
            Assert.IsFalse(enemy.GetComponentInChildren<Hitbox>().enabled, "An animation event after death cannot reopen damage.");
            int health = player.Health;
            yield return new WaitForSeconds(1);
            Assert.AreEqual(health, player.Health);
        }
    }
}

