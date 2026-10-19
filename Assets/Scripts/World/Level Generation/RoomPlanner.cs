using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// The packed location of one door, in cells from the room's lowest corner.
    /// </summary>
    public readonly struct DoorPlan
    {
        public DoorPlan(ConnectionBlueprint door, Vector2Int location)
        {
            Door = door;
            Location = location;
        }

        public ConnectionBlueprint Door { get; }
        public Vector2Int Location { get; }
    }

    /// <summary>
    /// Everything the packer will store on a room, worked out from its geometry. Holding a plan changes nothing on the room.
    /// </summary>
    public sealed class RoomPlan
    {
        public RoomPlan(Vector2Int originOffset, Vector2Int size, IReadOnlyList<DoorPlan> doors)
        {
            OriginOffset = originOffset;
            Size = size;
            Doors = doors;
        }

        public Vector2Int OriginOffset { get; }
        public Vector2Int Size { get; }
        public IReadOnlyList<DoorPlan> Doors { get; }
    }

    /// <summary>
    /// Works out the packed values for a room without modifying it. Pure: only transforms and fields are read.
    /// </summary>
    /// <remarks>
    /// Rounding rules, all measured in cells:
    /// <list type="bullet">
    /// <item>Origin offset rounds toward zero (ceiling of a negative number), so a pivot less than a full cell below the root is part of the first cell.</item>
    /// <item>Size rounds to the nearest cell, so a furthest pivot within half a cell of a boundary counts as sitting on it.</item>
    /// <item>A door snaps to the nearest grid point; <see cref="RoomValidator.GridTolerance"/> is how far it may be from that point before the validator refuses it.</item>
    /// </list>
    /// Size is the span of all child pivots, so <see cref="RoomValidator"/> flags pivots that sit outside the drawn geometry.
    /// </remarks>
    public static class RoomPlanner
    {
        /// <summary>
        /// Computes the origin offset, size and door locations for a room.
        /// </summary>
        /// <param name="room"> The room to plan.</param>
        /// <param name="cellSize"> The size of one world cell in unity units.</param>
        public static RoomPlan Plan(RoomBlueprint room, float cellSize)
        {
            Vector2Int origin = CalculateOriginOffset(room, cellSize);
            Vector2Int size = CalculateSize(room, origin, cellSize);
            List<DoorPlan> doors = SealedDoors(room)
                .Select(door => new DoorPlan(door, DoorLocation(room, door, cellSize, origin)))
                .ToList();

            return new RoomPlan(origin, size, doors);
        }

        /// <summary>
        /// The doors the packer collects: every ConnectionBlueprint in the room that has a SealingObject.
        /// </summary>
        public static IEnumerable<ConnectionBlueprint> SealedDoors(RoomBlueprint room)
        {
            return room.GetComponentsInChildren<ConnectionBlueprint>().Where(connection => connection.SealingObject != null);
        }

        /// <summary>
        /// The lowest corner of the room's geometry, in cells relative to the prefab root.
        /// A child pivot less than a full cell below the root is treated as part of the first cell rather than an extra one,
        /// because pieces such as wall trims sit slightly off the cell edge.
        /// </summary>
        public static Vector2Int CalculateOriginOffset(RoomBlueprint room, float cellSize)
        {
            List<Vector3> pivots = Pivots(room);
            float minX = pivots.Min(pivot => pivot.x);
            float minZ = pivots.Min(pivot => pivot.z);

            return new Vector2Int(
                Mathf.Min(0, Mathf.CeilToInt(minX / cellSize)),
                Mathf.Min(0, Mathf.CeilToInt(minZ / cellSize)));
        }

        /// <summary>
        /// The width and height of the room in cells, measured from its lowest corner to its furthest pivot.
        /// </summary>
        public static Vector2Int CalculateSize(RoomBlueprint room, Vector2Int originOffset, float cellSize)
        {
            List<Vector3> pivots = Pivots(room);
            float maxX = pivots.Max(pivot => pivot.x);
            float maxZ = pivots.Max(pivot => pivot.z);

            return new Vector2Int(
                Mathf.RoundToInt((maxX - originOffset.x * cellSize) / cellSize),
                Mathf.RoundToInt((maxZ - originOffset.y * cellSize) / cellSize));
        }

        /// <summary>
        /// A door's location in cells, measured from the room's lowest corner.
        /// </summary>
        public static Vector2Int DoorLocation(RoomBlueprint room, ConnectionBlueprint connection, float cellSize, Vector2Int originOffset)
        {
            Vector2 grid = GridPosition(room, connection.transform, cellSize);
            return new Vector2Int(
                Mathf.RoundToInt(grid.x) - originOffset.x,
                Mathf.RoundToInt(grid.y) - originOffset.y);
        }

        /// <summary>
        /// A transform's position in cells relative to the room root. The Z direction in the 3d worldspace maps to the Y direction in the 2d room space.
        /// </summary>
        public static Vector2 GridPosition(RoomBlueprint room, Transform transform, float cellSize)
        {
            Vector3 relative = transform.position - room.transform.position;
            return new Vector2(relative.x, relative.z) / cellSize;
        }

        private static List<Vector3> Pivots(RoomBlueprint room)
        {
            return room.GetComponentsInChildren<Transform>()
                .Select(transform => transform.position - room.transform.position)
                .ToList();
        }
    }
}
