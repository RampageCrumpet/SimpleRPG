using System.Collections;
using NUnit.Framework;
using SimpleRPG.Keys;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Checks the server side of keys and locked doors on a host: picking up a key records it, a door opens only for its key,
    /// and the door's lock objects are switched off for the machine that registered them. A real client is not started here;
    /// its view comes from the same replicated lists the host reads.
    /// </summary>
    public class DungeonKeyNetworkTests
    {
        private const string RingPrefab = "Assets/Tests/PlayMode/Prefabs/TestKeyRing.prefab";
        private const string KeyPrefab = "Assets/Prefabs/Interactables/KeyPickup.prefab";

        private GameObject managerObject;
        private NetworkManager manager;
        private GameObject ringObject;
        private DungeonKeyRing ring;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;

            GameObject ringPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RingPrefab);
            Assert.IsNotNull(ringPrefab, $"Could not load {RingPrefab}.");
            ringObject = Object.Instantiate(ringPrefab);
            ring = ringObject.GetComponent<DungeonKeyRing>();
            ringObject.GetComponent<NetworkObject>().Spawn();
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

            if (ringObject != null) Object.Destroy(ringObject);
            if (managerObject != null) Object.Destroy(managerObject);
            yield return null;
        }

        private GameObject Gate(int keyId)
        {
            var gate = new GameObject("Gate" + keyId);
            ring.RegisterLock(keyId, gate);
            return gate;
        }

        [UnityTest]
        public IEnumerator Door_WithoutItsKey_StaysLocked()
        {
            GameObject gate = Gate(1);
            ring.AddKey(2);
            yield return null;

            Assert.AreEqual(KeyUseResult.NotHeld, ring.TryOpen(1, 0f));
            Assert.IsTrue(gate.activeSelf);
            Assert.IsFalse(ring.IsOpen(1));
        }

        [UnityTest]
        public IEnumerator Door_WithItsKey_OpensAndUsesTheKeyUp()
        {
            GameObject gate = Gate(1);
            Assert.IsTrue(ring.AddKey(1));
            yield return null;
            Assert.IsTrue(ring.Holds(1));

            Assert.AreEqual(KeyUseResult.Opened, ring.TryOpen(1, 1f));
            yield return null;

            Assert.IsFalse(gate.activeSelf, "The lock object should switch off.");
            Assert.IsTrue(ring.IsOpen(1));
            Assert.IsFalse(ring.Holds(1), "Opening uses the key.");
        }

        [UnityTest]
        public IEnumerator Door_OpensBothEnds()
        {
            GameObject near = Gate(1);
            GameObject far = Gate(1);
            ring.AddKey(1);
            ring.TryOpen(1, 0f);
            yield return null;

            Assert.IsFalse(near.activeSelf);
            Assert.IsFalse(far.activeSelf);
        }

        [UnityTest]
        public IEnumerator Door_RegisteredAfterItOpened_StartsOpen()
        {
            ring.AddKey(1);
            ring.TryOpen(1, 0f);
            yield return null;

            GameObject lateGate = Gate(1);
            Assert.IsFalse(lateGate.activeSelf, "A machine that builds the door after it opened (a late joiner) must see it open.");
        }

        [UnityTest]
        public IEnumerator Door_FarAway_DoesNotOpenOrSpendTheKey()
        {
            GameObject gate = Gate(1);
            ring.AddKey(1);
            yield return null;

            Assert.AreEqual(KeyUseResult.TooFar, ring.TryOpen(1, 100f));
            Assert.IsTrue(gate.activeSelf);
            Assert.IsTrue(ring.Holds(1));
        }

        [UnityTest]
        public IEnumerator Key_CannotBeAddedTwice()
        {
            Assert.IsTrue(ring.AddKey(3));
            Assert.IsFalse(ring.AddKey(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyPickup_PickedUpNearby_GivesThePartyItsKeyAndDisappears()
        {
            KeyPickup pickup = SpawnKey(5);
            yield return null;

            Assert.IsTrue(pickup.TryPickUp(1f));
            yield return null;

            Assert.IsTrue(ring.Holds(5));
            Assert.IsFalse(pickup.IsSpawned, "The picked up key should leave the world.");
        }

        [UnityTest]
        public IEnumerator KeyPickup_FarAway_IsLeftWhereItLies()
        {
            KeyPickup pickup = SpawnKey(5);
            yield return null;

            Assert.IsFalse(pickup.TryPickUp(100f));
            Assert.IsFalse(ring.Holds(5));
            Assert.IsTrue(pickup.IsSpawned);
        }

        [UnityTest]
        public IEnumerator KeyPickup_ThenDoor_OpensTheDoorItsKeyIdNames()
        {
            GameObject gate5 = Gate(5);
            GameObject gate6 = Gate(6);
            SpawnKey(5).TryPickUp(0f);
            yield return null;

            Assert.AreEqual(KeyUseResult.NotHeld, ring.TryOpen(6, 0f));
            Assert.AreEqual(KeyUseResult.Opened, ring.TryOpen(5, 0f));
            yield return null;

            Assert.IsFalse(gate5.activeSelf);
            Assert.IsTrue(gate6.activeSelf);
        }

        private KeyPickup SpawnKey(int keyId)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefab);
            Assert.IsNotNull(prefab, $"Could not load {KeyPrefab}.");
            GameObject instance = Object.Instantiate(prefab);
            KeyPickup pickup = instance.GetComponent<KeyPickup>();
            pickup.Initialize(keyId);
            instance.GetComponent<NetworkObject>().Spawn();
            return pickup;
        }
    }
}
