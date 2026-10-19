using System.Collections.Generic;
using System.Linq;

namespace LevelGeneration
{
    /// <summary>
    /// What a selector is told about the dungeon so far when it picks the next room.
    /// </summary>
    public readonly struct SelectionContext
    {
        public SelectionContext(int placedRooms, int minimumRooms, int openDoors, IReadOnlyDictionary<RoomDefinition, int> placedCounts, PlacedDoor targetDoor = null, int floorCount = int.MaxValue)
        {
            FloorCount = floorCount;
            PlacedRooms = placedRooms;
            MinimumRooms = minimumRooms;
            OpenDoors = openDoors;
            PlacedCounts = placedCounts;
            TargetDoor = targetDoor;
        }

        /// <summary>
        /// The open door the room will be stood against, or null if there is none. A room with no door that could join it is no use.
        /// </summary>
        public PlacedDoor TargetDoor { get; }

        /// <summary>
        /// How many floors the dungeon may use. A room that would stand outside them is no use.
        /// </summary>
        public int FloorCount { get; }

        public int PlacedRooms { get; }
        public int MinimumRooms { get; }
        public int OpenDoors { get; }

        /// <summary>
        /// How many of each kind of room have been placed so far.
        /// </summary>
        public IReadOnlyDictionary<RoomDefinition, int> PlacedCounts { get; }

        /// <summary>
        /// True once the dungeon is big enough and the job is to close the remaining doors.
        /// </summary>
        public bool MinimumReached => PlacedRooms >= MinimumRooms;
    }

    /// <summary>
    /// Decides which room to try next. Implementations must draw only from the random generator they are given,
    /// so every machine picks the same rooms.
    /// </summary>
    public interface IRoomSelector
    {
        /// <summary>
        /// Picks the room the dungeon begins in.
        /// </summary>
        RoomDefinition SelectStart(System.Random random, IReadOnlyList<RoomDefinition> rooms);

        /// <summary>
        /// Picks the next room to try against an open door.
        /// </summary>
        /// <returns> A room, or null if no room is allowed right now.</returns>
        RoomDefinition Select(System.Random random, IReadOnlyList<RoomDefinition> rooms, SelectionContext context);
    }

    /// <summary>
    /// Picks rooms by weight. Start rooms are only used to begin, rooms at their maximum count are skipped, and once the
    /// dungeon is big enough dead ends are favoured so the last doors can be closed.
    /// </summary>
    public class WeightedRoomSelector : IRoomSelector
    {
        /// <summary>
        /// How much more likely a dead end is to be picked once the minimum number of rooms is reached.
        /// </summary>
        public float DeadEndMultiplierAfterMinimum { get; set; } = 3f;

        public RoomDefinition SelectStart(System.Random random, IReadOnlyList<RoomDefinition> rooms)
        {
            List<RoomDefinition> designated = rooms.Where(room => (room.Role & RoomRole.Start) != 0 && room.Weight > 0).ToList();
            if (designated.Count > 0)
            {
                return Pick(random, designated, room => room.Weight);
            }

            return Pick(random, rooms.Where(room => room.Weight > 0).ToList(), room => room.Weight);
        }

        public RoomDefinition Select(System.Random random, IReadOnlyList<RoomDefinition> rooms, SelectionContext context)
        {
            List<RoomDefinition> allowed = rooms.Where(room => IsAllowed(room, context)).ToList();
            if (allowed.Count == 0)
            {
                return null;
            }

            return Pick(random, allowed, room => Weight(room, context));
        }

        private static bool IsAllowed(RoomDefinition room, SelectionContext context)
        {
            if (room.Weight <= 0 || (room.Role & RoomRole.Start) != 0)
            {
                return false;
            }

            if (room.MaxCount > 0 && context.PlacedCounts.TryGetValue(room, out int placed) && placed >= room.MaxCount)
            {
                return false;
            }

            // Only rooms with a door of the same type and length that climbs the opposite way can join it.
            PlacedDoor target = context.TargetDoor;
            if (target != null && !room.Doors.Any(door => door.TypeId == target.TypeId && door.LengthCells == target.LengthCells && door.FloorDelta == -target.FloorDelta
                && FitsFloors(room, target.FarFloor - door.Level, context.FloorCount)))
            {
                return false;
            }

            if (target == null && room.Height > context.FloorCount)
            {
                return false;
            }

            return true;
        }

        private static bool FitsFloors(RoomDefinition room, int lowestFloor, int floorCount)
        {
            return lowestFloor >= 0 && lowestFloor + room.Height <= floorCount;
        }

        private float Weight(RoomDefinition room, SelectionContext context)
        {
            return context.MinimumReached && room.IsDeadEnd ? room.Weight * DeadEndMultiplierAfterMinimum : room.Weight;
        }

        private static RoomDefinition Pick(System.Random random, List<RoomDefinition> candidates, System.Func<RoomDefinition, float> weight)
        {
            if (candidates.Count == 0)
            {
                return null;
            }

            double total = candidates.Sum(room => (double)weight(room));
            double roll = random.NextDouble() * total;

            foreach (RoomDefinition room in candidates)
            {
                roll -= weight(room);
                if (roll < 0)
                {
                    return room;
                }
            }

            return candidates[candidates.Count - 1];
        }
    }
}
