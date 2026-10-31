using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// A door as the layout sees it: the grid point it stands on and the side it faces, in the room's own coordinates.
    /// A door is <see cref="LengthCells"/> long, so it joins a door standing that far along its facing and facing back.
    /// </summary>
    public readonly struct DoorDefinition
    {
        public DoorDefinition(int index, Vector2Int point, Side side, int lengthCells, int widthCells = 2, int typeId = ConnectionType.StandardId, int level = 0, int floorDelta = 0)
        {
            Index = index;
            Point = point;
            Side = side;
            LengthCells = lengthCells;
            WidthCells = widthCells;
            TypeId = typeId;
            Level = level;
            FloorDelta = floorDelta;
        }

        /// <summary>
        /// Which of the room's floors the door stands on, counted up from the room's lowest floor. Always below the room's height.
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// How many floors higher the door this one joins stands (negative for lower). Zero for a level door.
        /// A joined pair has opposite deltas; only a door of a vertical <see cref="ConnectionType"/> has one.
        /// </summary>
        public int FloorDelta { get; }

        /// <summary>
        /// What kind of door this is, as <see cref="ConnectionType.Id"/>. Doors only join doors of the same type.
        /// </summary>
        public int TypeId { get; }

        /// <summary>
        /// Position of this door in its room's door list. The spawner uses it to find the matching object on the instantiated room.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// The grid point (a cell corner) the door stands on, measured from the room's lowest corner.
        /// </summary>
        public Vector2Int Point { get; }

        public Side Side { get; }

        /// <summary>
        /// How many cells long the door is.
        /// </summary>
        public int LengthCells { get; }

        /// <summary>
        /// How many cells across the door is. A door stands on a grid point, centred on the line between cells,
        /// so this is even: 2 is the pair of cells either side of the line.
        /// </summary>
        public int WidthCells { get; }
    }

    /// <summary>
    /// What a room is for. A room with no role is an ordinary room that can be placed anywhere.
    /// </summary>
    [System.Flags]
    public enum RoomRole
    {
        None = 0,

        /// <summary>
        /// The dungeon begins in this room, and it is never placed anywhere else.
        /// </summary>
        Start = 1,

        /// <summary>
        /// The dungeon's goal. A room with this role has a single door and is never grown from; the generator swaps it in for
        /// the dead end farthest from the start that it fits.
        /// </summary>
        Exit = 2,
    }

    /// <summary>
    /// Everything the layout needs to know about a kind of room. Plain data: nothing here refers to a prefab or a scene object,
    /// so layouts can be generated and tested without Unity objects.
    /// </summary>
    public sealed class RoomDefinition
    {
        public RoomDefinition(int index, string name, Vector2Int size, Vector2Int originOffset, IReadOnlyList<DoorDefinition> doors,
            float weight = 1f, int maxCount = 0, RoomRole role = RoomRole.None, int height = 1, int minCount = 0, IReadOnlyList<Vector2Int> extraCells = null)
        {
            Index = index;
            Name = name;
            Size = size;
            OriginOffset = originOffset;
            Doors = doors;
            Weight = weight;
            MaxCount = maxCount;
            MinCount = Math.Max(0, minCount);
            Role = role;
            Height = Math.Max(1, height);
            ExtraCells = extraCells ?? new Vector2Int[0];
        }

        /// <summary>
        /// Cells the room claims beyond its rectangle (alcoves), measured from the room's lowest corner like door points. Each is a cell index.
        /// </summary>
        public IReadOnlyList<Vector2Int> ExtraCells { get; }

        /// <summary>
        /// How many floors high the room is. Every cell of its footprint is occupied on each of them.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// How likely this room is to be picked compared with the others in the set. Zero means never.
        /// </summary>
        public float Weight { get; }

        /// <summary>
        /// The most times this room may appear in one dungeon. Zero means no limit.
        /// </summary>
        public int MaxCount { get; }

        /// <summary>
        /// The fewest times this room must appear in one dungeon. Zero means it need not appear.
        /// </summary>
        public int MinCount { get; }

        public RoomRole Role { get; }

        /// <summary>
        /// True for a room with a single door, which can close off a branch.
        /// </summary>
        public bool IsDeadEnd => Doors.Count == 1;

        /// <summary>
        /// Position in the roomset. The spawner maps it back to a prefab.
        /// </summary>
        public int Index { get; }

        public string Name { get; }

        /// <summary>
        /// Width and height in cells, measured from the room's lowest corner.
        /// </summary>
        public Vector2Int Size { get; }

        /// <summary>
        /// Where the lowest corner sits relative to the prefab root, in cells. Only the spawner needs it.
        /// </summary>
        public Vector2Int OriginOffset { get; }

        public IReadOnlyList<DoorDefinition> Doors { get; }
    }
}
