using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks doors longer and wider than the standard one: the cells between two joined rooms stay clear, and the cells a wide door covers.
    /// </summary>
    public class PassageTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawnedObject in spawned)
            {
                Object.DestroyImmediate(spawnedObject);
            }
            spawned.Clear();
        }

        [Test]
        public void RoomsJoinedByALongDoor_StandApartWithAGapBetween()
        {
            List<RoomDefinition> set = TestRooms.LongDoorSet();
            PlacedRoom left = new PlacedRoom(0, set[1], Vector2Int.zero, 0);
            PlacedRoom right = new PlacedRoom(1, set[1], new Vector2Int(8, 0), 0);

            Assert.IsTrue(left.Doors[1].Mates(right.Doors[0]));
            Assert.IsFalse(left.Cells().Intersect(right.Cells()).Any());
            Assert.IsFalse(left.Contains(new Vector2Int(6, 3)) || right.Contains(new Vector2Int(6, 3)), "Cell (6,3) is in the gap.");
        }

        [Test]
        public void ARoomInTheGapBetweenJoinedDoors_IsReported()
        {
            List<RoomDefinition> set = TestRooms.LongDoorSet();
            PlacedRoom left = new PlacedRoom(0, set[1], Vector2Int.zero, 0);
            PlacedRoom right = new PlacedRoom(1, set[1], new Vector2Int(8, 0), 0);
            var tiny = new RoomDefinition(9, "Tiny", new Vector2Int(2, 2), Vector2Int.zero, new List<DoorDefinition>());
            PlacedRoom blocker = new PlacedRoom(2, tiny, new Vector2Int(6, 2), 0);

            var layout = new Layout(0, 0, new[] { left, right, blocker }, new[] { left.Doors[0], right.Doors[1] });

            Assert.That(LayoutValidator.Validate(layout).Select(v => v.Rule), Does.Contain(LayoutValidator.RulePassageBlocked));
        }

        [Test]
        public void JoinedRoomsWithAnEmptyGap_AreValid()
        {
            List<RoomDefinition> set = TestRooms.LongDoorSet();
            PlacedRoom left = new PlacedRoom(0, set[1], Vector2Int.zero, 0);
            PlacedRoom right = new PlacedRoom(1, set[1], new Vector2Int(8, 0), 0);

            var layout = new Layout(0, 0, new[] { left, right }, new[] { left.Doors[0], right.Doors[1] });

            Assert.That(LayoutValidator.Validate(layout), Is.Empty);
        }

        [Test]
        public void LongDoorSet_AlwaysGivesValidLayouts_WithNothingInTheGaps([NUnit.Framework.Range(8, 20, 6)] int roomCount)
        {
            var failures = new List<string>();

            for (int seed = 0; seed < 200; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, TestRooms.LongDoorSet()).Generate(roomCount);

                if (!result.Success)
                {
                    failures.Add($"Seed {seed}: {result}");
                    continue;
                }

                failures.AddRange(LayoutValidator.Validate(result.Layout).Select(violation => $"Seed {seed}: {violation}"));
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(10)));
        }

        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        [TestCase(6, 6)]
        public void DoorWidth_CoversAnEvenNumberOfCellsCentredOnTheLine(int widthCells, int expectedCells)
        {
            List<Vector2Int> row = GridMath.CellsAcross(new Vector2Int(5, 5), Side.East, 0, widthCells).ToList();

            Assert.That(row, Has.Count.EqualTo(expectedCells));
            Assert.AreEqual(5 - expectedCells / 2, row.Min(cell => cell.y), "Centred on the line y=5");
            Assert.AreEqual(5 + expectedCells / 2 - 1, row.Max(cell => cell.y));
            Assert.That(row.Select(cell => cell.x).Distinct(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void WideDoor_BlocksARoomStandingAtTheEdgeOfItsWidth()
        {
            // A 4 cell wide door's passage covers y=1..4 at the point (2,3); a narrow door would only cover y=2..3.
            var wide = new RoomDefinition(0, "Wide", new Vector2Int(6, 6), Vector2Int.zero,
                new List<DoorDefinition> { new DoorDefinition(0, new Vector2Int(4, 3), Side.East, 4, widthCells: 4) });
            var narrow = new RoomDefinition(1, "Narrow", new Vector2Int(6, 6), Vector2Int.zero,
                new List<DoorDefinition> { new DoorDefinition(0, new Vector2Int(4, 3), Side.East, 4) });

            PlacedDoor wideDoor = new PlacedRoom(0, wide, Vector2Int.zero, 0).Doors[0];
            PlacedDoor narrowDoor = new PlacedRoom(0, narrow, Vector2Int.zero, 0).Doors[0];
            var edgeCell = new Vector2Int(6, 1);

            Assert.That(wideDoor.PassageCells().Select(p => p.cell), Has.Member(edgeCell));
            Assert.That(narrowDoor.PassageCells().Select(p => p.cell), Has.No.Member(edgeCell));
        }

        [TestCase(2f, 2)]   // 0.4 cells: still the pair either side of the line
        [TestCase(5f, 2)]   // one cell
        [TestCase(12f, 4)]  // 2.4 cells rounds up to the next even width
        [TestCase(20f, 4)]  // four cells
        public void Baker_TurnsDoorWidthInUnitsIntoEvenCells(float widthUnits, int expectedCells)
        {
            var root = new GameObject("BakedRoom");
            spawned.Add(root);
            RoomBlueprint room = root.AddComponent<RoomBlueprint>();
            room.Size = new Vector2Int(6, 6);

            var door = new GameObject("Door");
            door.transform.SetParent(root.transform, false);
            door.transform.rotation = Quaternion.LookRotation(Vector3.right);
            ConnectionBlueprint connection = door.AddComponent<ConnectionBlueprint>();
            connection.ConnectionLength = 20f;
            connection.ConnectionWidth = widthUnits;
            connection.location = new Vector2Int(4, 3);
            room.SetConnections(new[] { connection });

            RoomDefinition baked = RoomDefinitionBaker.Bake(room, 0, 5f);

            Assert.AreEqual(expectedCells, baked.Doors[0].WidthCells);
            Assert.AreEqual(4, baked.Doors[0].LengthCells);
            Assert.AreEqual(new Vector2Int(4, 3), baked.Doors[0].Point);
            Assert.AreEqual(Side.East, baked.Doors[0].Side);
        }
    }
}
