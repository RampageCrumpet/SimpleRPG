using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
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
        private readonly List<Object> createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in createdAssets)
            {
                Object.DestroyImmediate(asset);
            }
            createdAssets.Clear();

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
        public void DoorOffGrid_AllowsModellingNoise()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15.05f, 0, 20), new Vector2Int(0, 1));

            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule == RoomValidator.RuleDoorOffGrid), Is.Empty);
        }

        [Test]
        public void DoorAPillarWidthOffGrid_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15.5f, 0, 20), new Vector2Int(0, 1));

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorOffGrid);
        }

        [Test]
        public void GeometryThatFillsTheFootprint_IsAccepted()
        {
            RoomBlueprint room = CreateValidRoom();
            AddFloor(room);

            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule.StartsWith("geometry-")), Is.Empty);
        }

        [Test]
        public void GeometryPastTheFootprint_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddFloor(room);
            GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.transform.SetParent(room.transform, false);
            trim.transform.position = new Vector3(31f, 0, 15);
            trim.transform.localScale = new Vector3(2, 1, 30);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleGeometryOutsideFootprint);
        }

        [Test]
        public void GeometryShortOfTheFootprint_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(room.transform, false);
            floor.transform.position = new Vector3(14.5f, 0, 15);
            floor.transform.localScale = new Vector3(29, 1, 30);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleGeometryShortOfFootprint);
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

        [Test]
        public void ModularDungeonRooms_UseChamberEdgesForFlushDoors()
        {
            Roomset roomset = AssetDatabase.LoadAssetAtPath<Roomset>("Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset");
            Assert.IsNotNull(roomset);
            RoomBlueprint crossroads = roomset.RoomCollection.Single(room => room.name == "Room_Crossroads");
            Assert.That(crossroads.Size, Is.EqualTo(new Vector2Int(6, 6)), "The crossroads footprint must stop at the chamber walls.");

            foreach (RoomBlueprint room in roomset.RoomCollection)
            {
                Assert.That(room.transform.Cast<Transform>().Any(child => child.name == "Connection" || child.name.StartsWith("Connection (")), Is.False, room.name);
                Assert.That(room.Connections.All(door => door.ConnectionLength == 0 && door.DoorObject != null), Is.True, room.name);
            }
        }

        [Test]
        public void ModularDungeonDoors_AllHaveALockObject()
        {
            Roomset roomset = AssetDatabase.LoadAssetAtPath<Roomset>("Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset");
            Assert.IsNotNull(roomset, "Could not load ModularDungeon roomset.");

            List<RoomValidationError> warnings = RoomValidator.CheckLockWarnings(roomset);

            Assert.That(warnings, Is.Empty, string.Join("\n", warnings.Select(warning => warning.ToString())));
        }

        [TestCase(-1.5f, 0)]  // 0.3 cells below the root is part of the first cell
        [TestCase(-4.9f, 0)]  // still less than a full cell
        [TestCase(-6f, -1)]   // 1.2 cells below needs a cell of its own
        [TestCase(-11f, -2)]
        public void OriginOffset_RoundsTowardZero(float pivotZ, int expectedOffset)
        {
            RoomBlueprint room = CreateValidRoom();
            var low = new GameObject("Low");
            low.transform.SetParent(room.transform, false);
            low.transform.position = new Vector3(0, 0, pivotZ);

            Assert.AreEqual(expectedOffset, RoomPlanner.CalculateOriginOffset(room, CellSize).y);
        }

        [TestCase(31f, 6)]   // 6.2 cells
        [TestCase(32.4f, 6)] // 6.48 cells rounds down
        [TestCase(32.6f, 7)] // 6.52 cells rounds up
        public void Size_RoundsToTheNearestCell(float farPivotX, int expectedWidth)
        {
            RoomBlueprint room = CreateValidRoom();
            room.transform.Find("Corner").position = new Vector3(farPivotX, 0, 30);

            Assert.AreEqual(expectedWidth, RoomPlanner.Plan(room, CellSize).Size.x);
        }

        [Test]
        public void RotatedRoot_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            room.transform.rotation = Quaternion.Euler(0, 90, 0);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleRootTransform);
        }

        [Test]
        public void ScaledRoot_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            room.transform.localScale = new Vector3(2, 1, 2);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleRootTransform);
        }

        [TestCase(12f)]
        [TestCase(2f)]
        [TestCase(-5f)]
        public void DoorLengthNotAWholeNumberOfCells_IsReported(float length)
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15.5f, 0, 20), new Vector2Int(0, 1), length);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorLength);
        }

        [Test]
        public void FlushDoorWithoutADoorObject_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15, 0, 30), new Vector2Int(0, 1), 0f);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorFlushNeedsDoorObject);
        }

        [Test]
        public void FlushDoorWithADoorObject_IsAccepted()
        {
            RoomBlueprint room = CreateValidRoom();
            ConnectionBlueprint door = AddDoor(room, new Vector3(15, 0, 30), new Vector2Int(0, 1), 0f);
            GameObject leaf = new GameObject("DoorLeaf");
            leaf.transform.SetParent(room.transform, false);
            leaf.transform.position = door.transform.position;
            spawned.Add(leaf);
            door.DoorObject = leaf;

            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule.StartsWith("door-")), Is.Empty);
        }

        [Test]
        public void ClaimedCellOutsideTheRectangle_IsPlannedAndAccepted()
        {
            RoomBlueprint room = CreateValidRoom();
            AddFloor(room);
            AddClaimedCell(room, new Vector3(32.5f, 0, 2.5f));

            RoomPlan plan = RoomPlanner.Plan(room, CellSize);

            Assert.That(plan.ExtraCells, Is.EqualTo(new[] { new Vector2Int(6, 0) }));
            Assert.That(plan.Size, Is.EqualTo(new Vector2Int(6, 6)), "A marker must not stretch the room.");
            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule.StartsWith("claimed-")), Is.Empty);
        }

        [Test]
        public void GeometryInAClaimedCell_IsAccepted()
        {
            RoomBlueprint room = CreateValidRoom();
            AddFloor(room);
            GameObject alcove = GameObject.CreatePrimitive(PrimitiveType.Cube);
            alcove.transform.SetParent(room.transform, false);
            alcove.transform.position = new Vector3(32.5f, 0, 2.5f);
            alcove.transform.localScale = new Vector3(5, 1, 5);
            AddClaimedCell(room, new Vector3(32.5f, 0, 2.5f));

            Assert.That(RoomValidator.Validate(room, CellSize).Where(error => error.Rule.StartsWith("geometry-")), Is.Empty);
        }

        [Test]
        public void GeometryInAnUnclaimedCell_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddFloor(room);
            GameObject alcove = GameObject.CreatePrimitive(PrimitiveType.Cube);
            alcove.transform.SetParent(room.transform, false);
            alcove.transform.position = new Vector3(32.5f, 0, 2.5f);
            alcove.transform.localScale = new Vector3(5, 1, 5);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleGeometryOutsideFootprint);
        }

        [Test]
        public void ClaimedCellInsideTheRectangle_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddClaimedCell(room, new Vector3(12.5f, 0, 12.5f));

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleClaimedCellInsideFootprint);
        }

        [Test]
        public void ClaimedCellAwayFromTheRoom_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddClaimedCell(room, new Vector3(42.5f, 0, 2.5f));

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleClaimedCellDetached);
        }

        private void AddClaimedCell(RoomBlueprint room, Vector3 position)
        {
            GameObject marker = new GameObject("Claim");
            marker.transform.SetParent(room.transform, false);
            marker.transform.position = position;
            marker.AddComponent<RoomClaimedCell>();
            spawned.Add(marker);
        }

        [Test]
        public void DoorWithoutWidth_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15.5f, 0, 20), new Vector2Int(0, 1)).ConnectionWidth = 0f;

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleDoorWidth);
        }

        [Test]
        public void LockObjectInsideTheRoomAndInactive_IsAccepted()
        {
            RoomBlueprint room = CreateValidRoom();
            foreach (ConnectionBlueprint door in room.GetComponentsInChildren<ConnectionBlueprint>())
            {
                AddLock(door);
            }

            Assert.That(RoomValidator.Validate(room, CellSize), Is.Empty);
        }

        [Test]
        public void LockObjectOutsideTheRoom_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            ConnectionBlueprint door = room.GetComponentsInChildren<ConnectionBlueprint>().First();
            door.LockObject = new GameObject("ExternalLock");
            spawned.Add(door.LockObject);
            door.LockObject.SetActive(false);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleLockOutsideRoom);
        }

        [Test]
        public void LockObjectThatStartsActive_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddLock(room.GetComponentsInChildren<ConnectionBlueprint>().First()).SetActive(true);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleLockStartsActive);
        }

        [Test]
        public void DoorWithoutLockObject_IsNotAnError()
        {
            RoomBlueprint room = CreateValidRoom();

            Assert.That(RoomValidator.Validate(room, CellSize), Is.Empty);
        }

        [Test]
        public void DoorsWithoutLockObject_AreWarnedAboutPerRoom()
        {
            RoomBlueprint room = CreateValidRoom();
            ConnectionBlueprint[] doors = room.GetComponentsInChildren<ConnectionBlueprint>();
            AddLock(doors[0]);
            Roomset roomset = CreateRoomset(room);

            List<RoomValidationError> warnings = RoomValidator.CheckLockWarnings(roomset);

            Assert.That(warnings.Select(warning => warning.Rule), Is.EqualTo(new[] { RoomValidator.RuleRoomsetDoorNoLock }));
            StringAssert.Contains("3 door(s)", warnings.Single().Message);
        }

        [Test]
        public void RoomWhereEveryDoorHasALock_HasNoLockWarning()
        {
            RoomBlueprint room = CreateValidRoom();
            foreach (ConnectionBlueprint door in room.GetComponentsInChildren<ConnectionBlueprint>())
            {
                AddLock(door);
            }

            Assert.That(RoomValidator.CheckLockWarnings(CreateRoomset(room)), Is.Empty);
        }

        [Test]
        public void SealingObjectOutsideTheRoom_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            ConnectionBlueprint door = AddDoor(room, new Vector3(15.5f, 0, 20), new Vector2Int(0, 1));
            door.SealingObject = new GameObject("External");
            spawned.Add(door.SealingObject);
            door.SealingObject.SetActive(false);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleSealOutsideRoom);
        }

        [Test]
        public void SealingObjectThatStartsActive_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddDoor(room, new Vector3(15.5f, 0, 20), new Vector2Int(0, 1)).SealingObject.SetActive(true);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleSealStartsActive);
        }

        [Test]
        public void StrayEmptyFarFromTheGeometry_IsReported()
        {
            RoomBlueprint room = CreateValidRoom();
            AddFloor(room);
            Assert.That(RoomValidator.Validate(room, CellSize), Is.Empty, "A room whose pivots all sit within its geometry is fine.");

            var stray = new GameObject("Stray");
            stray.transform.SetParent(room.transform, false);
            stray.transform.position = new Vector3(200, 0, 200);

            AssertRule(RoomValidator.Validate(room, CellSize), RoomValidator.RuleStrayPivot);
        }

        [Test]
        public void Roomset_WithADeadEnd_HasNoCompositionErrors()
        {
            Roomset roomset = CreateRoomset(CreateValidRoom(), CreateRoomWithDoors(1));

            Assert.That(RoomValidator.CheckRoomsetComposition(roomset), Is.Empty);
        }

        [Test]
        public void Roomset_WithoutRooms_IsReported()
        {
            Roomset roomset = CreateRoomset();

            AssertRule(RoomValidator.ValidateRoomset(roomset), RoomValidator.RuleRoomsetEmpty);
        }

        [Test]
        public void Roomset_RoomWithoutDoors_IsReported()
        {
            Roomset roomset = CreateRoomset(CreateRoomWithDoors(1), CreateRoomWithDoors(0));

            AssertRule(RoomValidator.CheckRoomsetComposition(roomset), RoomValidator.RuleRoomsetRoomNoDoors);
        }

        [Test]
        public void Roomset_WithoutADeadEnd_IsReported()
        {
            Roomset roomset = CreateRoomset(CreateValidRoom(), CreateRoomWithDoors(2));

            AssertRule(RoomValidator.CheckRoomsetComposition(roomset), RoomValidator.RuleRoomsetNoDeadEnd);
        }

        [Test]
        public void Roomset_WithTheSameRoomTwice_IsReported()
        {
            RoomBlueprint deadEnd = CreateRoomWithDoors(1);
            Roomset roomset = CreateRoomset(deadEnd, deadEnd, CreateValidRoom());

            AssertRule(RoomValidator.CheckRoomsetComposition(roomset), RoomValidator.RuleRoomsetDuplicate);
        }

        [Test]
        public void Roomset_WithDoorsOfDifferentLengths_IsReported()
        {
            RoomBlueprint shortDoor = CreateRoomWithDoors(1);
            shortDoor.GetComponentInChildren<ConnectionBlueprint>().ConnectionLength = 15f;
            Roomset roomset = CreateRoomset(CreateRoomWithDoors(1), shortDoor);

            AssertRule(RoomValidator.CheckRoomsetComposition(roomset), RoomValidator.RuleRoomsetDoorLengths);
        }

        [Test]
        public void Roomset_WhereNothingCanBePlaced_IsReported()
        {
            RoomBlueprint start = CreateRoomWithDoors(1);
            start.Role = RoomRole.Start;
            RoomBlueprint unpicked = CreateRoomWithDoors(1);
            unpicked.SelectionWeight = 0f;
            Roomset roomset = CreateRoomset(start, unpicked);

            AssertRule(RoomValidator.CheckRoomsetComposition(roomset), RoomValidator.RuleRoomsetNothingPlaceable);
        }

        [Test]
        public void Roomset_WithNegativeWeight_IsReported()
        {
            RoomBlueprint room = CreateRoomWithDoors(1);
            room.SelectionWeight = -1f;
            Roomset roomset = CreateRoomset(room, CreateRoomWithDoors(1));

            AssertRule(RoomValidator.CheckRoomsetComposition(roomset), RoomValidator.RuleRoomsetRoomWeight);
        }

        private Roomset CreateRoomset(params RoomBlueprint[] rooms)
        {
            Roomset roomset = ScriptableObject.CreateInstance<Roomset>();
            roomset.name = "TestRoomset";
            roomset.cellSize = CellSize;
            roomset.RoomCollection = rooms.ToList();
            createdAssets.Add(roomset);
            return roomset;
        }

        /// <summary>
        /// A 6x6 cell room with the given number of correctly placed doors, taken in the order top, bottom, left, right.
        /// </summary>
        private RoomBlueprint CreateRoomWithDoors(int doorCount)
        {
            GameObject root = new GameObject("TestRoom");
            spawned.Add(root);
            RoomBlueprint room = root.AddComponent<RoomBlueprint>();

            new GameObject("Corner").transform.SetParent(root.transform, false);
            root.transform.Find("Corner").position = new Vector3(30, 0, 30);

            Vector3[] positions = { new Vector3(15, 0, 20), new Vector3(15, 0, 10), new Vector3(10, 0, 15), new Vector3(20, 0, 15) };
            Vector2Int[] facings = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };
            for (int i = 0; i < doorCount; i++)
            {
                AddDoor(room, positions[i], facings[i]);
            }

            return room;
        }

        /// <summary>
        /// Gives a room a floor mesh covering its 6x6 cells, so the validator has geometry to compare pivots with.
        /// </summary>
        private void AddFloor(RoomBlueprint room)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(room.transform, false);
            floor.transform.position = new Vector3(15, 0, 15);
            floor.transform.localScale = new Vector3(30, 1, 30);
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

        private ConnectionBlueprint AddDoor(RoomBlueprint room, Vector3 position, Vector2Int facing, float length = DoorLength, bool seal = true, Transform parent = null)
        {
            GameObject door = new GameObject("Door");
            spawned.Add(door);
            door.transform.SetParent(parent != null ? parent : room.transform, false);
            door.transform.position = position;
            door.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.y));

            ConnectionBlueprint connection = door.AddComponent<ConnectionBlueprint>();
            connection.ConnectionLength = length;
            connection.ConnectionWidth = 1f;

            if (seal)
            {
                connection.SealingObject = new GameObject("Seal");
                spawned.Add(connection.SealingObject);
                connection.SealingObject.transform.SetParent(door.transform, false);
                connection.SealingObject.SetActive(false);
            }

            return connection;
        }

        private GameObject AddLock(ConnectionBlueprint door)
        {
            door.LockObject = new GameObject("Lock");
            spawned.Add(door.LockObject);
            door.LockObject.transform.SetParent(door.transform, false);
            door.LockObject.SetActive(false);
            return door.LockObject;
        }

        private static void AssertRule(List<RoomValidationError> errors, string rule)
        {
            Assert.That(errors.Select(error => error.Rule), Does.Contain(rule), string.Join("\n", errors.Select(error => error.ToString())));
        }
    }
}
