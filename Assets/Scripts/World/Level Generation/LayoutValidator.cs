using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// One rule a generated layout broke.
    /// </summary>
    public readonly struct LayoutViolation
    {
        public LayoutViolation(string rule, string message)
        {
            Rule = rule;
            Message = message;
        }

        public string Rule { get; }
        public string Message { get; }

        public override string ToString() => $"[{Rule}] {Message}";
    }

    /// <summary>
    /// Checks a finished layout from its geometry alone, independently of the code that built it, so a generator bug
    /// fails loudly instead of producing doors that open onto walls. Pure: nothing is modified.
    /// </summary>
    public static class LayoutValidator
    {
        public const string RuleRoomOverlap = "room-overlap";
        public const string RuleDoorFacesWall = "door-faces-wall";
        public const string RuleDoorOpen = "door-open";
        public const string RuleDoorFacesOwnRoom = "door-faces-own-room";
        public const string RulePassageBlocked = "passage-blocked";
        public const string RuleDisconnected = "rooms-disconnected";
        public const string RuleRoomOveruse = "room-overuse";
        public const string RuleStartRoomMisplaced = "start-room-misplaced";

        /// <summary>
        /// Validates a layout. A sealed door is a wall whatever lies beyond it, so only unsealed doors are checked.
        /// </summary>
        /// <returns> Every violation found. Empty when the layout is valid.</returns>
        public static List<LayoutViolation> Validate(Layout layout)
        {
            var violations = new List<LayoutViolation>();
            if (layout.Rooms.Count == 0)
            {
                return violations;
            }

            Dictionary<Vector2Int, PlacedRoom> occupancy = BuildOccupancy(layout.Rooms, violations);
            CheckRoomLimits(layout.Rooms, violations);

            var sealedSet = new HashSet<PlacedDoor>(layout.SealedDoors);
            var unsealed = layout.Rooms.SelectMany(room => room.Doors).Where(door => !sealedSet.Contains(door)).ToList();
            var doorsByPosition = new Dictionary<(Vector2Int, Side), PlacedDoor>();
            foreach (PlacedDoor door in unsealed)
            {
                doorsByPosition[(door.Point, door.Side)] = door;
            }

            var links = layout.Rooms.ToDictionary(room => room, room => new HashSet<PlacedRoom>());
            foreach (PlacedDoor door in unsealed)
            {
                CheckDoor(door, occupancy, doorsByPosition, links, violations);
            }

            CheckConnectivity(layout.Rooms, links, violations);
            return violations;
        }

        private static void CheckRoomLimits(IReadOnlyList<PlacedRoom> rooms, List<LayoutViolation> violations)
        {
            foreach (IGrouping<RoomDefinition, PlacedRoom> group in rooms.GroupBy(room => room.Definition))
            {
                if (group.Key.MaxCount > 0 && group.Count() > group.Key.MaxCount)
                {
                    violations.Add(new LayoutViolation(RuleRoomOveruse,
                        $"'{group.Key.Name}' appears {group.Count()} times but is limited to {group.Key.MaxCount}."));
                }
            }

            foreach (PlacedRoom room in rooms.Skip(1).Where(room => (room.Definition.Role & RoomRole.Start) != 0))
            {
                violations.Add(new LayoutViolation(RuleStartRoomMisplaced, $"{room} is a start room but is not where the dungeon begins."));
            }
        }

        private static Dictionary<Vector2Int, PlacedRoom> BuildOccupancy(IReadOnlyList<PlacedRoom> rooms, List<LayoutViolation> violations)
        {
            var occupancy = new Dictionary<Vector2Int, PlacedRoom>();

            foreach (PlacedRoom room in rooms)
            {
                foreach (Vector2Int cell in room.Cells())
                {
                    if (occupancy.TryGetValue(cell, out PlacedRoom other))
                    {
                        violations.Add(new LayoutViolation(RuleRoomOverlap, $"{room} and {other} both occupy cell {cell}."));
                    }
                    else
                    {
                        occupancy[cell] = room;
                    }
                }
            }

            return occupancy;
        }

        private static void CheckDoor(
            PlacedDoor door,
            Dictionary<Vector2Int, PlacedRoom> occupancy,
            Dictionary<(Vector2Int, Side), PlacedDoor> doorsByPosition,
            Dictionary<PlacedRoom, HashSet<PlacedRoom>> links,
            List<LayoutViolation> violations)
        {
            // The door on the other side has to stand exactly where this one ends, facing back, and be as long.
            if (doorsByPosition.TryGetValue((door.Far, GridMath.Opposite(door.Side)), out PlacedDoor mate) && door.Mates(mate) && mate.Room != door.Room)
            {
                links[door.Room].Add(mate.Room);

                // Whatever lies between the two doors has to be one of their rooms or empty.
                foreach ((int slice, Vector2Int cell) in door.PassageCells())
                {
                    if (slice < door.LengthCells && occupancy.TryGetValue(cell, out PlacedRoom blocker) && blocker != door.Room && blocker != mate.Room)
                    {
                        violations.Add(new LayoutViolation(RulePassageBlocked, $"{blocker} stands in the passage between {door} and {mate}."));
                        break;
                    }
                }

                return;
            }

            foreach ((int slice, Vector2Int cell) in door.PassageCells())
            {
                if (!occupancy.TryGetValue(cell, out PlacedRoom occupant))
                {
                    continue;
                }

                if (occupant == door.Room)
                {
                    // The cells just past the door normally belong to its own room; only the first pair beyond its end is a problem.
                    if (slice == door.LengthCells)
                    {
                        violations.Add(new LayoutViolation(RuleDoorFacesOwnRoom, $"{door} opens onto its own room."));
                        return;
                    }
                    continue;
                }

                violations.Add(new LayoutViolation(RuleDoorFacesWall, $"{door} opens onto a wall of {occupant}."));
                return;
            }

            violations.Add(new LayoutViolation(RuleDoorOpen, $"{door} opens onto empty space and is not sealed."));
        }

        private static void CheckConnectivity(IReadOnlyList<PlacedRoom> rooms, Dictionary<PlacedRoom, HashSet<PlacedRoom>> links, List<LayoutViolation> violations)
        {
            var reached = new HashSet<PlacedRoom> { rooms[0] };
            var pending = new Queue<PlacedRoom>();
            pending.Enqueue(rooms[0]);

            while (pending.Count > 0)
            {
                foreach (PlacedRoom next in links[pending.Dequeue()])
                {
                    if (reached.Add(next))
                    {
                        pending.Enqueue(next);
                    }
                }
            }

            if (reached.Count != rooms.Count)
            {
                violations.Add(new LayoutViolation(RuleDisconnected,
                    $"{rooms.Count - reached.Count} of {rooms.Count} rooms cannot be reached from the first room."));
            }
        }
    }
}
