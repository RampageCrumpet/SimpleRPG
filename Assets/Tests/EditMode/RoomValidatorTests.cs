using System.Collections.Generic;
using System.Linq;
using LevelGeneration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that <see cref="RoomValidator"/> flags each broken convention and accepts a correctly authored room.
    /// Rooms are built in code: a 6x6 cell room with its lowest corner at the origin and four doors, one per edge.
    /// </summary>
    public class RoomValidatorTests
    {
        private const float CellSize = 5f;
        private const float DoorLength = 20f;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject room in spawned)
            {
                if (room != null)
                {
                    Object.DestroyImmediate(room);
                }
            }
            spawned.Clear();
        }

        [Test]
        public void ValidRoom_HasNoErrors()
        {
            RoomBlueprint room = CreateValidRoom();

            Assert.That(RoomValidator.Validate(room, CellSize), Is.Empty);
        }

        [Test]
        public void DoorOffGrid_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(12.6f, 0, 20), new Vector2Int(0, 1));

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorOffGrid);
        }

        [Test]
        public void DoorOffGrid_AllowsAuthoredPillarOffset()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15.5f, 0, 20), new Vector2Int(0, 1));

            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule == RoomValidator.RuleDoorOffGrid), Is.Empty);
        }

        [Test]
        public void DoorTargetInsideFootprint_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            // Faces inwards from the top edge, so the cell it opens onto is inside the room.
            AddDoor(room, new Vector3(15, 0, 25), new Vector2Int(0, -1), 5f);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorTargetInFootprint);
        }

        [Test]
        public void DoorInset_IsReported_WhenEnforced()
        {
            RoomBlueprint room = CreateValidRoom();
            // Top edge door one cell in instead of the two it asks for.
            AddDoor(room, new Vector3(25, 0, 25), new Vector2Int(0, 1), enforceInset: true, insetCells: 2);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorInset);
        }

        [Test]
        public void DoorInset_IsNotChecked_WhenNotEnforced()
        {
            RoomBlueprint room = CreateValidRoom();
            // Same one-cell-in door, but this connection places doors freely.
            AddDoor(room, new Vector3(25, 0, 25), new Vector2Int(0, 1));

            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule == RoomValidator.RuleDoorInset), Is.Empty);
        }

        [Test]
        public void DoorInset_PassesAtItsOwnInset()
        {
            RoomBlueprint room = CreateValidRoom();
            // Styles can differ per connection: this one enforces an inset of 1 and sits exactly there.
            AddDoor(room, new Vector3(25, 0, 25), new Vector2Int(0, 1), enforceInset: true, insetCells: 1);

            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule == RoomValidator.RuleDoorInset), Is.Empty);
        }

        [Test]
        public void DuplicateDoor_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15, 0, 20), new Vector2Int(0, 1));

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorDuplicate);
        }

        [Test]
        public void DoorWithoutSealingObject_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15, 0, 20), new Vector2Int(0, 1), DoorLength, seal: false);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorMissingSeal);
        }

        [Test]
        public void SealedDoorNestedInSubObject_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            Transform corner = room.transform.Find("Corner");
            AddDoor(room, new Vector3(15, 0, 20), new Vector2Int(0, 1), DoorLength, parent: corner);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorStrayNested);
        }

        [Test]
        public void UnsealedNestedBlueprint_IsIgnored()
        {
            RoomBlueprint room = CreateValidRoom();
            Transform corner = room.transform.Find("Corner");
            AddDoor(room, new Vector3(12.6f, 0, 20), new Vector2Int(0, 1), DoorLength, seal: false, parent: corner);

            Assert.That(RoomValidator.Validate(room, CellSize), Is.Empty);
        }

        [Test]
        public void DoorNotAxisAligned_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15, 0, 20), new Vector2Int(1, 1));

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorNotAxisAligned);
        }

        [Test]
        public void StoredSize_DisagreesWithGeometry_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            room.Size = new Vector2Int(5, 6);
            room.OriginOffset = Vector2Int.zero;

            AssertRule(RoomValidator.CheckPackedData(room, CellSize), RoomValidator.RuleSizeVsGeometry);
        }

        [Test]
        public void StoredOriginOffset_IsPositive_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            room.Size = new Vector2Int(6, 6);
            room.OriginOffset = new Vector2Int(0, 1);

            AssertRule(RoomValidator.CheckPackedData(room, CellSize), RoomValidator.RuleOriginOffset);
        }

        [Test]
        public void StoredDoorList_IsStale_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            room.Size = new Vector2Int(6, 6);
            room.OriginOffset = Vector2Int.zero;
            // Nothing packed yet, so the stored list is empty while the geometry has four doors.

            AssertRule(RoomValidator.CheckPackedData(room, CellSize), RoomValidator.RulePackedDoorData);
        }

        [Test]
        public void Plan_DoesNotModifyTheRoom()
        {
            RoomBlueprint room = CreateValidRoom();
            Vector2Int sizeBefore = room.Size;
            Vector2Int originBefore = room.OriginOffset;

            RoomPlan plan = RoomPlanner.Plan(room, CellSize);

            Assert.That(room.Size, Is.EqualTo(sizeBefore));
            Assert.That(room.OriginOffset, Is.EqualTo(originBefore));
            Assert.That(plan.Size, Is.EqualTo(new Vector2Int(6, 6)));
            Assert.That(plan.Doors, Has.Count.EqualTo(4));
        }

        [Test]
        public void ModularDungeonRooms_AllPassValidation()
        {
            Roomset roomset = AssetDatabase.LoadAssetAtPath<Roomset>("Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset");
            Assert.IsNotNull(roomset, "Could not load ModularDungeon roomset.");

            List<RoomValidationError> errors = RoomValidator.ValidateRoomset(roomset);

            Assert.That(errors, Is.Empty, string.Join("\n", errors.Select(error => error.ToString())));
        }

        /// <summary>
        /// A 6x6 cell room with its lowest corner at the origin, with one correctly placed door on each edge.
        /// </summary>
        private RoomBlueprint CreateValidRoom()
        {
            GameObject root = new GameObject("TestRoom");
            spawned.Add(root);
            RoomBlueprint room = root.AddComponent<RoomBlueprint>();

            // A pivot at the far corner gives the room its 6x6 span.
            new GameObject("Corner").transform.SetParent(root.transform, false);
            root.transform.Find("Corner").position = new Vector3(30, 0, 30);

            AddDoor(room, new Vector3(15, 0, 20), new Vector2Int(0, 1));  // top, inset 2 from y=6
            AddDoor(room, new Vector3(15, 0, 10), new Vector2Int(0, -1)); // bottom, inset 2 from y=0
            AddDoor(room, new Vector3(10, 0, 15), new Vector2Int(-1, 0)); // left, inset 2 from x=0
            AddDoor(room, new Vector3(20, 0, 15), new Vector2Int(1, 0));  // right, inset 2 from x=6

            return room;
        }

        private ConnectionBlueprint AddDoor(RoomBlueprint room, Vector3 position, Vector2Int facing, float length = DoorLength, bool seal = true, Transform parent = null, bool enforceInset = false, int insetCells = 2)
        {
            GameObject door = new GameObject("Door");
            spawned.Add(door);
            door.transform.SetParent(parent != null ? parent : room.transform, false);
            door.transform.position = position;
            door.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.y));

            ConnectionBlueprint connection = door.AddComponent<ConnectionBlueprint>();
            connection.ConnectionLength = length;
            connection.ConnectionWidth = 1f;
            connection.EnforceDoorInset = enforceInset;
            connection.DoorInsetCells = insetCells;

            if (seal)
            {
                connection.SealingObject = new GameObject("Seal");
                spawned.Add(connection.SealingObject);
                connection.SealingObject.transform.SetParent(door.transform, false);
            }

            return connection;
        }

        private static void AssertRule(List<RoomValidationError> errors, string rule)
        {
            Assert.That(errors.Select(error => error.Rule), Does.Contain(rule), string.Join("\n", errors.Select(error => error.ToString())));
        }
    }
}
