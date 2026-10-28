using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that <see cref="LayoutValidator"/> flags each way a layout can be wrong, using small hand-built layouts.
    /// Rooms are 6x6 cells with doors inset two cells from their edges and 4 cells long, like the real roomsets.
    /// </summary>
    public class LayoutValidatorTests
    {
        private static readonly RoomDefinition Corridor = TestRooms.Corridor();

        [Test]
        public void TwoMatedRooms_AreValid()
        {
            PlacedRoom left = new PlacedRoom(0, Corridor, new Vector2Int(0, 0), 0);
            PlacedRoom right = new PlacedRoom(1, Corridor, new Vector2Int(6, 0), 0);

            // The outer doors open onto empty space, so they have to be sealed. Doors are in definition order: west first, east second.
            Layout layout = new Layout(0, 0, new[] { left, right }, new[] { left.Doors[0], right.Doors[1] });

            Assert.That(LayoutValidator.Validate(layout), Is.Empty);
        }

        [Test]
        public void MatedRoomsTurnedTogether_AreValid([NUnit.Framework.Range(0, 3)] int turns)
        {
            // Turning both rooms about the same point keeps them joined.
            PlacedRoom left = new PlacedRoom(0, Corridor, Vector2Int.zero, turns);
            PlacedDoor leftEast = left.Doors[1];
            PlacedRoom right = new PlacedRoom(1, Corridor, leftEast.Far - GridMath.RotatePoint(new Vector2Int(2, 3), turns), turns);

            Layout layout = new Layout(0, 0, new[] { left, right }, new[] { left.Doors[0], right.Doors[1] });

            Assert.That(LayoutValidator.Validate(layout), Is.Empty, string.Join("\n", LayoutValidator.Validate(layout)));
        }

        [Test]
        public void OverlappingRooms_AreReported()
        {
            PlacedRoom left = new PlacedRoom(0, Corridor, new Vector2Int(0, 0), 0);
            PlacedRoom right = new PlacedRoom(1, Corridor, new Vector2Int(3, 0), 0);

            AssertRule(LayoutValidator.Validate(AllSealed(left, right)), LayoutValidator.RuleRoomOverlap);
        }

        [Test]
        public void DoorOpeningOntoAWall_IsReported()
        {
            PlacedRoom left = new PlacedRoom(0, Corridor, new Vector2Int(0, 0), 0);
            // One cell too high: the left room's east door opens onto the second room, but not onto its door.
            PlacedRoom right = new PlacedRoom(1, Corridor, new Vector2Int(6, 1), 0);

            // Only the outer doors are sealed; the two inner doors are the ones that should line up and do not.
            Layout layout = new Layout(0, 0, new[] { left, right }, new[] { left.Doors[0], right.Doors[1] });

            AssertRule(LayoutValidator.Validate(layout), LayoutValidator.RuleDoorFacesWall);
        }

        [Test]
        public void UnsealedDoorOnEmptySpace_IsReported()
        {
            PlacedRoom only = new PlacedRoom(0, Corridor, Vector2Int.zero, 0);

            AssertRule(LayoutValidator.Validate(new Layout(0, 0, new[] { only }, new PlacedDoor[0])), LayoutValidator.RuleDoorOpen);
            Assert.That(LayoutValidator.Validate(AllSealed(only)), Is.Empty);
        }

        [Test]
        public void RoomsThatAreNotJoined_AreReported()
        {
            PlacedRoom first = new PlacedRoom(0, Corridor, new Vector2Int(0, 0), 0);
            PlacedRoom far = new PlacedRoom(1, Corridor, new Vector2Int(40, 0), 0);

            AssertRule(LayoutValidator.Validate(AllSealed(first, far)), LayoutValidator.RuleDisconnected);
        }

        [Test]
        public void DoorOpeningOntoItsOwnRoom_IsReported()
        {
            // A 2 cell long east door at x=2 ends at x=4, inside the same 6 cell wide room.
            RoomDefinition inward = new RoomDefinition(0, "Inward", new Vector2Int(6, 6), Vector2Int.zero,
                new List<DoorDefinition> { new DoorDefinition(0, new Vector2Int(2, 3), Side.East, 2) });
            PlacedRoom room = new PlacedRoom(0, inward, Vector2Int.zero, 0);

            AssertRule(LayoutValidator.Validate(new Layout(0, 0, new[] { room }, new PlacedDoor[0])), LayoutValidator.RuleDoorFacesOwnRoom);
        }

        [Test]
        public void SealedDoorFacingAWall_IsAccepted()
        {
            // The sealed door's wall is the second room's wall; nothing leads through it, so it is fine.
            PlacedRoom left = new PlacedRoom(0, Corridor, new Vector2Int(0, 0), 0);
            PlacedRoom right = new PlacedRoom(1, Corridor, new Vector2Int(6, 1), 0);
            Layout layout = new Layout(0, 0, new[] { left, right }, left.Doors.Concat(right.Doors).ToList());

            Assert.That(LayoutValidator.Validate(layout).Select(v => v.Rule), Does.Not.Contain(LayoutValidator.RuleDoorFacesWall));
        }

        private static Layout AllSealed(params PlacedRoom[] rooms)
        {
            return new Layout(0, 0, rooms, rooms.SelectMany(room => room.Doors).ToList());
        }

        private static void AssertRule(List<LayoutViolation> violations, string rule)
        {
            Assert.That(violations.Select(violation => violation.Rule), Does.Contain(rule), string.Join("\n", violations.Select(violation => violation.ToString())));
        }
    }

    /// <summary>
    /// Hand-built room definitions shared by the layout tests: 6x6 cells, doors two cells in from the edge and four cells long.
    /// </summary>
    internal static class TestRooms
    {
        private const int DoorLength = 4;

        private static DoorDefinition West(int index) => new DoorDefinition(index, new Vector2Int(2, 3), Side.West, DoorLength);
        private static DoorDefinition East(int index) => new DoorDefinition(index, new Vector2Int(4, 3), Side.East, DoorLength);
        private static DoorDefinition North(int index) => new DoorDefinition(index, new Vector2Int(3, 4), Side.North, DoorLength);
        private static DoorDefinition South(int index) => new DoorDefinition(index, new Vector2Int(3, 2), Side.South, DoorLength);

        public static RoomDefinition Corridor(int index = 0) => Room(index, "Corridor", West(0), East(1));

        public static RoomDefinition Crossroads(int index = 0) => Room(index, "Crossroads", West(0), East(1), North(2), South(3));

        public static RoomDefinition Corner(int index = 0) => Room(index, "Corner", West(0), North(1));

        public static RoomDefinition DeadEnd(int index = 0) => Room(index, "DeadEnd", West(0));

        /// <summary>
        /// Rooms whose doors are 6 cells long but only reach 2 cells into each room, so two joined rooms stand 2 cells apart with a gap between.
        /// </summary>
        public static List<RoomDefinition> LongDoorSet()
        {
            DoorDefinition west = new DoorDefinition(0, new Vector2Int(2, 3), Side.West, 6);
            DoorDefinition east = new DoorDefinition(1, new Vector2Int(4, 3), Side.East, 6);
            DoorDefinition north = new DoorDefinition(1, new Vector2Int(3, 4), Side.North, 6);

            return new List<RoomDefinition>
            {
                Room(0, "LongDeadEnd", west),
                Room(1, "LongCorridor", west, east),
                Room(2, "LongCorner", west, north),
            };
        }

        /// <summary>
        /// A small set that can always close: it has a dead end, a corridor, a corner and a crossroads.
        /// </summary>
        public static List<RoomDefinition> MixedSet() => new List<RoomDefinition> { DeadEnd(0), Corridor(1), Corner(2), Crossroads(3) };

        private static RoomDefinition Room(int index, string name, params DoorDefinition[] doors)
        {
            return new RoomDefinition(index, name, new Vector2Int(6, 6), Vector2Int.zero, doors.ToList());
        }
    }
}
