using System;
using System.Collections.Generic;
using System.Text;
using SimpleRPG.LevelGeneration;

namespace SimpleRPG.RoomGeneration
{
    public sealed class RoomPlanIssue
    {
        public RoomPlanIssue(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public string Code { get; }
        public string Message { get; }
        public override string ToString() => $"{Code}: {Message}";
    }

    public sealed class RoomPlanReport
    {
        public List<RoomPlanIssue> Issues { get; } = new List<RoomPlanIssue>();
        public bool IsValid => Issues.Count == 0;

        public bool Has(string code)
        {
            foreach (RoomPlanIssue issue in Issues)
            {
                if (issue.Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            if (IsValid)
            {
                return "valid";
            }

            var text = new StringBuilder();
            for (int i = 0; i < Issues.Count; i++)
            {
                text.Append(i == 0 ? "" : " | ").Append(Issues[i]);
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Checks a structural plan against the context it was made for, independently of the algorithm that made it. A generator is never trusted
    /// to have kept its own promises: the openings and protected regions are rebuilt from the layout's connections and compared, obstruction is
    /// tested on the same resolved pieces that are meshed, and reachability is tested by moving a square of the minimum passage width through
    /// a raster of the walkable floor, including up and down the stairs.
    /// </summary>
    public static class RoomPlanValidator
    {
        public const string GenerationFailed = "generation-failed";
        public const string BadSettings = "bad-settings";
        public const string BadConnections = "bad-connections";
        public const string ConnectionChanged = "connection-changed";
        public const string OutOfBounds = "out-of-bounds";
        public const string Obstructed = "opening-obstructed";
        public const string Stairs = "stairs-invalid";
        public const string Headroom = "headroom";
        public const string Blocked = "connection-blocked";
        public const string Unreachable = "connections-unreachable";
        public const string NoInterior = "no-usable-interior";

        private const int MaxMessagesPerCode = 6;

        public static RoomPlanReport Validate(GeneratedRoom room, RoomGenerationContext context)
        {
            var report = new RoomPlanReport();

            if (room == null)
            {
                report.Issues.Add(new RoomPlanIssue(GenerationFailed, "The generator returned no plan."));
                return report;
            }

            if (room.IsFailure)
            {
                report.Issues.Add(new RoomPlanIssue(GenerationFailed, room.Failure));
                return report;
            }

            foreach (string problem in context.Settings.Check())
            {
                report.Issues.Add(new RoomPlanIssue(BadSettings, problem));
            }

            if (!report.IsValid)
            {
                return report;
            }

            ConnectionMap map = ConnectionMap.Build(context);
            if (map.Failure != null)
            {
                report.Issues.Add(new RoomPlanIssue(BadConnections, map.Failure));
                return report;
            }

            CheckConnectionsKept(room, map, report);
            CheckBounds(room, context, map, report);
            List<StructuralPiece> pieces = room.ResolvePieces();
            CheckObstruction(room, pieces, report);
            CheckStairs(room, context, map, pieces, report);

            if (report.IsValid)
            {
                CheckReachability(room, context, map, pieces, report);
            }

            return report;
        }

        // ---- Connections ----

        private static void CheckConnectionsKept(GeneratedRoom room, ConnectionMap map, RoomPlanReport report)
        {
            bool same = room.Openings.Count == map.Openings.Count && room.Protected.Count == map.Protected.Count;
            for (int i = 0; same && i < map.Openings.Count; i++)
            {
                same = room.Openings[i].ConnectionIndex == map.Openings[i].ConnectionIndex
                    && room.Openings[i].Side == map.Openings[i].Side
                    && room.Openings[i].Volume.Equals(map.Openings[i].Volume)
                    && room.Protected[i].ConnectionIndex == map.Protected[i].ConnectionIndex
                    && room.Protected[i].Doorway.Equals(map.Protected[i].Doorway)
                    && room.Protected[i].Approach.Equals(map.Protected[i].Approach);
            }

            if (!same)
            {
                report.Issues.Add(new RoomPlanIssue(ConnectionChanged, "The plan's openings or protected regions differ from the ones the assigned connections require."));
            }
        }

        // ---- Bounds ----

        private static void CheckBounds(GeneratedRoom room, RoomGenerationContext context, ConnectionMap map, RoomPlanReport report)
        {
            RoomGenerationSettings s = context.Settings;
            RectMm footprint = context.FootprintMm;

            if (room.Footprint != footprint)
            {
                report.Issues.Add(new RoomPlanIssue(OutOfBounds, $"The plan's footprint {room.Footprint} is not the assigned {footprint}."));
                return;
            }

            int minZ = context.LevelElevation(map.MinLevel) - s.FloorThicknessMm;
            int maxZ = context.Levels * s.FloorHeightMm;
            int reported = 0;

            void Check(BoxMm box, string what)
            {
                bool bad = !footprint.Contains(box.Area) || box.MinZ < minZ || (s.FloorHeightMm > 0 && box.MaxZ > maxZ);
                if (bad && reported++ < MaxMessagesPerCode)
                {
                    report.Issues.Add(new RoomPlanIssue(OutOfBounds, $"{what} {box} lies outside the footprint {footprint} or the room's height {minZ}..{maxZ}."));
                }
            }

            foreach (StructuralPiece piece in room.ResolvePieces())
            {
                Check(piece.Box, piece.Kind.ToString());
            }

            foreach (SolidVolume solid in room.Solids)
            {
                if (solid.Box.IsEmpty && reported++ < MaxMessagesPerCode)
                {
                    report.Issues.Add(new RoomPlanIssue(OutOfBounds, $"A solid {solid.Box} has no volume."));
                }
            }
        }

        // ---- Obstruction ----

        private static void CheckObstruction(GeneratedRoom room, List<StructuralPiece> pieces, RoomPlanReport report)
        {
            int reported = 0;
            foreach (ProtectedRegion region in room.Protected)
            {
                foreach (StructuralPiece piece in pieces)
                {
                    bool hitsDoorway = piece.Box.Overlaps(region.Doorway);
                    bool hitsApproach = piece.Box.Overlaps(region.Approach);
                    if ((hitsDoorway || hitsApproach) && reported++ < MaxMessagesPerCode)
                    {
                        report.Issues.Add(new RoomPlanIssue(Obstructed,
                            $"A {piece.Kind} {piece.Box} blocks the {(hitsDoorway ? "doorway" : "approach")} of connection {region.ConnectionIndex}."));
                    }
                }
            }
        }

        // ---- Stairs ----

        private static void CheckStairs(GeneratedRoom room, RoomGenerationContext context, ConnectionMap map, List<StructuralPiece> pieces, RoomPlanReport report)
        {
            RoomGenerationSettings s = context.Settings;
            var seen = new HashSet<int>();

            foreach (StairRun run in room.Stairs)
            {
                string name = $"Stairs from level {run.FromLevel}";
                if (run.FromLevel < map.MinLevel || run.FromLevel >= map.MaxLevel || !seen.Add(run.FromLevel))
                {
                    report.Issues.Add(new RoomPlanIssue(Stairs, $"{name} are not needed or are duplicated (the room's doors span levels {map.MinLevel}..{map.MaxLevel})."));
                    continue;
                }

                if (run.Flights.Count == 0 || run.Landings.Count != run.Flights.Count - 1)
                {
                    report.Issues.Add(new RoomPlanIssue(Stairs, $"{name} have {run.Flights.Count} flight(s) and {run.Landings.Count} landing(s)."));
                    continue;
                }

                int bottom = context.LevelElevation(run.FromLevel);
                int top = context.LevelElevation(run.ToLevel);
                if (run.First.BaseZ != bottom || run.Last.TopZ != top)
                {
                    report.Issues.Add(new RoomPlanIssue(Stairs, $"{name} run from {run.First.BaseZ} to {run.Last.TopZ} but the levels are at {bottom} and {top}."));
                }

                for (int k = 0; k < run.Flights.Count; k++)
                {
                    StairFlight flight = run.Flights[k];
                    if (flight.Steps < 1 || flight.RiseMm <= 0)
                    {
                        report.Issues.Add(new RoomPlanIssue(Stairs, $"{name}, flight {k} has {flight.Steps} step(s) and a rise of {flight.RiseMm}."));
                        continue;
                    }

                    int riser = (flight.RiseMm + flight.Steps - 1) / flight.Steps;
                    int tread = flight.RunMm / flight.Steps;
                    if (riser > s.StepHeightMm || tread < s.StepDepthMm)
                    {
                        report.Issues.Add(new RoomPlanIssue(Stairs, $"{name}, flight {k} has risers up to {riser} mm (limit {s.StepHeightMm}) and treads {tread} mm (minimum {s.StepDepthMm})."));
                    }

                    if (flight.WidthMm < s.MinPassageWidthMm)
                    {
                        report.Issues.Add(new RoomPlanIssue(Stairs, $"{name}, flight {k} is {flight.WidthMm} mm wide, under the minimum passage of {s.MinPassageWidthMm} mm."));
                    }

                    if (k > 0)
                    {
                        FloorSurface landing = run.Landings[k - 1];
                        StairFlight before = run.Flights[k - 1];
                        bool joined = before.TopZ == flight.BaseZ && landing.TopZ == flight.BaseZ
                            && landing.Area.Inflate(1).Overlaps(before.Area) && landing.Area.Inflate(1).Overlaps(flight.Area)
                            && Math.Min(landing.Area.Width, landing.Area.Height) >= s.MinPassageWidthMm;
                        if (!joined)
                        {
                            report.Issues.Add(new RoomPlanIssue(Stairs, $"{name}, flights {k - 1} and {k} are not joined by a landing at the right height and size."));
                        }
                    }

                    // Headroom: nothing may stand within the clearance height above any tread.
                    for (int step = 0; step < flight.Steps; step++)
                    {
                        var clear = new BoxMm(flight.StepArea(step), flight.StepTopZ(step), flight.StepTopZ(step) + s.ClearanceHeightMm);
                        bool blocked = false;
                        foreach (StructuralPiece piece in pieces)
                        {
                            if (piece.Box.Overlaps(clear))
                            {
                                report.Issues.Add(new RoomPlanIssue(Headroom, $"{name}, flight {k}, step {step}: a {piece.Kind} {piece.Box} leaves less than {s.ClearanceHeightMm} mm of headroom."));
                                blocked = true;
                                break;
                            }
                        }

                        if (blocked)
                        {
                            break;
                        }
                    }
                }
            }

            for (int level = map.MinLevel; level < map.MaxLevel; level++)
            {
                if (!seen.Contains(level))
                {
                    report.Issues.Add(new RoomPlanIssue(Stairs, $"No stairs join level {level} to level {level + 1}, but doors stand on levels {map.MinLevel}..{map.MaxLevel}."));
                }
            }
        }

        // ---- Reachability ----

        private sealed class Layer
        {
            public bool[] Bad;
            public int[] Sum;
        }

        private static void CheckReachability(GeneratedRoom room, RoomGenerationContext context, ConnectionMap map, List<StructuralPiece> pieces, RoomPlanReport report)
        {
            RoomGenerationSettings s = context.Settings;
            if (room.Protected.Count == 0)
            {
                return;
            }

            int g = s.ValidationResolutionMm;
            int n = (s.MinPassageWidthMm + g - 1) / g;
            RectMm footprint = context.FootprintMm;
            int nx = (footprint.Width + g - 1) / g;
            int ny = (footprint.Height + g - 1) / g;
            int levelCount = map.MaxLevel - map.MinLevel + 1;

            if (nx < n || ny < n)
            {
                report.Issues.Add(new RoomPlanIssue(NoInterior, $"The room is smaller than the minimum passage of {s.MinPassageWidthMm} mm."));
                return;
            }

            var layers = new Layer[levelCount];
            for (int index = 0; index < levelCount; index++)
            {
                layers[index] = BuildLayer(room, pieces, context, map.MinLevel + index, g, nx, ny);
            }

            bool Passable(int layer, int ix, int iy)
            {
                if (ix < 0 || iy < 0 || ix + n > nx || iy + n > ny)
                {
                    return false;
                }

                int[] sum = layers[layer].Sum;
                int stride = nx + 1;
                return sum[(iy + n) * stride + ix + n] - sum[iy * stride + ix + n] - sum[(iy + n) * stride + ix] + sum[iy * stride + ix] == 0;
            }

            int Index(int layer, int ix, int iy) => (layer * ny + iy) * nx + ix;

            // Each connection: the squares from the inner end of its approach out through the doorway must all be walkable.
            var seeds = new List<int>();
            foreach (ProtectedRegion region in room.Protected)
            {
                int layer = region.Level - map.MinLevel;
                RectMm approach = region.Approach.Area;
                RectMm doorway = region.Doorway.Area;
                bool alongY = region.Side == Side.East || region.Side == Side.West;
                int lateralCentre = alongY ? approach.CenterY : approach.CenterX;
                int lateral = FloorDiv(lateralCentre - n * g / 2, g);
                int startCell, endCell, step;

                switch (region.Side)
                {
                    case Side.East: startCell = FloorDiv(approach.MinX, g); endCell = FloorDiv(doorway.MaxX, g) - n; step = 1; break;
                    case Side.West: startCell = FloorDiv(approach.MaxX, g) - n; endCell = FloorDiv(doorway.MinX, g); step = -1; break;
                    case Side.North: startCell = FloorDiv(approach.MinY, g); endCell = FloorDiv(doorway.MaxY, g) - n; step = 1; break;
                    default: startCell = FloorDiv(approach.MaxY, g) - n; endCell = FloorDiv(doorway.MinY, g); step = -1; break;
                }

                bool clear = true;
                for (int cell = startCell; ; cell += step)
                {
                    int ix = alongY ? cell : lateral;
                    int iy = alongY ? lateral : cell;
                    if (!Passable(layer, ix, iy))
                    {
                        clear = false;
                        break;
                    }

                    if (cell == endCell)
                    {
                        break;
                    }
                }

                if (!clear)
                {
                    report.Issues.Add(new RoomPlanIssue(Blocked, $"Connection {region.ConnectionIndex} cannot be walked through: a {s.MinPassageWidthMm} mm square does not fit from its approach through its doorway."));
                    seeds.Add(-1);
                }
                else
                {
                    seeds.Add(Index(layer, alongY ? startCell : lateral, alongY ? lateral : startCell));
                }
            }

            if (!report.IsValid)
            {
                return;
            }

            // Stairs link two squares: the one just before the first step and the one just past the last.
            var links = new Dictionary<int, List<int>>();
            void Link(int a, int b)
            {
                if (!links.TryGetValue(a, out List<int> list))
                {
                    links[a] = list = new List<int>();
                }

                list.Add(b);
            }

            foreach (StairRun run in room.Stairs)
            {
                (int ex, int ey) = Portal(run.First, true, g, n);
                (int xx, int xy) = Portal(run.Last, false, g, n);
                int lower = run.FromLevel - map.MinLevel;
                int upper = run.ToLevel - map.MinLevel;
                bool lowerOk = Passable(lower, ex, ey);
                bool upperOk = Passable(upper, xx, xy);
                if (!lowerOk || !upperOk)
                {
                    report.Issues.Add(new RoomPlanIssue(Stairs, $"Stairs from level {run.FromLevel}: a {s.MinPassageWidthMm} mm square does not fit {(lowerOk ? "past the top step" : "before the first step")}."));
                    continue;
                }

                int a = Index(lower, ex, ey);
                int b = Index(upper, xx, xy);
                Link(a, b);
                Link(b, a);
            }

            if (!report.IsValid)
            {
                return;
            }

            var visited = new bool[levelCount * nx * ny];
            var queue = new Queue<int>();
            visited[seeds[0]] = true;
            queue.Enqueue(seeds[0]);
            int reachable = 0;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                reachable++;
                int ix = current % nx;
                int iy = current / nx % ny;
                int layer = current / (nx * ny);

                void Visit(int nextLayer, int nextX, int nextY)
                {
                    if (!Passable(nextLayer, nextX, nextY))
                    {
                        return;
                    }

                    int next = Index(nextLayer, nextX, nextY);
                    if (!visited[next])
                    {
                        visited[next] = true;
                        queue.Enqueue(next);
                    }
                }

                Visit(layer, ix + 1, iy);
                Visit(layer, ix - 1, iy);
                Visit(layer, ix, iy + 1);
                Visit(layer, ix, iy - 1);

                if (links.TryGetValue(current, out List<int> linked))
                {
                    foreach (int target in linked)
                    {
                        if (!visited[target])
                        {
                            visited[target] = true;
                            queue.Enqueue(target);
                        }
                    }
                }
            }

            for (int i = 1; i < seeds.Count; i++)
            {
                if (!visited[seeds[i]])
                {
                    report.Issues.Add(new RoomPlanIssue(Unreachable, $"Connection {room.Protected[i].ConnectionIndex} cannot be reached from connection {room.Protected[0].ConnectionIndex} through the room."));
                }
            }

            int usable = (2 * n + 1) * (2 * n + 1);
            if (report.IsValid && reachable < usable)
            {
                report.Issues.Add(new RoomPlanIssue(NoInterior, $"Only {reachable} walkable positions are reachable from the connections; a usable interior needs at least {usable}."));
            }
        }

        private static Layer BuildLayer(GeneratedRoom room, List<StructuralPiece> pieces, RoomGenerationContext context, int level, int g, int nx, int ny)
        {
            RoomGenerationSettings s = context.Settings;
            int floorZ = context.LevelElevation(level);
            var layer = new Layer { Bad = new bool[nx * ny], Sum = new int[(nx + 1) * (ny + 1)] };
            bool[] bad = layer.Bad;
            for (int i = 0; i < bad.Length; i++)
            {
                bad[i] = true;
            }

            // Walkable only where a floor of this level covers the whole cell.
            foreach (FloorSurface floor in room.Floors)
            {
                if (floor.Level != level || floor.TopZ != floorZ)
                {
                    continue;
                }

                int x0 = CeilDiv(floor.Area.MinX, g), x1 = FloorDiv(floor.Area.MaxX, g);
                int y0 = CeilDiv(floor.Area.MinY, g), y1 = FloorDiv(floor.Area.MaxY, g);
                for (int y = Math.Max(0, y0); y < Math.Min(ny, y1); y++)
                {
                    for (int x = Math.Max(0, x0); x < Math.Min(nx, x1); x++)
                    {
                        bad[y * nx + x] = false;
                    }
                }
            }

            // Anything solid within the clearance height above the floor blocks every cell it touches.
            foreach (StructuralPiece piece in pieces)
            {
                BoxMm box = piece.Box;
                if (box.MinZ >= floorZ + s.ClearanceHeightMm || box.MaxZ <= floorZ)
                {
                    continue;
                }

                int x0 = FloorDiv(box.Area.MinX, g), x1 = CeilDiv(box.Area.MaxX, g);
                int y0 = FloorDiv(box.Area.MinY, g), y1 = CeilDiv(box.Area.MaxY, g);
                for (int y = Math.Max(0, y0); y < Math.Min(ny, y1); y++)
                {
                    for (int x = Math.Max(0, x0); x < Math.Min(nx, x1); x++)
                    {
                        bad[y * nx + x] = true;
                    }
                }
            }

            int stride = nx + 1;
            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    layer.Sum[(y + 1) * stride + x + 1] = layer.Sum[y * stride + x + 1] + layer.Sum[(y + 1) * stride + x] - layer.Sum[y * stride + x] + (bad[y * nx + x] ? 1 : 0);
                }
            }

            return layer;
        }

        /// <summary>
        /// The square position (lowest corner, in raster cells) just before a flight's first step, or just past its last.
        /// </summary>
        private static (int x, int y) Portal(StairFlight flight, bool entry, int g, int n)
        {
            RectMm a = flight.Area;
            bool climbsAlongX = flight.Direction == Side.East || flight.Direction == Side.West;
            int lateralCentre = climbsAlongX ? a.CenterY : a.CenterX;
            int lateral = FloorDiv(lateralCentre - n * g / 2, g);

            // Entering a flight, the walker stands behind its start; leaving, beyond its end.
            Side facing = entry ? GridMath.Opposite(flight.Direction) : flight.Direction;
            switch (facing)
            {
                case Side.East: return (CeilDiv(a.MaxX, g), lateral);
                case Side.West: return (FloorDiv(a.MinX, g) - n, lateral);
                case Side.North: return (lateral, CeilDiv(a.MaxY, g));
                default: return (lateral, FloorDiv(a.MinY, g) - n);
            }
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            return value % divisor != 0 && (value < 0) != (divisor < 0) ? quotient - 1 : quotient;
        }

        private static int CeilDiv(int value, int divisor) => -FloorDiv(-value, divisor);
    }
}
