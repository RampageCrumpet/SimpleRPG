using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the integer rotation and door-mating rules for all four quarter turns.
    /// </summary>
    public class GridMathTests
    {
        private static readonly int[] AllTurns = { 0, 1, 2, 3 };
        private static readonly Side[] AllSides = { Side.North, Side.East, Side.South, Side.West };

        [Test]
        public void RotatePoint_MatchesUnityRotationAboutY([ValueSource(nameof(AllTurns))] int turns)
        {
            Quaternion rotation = Quaternion.Euler(0, 90 * turns, 0);

            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    Vector3 expected = rotation * new Vector3(x, 0, y);
                    Vector2Int actual = GridMath.RotatePoint(new Vector2Int(x, y), turns);

                    Assert.AreEqual(Mathf.RoundToInt(expected.x), actual.x, $"x of ({x},{y}) turned {turns}");
                    Assert.AreEqual(Mathf.RoundToInt(expected.z), actual.y, $"y of ({x},{y}) turned {turns}");
                }
            }
        }

        [Test]
        public void RotateSideAndDirection_Agree([ValueSource(nameof(AllSides))] Side side, [ValueSource(nameof(AllTurns))] int turns)
        {
            Vector2Int rotatedDirection = GridMath.RotatePoint(GridMath.Direction(side), turns);

            Assert.AreEqual(rotatedDirection, GridMath.Direction(GridMath.Rotate(side, turns)));
            Assert.AreEqual(side, GridMath.FromDirection(GridMath.Direction(side)));
            Assert.AreEqual(GridMath.Direction(side) * -1, GridMath.Direction(GridMath.Opposite(side)));
        }

        [Test]
        public void RotatedCells_FillTheRotatedFootprint([ValueSource(nameof(AllTurns))] int turns)
        {
            var size = new Vector2Int(5, 3);
            var turned = new HashSet<Vector2Int>();
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    turned.Add(GridMath.RotateCell(new Vector2Int(x, y), turns));
                }
            }

            Vector2Int min = GridMath.RotatedFootprintMin(size, turns);
            Vector2Int rotatedSize = GridMath.RotateSize(size, turns);
            var expected = new HashSet<Vector2Int>();
            for (int x = 0; x < rotatedSize.x; x++)
            {
                for (int y = 0; y < rotatedSize.y; y++)
                {
                    expected.Add(min + new Vector2Int(x, y));
                }
            }

            Assert.That(turned, Is.EquivalentTo(expected));
        }

        [Test]
        public void PassageCells_TurnWithTheirDoor([ValueSource(nameof(AllSides))] Side side, [ValueSource(nameof(AllTurns))] int turns)
        {
            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    var definition = new DoorDefinition(0, new Vector2Int(x, y), side, 3);

                    // The same door in a room that is not turned, and in one turned about the same origin.
                    var upright = new PlacedRoom(0, Room(definition), Vector2Int.zero, 0).Doors[0];
                    var turned = new PlacedRoom(0, Room(definition), Vector2Int.zero, turns).Doors[0];

                    var expected = upright.PassageCells().Select(passage => (passage.slice, GridMath.RotateCell(passage.cell, turns))).ToList();

                    Assert.That(turned.PassageCells().ToList(), Is.EquivalentTo(expected), $"door at ({x},{y}) facing {side} turned {turns}");
                }
            }
        }

        [Test]
        public void DoorsFacingEachOther_MateInBothDirections([ValueSource(nameof(AllSides))] Side side, [ValueSource(nameof(AllTurns))] int turnsA, [ValueSource(nameof(AllTurns))] int turnsB)
        {
            for (int length = 2; length <= 4; length++)
            {
                var roomA = new PlacedRoom(0, Room(new DoorDefinition(0, new Vector2Int(3, 2), side, length)), new Vector2Int(10, -4), turnsA);
                PlacedDoor doorA = roomA.Doors[0];

                // Stand room B so that its door is where A's door ends, facing back.
                Side localSideB = GridMath.Rotate(GridMath.Opposite(doorA.Side), -turnsB);
                RoomDefinition definitionB = Room(new DoorDefinition(0, new Vector2Int(1, 4), localSideB, length));
                Vector2Int positionB = doorA.Far - GridMath.RotatePoint(new Vector2Int(1, 4), turnsB);
                PlacedDoor doorB = new PlacedRoom(1, definitionB, positionB, turnsB).Doors[0];

                Assert.IsTrue(doorA.Mates(doorB), $"A should mate B (length {length})");
                Assert.IsTrue(doorB.Mates(doorA), $"B should mate A (length {length})");
                Assert.AreEqual(doorA.Point, doorB.Far);
            }
        }

        [Test]
        public void DoorsOfDifferentLength_DoNotMate()
        {
            var roomA = new PlacedRoom(0, Room(new DoorDefinition(0, new Vector2Int(4, 3), Side.East, 4)), Vector2Int.zero, 0);
            var roomB = new PlacedRoom(1, Room(new DoorDefinition(0, new Vector2Int(2, 3), Side.West, 3)), new Vector2Int(6, 0), 0);

            Assert.IsFalse(roomA.Doors[0].Mates(roomB.Doors[0]));
        }

        private static RoomDefinition Room(params DoorDefinition[] doors)
        {
            return new RoomDefinition(0, "Test", new Vector2Int(6, 6), Vector2Int.zero, doors.ToList());
        }
    }
}
