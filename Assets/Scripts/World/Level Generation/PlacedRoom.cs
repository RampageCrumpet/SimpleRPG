using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// A room at a fixed place in a layout. Immutable: where it stands, how far it is turned and where its doors ended up are
    /// worked out once, so nothing can change under a search that is still comparing candidates.
    /// </summary>
    public sealed class PlacedRoom
    {
        public PlacedRoom(int id, RoomDefinition definition, Vector2Int position, int turns)
        {
            Id = id;
            Definition = definition;
            Position = position;
            Turns = GridMath.Normalize(turns);

            Size = GridMath.RotateSize(definition.Size, Turns);
            Min = position + GridMath.RotatedFootprintMin(definition.Size, Turns);
            Doors = definition.Doors.Select(door => new PlacedDoor(this, door)).ToList();
        }

        /// <summary>
        /// Stable within a layout: the order rooms were placed in.
        /// </summary>
        public int Id { get; }

        public RoomDefinition Definition { get; }

        /// <summary>
        /// The room's lowest corner before it was turned. Turning happens about this point.
        /// </summary>
        public Vector2Int Position { get; }

        /// <summary>
        /// Clockwise quarter turns, 0 to 3.
        /// </summary>
        public int Turns { get; }

        /// <summary>
        /// Width and height in cells after turning.
        /// </summary>
        public Vector2Int Size { get; }

        /// <summary>
        /// The lowest cell the room occupies.
        /// </summary>
        public Vector2Int Min { get; }

        public IReadOnlyList<PlacedDoor> Doors { get; }

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= Min.x && cell.x < Min.x + Size.x && cell.y >= Min.y && cell.y < Min.y + Size.y;
        }

        public IEnumerable<Vector2Int> Cells()
        {
            for (int x = 0; x < Size.x; x++)
            {
                for (int y = 0; y < Size.y; y++)
                {
                    yield return new Vector2Int(Min.x + x, Min.y + y);
                }
            }
        }

        public override string ToString() => $"{Definition.Name}#{Id} at {Position} turned {Turns}";
    }

    /// <summary>
    /// A door of a placed room, in layout coordinates.
    /// </summary>
    public sealed class PlacedDoor
    {
        public PlacedDoor(PlacedRoom room, DoorDefinition definition)
        {
            Room = room;
            Index = definition.Index;
            Point = room.Position + GridMath.RotatePoint(definition.Point, room.Turns);
            Side = GridMath.Rotate(definition.Side, room.Turns);
            LengthCells = definition.LengthCells;
            WidthCells = definition.WidthCells;
            TypeId = definition.TypeId;
        }

        /// <summary>
        /// What kind of door this is. Doors only join doors of the same type.
        /// </summary>
        public int TypeId { get; }

        public PlacedRoom Room { get; }

        /// <summary>
        /// Position in the room's door list, which is also the position on the instantiated room.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// The grid point (a cell corner) the door stands on.
        /// </summary>
        public Vector2Int Point { get; }

        public Side Side { get; }

        public int LengthCells { get; }

        public int WidthCells { get; }

        public Vector2Int Direction => GridMath.Direction(Side);

        /// <summary>
        /// The point where the door ends. A door that joins this one stands here, facing back.
        /// </summary>
        public Vector2Int Far => Point + Direction * LengthCells;

        /// <summary>
        /// The cells the door passes through: a row across its line for each cell of its length and one more past its end.
        /// Slice 0 is just in front of the door and slice <see cref="LengthCells"/> is the first row beyond it.
        /// Anything standing in them has to be its own room, or the room on the other side of the door.
        /// </summary>
        public IEnumerable<(int slice, Vector2Int cell)> PassageCells()
        {
            for (int slice = 0; slice <= LengthCells; slice++)
            {
                foreach (Vector2Int cell in GridMath.CellsAcross(Point, Side, slice, WidthCells))
                {
                    yield return (slice, cell);
                }
            }
        }

        /// <summary>
        /// True if the other door is the one this door joins: it stands where this one ends, faces back, is as long and is the same type.
        /// </summary>
        public bool Mates(PlacedDoor other)
        {
            return other.Side == GridMath.Opposite(Side) && other.LengthCells == LengthCells && other.TypeId == TypeId && other.Point == Far;
        }

        public override string ToString() => $"door {Index} of {Room} at {Point} facing {Side}";
    }
}
