using System.Collections.Generic;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// The structure every generator shares: perimeter walls with the assigned openings cut through them, the protected regions, floors with
    /// wells where stairs arrive, and the ceiling. Generators add their own interior on top, so no algorithm builds its own shell.
    /// </summary>
    public static class RoomShell
    {
        /// <summary>
        /// The height of the underside of whatever covers a level: the floor slab of the next level, or the ceiling over the highest.
        /// </summary>
        public static int LevelTop(RoomGenerationContext context, ConnectionMap map, int level)
        {
            RoomGenerationSettings s = context.Settings;
            return level < map.MaxLevel
                ? context.LevelElevation(level + 1) - s.FloorThicknessMm
                : context.LevelElevation(level) + s.CeilingHeightMm;
        }

        /// <summary>
        /// The room inside its perimeter walls.
        /// </summary>
        public static RectMm Interior(RoomGenerationContext context)
        {
            int t = context.Settings.WallThicknessMm;
            return context.FootprintMm.Inflate(-t);
        }

        /// <summary>
        /// Starts a plan: the perimeter walls, the openings and the protected regions. Returns null if the footprint cannot hold a usable room;
        /// <paramref name="failure"/> says why.
        /// </summary>
        public static GeneratedRoom Begin(IRoomGenerator generator, RoomGenerationContext context, ConnectionMap map, out string failure)
        {
            RoomGenerationSettings s = context.Settings;
            RectMm footprint = context.FootprintMm;
            RectMm interior = Interior(context);
            failure = null;

            if (interior.Width < s.MinPassageWidthMm || interior.Height < s.MinPassageWidthMm)
            {
                failure = $"The footprint {footprint.Width}x{footprint.Height} mm leaves an interior of {interior.Width}x{interior.Height} mm inside {s.WallThicknessMm} mm walls, narrower than the minimum passage of {s.MinPassageWidthMm} mm.";
                return null;
            }

            var room = new GeneratedRoom(generator.Id, context.Seed, footprint, map.MinLevel, map.MaxLevel);
            room.Openings.AddRange(map.Openings);
            room.Protected.AddRange(map.Protected);

            int bottom = context.LevelElevation(map.MinLevel);
            int top = LevelTop(context, map, map.MaxLevel);
            int t = s.WallThicknessMm;
            room.Walls.Add(new WallSegment(new RectMm(footprint.MinX, footprint.MaxY - t, footprint.MaxX, footprint.MaxY), bottom, top, WallKind.Perimeter));
            room.Walls.Add(new WallSegment(new RectMm(footprint.MinX, footprint.MinY, footprint.MaxX, footprint.MinY + t), bottom, top, WallKind.Perimeter));
            room.Walls.Add(new WallSegment(new RectMm(footprint.MinX, footprint.MinY + t, footprint.MinX + t, footprint.MaxY - t), bottom, top, WallKind.Perimeter));
            room.Walls.Add(new WallSegment(new RectMm(footprint.MaxX - t, footprint.MinY + t, footprint.MaxX, footprint.MaxY - t), bottom, top, WallKind.Perimeter));
            return room;
        }

        /// <summary>
        /// Completes a plan once its stairs are chosen: a floor for each level used (with a well cut out where stairs arrive from below),
        /// and the ceiling over the highest.
        /// </summary>
        public static void Finish(GeneratedRoom room, RoomGenerationContext context, ConnectionMap map)
        {
            RoomGenerationSettings s = context.Settings;
            RectMm footprint = context.FootprintMm;

            for (int level = map.MinLevel; level <= map.MaxLevel; level++)
            {
                var pieces = new List<RectMm> { footprint };
                var next = new List<RectMm>();

                foreach (StairRun run in room.Stairs)
                {
                    if (run.ToLevel != level)
                    {
                        continue;
                    }

                    foreach (RectMm well in run.Well())
                    {
                        next.Clear();
                        foreach (RectMm piece in pieces)
                        {
                            piece.Subtract(well, next);
                        }

                        pieces.Clear();
                        pieces.AddRange(next);
                    }
                }

                foreach (RectMm piece in pieces)
                {
                    room.Floors.Add(new FloorSurface(piece, context.LevelElevation(level), s.FloorThicknessMm, level));
                }
            }

            room.Ceilings.Add(new CeilingSlab(footprint, LevelTop(context, map, map.MaxLevel), s.CeilingThicknessMm));
        }
    }
}
