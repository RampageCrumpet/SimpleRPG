using System.Collections.Generic;
using System.Linq;

namespace LevelGeneration
{
    /// <summary>
    /// A joined pair of doors that stays shut until the player has the key, and the room the key is found in.
    /// Each lock has its own key, numbered from 1 within a layout.
    /// </summary>
    public sealed class LockedDoor
    {
        public LockedDoor(PlacedDoor door, PlacedDoor mate, int keyId, PlacedRoom keyRoom)
        {
            Door = door;
            Mate = mate;
            KeyId = keyId;
            KeyRoom = keyRoom;
        }

        /// <summary>
        /// One side of the lock.
        /// </summary>
        public PlacedDoor Door { get; }

        /// <summary>
        /// The door on the other side, which is locked with it.
        /// </summary>
        public PlacedDoor Mate { get; }

        /// <summary>
        /// Which key opens it. Unique within a layout.
        /// </summary>
        public int KeyId { get; }

        /// <summary>
        /// The room the key lies in.
        /// </summary>
        public PlacedRoom KeyRoom { get; }

        public override string ToString() => $"lock {KeyId} on {Door} (key in {KeyRoom})";
    }

    /// <summary>
    /// Works out which rooms a player can reach, so locks can be placed and checked without ever shutting anyone in.
    /// Pure: nothing is modified.
    /// </summary>
    public static class LockSolver
    {
        /// <summary>
        /// Every pair of doors that join two rooms, once each, as (lower room id side, other side). Sealed doors join nothing.
        /// </summary>
        public static List<(PlacedDoor a, PlacedDoor b)> Joins(IReadOnlyList<PlacedRoom> rooms, IEnumerable<PlacedDoor> sealedDoors)
        {
            var sealedSet = new HashSet<PlacedDoor>(sealedDoors);
            var byPosition = new Dictionary<(UnityEngine.Vector2Int, Side, int), PlacedDoor>();
            foreach (PlacedDoor door in rooms.SelectMany(room => room.Doors).Where(door => !sealedSet.Contains(door)))
            {
                byPosition[(door.Point, door.Side, door.Floor)] = door;
            }

            var joins = new List<(PlacedDoor, PlacedDoor)>();
            foreach (PlacedRoom room in rooms)
            {
                foreach (PlacedDoor door in room.Doors.Where(door => !sealedSet.Contains(door)))
                {
                    if (byPosition.TryGetValue((door.Far, GridMath.Opposite(door.Side), door.FarFloor), out PlacedDoor other)
                        && door.Mates(other) && room.Id < other.Room.Id)
                    {
                        joins.Add((door, other));
                    }
                }
            }

            return joins;
        }

        /// <summary>
        /// The rooms reachable from the first room along joins, ignoring locks, optionally without crossing one join.
        /// </summary>
        public static HashSet<PlacedRoom> Connected(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<(PlacedDoor a, PlacedDoor b)> joins, (PlacedDoor a, PlacedDoor b)? skip = null)
        {
            return Walk(rooms, joins, skip, null);
        }

        /// <summary>
        /// The rooms a player can reach from the first room: through open joins, and through locked ones once the key has been picked up
        /// in a room already reached. Repeats until nothing more opens.
        /// </summary>
        public static HashSet<PlacedRoom> Reachable(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<(PlacedDoor a, PlacedDoor b)> joins, IReadOnlyList<LockedDoor> locks)
        {
            return Walk(rooms, joins, null, locks);
        }

        private static HashSet<PlacedRoom> Walk(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<(PlacedDoor a, PlacedDoor b)> joins, (PlacedDoor a, PlacedDoor b)? skip, IReadOnlyList<LockedDoor> locks)
        {
            var reached = new HashSet<PlacedRoom>();
            if (rooms.Count == 0)
            {
                return reached;
            }

            var lockOf = new Dictionary<PlacedDoor, LockedDoor>();
            foreach (LockedDoor locked in locks ?? new LockedDoor[0])
            {
                lockOf[locked.Door] = locked;
                lockOf[locked.Mate] = locked;
            }

            var keys = new HashSet<int>();
            reached.Add(rooms[0]);

            bool changed = true;
            while (changed)
            {
                changed = false;

                foreach (LockedDoor locked in lockOf.Values.Distinct())
                {
                    if (reached.Contains(locked.KeyRoom) && keys.Add(locked.KeyId))
                    {
                        changed = true;
                    }
                }

                foreach ((PlacedDoor a, PlacedDoor b) in joins)
                {
                    if (skip.HasValue && skip.Value.a == a && skip.Value.b == b)
                    {
                        continue;
                    }

                    if (lockOf.TryGetValue(a, out LockedDoor locked) && !keys.Contains(locked.KeyId))
                    {
                        continue;
                    }

                    if (reached.Contains(a.Room) && reached.Add(b.Room))
                    {
                        changed = true;
                    }
                    else if (reached.Contains(b.Room) && reached.Add(a.Room))
                    {
                        changed = true;
                    }
                }
            }

            return reached;
        }
    }
}
