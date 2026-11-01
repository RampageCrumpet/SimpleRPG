using System;
using System.Collections.Generic;

namespace SimpleRPG.RoomGeneration
{
    // Conventions shared by every type in this namespace.
    //
    // * All lengths are whole millimetres (int). Structural decisions, checks and checksums never touch floating point.
    // * Coordinates are in the room's own unturned frame, measured from its lowest corner: X and Y are the layout grid's x and y
    //   (world x and z), Z is up (world y). Z = 0 is the room's base, where level 0 stands.
    // * Rectangles and boxes are half-open in spirit: two shapes that only share an edge or face do not overlap.

    /// <summary>
    /// An axis-aligned rectangle on the XY plane, in millimetres. An empty rectangle (no area) is valid and overlaps nothing.
    /// </summary>
    public readonly struct RectMm : IEquatable<RectMm>
    {
        public RectMm(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }

        public int Width => MaxX - MinX;
        public int Height => MaxY - MinY;
        public bool IsEmpty => MaxX <= MinX || MaxY <= MinY;
        public int CenterX => MinX + (MaxX - MinX) / 2;
        public int CenterY => MinY + (MaxY - MinY) / 2;

        public static RectMm FromSize(int x, int y, int width, int height) => new RectMm(x, y, x + width, y + height);

        /// <summary>
        /// True if the rectangles share area. Rectangles that only touch along an edge do not overlap.
        /// </summary>
        public bool Overlaps(RectMm other) => !IsEmpty && !other.IsEmpty && MinX < other.MaxX && other.MinX < MaxX && MinY < other.MaxY && other.MinY < MaxY;

        /// <summary>
        /// True if the other rectangle lies wholly inside this one (edges may touch).
        /// </summary>
        public bool Contains(RectMm other) => other.MinX >= MinX && other.MaxX <= MaxX && other.MinY >= MinY && other.MaxY <= MaxY;

        public bool Contains(int x, int y) => x >= MinX && x < MaxX && y >= MinY && y < MaxY;

        public RectMm Inflate(int amount) => new RectMm(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);

        public RectMm Translate(int dx, int dy) => new RectMm(MinX + dx, MinY + dy, MaxX + dx, MaxY + dy);

        public RectMm Intersect(RectMm other) => new RectMm(Math.Max(MinX, other.MinX), Math.Max(MinY, other.MinY), Math.Min(MaxX, other.MaxX), Math.Min(MaxY, other.MaxY));

        public RectMm Union(RectMm other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return new RectMm(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));
        }

        /// <summary>
        /// Appends what is left of this rectangle after removing <paramref name="hole"/>, as at most four disjoint rectangles in a fixed order.
        /// </summary>
        public void Subtract(RectMm hole, List<RectMm> output)
        {
            if (IsEmpty)
            {
                return;
            }

            if (!Overlaps(hole))
            {
                output.Add(this);
                return;
            }

            RectMm cut = Intersect(hole);
            Add(output, new RectMm(MinX, MinY, MaxX, cut.MinY));
            Add(output, new RectMm(MinX, cut.MaxY, MaxX, MaxY));
            Add(output, new RectMm(MinX, cut.MinY, cut.MinX, cut.MaxY));
            Add(output, new RectMm(cut.MaxX, cut.MinY, MaxX, cut.MaxY));
        }

        private static void Add(List<RectMm> output, RectMm rect)
        {
            if (!rect.IsEmpty)
            {
                output.Add(rect);
            }
        }

        public bool Equals(RectMm other) => MinX == other.MinX && MinY == other.MinY && MaxX == other.MaxX && MaxY == other.MaxY;
        public override bool Equals(object obj) => obj is RectMm other && Equals(other);
        public override int GetHashCode() => (((MinX * 397) ^ MinY) * 397 ^ MaxX) * 397 ^ MaxY;
        public static bool operator ==(RectMm a, RectMm b) => a.Equals(b);
        public static bool operator !=(RectMm a, RectMm b) => !a.Equals(b);
        public override string ToString() => $"[{MinX},{MinY} .. {MaxX},{MaxY}]";
    }

    /// <summary>
    /// An axis-aligned box: a rectangle on the XY plane between two heights, in millimetres.
    /// </summary>
    public readonly struct BoxMm : IEquatable<BoxMm>
    {
        public BoxMm(RectMm area, int minZ, int maxZ)
        {
            Area = area;
            MinZ = minZ;
            MaxZ = Math.Max(minZ, maxZ);
        }

        public RectMm Area { get; }
        public int MinZ { get; }
        public int MaxZ { get; }

        public bool IsEmpty => Area.IsEmpty || MaxZ <= MinZ;

        public bool Overlaps(BoxMm other) => !IsEmpty && !other.IsEmpty && Area.Overlaps(other.Area) && MinZ < other.MaxZ && other.MinZ < MaxZ;

        public bool Contains(BoxMm other) => Area.Contains(other.Area) && other.MinZ >= MinZ && other.MaxZ <= MaxZ;

        /// <summary>
        /// Appends what is left of this box after removing <paramref name="cut"/>, as disjoint boxes in a fixed order.
        /// </summary>
        public void Subtract(BoxMm cut, List<BoxMm> output)
        {
            if (IsEmpty)
            {
                return;
            }

            if (!Overlaps(cut))
            {
                output.Add(this);
                return;
            }

            var pieces = new List<RectMm>(4);
            Area.Subtract(cut.Area, pieces);
            foreach (RectMm piece in pieces)
            {
                output.Add(new BoxMm(piece, MinZ, MaxZ));
            }

            RectMm middle = Area.Intersect(cut.Area);
            int lowCut = Math.Max(MinZ, cut.MinZ);
            int highCut = Math.Min(MaxZ, cut.MaxZ);
            if (lowCut > MinZ)
            {
                output.Add(new BoxMm(middle, MinZ, lowCut));
            }

            if (highCut < MaxZ)
            {
                output.Add(new BoxMm(middle, highCut, MaxZ));
            }
        }

        public bool Equals(BoxMm other) => Area.Equals(other.Area) && MinZ == other.MinZ && MaxZ == other.MaxZ;
        public override bool Equals(object obj) => obj is BoxMm other && Equals(other);
        public override int GetHashCode() => (Area.GetHashCode() * 397 ^ MinZ) * 397 ^ MaxZ;
        public override string ToString() => $"{Area} z {MinZ}..{MaxZ}";
    }
}
