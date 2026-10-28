using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LevelGeneration;
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
    /// Keys and locked doors with a real second NetworkManager acting as a client in the same process: the client picks up a key
    /// and opens a door through the real RPCs, sees the host's changes, a client that joins after a door opened sees it open, and a late joiner is given every player's character.
    /// The host's NetworkManager is created first, so it is <c>NetworkManager.Singleton</c>, which the server side code reads.
    /// </summary>
    public class DungeonKeyClientTests
    {
        private const string RingPrefab = "Assets/Tests/PlayMode/Prefabs/TestKeyRing.prefab";
        private const string KeyPrefab = "Assets/Prefabs/Interactables/KeyPickup.prefab";
        private const string CharacterPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string PlayerPrefab = "Assets/Tests/PlayMode/Prefabs/TestPlayer.prefab";

        private NetworkManager host;
        private readonly List<NetworkManager> clients = new List<NetworkManager>();
        private DungeonKeyRing hostRing;
        private GameObject mainCamera;

        /// <summary>
        /// A locally owned character registers with the scene's UI (RegisterWithUI.AttachToUI), which uses Single() and throws in
        /// a bare test scene. It happens once for every owner spawn, so each manager started here declares it.
        /// </summary>
        private static void ExpectNoUiException()
        {
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Sequence contains no matching element"));
        }

        private static NetworkManager NewManager(string name)
        {
            var go = new GameObject(name);
            UnityTransport transport = go.AddComponent<UnityTransport>();
            NetworkManager manager = go.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = Load(PlayerPrefab) };
            manager.AddNetworkPrefab(Load(RingPrefab));
            manager.AddNetworkPrefab(Load(KeyPrefab));
            manager.AddNetworkPrefab(Load(CharacterPrefab));
            return manager;
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load {path}.");
            return prefab;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The first person controller of every spawned character looks for the scene's MainCamera.
            mainCamera = new GameObject("TestCamera", typeof(Camera)) { tag = "MainCamera" };
            host = NewManager("TestHost");
            ExpectNoUiException();
            Assert.IsTrue(host.StartHost(), "The test host should start.");
            yield return null;

            hostRing = Object.Instantiate(Load(RingPrefab)).GetComponent<DungeonKeyRing>();
            hostRing.GetComponent<NetworkObject>().Spawn();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Every owned character parents the one MainCamera to itself, so whichever is destroyed first takes the camera with it
            // while the other is still looking through it.
            foreach (FirstPersonCharacterController controller in Object.FindObjectsByType<FirstPersonCharacterController>(FindObjectsSortMode.None))
            {
                controller.enabled = false;
            }

            foreach (NetworkManager client in clients)
            {
                if (client != null && client.IsListening) client.Shutdown();
            }

            yield return null;

            if (host != null && host.IsListening)
            {
                host.Shutdown();
                yield return new WaitUntil(() => !host.IsListening);
            }

            foreach (NetworkManager client in clients)
            {
                if (client != null) Object.Destroy(client.gameObject);
            }

            clients.Clear();
            if (hostRing != null) Object.Destroy(hostRing.gameObject);
            if (host != null) Object.Destroy(host.gameObject);
            yield return null;

            // The characters read the camera until they are gone, so it goes last.
            if (mainCamera != null) Object.Destroy(mainCamera);
            yield return null;
        }

        private IEnumerator Connect(string name)
        {
            NetworkManager client = NewManager(name);
            ExpectNoUiException();

            // Both managers live in one process and share the spawn bookkeeping, so the client's copy of the host's objects
            // trips Netcode's duplicate-id check. A real second process would not.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already in the spawned list"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Size mismatch"));
            clients.Add(client);
            Assert.IsTrue(client.StartClient(), "The test client should start.");
            yield return WaitFor(() => client.IsConnectedClient && client.LocalClient.PlayerObject != null, $"{name} did not connect.");
            yield return WaitFor(() => ClientRing(client) != null, $"{name} did not receive the key ring.");
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, string failure, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail(failure);
                yield return null;
            }
        }

        private static DungeonKeyRing ClientRing(NetworkManager client)
        {
            foreach (NetworkObject spawned in client.SpawnManager.SpawnedObjects.Values)
            {
                if (spawned.TryGetComponent(out DungeonKeyRing ring)) return ring;
            }

            return null;
        }

        private static KeyPickup ClientPickup(NetworkManager client, int keyId)
        {
            foreach (NetworkObject spawned in client.SpawnManager.SpawnedObjects.Values)
            {
                if (spawned.TryGetComponent(out KeyPickup pickup) && pickup.KeyId == keyId) return pickup;
            }

            return null;
        }

        private KeyPickup SpawnKey(int keyId)
        {
            GameObject instance = Object.Instantiate(Load(KeyPrefab), new Vector3(0f, 5f, 0.5f), Quaternion.identity);
            KeyPickup pickup = instance.GetComponent<KeyPickup>();
            pickup.Initialize(keyId);
            instance.GetComponent<NetworkObject>().Spawn();
            return pickup;
        }

        private static GameObject Gate(DungeonKeyRing ring, int keyId)
        {
            // The server places the player's character at (0, 5, 0), so this gate is within door range of it.
            var gate = new GameObject("Gate" + keyId);
            gate.transform.position = new Vector3(0f, 5f, 1f);
            ring.RegisterLock(keyId, gate);
            return gate;
        }

        [UnityTest]
        public IEnumerator Client_PicksUpKey_ThroughTheRpc_AndTheKeyLeavesTheWorld()
        {
            yield return Connect("Client");
            DungeonKeyRing clientRing = ClientRing(clients[0]);
            SpawnKey(5);
            yield return WaitFor(() => ClientPickup(clients[0], 5) != null, "The client never received the key pickup.");
            KeyPickup clientPickup = ClientPickup(clients[0], 5);
            Assert.AreEqual(5, clientPickup.KeyId, "The key id should replicate with the spawn.");

            clientPickup.RequestPickUp();

            yield return WaitFor(() => hostRing.Holds(5), "The server never recorded the key.");
            yield return WaitFor(() => clientRing.Holds(5), "The client's ring never showed the key.");
            yield return WaitFor(() => ClientPickup(clients[0], 5) == null, "The picked up key should despawn on the client.");
        }

        [UnityTest]
        public IEnumerator Client_OpensDoor_ThroughTheRpc_WithItsKey_AndEveryMachineSeesItOpen()
        {
            yield return Connect("Client");
            DungeonKeyRing clientRing = ClientRing(clients[0]);
            GameObject hostGate = Gate(hostRing, 1);
            GameObject clientGate = Gate(clientRing, 1);
            hostRing.AddKey(1);
            yield return WaitFor(() => clientRing.Holds(1), "The client never saw it holds key 1.");

            clientRing.RequestOpen(1);

            yield return WaitFor(() => hostRing.IsOpen(1), "The server never opened the door.");
            yield return WaitFor(() => clientRing.IsOpen(1) && !clientGate.activeSelf, "The client's door never opened.");
            Assert.IsFalse(hostGate.activeSelf);
            Assert.IsFalse(clientRing.Holds(1), "Opening uses the key up.");
        }

        [UnityTest]
        public IEnumerator Client_CannotOpenDoor_WithoutItsKey()
        {
            yield return Connect("Client");
            DungeonKeyRing clientRing = ClientRing(clients[0]);
            GameObject hostGate = Gate(hostRing, 1);
            GameObject clientGate = Gate(clientRing, 1);
            hostRing.AddKey(2);
            yield return WaitFor(() => clientRing.Holds(2), "The client never saw it holds key 2.");

            clientRing.RequestOpen(1);
            for (int i = 0; i < 30; i++) yield return null;

            Assert.IsFalse(hostRing.IsOpen(1));
            Assert.IsFalse(clientRing.IsOpen(1));
            Assert.IsTrue(hostGate.activeSelf);
            Assert.IsTrue(clientGate.activeSelf);
        }

        [UnityTest]
        public IEnumerator HostOpensDoor_ClientSeesTheDoorOpen()
        {
            yield return Connect("Client");
            DungeonKeyRing clientRing = ClientRing(clients[0]);
            GameObject clientGate = Gate(clientRing, 1);
            hostRing.AddKey(1);
            hostRing.TryOpen(1, 0f);

            yield return WaitFor(() => !clientGate.activeSelf, "The client's gate should switch off when the host opens the door.");
        }

        [UnityTest]
        public IEnumerator LateJoiningClient_BuildsTheDoorAfterItOpened_AndSeesItOpen()
        {
            hostRing.AddKey(1);
            hostRing.TryOpen(1, 0f);
            hostRing.AddKey(2);
            yield return null;

            yield return Connect("LateClient");
            DungeonKeyRing lateRing = ClientRing(clients[0]);
            yield return WaitFor(() => lateRing.IsOpen(1) && lateRing.Holds(2), "The late joiner did not receive the ring state.");

            GameObject openedGate = Gate(lateRing, 1);
            GameObject lockedGate = Gate(lateRing, 2);

            Assert.IsFalse(openedGate.activeSelf, "A door that opened before the client joined must be open for it.");
            Assert.IsTrue(lockedGate.activeSelf, "A door that is still locked must stay locked for the late joiner.");
        }

        [UnityTest]
        public IEnumerator LateJoiningClient_IsGivenTheCharacterOfEveryPlayer_ItsOwnAndTheHosts()
        {
            // The host's player spawned its character before the client existed, so no RPC sent at that moment can reach the late joiner.
            Player hostPlayer = host.LocalClient.PlayerObject.GetComponent<Player>();
            yield return WaitFor(() => hostPlayer.character != null, "The host's player should have spawned its character.");
            yield return null;

            yield return Connect("LateClient");
            NetworkManager late = clients[0];
            Player ownPlayer = late.LocalClient.PlayerObject.GetComponent<Player>();
            Player hostCopy = null;
            yield return WaitFor(() =>
            {
                hostCopy = late.SpawnManager.SpawnedObjects.Values
                    .Select(o => o.GetComponent<Player>())
                    .FirstOrDefault(p => p != null && p.OwnerClientId == host.LocalClientId);
                return hostCopy != null;
            }, "The late joiner should see the host's player.");

            yield return WaitFor(() => hostCopy.character != null, "The host's character, spawned before the join, should be bound on the late joiner.");
            yield return WaitFor(() => ownPlayer.character != null, "The late joiner's own character should be bound.");

            Assert.AreSame(late, hostCopy.character.NetworkManager, "the bound character is the late client's copy, not the host's object");
            Assert.IsTrue(hostCopy.character.IsSpawned);
            Assert.AreEqual(host.LocalClientId, hostCopy.character.OwnerClientId);
            Assert.AreEqual(late.LocalClientId, ownPlayer.character.OwnerClientId);
            Assert.AreNotSame(hostCopy.character, ownPlayer.character);
        }

        [UnityTest]
        public IEnumerator Attach_GivesEachLockObjectOneInteractable_ThatAnswersForEveryCollider()
        {
            var rooms = new List<RoomBlueprint>();
            var placed = new List<PlacedRoom>();
            var gates = new List<GameObject>();
            for (int i = 0; i < 2; i++)
            {
                var door = new DoorDefinition(0, new Vector2Int(0, 1), Side.West, 4);
                var definition = new RoomDefinition(i, "Room" + i, new Vector2Int(4, 4), Vector2Int.zero, new[] { door });
                placed.Add(new PlacedRoom(i, definition, Vector2Int.zero, 0));

                var roomObject = new GameObject("Room" + i);
                var room = roomObject.AddComponent<RoomBlueprint>();
                var connection = new GameObject("Connection").AddComponent<ConnectionBlueprint>();
                connection.transform.SetParent(roomObject.transform, false);

                var gate = new GameObject("LockGate");
                gate.transform.SetParent(connection.transform, false);
                for (int c = 0; c < 2; c++)
                {
                    var part = new GameObject("Part" + c, typeof(BoxCollider));
                    part.transform.SetParent(gate.transform, false);
                }

                gate.SetActive(false);
                connection.LockObject = gate;
                room.SetConnections(new[] { connection });
                rooms.Add(room);
                gates.Add(gate);
            }

            var locked = new LockedDoor(new PlacedDoor(placed[0], placed[0].Definition.Doors[0]), new PlacedDoor(placed[1], placed[1].Definition.Doors[0]), 7, placed[0]);
            DungeonLocks.Attach(hostRing, rooms, new[] { locked });
            yield return null;

            foreach (GameObject gate in gates)
            {
                var interactables = gate.GetComponentsInChildren<LockedDoorInteractable>(true);
                Assert.AreEqual(1, interactables.Length, "Each lock object should have exactly one interactable.");
                foreach (Collider part in gate.GetComponentsInChildren<Collider>(true))
                {
                    Assert.IsTrue(interactables[0].IsLookedAtVia(part), "Looking at either collider should count.");
                }
            }

            Assert.IsFalse(gates[0].GetComponentInChildren<LockedDoorInteractable>(true).IsLookedAtVia(gates[1].GetComponentInChildren<Collider>(true)),
                "A collider of the other door must not count.");

            foreach (GameObject room in rooms.ConvertAll(x => x.gameObject)) Object.Destroy(room);
        }
    }
}
