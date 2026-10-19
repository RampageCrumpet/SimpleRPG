using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// One of the four sides of a cell. Ordered clockwise seen from above, so a quarter turn adds one.
    /// </summary>
    public enum Side
    {
        North = 0, // +y
        East = 1,  // +x
        South = 2, // -y
        West = 3,  // -x
    }

    /// <summary>
    /// Exact integer geometry for the dungeon grid. Rooms only ever turn in quarter turns, so rotation is a swap and a sign
    /// change: no floating point, nothing to round. Quarter turns match <c>Quaternion.Euler(0, 90 * turns, 0)</c> when
    /// grid x and y are world x and z.
    /// </summary>
    /// <remarks>
    /// Two kinds of coordinate are rotated here. A point is a cell corner and rotates like a vector. A cell is the unit square
    /// above and to the right of its index, so its index moves differently: a quarter turn takes cell (x, y) to (y, -x - 1).
    /// </remarks>
    public static class GridMath
    {
        /// <summary>
        /// The direction a side faces.
        /// </summary>
        public static Vector2Int Direction(Side side)
        {
            switch (side)
            {
                case Side.North: return new Vector2Int(0, 1);
                case Side.East: return new Vector2Int(1, 0);
                case Side.South: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }

        /// <summary>
        /// The side that faces a direction.
        /// </summary>
        /// <exception cref="ArgumentException"> The direction is not one of the four axis directions.</exception>
        public static Side FromDirection(Vector2Int direction)
        {
            if (direction == new Vector2Int(0, 1)) return Side.North;
            if (direction == new Vector2Int(1, 0)) return Side.East;
            if (direction == new Vector2Int(0, -1)) return Side.South;
            if (direction == new Vector2Int(-1, 0)) return Side.West;

            throw new ArgumentException($"{direction} is not an axis direction.", nameof(direction));
        }

        public static Side Opposite(Side side)
        {
            return Rotate(side, 2);
        }

        /// <summary>
        /// Turns a side clockwise (seen from above) by whole quarter turns.
        /// </summary>
        public static Side Rotate(Side side, int turns)
        {
            return (Side)(((int)side + Normalize(turns)) & 3);
        }

        /// <summary>
        /// Rotates a point (a cell corner) about the origin.
        /// </summary>
        public static Vector2Int RotatePoint(Vector2Int point, int turns)
        {
            switch (Normalize(turns))
            {
                case 0: return point;
                case 1: return new Vector2Int(point.y, -point.x);
                case 2: return new Vector2Int(-point.x, -point.y);
                default: return new Vector2Int(-point.y, point.x);
            }
        }

        /// <summary>
        /// Rotates a cell index about the origin corner. The cell's whole square turns with it, so its index shifts by one on the axes that go negative.
        /// </summary>
        public static Vector2Int RotateCell(Vector2Int cell, int turns)
        {
            switch (Normalize(turns))
            {
                case 0: return cell;
                case 1: return new Vector2Int(cell.y, -cell.x - 1);
                case 2: return new Vector2Int(-cell.x - 1, -cell.y - 1);
                default: return new Vector2Int(-cell.y - 1, cell.x);
            }
        }

        /// <summary>
        /// The width and height of a footprint after it is turned.
        /// </summary>
        public static Vector2Int RotateSize(Vector2Int size, int turns)
        {
            return (Normalize(turns) & 1) == 0 ? size : new Vector2Int(size.y, size.x);
        }

        /// <summary>
        /// The lowest cell of a footprint that spans (0,0) to <paramref name="size"/> before it is turned about the origin.
        /// </summary>
        public static Vector2Int RotatedFootprintMin(Vector2Int size, int turns)
        {
            switch (Normalize(turns))
            {
                case 0: return Vector2Int.zero;
                case 1: return new Vector2Int(0, -size.x);
                case 2: return new Vector2Int(-size.x, -size.y);
                default: return new Vector2Int(-size.y, 0);
            }
        }

        /// <summary>
        /// The row of cells straddling the line through a point, <paramref name="distance"/> cells along <paramref name="side"/>.
        /// A point is a corner shared by four cells, so a door standing on one is centred on the line between cells and always covers
        /// an even number of them: the two flanking the line, or more for a wide door.
        /// </summary>
        /// <param name="point"> The grid point the door stands on.</param>
        /// <param name="side"> The way the door faces.</param>
        /// <param name="distance"> 0 for the row of cells just in front of the point, 1 for the row after that, and so on.</param>
        /// <param name="widthCells"> How many cells across the door is. Odd widths are rounded up to the next even number, and the minimum is 2.</param>
        public static IEnumerable<Vector2Int> CellsAcross(Vector2Int point, Side side, int distance, int widthCells = 2)
        {
            Vector2Int direction = Direction(side);
            int half = Math.Max(1, (widthCells + 1) / 2);

            if (direction.x != 0)
            {
                int x = direction.x > 0 ? point.x + distance : point.x - 1 - distance;
                for (int offset = -half; offset < half; offset++)
                {
                    yield return new Vector2Int(x, point.y + offset);
                }
            }
            else
            {
                int y = direction.y > 0 ? point.y + distance : point.y - 1 - distance;
                for (int offset = -half; offset < half; offset++)
                {
                    yield return new Vector2Int(point.x + offset, y);
                }
            }
        }

        public static int Normalize(int turns)
        {
            return ((turns % 4) + 4) % 4;
        }
    }
}
