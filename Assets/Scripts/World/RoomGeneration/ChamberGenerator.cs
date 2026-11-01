using System;
using System.Collections.Generic;
using SimpleRPG.LevelGeneration;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Relatively open rooms: perimeter walls, floors and a ceiling, optionally with pillars and partial partitions, and with stairs
    /// wherever the connections stand on different levels. An empty chamber is a normal result. Every attempt is checked by
    /// <see cref="RoomPlanValidator"/>; features that break a connection or a passage are dropped on the next attempt, and the last attempt has none.
    /// </summary>
    public sealed class ChamberGenerator : IRoomGenerator
    {
        public const string GeneratorId = "chamber";

        public string Id => GeneratorId;

        public int Version => 1;

        public GeneratedRoom Generate(RoomGenerationContext context)
        {
            List<string> problems = context.Settings.Check();
            if (problems.Count > 0)
            {
                return GeneratedRoom.Failed(Id, context.Seed, string.Join(" ", problems));
            }

            ConnectionMap map = ConnectionMap.Build(context);
            if (map.Failure != null)
            {
                return GeneratedRoom.Failed(Id, context.Seed, map.Failure);
            }

            ChamberSettings chamber = context.GetSettings<ChamberSettings>();
            bool wantsFeatures = chamber.Pillars != PillarLayout.None || chamber.Partitions > 0;
            bool needsStairs = map.MaxLevel > map.MinLevel;
            int featureAttempts = wantsFeatures ? Math.Max(0, chamber.FeatureAttempts) : 0;
            int plainAttempts = needsStairs ? Math.Max(1, chamber.StairAttempts) : 1;
            string lastReason = null;

            for (int attempt = 0; attempt < featureAttempts + plainAttempts; attempt++)
            {
                bool features = attempt < featureAttempts;
                GeneratedRoom room = Build(context, map, chamber, attempt, features, out string reason);
                if (room == null)
                {
                    lastReason = reason;
                    continue;
                }

                RoomPlanReport report = RoomPlanValidator.Validate(room, context);
                if (report.IsValid)
                {
                    return room;
                }

                lastReason = report.ToString();
            }

            return GeneratedRoom.Failed(Id, context.Seed, lastReason ?? "no attempt produced a valid chamber");
        }

        private GeneratedRoom Build(RoomGenerationContext context, ConnectionMap map, ChamberSettings chamber, int attempt, bool features, out string reason)
        {
            GeneratedRoom room = RoomShell.Begin(this, context, map, out reason);
            if (room == null)
            {
                return null;
            }

            var stairRandom = new RoomRandom(RoomSeed.Stream(context.Seed, 1000 + attempt));
            for (int level = map.MinLevel; level < map.MaxLevel; level++)
            {
                StairRun run = PlaceStairs(context, room, level, ref stairRandom, out reason);
                if (run == null)
                {
                    return null;
                }

                room.Stairs.Add(run);
            }

            if (features)
            {
                var featureRandom = new RoomRandom(RoomSeed.Stream(context.Seed, 2000 + attempt));
                for (int level = map.MinLevel; level <= map.MaxLevel; level++)
                {
                    AddFeatures(context, map, room, chamber, level, ref featureRandom);
                }
            }

            RoomShell.Finish(room, context, map);
            reason = null;
            return room;
        }

        // ---- Stairs ----

        private static StairRun PlaceStairs(RoomGenerationContext context, GeneratedRoom room, int level, ref RoomRandom random, out string reason)
        {
            RoomGenerationSettings s = context.Settings;
            RectMm interior = RoomShell.Interior(context);
            int resolution = s.PlanningResolutionMm;
            Side[] directions = { Side.East, Side.North, Side.West, Side.South };
            int floorZ = context.LevelElevation(level);
            string tightest = null;

            for (int flights = 1; flights <= s.MaxStairFlights; flights++)
            {
                StairTemplate template = StairPlanner.Plan(s, s.FloorHeightMm, flights);
                if (template == null)
                {
                    break;
                }

                var fitting = new List<Side>();
                foreach (Side direction in directions)
                {
                    (int w, int h) = template.Size(direction);
                    if (w <= interior.Width && h <= interior.Height)
                    {
                        fitting.Add(direction);
                    }
                }

                if (fitting.Count == 0)
                {
                    (int w, int h) = template.Size(Side.East);
                    tightest = $"{flights} flight(s) need {w}x{h} mm (or {h}x{w}) but the interior is {interior.Width}x{interior.Height} mm";
                    continue;
                }

                for (int sample = 0; sample < 64; sample++)
                {
                    Side direction = fitting[random.Next(fitting.Count)];
                    (int w, int h) = template.Size(direction);
                    int ox = interior.MinX + random.Next((interior.Width - w) / resolution + 1) * resolution;
                    int oy = interior.MinY + random.Next((interior.Height - h) / resolution + 1) * resolution;
                    StairRun run = template.Place(direction, ox, oy, level, floorZ, s.FloorThicknessMm);
                    if (RunFits(context, room, run, interior))
                    {
                        reason = null;
                        return run;
                    }
                }

                tightest = $"{flights} flight(s) fit the interior but no position kept clear of the connections and other stairs";
            }

            reason = $"No stairs can join level {level} to level {level + 1}: {tightest ?? "the stair settings are unusable"}.";
            return null;
        }

        /// <summary>
        /// Cheap placement rules; the validator makes the final decision on the whole room.
        /// </summary>
        private static bool RunFits(RoomGenerationContext context, GeneratedRoom room, StairRun run, RectMm interior)
        {
            int depth = context.Settings.MinPassageWidthMm;
            RectMm entry = run.First.EntryStrip(depth);
            RectMm exit = run.Last.ExitStrip(depth);

            foreach (RectMm area in run.Well())
            {
                if (!interior.Contains(area))
                {
                    return false;
                }

                foreach (ProtectedRegion region in room.Protected)
                {
                    if ((region.Level == run.FromLevel || region.Level == run.ToLevel)
                        && (area.Overlaps(region.Approach.Area) || area.Overlaps(region.Doorway.Area)))
                    {
                        return false;
                    }
                }

                foreach (StairRun other in room.Stairs)
                {
                    foreach (RectMm otherArea in other.Well())
                    {
                        if (area.Overlaps(otherArea))
                        {
                            return false;
                        }
                    }
                }
            }

            if (!interior.Contains(entry) || !interior.Contains(exit))
            {
                return false;
            }

            foreach (StairRun other in room.Stairs)
            {
                foreach (RectMm otherArea in other.Well())
                {
                    if (entry.Overlaps(otherArea) || exit.Overlaps(otherArea))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // ---- Features ----

        private static void AddFeatures(RoomGenerationContext context, ConnectionMap map, GeneratedRoom room, ChamberSettings chamber, int level, ref RoomRandom random)
        {
            RoomGenerationSettings s = context.Settings;
            RectMm interior = RoomShell.Interior(context);
            int floorZ = context.LevelElevation(level);
            int topZ = RoomShell.LevelTop(context, map, level);
            int resolution = s.PlanningResolutionMm;
            int margin = s.MinPassageWidthMm;

            // Where nothing but open floor may be: in front of the doors and around the stairs that touch this level.
            var keepOut = new List<RectMm>();
            foreach (ProtectedRegion region in room.Protected)
            {
                if (region.Level == level)
                {
                    keepOut.Add(region.Approach.Area.Union(region.Doorway.Area).Inflate(margin));
                }
            }

            foreach (StairRun run in room.Stairs)
            {
                if (run.FromLevel == level || run.ToLevel == level)
                {
                    keepOut.Add(run.Bounds().Inflate(margin));
                }
            }

            var placed = new List<RectMm>();

            if (chamber.Pillars != PillarLayout.None)
            {
                int size = Math.Max(resolution, chamber.PillarSizeMm);
                int spacing = Math.Max(Snap(chamber.PillarSpacingMm, resolution), Snap(size + margin, resolution) + resolution);
                spacing += random.Next(3) * resolution;

                var centres = new List<(int x, int y)>();
                if (chamber.Pillars == PillarLayout.Grid)
                {
                    int columns = Math.Max(0, interior.Width / spacing);
                    int rows = Math.Max(0, interior.Height / spacing);
                    int x0 = interior.CenterX - (columns - 1) * spacing / 2;
                    int y0 = interior.CenterY - (rows - 1) * spacing / 2;
                    for (int row = 0; row < rows; row++)
                    {
                        for (int column = 0; column < columns; column++)
                        {
                            centres.Add((x0 + column * spacing, y0 + row * spacing));
                        }
                    }
                }
                else
                {
                    bool alongX = interior.Width >= interior.Height;
                    int longLength = alongX ? interior.Width : interior.Height;
                    int shortLength = alongX ? interior.Height : interior.Width;
                    int count = Math.Max(0, longLength / spacing);
                    int start = (alongX ? interior.CenterX : interior.CenterY) - (count - 1) * spacing / 2;
                    int shortMin = alongX ? interior.MinY : interior.MinX;
                    int rowA = shortMin + Snap(shortLength / 3, resolution);
                    int rowB = shortMin + shortLength - Snap(shortLength / 3, resolution);
                    for (int i = 0; i < count; i++)
                    {
                        int along = start + i * spacing;
                        centres.Add(alongX ? (along, rowA) : (rowA, along));
                        centres.Add(alongX ? (along, rowB) : (rowB, along));
                    }
                }

                foreach ((int x, int y) in centres)
                {
                    var pillar = new RectMm(x - size / 2, y - size / 2, x - size / 2 + size, y - size / 2 + size);
                    if (!interior.Contains(pillar.Inflate(margin)) || Hits(keepOut, pillar))
                    {
                        continue;
                    }

                    room.Solids.Add(new SolidVolume(new BoxMm(pillar, floorZ, topZ)));
                    placed.Add(pillar);
                }
            }

            for (int i = 0; i < chamber.Partitions; i++)
            {
                for (int tryNumber = 0; tryNumber < 24; tryNumber++)
                {
                    RectMm? candidate = PartitionCandidate(interior, s, resolution, ref random);
                    if (candidate == null)
                    {
                        continue;
                    }

                    RectMm partition = candidate.Value;
                    if (Hits(keepOut, partition) || Hits(placed, partition.Inflate(margin)))
                    {
                        continue;
                    }

                    room.Walls.Add(new WallSegment(partition, floorZ, topZ, WallKind.Partition));
                    placed.Add(partition);
                    break;
                }
            }
        }

        private static RectMm? PartitionCandidate(RectMm interior, RoomGenerationSettings s, int resolution, ref RoomRandom random)
        {
            Side from = (Side)random.Next(4);
            bool alongX = from == Side.North || from == Side.South;
            int across = alongX ? interior.Height : interior.Width;
            int along = alongX ? interior.Width : interior.Height;

            // Long enough to matter, short enough to leave a passage of at least twice the minimum at the far end.
            int longest = across - 2 * s.MinPassageWidthMm;
            int shortest = Math.Max(resolution, across / 4);
            if (longest < shortest)
            {
                return null;
            }

            int length = Snap(shortest + random.Next((longest - shortest) / resolution + 1) * resolution, resolution);
            length = Math.Min(length, longest);
            int t = s.WallThicknessMm;
            int lane = s.MinPassageWidthMm;
            int room = along - 2 * lane - t;
            if (room <= 0)
            {
                return null;
            }

            int offset = lane + random.Next(room / resolution + 1) * resolution;
            offset = Math.Min(offset, along - lane - t);

            switch (from)
            {
                case Side.North: return new RectMm(interior.MinX + offset, interior.MaxY - length, interior.MinX + offset + t, interior.MaxY);
                case Side.South: return new RectMm(interior.MinX + offset, interior.MinY, interior.MinX + offset + t, interior.MinY + length);
                case Side.East: return new RectMm(interior.MaxX - length, interior.MinY + offset, interior.MaxX, interior.MinY + offset + t);
                default: return new RectMm(interior.MinX, interior.MinY + offset, interior.MinX + length, interior.MinY + offset + t);
            }
        }

        private static bool Hits(List<RectMm> rects, RectMm rect)
        {
            foreach (RectMm other in rects)
            {
                if (other.Overlaps(rect))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Snap(int value, int step) => step <= 0 ? value : value / step * step;
    }
}
