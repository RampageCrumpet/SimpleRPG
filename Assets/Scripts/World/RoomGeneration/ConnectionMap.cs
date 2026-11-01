using System;
using System.Collections.Generic;
using SimpleRPG.LevelGeneration;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Turns the layout's connections into room-local openings and protected regions. This is the one place that reads door geometry:
    /// generators take the result as given, and the validator rebuilds it independently from the same context to check the plan against it.
    /// Doors are never moved or snapped; their millimetre position is the layout grid point times the cell size.
    /// </summary>
    public sealed class ConnectionMap
    {
        private ConnectionMap()
        {
        }

        public List<Opening> Openings { get; } = new List<Opening>();
        public List<ProtectedRegion> Protected { get; } = new List<ProtectedRegion>();

        /// <summary>
        /// Why the connections cannot be realised, or null if they can.
        /// </summary>
        public string Failure { get; private set; }

        /// <summary>
        /// The lowest and highest levels any connection stands on (both 0 for a room with no connections).
        /// </summary>
        public int MinLevel { get; private set; }

        public int MaxLevel { get; private set; }

        public static ConnectionMap Build(RoomGenerationContext context)
        {
            var map = new ConnectionMap();
            map.Fill(context);
            return map;
        }

        private void Fill(RoomGenerationContext context)
        {
            RoomGenerationSettings s = context.Settings;
            int cell = s.CellSizeMm;
            RectMm footprint = context.FootprintMm;
            int wall = s.WallThicknessMm;

            if (context.FootprintCells.x <= 0 || context.FootprintCells.y <= 0)
            {
                Failure = $"The footprint {context.FootprintCells.x}x{context.FootprintCells.y} has no area.";
                return;
            }

            if (context.Levels > 1 && s.FloorHeightMm <= 0)
            {
                Failure = $"The room has {context.Levels} levels but the floor height is {s.FloorHeightMm}.";
                return;
            }

            MinLevel = int.MaxValue;
            MaxLevel = int.MinValue;

            for (int i = 0; i < context.Connections.Count; i++)
            {
                DoorDefinition door = context.Connections[i];
                string name = $"Connection {i} ({door.Point.x},{door.Point.y} facing {door.Side})";

                if (door.FloorDelta != 0)
                {
                    Failure = $"{name} joins a door {door.FloorDelta} floor(s) away (a ladder or shaft type). Procedural rooms support level connections; stairs are built inside the room between doors on different levels.";
                    return;
                }

                if (door.LengthCells != 0)
                {
                    Failure = $"{name} is {door.LengthCells} cell(s) long. Procedural rooms support only shared joins (length 0), because the passage between the rooms lies outside the footprint.";
                    return;
                }

                if (door.Level < 0 || door.Level >= context.Levels)
                {
                    Failure = $"{name} is on level {door.Level} but the room has {context.Levels} level(s).";
                    return;
                }

                int width = door.WidthCells * cell;
                if (width < s.MinPassageWidthMm)
                {
                    Failure = $"{name} is {width} mm wide, narrower than the minimum passage of {s.MinPassageWidthMm} mm.";
                    return;
                }

                int px = door.Point.x * cell;
                int py = door.Point.y * cell;
                bool alongY = door.Side == Side.East || door.Side == Side.West;
                int centre = alongY ? py : px;
                int half = width / 2;
                int spanMin = centre - half;
                int spanMax = spanMin + width;
                int edgeLength = alongY ? footprint.Height : footprint.Width;
                int depthAvailable = alongY ? footprint.Width : footprint.Height;

                bool onEdge = door.Side == Side.East ? px == footprint.MaxX
                    : door.Side == Side.West ? px == footprint.MinX
                    : door.Side == Side.North ? py == footprint.MaxY
                    : py == footprint.MinY;
                if (!onEdge)
                {
                    Failure = $"{name} does not stand on the footprint edge it faces.";
                    return;
                }

                if (spanMin < 0 || spanMax > edgeLength)
                {
                    Failure = $"{name} spans {spanMin}..{spanMax} mm, outside the {edgeLength} mm edge it stands on.";
                    return;
                }

                if (wall + s.ApproachDepthMm > depthAvailable)
                {
                    Failure = $"{name}: the wall ({wall} mm) and approach ({s.ApproachDepthMm} mm) are deeper than the room ({depthAvailable} mm).";
                    return;
                }

                int floorZ = context.LevelElevation(door.Level);
                BoxMm doorway = new BoxMm(Strip(footprint, door.Side, 0, wall, spanMin, spanMax), floorZ, floorZ + s.OpeningHeightMm);

                // A door that runs up to a corner still leaves the perpendicular wall standing there, so the approach stops at the interior.
                int approachMin = Math.Max(spanMin, wall);
                int approachMax = Math.Min(spanMax, edgeLength - wall);
                BoxMm approach = new BoxMm(Strip(footprint, door.Side, wall, wall + s.ApproachDepthMm, approachMin, approachMax), floorZ, floorZ + s.OpeningHeightMm);

                for (int other = 0; other < Openings.Count; other++)
                {
                    BoxMm existing = Openings[other].Volume;
                    if (Protected[other].Approach.Overlaps(approach) || existing.Overlaps(doorway))
                    {
                        Failure = $"{name} overlaps connection {other}; their openings or approaches cross.";
                        return;
                    }
                }

                Openings.Add(new Opening(i, door.Side, doorway));
                Protected.Add(new ProtectedRegion(i, door.Level, door.Side, doorway, approach));
                MinLevel = Math.Min(MinLevel, door.Level);
                MaxLevel = Math.Max(MaxLevel, door.Level);
            }

            if (context.Connections.Count == 0)
            {
                MinLevel = 0;
                MaxLevel = 0;
            }
        }

        /// <summary>
        /// A band along one side of the footprint from <paramref name="from"/> to <paramref name="to"/> millimetres in from that edge, across the span.
        /// </summary>
        public static RectMm Strip(RectMm footprint, Side side, int from, int to, int spanMin, int spanMax)
        {
            switch (side)
            {
                case Side.East: return new RectMm(footprint.MaxX - to, spanMin, footprint.MaxX - from, spanMax);
                case Side.West: return new RectMm(footprint.MinX + from, spanMin, footprint.MinX + to, spanMax);
                case Side.North: return new RectMm(spanMin, footprint.MaxY - to, spanMax, footprint.MaxY - from);
                default: return new RectMm(spanMin, footprint.MinY + from, spanMax, footprint.MinY + to);
            }
        }
    }
}
