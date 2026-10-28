using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// What the player can interact with (the interactable under the crosshair, in range and enabled) and what a chest holds when it spawns.
    /// </summary>
    public class InteractionAndChestTests
    {
        private const string ChestPrefab = "Assets/Prefabs/Interactables/Chest.prefab";

        private sealed class StubInteractable : Interactable
        {
            // A component added in code has no authored range, which would put every target out of reach.
            private void Awake() => InteractionRange = 5f;

            /// <summary>Like the key door and the item pickup, count every collider under this object as looking at it.</summary>
            public bool AcceptsChildColliders;

            public override bool IsLookedAtVia(Collider collider) =>
                AcceptsChildColliders ? collider.transform.IsChildOf(transform) : base.IsLookedAtVia(collider);

            public override void ActivateInteraction(Interactor interactor) { }
            public override void FinishInteraction(Interactor interactor) { }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private GameObject cameraObject;
        private Interactor interactor;
        private NetworkManager host;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InteractableManager.Clear();
            cameraObject = Track(new GameObject("TestMainCamera", typeof(Camera)));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // The interactor is left inactive so Start (which needs a Character) never runs; looking up a target does not use it.
            var holder = Track(new GameObject("Interactor"));
            holder.SetActive(false);
            interactor = holder.AddComponent<Interactor>();
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null && host.IsListening)
            {
                host.Shutdown();
                yield return new WaitUntil(() => !host.IsListening);
            }

            foreach (GameObject go in created) if (go != null) Object.Destroy(go);
            created.Clear();
            host = null;
            yield return null;
            InteractableManager.Clear();
            GameplayServices.Reset();
        }

        private GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        private StubInteractable MakeTarget(Vector3 position)
        {
            GameObject go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.transform.position = position;
            return go.AddComponent<StubInteractable>();
        }

        // ----- Interaction target -----

        [UnityTest]
        public IEnumerator LookingAtAnInteractableInRange_FindsIt()
        {
            StubInteractable target = MakeTarget(new Vector3(0f, 0f, 3f));
            yield return null;
            Physics.SyncTransforms();

            Assert.AreSame(target, interactor.FindInteractionTarget());
        }

        private StubInteractable MakeTargetWithChildCollider()
        {
            StubInteractable target = MakeTarget(new Vector3(0f, 0f, 3f));
            target.GetComponent<Collider>().enabled = false;
            GameObject part = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            part.transform.SetParent(target.transform, false);
            return target;
        }

        [UnityTest]
        public IEnumerator AChildCollider_CountsOnlyWhenTheInteractableAcceptsIt()
        {
            StubInteractable target = MakeTargetWithChildCollider();
            yield return null;
            Physics.SyncTransforms();

            Assert.IsNull(interactor.FindInteractionTarget(), "by default only the interactable's own collider counts");

            target.AcceptsChildColliders = true;
            Assert.AreSame(target, interactor.FindInteractionTarget(), "an interactable that accepts child colliders (a key door, a pickup) is found through them");
        }

        [UnityTest]
        public IEnumerator AnInteractableOutOfRange_IsNotFound()
        {
            StubInteractable target = MakeTarget(new Vector3(0f, 0f, 3f));
            yield return null;
            Physics.SyncTransforms();
            interactor.transform.position = new Vector3(0f, 0f, -(target.InteractionRange + 5f));

            Assert.IsNull(interactor.FindInteractionTarget());
        }

        [UnityTest]
        public IEnumerator LookingAtSomethingThatIsNotInteractable_FindsNothing()
        {
            MakeTarget(new Vector3(5f, 0f, 3f));
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 0f, 3f);
            yield return null;
            Physics.SyncTransforms();

            Assert.IsNull(interactor.FindInteractionTarget(), "the wall under the crosshair is not an interactable");
        }

        [UnityTest]
        public IEnumerator ADisabledInteractable_IsNotFound()
        {
            StubInteractable target = MakeTarget(new Vector3(0f, 0f, 3f));
            yield return null;
            Physics.SyncTransforms();
            target.enabled = false;

            Assert.IsNull(interactor.FindInteractionTarget());
        }

        // ----- Chest contents -----

        private NetworkManager StartHost()
        {
            var go = Track(new GameObject("TestHost"));
            UnityTransport transport = go.AddComponent<UnityTransport>();
            NetworkManager manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            manager.AddNetworkPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab));
            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            return manager;
        }

        private List<string> SpawnChestAndListItems(int seed)
        {
            GameplayServices.Reset().Random = new SeededRandom(seed);
            GameObject chest = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab));
            chest.GetComponent<NetworkObject>().Spawn();
            created.Add(chest);
            List<string> names = chest.GetComponent<Inventory>().GetItems().Select(i => i.item.ItemName).OrderBy(n => n).ToList();
            chest.GetComponent<NetworkObject>().Despawn();
            return names;
        }

        [UnityTest]
        public IEnumerator ASpawnedChest_IsFilledFromItsLootTable_ReproduciblyForASeed()
        {
            host = StartHost();
            yield return null;

            var distinct = new HashSet<string>();
            bool anyLoot = false;
            for (int seed = 1; seed <= 12; seed++)
            {
                List<string> first = SpawnChestAndListItems(seed);
                List<string> again = SpawnChestAndListItems(seed);
                CollectionAssert.AreEqual(first, again, $"seed {seed} must give the same chest contents every time");
                anyLoot |= first.Count > 0;
                distinct.Add(string.Join("|", first));
                yield return null;
            }

            Assert.IsTrue(anyLoot, "a chest's loot table should give it something for at least one of twelve seeds");
            Assert.Greater(distinct.Count, 1, "different seeds should not all give the same chest");
        }
    }
}
