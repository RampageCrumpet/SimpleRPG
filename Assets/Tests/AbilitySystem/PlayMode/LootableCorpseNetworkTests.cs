using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.AI;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// An enemy cannot be searched while it lives; when it dies the server rolls the enemy's own loot table, once, into its inventory,
    /// and the body then opens like a chest.
    /// </summary>
    public class LootableCorpseNetworkTests
    {
        private const string MonsterPrefab = "Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab";

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameplayServices.Reset();
            AbilityServices.Reset();

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

            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (managerObject != null) Object.Destroy(managerObject);
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        /// <summary>The shipped skeleton without its chasing AI, which needs a baked navigation mesh these tests do not have.</summary>
        private Character SpawnSkeleton()
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefab), Vector3.zero, Quaternion.identity);
            spawned.Add(instance);
            Object.DestroyImmediate(instance.GetComponent<AIController>());
            NavigationGrounding grounding = instance.GetComponent<NavigationGrounding>();
            if (grounding != null) Object.DestroyImmediate(grounding);
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null) Object.DestroyImmediate(agent);

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private static void Kill(Character target) => target.TakeDamage(new DamageInfo { Damage = 100000 }, HitDetection.BodyLocation.Torso);

        private static List<string> Loot(Character skeleton) =>
            skeleton.GetComponent<Inventory>().GetItems().Select(i => i.item.ItemName).OrderBy(n => n).ToList();

        [UnityTest]
        public IEnumerator ALivingEnemy_CannotBeSearched_AndHoldsNothing()
        {
            Character skeleton = SpawnSkeleton();
            yield return null;
            LootableCorpse corpse = skeleton.GetComponent<LootableCorpse>();

            Assert.IsTrue(skeleton.IsAlive);
            Assert.IsFalse(corpse.CanBeInteractedWith);
            Assert.IsEmpty(Loot(skeleton));
        }

        [UnityTest]
        public IEnumerator WhenAnEnemyDies_ItsOwnLootTableIsRolledOnce_AndTheBodyCanBeSearched()
        {
            Character skeleton = SpawnSkeleton();
            yield return null;
            LootableCorpse corpse = skeleton.GetComponent<LootableCorpse>();

            Kill(skeleton);
            yield return null;

            Assert.IsFalse(skeleton.IsAlive);
            Assert.IsTrue(corpse.CanBeInteractedWith, "a dead enemy can be searched");
            List<string> loot = Loot(skeleton);
            Assert.AreEqual(1, loot.Count, "the skeleton's loot table gives one item");
            StringAssert.StartsWith("Golden", loot[0], "from the gold table the skeleton's own table nests");

            Kill(skeleton);
            yield return null;
            CollectionAssert.AreEqual(loot, Loot(skeleton), "a body is rolled once, however many hits it takes afterwards");
        }

        [UnityTest]
        public IEnumerator TheBodysInventory_IsOpenToPlayersNearby()
        {
            Character skeleton = SpawnSkeleton();
            yield return null;
            Kill(skeleton);
            yield return null;

            LootableCorpse corpse = skeleton.GetComponent<LootableCorpse>();
            Inventory inventory = skeleton.GetComponent<Inventory>();
            Assert.Greater(inventory.SharedAccessRange, corpse.InteractionRange, "the server lets players move items within the body's reach");
        }

        [UnityTest]
        public IEnumerator AnyColliderOfTheBody_CountsAsLookingAtIt()
        {
            Character skeleton = SpawnSkeleton();
            yield return null;
            LootableCorpse corpse = skeleton.GetComponent<LootableCorpse>();

            Collider[] ownColliders = skeleton.GetComponentsInChildren<Collider>(true);
            Assert.IsNotEmpty(ownColliders);
            foreach (Collider collider in ownColliders)
            {
                Assert.IsTrue(corpse.IsLookedAtVia(collider), collider.name + " belongs to the body");
            }

            GameObject other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawned.Add(other);
            Assert.IsFalse(corpse.IsLookedAtVia(other.GetComponent<Collider>()), "another object's collider does not");
        }

        [UnityTest]
        public IEnumerator TheSameSeed_GivesTheSameBody_AndSeedsVary()
        {
            var distinct = new HashSet<string>();
            for (int seed = 1; seed <= 10; seed++)
            {
                List<string> first = KillOneSkeletonWithSeed(seed);
                List<string> again = KillOneSkeletonWithSeed(seed);
                CollectionAssert.AreEqual(first, again, $"seed {seed} must give the same loot every time");
                distinct.Add(string.Join("|", first));
                yield return null;
            }

            Assert.Greater(distinct.Count, 1, "different seeds should not all give the same loot");
        }

        private List<string> KillOneSkeletonWithSeed(int seed)
        {
            GameplayServices.Current.Random = new SeededRandom(seed);
            Character skeleton = SpawnSkeleton();
            Kill(skeleton);
            List<string> loot = Loot(skeleton);
            skeleton.GetComponent<NetworkObject>().Despawn();
            return loot;
        }
    }
}
