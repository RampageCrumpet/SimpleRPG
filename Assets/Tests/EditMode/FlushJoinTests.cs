using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleRPG.LevelGeneration;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Rooms that join with no corridor between them (doors of length zero) and rooms that claim cells beyond their rectangle.
    /// </summary>
    public class FlushJoinTests
    {
        private const int Seeds = 100;

        private static DoorDefinition West(int index) => new DoorDefinition(index, new Vector2Int(0, 2), Side.West, 0);
        private static DoorDefinition East(int index) => new DoorDefinition(index, new Vector2Int(4, 2), Side.East, 0);
        private static DoorDefinition North(int index) => new DoorDefinition(index, new Vector2Int(2, 4), Side.North, 0);
        private static DoorDefinition South(int index) => new DoorDefinition(index, new Vector2Int(2, 0), Side.South, 0);

        private static RoomDefinition Room(int index, string name, IReadOnlyList<Vector2Int> extraCells, params DoorDefinition[] doors)
        {
            return new RoomDefinition(index, name, new Vector2Int(4, 4), Vector2Int.zero, doors.ToList(), extraCells: extraCells);
        }

        /// <summary>
        /// 4x4 rooms whose doors stand on the edge and are zero cells long, so joined rooms touch.
        /// </summary>
        private static List<RoomDefinition> FlushSet(IReadOnlyList<Vector2Int> extraCells = null)
        {
            return new List<RoomDefinition>
            {
                Room(0, "DeadEnd", null, West(0)),
                Room(1, "Corridor", extraCells, West(0), East(1)),
                Room(2, "Corner", null, West(0), North(1)),
                Room(3, "Crossroads", extraCells, West(0), East(1), North(2), South(3)),
            };
        }

        [Test]
        public void FlushRooms_AlwaysGiveValidLayouts()
        {
            for (int seed = 0; seed < Seeds; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, FlushSet()).Generate(15);

                Assert.IsTrue(result.Success, $"seed {seed}: {result}");
                Assert.That(result.Layout.Rooms.Count, Is.GreaterThanOrEqualTo(15));
                Assert.That(LayoutValidator.Validate(result.Layout), Is.Empty, $"seed {seed}");
            }
        }

        [Test]
        public void FlushRooms_TouchWhereTheyJoin()
        {
            GenerationResult result = new LayoutGenerator(3, FlushSet()).Generate(15);
            List<(PlacedDoor a, PlacedDoor b)> joins = LockSolver.Joins(result.Layout.Rooms, result.Layout.SealedDoors);

            Assert.That(joins, Is.Not.Empty);
            foreach ((PlacedDoor a, PlacedDoor b) in joins)
            {
                Assert.That(a.Point, Is.EqualTo(b.Point), "Both doors of a flush join stand on the same line.");
            }
        }

        [Test]
        public void RoomsThatClaimExtraCells_NeverOverlap()
        {
            var alcove = new[] { new Vector2Int(4, 0), new Vector2Int(-1, 3) };

            for (int seed = 0; seed < Seeds; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, FlushSet(alcove)).Generate(15);

                Assert.IsTrue(result.Success, $"seed {seed}: {result}");
                Assert.That(LayoutValidator.Validate(result.Layout), Is.Empty, $"seed {seed}");
            }
        }

        [Test]
        public void ClaimedCells_TurnWithTheRoom([NUnit.Framework.Range(0, 3)] int turns)
        {
            RoomDefinition definition = FlushSet(new[] { new Vector2Int(4, 0) })[1];
            var room = new PlacedRoom(0, definition, new Vector2Int(10, 10), turns);

            List<Vector2Int> cells = room.Cells().ToList();
            Assert.That(cells, Has.Count.EqualTo(17));
            Assert.That(cells.Distinct().Count(), Is.EqualTo(17), "No cell may be listed twice.");

            Vector2Int claimed = room.ExtraCells.Single();
            Assert.That(room.Contains(claimed));
            Assert.That(claimed.x < room.Min.x || claimed.x >= room.Min.x + room.Size.x || claimed.y < room.Min.y || claimed.y >= room.Min.y + room.Size.y, "The claimed cell lies outside the rectangle.");

            // It touches the rectangle along an edge, not just at a corner.
            int dx = claimed.x < room.Min.x ? room.Min.x - claimed.x : claimed.x >= room.Min.x + room.Size.x ? claimed.x - (room.Min.x + room.Size.x - 1) : 0;
            int dy = claimed.y < room.Min.y ? room.Min.y - claimed.y : claimed.y >= room.Min.y + room.Size.y ? claimed.y - (room.Min.y + room.Size.y - 1) : 0;
            Assert.That(dx + dy, Is.EqualTo(1));
        }

        [Test]
        public void ClaimedCellInsideTheRectangle_IsIgnored()
        {
            RoomDefinition definition = FlushSet(new[] { new Vector2Int(1, 1) })[1];
            var room = new PlacedRoom(0, definition, Vector2Int.zero, 0);

            Assert.That(room.Cells().Count(), Is.EqualTo(16));
        }
    }
}
