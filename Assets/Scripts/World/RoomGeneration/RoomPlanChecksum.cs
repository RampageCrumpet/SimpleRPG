using System.Collections.Generic;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// A checksum of a structural plan: every list, in order, as integers. It says nothing about meshes or floating point, so two machines
    /// can compare plans without comparing vertices. Never returns 0, so 0 can mean "not provided".
    /// </summary>
    public static class RoomPlanChecksum
    {
        public static int Of(GeneratedRoom room)
        {
            ulong h = 14695981039346656037UL;
            h = RoomSeed.Add(h, room.GeneratorId ?? string.Empty);
            h = RoomSeed.Add(h, room.IsFailure ? 1 : 0);
            h = Rect(h, room.Footprint);
            h = RoomSeed.Add(h, room.MinLevel);
            h = RoomSeed.Add(h, room.MaxLevel);

            h = RoomSeed.Add(h, room.Floors.Count);
            foreach (FloorSurface f in room.Floors)
            {
                h = Rect(h, f.Area);
                h = RoomSeed.Add(h, f.TopZ);
                h = RoomSeed.Add(h, f.ThicknessMm);
                h = RoomSeed.Add(h, f.Level);
                h = RoomSeed.Add(h, f.IsLanding ? 1 : 0);
            }

            h = RoomSeed.Add(h, room.Ceilings.Count);
            foreach (CeilingSlab c in room.Ceilings)
            {
                h = Rect(h, c.Area);
                h = RoomSeed.Add(h, c.BottomZ);
                h = RoomSeed.Add(h, c.ThicknessMm);
            }

            h = RoomSeed.Add(h, room.Walls.Count);
            foreach (WallSegment w in room.Walls)
            {
                h = Rect(h, w.Area);
                h = RoomSeed.Add(h, w.BottomZ);
                h = RoomSeed.Add(h, w.TopZ);
                h = RoomSeed.Add(h, (int)w.Kind);
            }

            h = RoomSeed.Add(h, room.Solids.Count);
            foreach (SolidVolume s in room.Solids)
            {
                h = Box(h, s.Box);
                h = RoomSeed.Add(h, (int)s.Kind);
            }

            h = RoomSeed.Add(h, room.Stairs.Count);
            foreach (StairRun run in room.Stairs)
            {
                h = RoomSeed.Add(h, run.FromLevel);
                h = RoomSeed.Add(h, run.Flights.Count);
                foreach (StairFlight f in run.Flights)
                {
                    h = Rect(h, f.Area);
                    h = RoomSeed.Add(h, (int)f.Direction);
                    h = RoomSeed.Add(h, f.BaseZ);
                    h = RoomSeed.Add(h, f.RiseMm);
                    h = RoomSeed.Add(h, f.Steps);
                    h = RoomSeed.Add(h, f.SupportZ);
                }

                h = RoomSeed.Add(h, run.Landings.Count);
                foreach (FloorSurface l in run.Landings)
                {
                    h = Rect(h, l.Area);
                    h = RoomSeed.Add(h, l.TopZ);
                    h = RoomSeed.Add(h, l.ThicknessMm);
                }
            }

            h = RoomSeed.Add(h, room.Openings.Count);
            foreach (Opening o in room.Openings)
            {
                h = RoomSeed.Add(h, o.ConnectionIndex);
                h = RoomSeed.Add(h, (int)o.Side);
                h = Box(h, o.Volume);
            }

            h = RoomSeed.Add(h, room.Protected.Count);
            foreach (ProtectedRegion p in room.Protected)
            {
                h = RoomSeed.Add(h, p.ConnectionIndex);
                h = RoomSeed.Add(h, p.Level);
                h = Box(h, p.Doorway);
                h = Box(h, p.Approach);
            }

            int result = (int)(uint)(h ^ (h >> 32));
            return result == 0 ? 1 : result;
        }

        /// <summary>
        /// Combines per-room checksums into one for the dungeon. Pass them in a stable order (by placed room id).
        /// </summary>
        public static int Combine(IEnumerable<int> checksums)
        {
            ulong h = 14695981039346656037UL;
            int count = 0;
            foreach (int checksum in checksums)
            {
                h = RoomSeed.Add(h, checksum);
                count++;
            }

            h = RoomSeed.Add(h, count);
            int result = (int)(uint)(h ^ (h >> 32));
            return result == 0 ? 1 : result;
        }

        private static ulong Rect(ulong h, RectMm r)
        {
            h = RoomSeed.Add(h, r.MinX);
            h = RoomSeed.Add(h, r.MinY);
            h = RoomSeed.Add(h, r.MaxX);
            return RoomSeed.Add(h, r.MaxY);
        }

        private static ulong Box(ulong h, BoxMm b)
        {
            h = Rect(h, b.Area);
            h = RoomSeed.Add(h, b.MinZ);
            return RoomSeed.Add(h, b.MaxZ);
        }
    }
}
