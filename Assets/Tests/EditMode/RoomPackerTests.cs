using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using SimpleRPG.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that the packer writes packed values onto good rooms, and leaves every asset untouched when anything is wrong.
    /// Rooms are saved as throwaway prefab assets in a folder that is deleted afterwards.
    /// </summary>
    public class RoomPackerTests
    {
        private const string Folder = "Assets/Tests/EditMode/TempPackerRooms";
        private const float CellSize = 5f;

        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Tests/EditMode", "TempPackerRooms");
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in created)
            {
                Object.DestroyImmediate(asset);
            }
            created.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void GoodRoom_IsPackedAndSaved()
        {
            RoomBlueprint room = SaveRoom("Good", doorCount: 2);

            PackResult result = RoomPacker.PackRoom(room, CellSize);

            Assert.IsTrue(result.Packed, string.Join("\n", result.Errors));
            RoomBlueprint saved = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(room)).GetComponent<RoomBlueprint>();
            Assert.AreEqual(new Vector2Int(6, 6), saved.Size);
            Assert.AreEqual(2, saved.Connections.Count());
            Assert.That(RoomValidator.CheckPackedData(saved, CellSize), Is.Empty);
        }

        [Test]
        public void BadRoom_IsRefusedAndTheAssetIsLeftUntouched()
        {
            RoomBlueprint room = SaveRoom("Bad", doorCount: 2, extraDuplicateDoor: true);
            string path = AssetDatabase.GetAssetPath(room);
            string before = File.ReadAllText(path);

            PackResult result = RoomPacker.PackRoom(room, CellSize);

            Assert.IsFalse(result.Packed);
            Assert.That(result.Errors.Select(error => error.Rule), Does.Contain(RoomValidator.RuleDoorDuplicate));
            Assert.AreEqual(before, File.ReadAllText(path), "A refused room must not change on disk.");
        }

        [Test]
        public void RoomThatIsNotAnAsset_IsRefused()
        {
            var scene = new GameObject("SceneRoom");
            created.Add(scene);
            RoomBlueprint room = scene.AddComponent<RoomBlueprint>();

            PackResult result = RoomPacker.PackRoom(room, CellSize);

            Assert.IsFalse(result.Packed);
            Assert.That(result.Errors.Select(error => error.Rule), Does.Contain(RoomPacker.RuleNotAnAsset));
        }

        [Test]
        public void Roomset_IsPackedAllOrNothing()
        {
            RoomBlueprint good = SaveRoom("GoodOne", doorCount: 1);
            RoomBlueprint bad = SaveRoom("BadOne", doorCount: 1, extraDuplicateDoor: true);
            Roomset roomset = CreateRoomset(good, bad);
            string goodBefore = File.ReadAllText(AssetDatabase.GetAssetPath(good));

            List<PackResult> results = RoomPacker.PackRoomset(roomset);

            Assert.That(results.Any(result => result.Packed), Is.False, "No room may be packed when one is refused.");
            Assert.That(results.First(result => result.Room == bad).Errors, Is.Not.Empty);
            Assert.AreEqual(goodBefore, File.ReadAllText(AssetDatabase.GetAssetPath(good)), "The good room must not have been written.");
        }

        [Test]
        public void Roomset_WhoseRoomsAreAllGood_IsPackedTogether()
        {
            Roomset roomset = CreateRoomset(SaveRoom("One", doorCount: 1), SaveRoom("Two", doorCount: 2));

            List<PackResult> results = RoomPacker.PackRoomset(roomset);

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results.All(result => result.Packed), string.Join("\n", results.SelectMany(result => result.Errors)));
        }

        [Test]
        public void Roomset_WithAProblemOfItsOwn_IsRefusedWholesale()
        {
            // Two-door rooms only: valid individually, but nothing can end a branch.
            Roomset roomset = CreateRoomset(SaveRoom("Corridor", doorCount: 2));

            List<PackResult> results = RoomPacker.PackRoomset(roomset);

            Assert.That(results.Any(result => result.Packed), Is.False);
            Assert.That(results.SelectMany(result => result.Errors).Select(error => error.Rule), Does.Contain(RoomValidator.RuleRoomsetNoDeadEnd));
        }

        private Roomset CreateRoomset(params RoomBlueprint[] rooms)
        {
            Roomset roomset = ScriptableObject.CreateInstance<Roomset>();
            roomset.name = "TempRoomset";
            roomset.cellSize = CellSize;
            roomset.RoomCollection = rooms.ToList();
            created.Add(roomset);
            return roomset;
        }

        /// <summary>
        /// Saves a 6x6 cell room with the given number of correctly placed doors as a prefab asset and returns the asset's component.
        /// </summary>
        private RoomBlueprint SaveRoom(string name, int doorCount, bool extraDuplicateDoor = false)
        {
            var root = new GameObject(name);
            root.AddComponent<RoomBlueprint>();
            new GameObject("Corner").transform.SetParent(root.transform, false);
            root.transform.Find("Corner").position = new Vector3(30, 0, 30);

            Vector3[] positions = { new Vector3(10, 0, 15), new Vector3(20, 0, 15) };
            Vector2Int[] facings = { new Vector2Int(-1, 0), new Vector2Int(1, 0) };
            for (int i = 0; i < doorCount; i++)
            {
                AddDoor(root, positions[i], facings[i]);
            }

            if (extraDuplicateDoor)
            {
                AddDoor(root, positions[0], facings[0]);
            }

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return asset.GetComponent<RoomBlueprint>();
        }

        private static void AddDoor(GameObject room, Vector3 position, Vector2Int facing)
        {
            var door = new GameObject("Door");
            door.transform.SetParent(room.transform, false);
            door.transform.position = position;
            door.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.y));

            ConnectionBlueprint connection = door.AddComponent<ConnectionBlueprint>();
            connection.ConnectionLength = 20f;
            connection.ConnectionWidth = 1f;
            connection.SealingObject = new GameObject("Seal");
            connection.SealingObject.transform.SetParent(door.transform, false);
            connection.SealingObject.SetActive(false);
        }
    }
}
