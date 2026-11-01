using System;
using System.Collections.Generic;
using SimpleRPG.LevelGeneration;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// A staircase worked out in its own frame, before it is given a place: <c>u</c> runs along the first flight's climb, <c>v</c> across it.
    /// One flight is a straight stair; more flights switch back side by side, each pair joined by a landing.
    /// </summary>
    public sealed class StairTemplate
    {
        internal StairTemplate(int flights, int along, int across, List<(RectMm area, bool forward, int baseOffset, int rise, int steps)> localFlights,
            List<(RectMm area, int zOffset)> localLandings)
        {
            Flights = flights;
            AlongMm = along;
            AcrossMm = across;
            LocalFlights = localFlights;
            LocalLandings = localLandings;
        }

        public int Flights { get; }

        /// <summary>
        /// The extent along u, and across it.
        /// </summary>
        public int AlongMm { get; }

        public int AcrossMm { get; }

        internal List<(RectMm area, bool forward, int baseOffset, int rise, int steps)> LocalFlights { get; }
        internal List<(RectMm area, int zOffset)> LocalLandings { get; }

        /// <summary>
        /// The width and height (x and y extent) of the run once it climbs towards <paramref name="direction"/>.
        /// </summary>
        public (int width, int height) Size(Side direction) => direction == Side.East || direction == Side.West ? (AlongMm, AcrossMm) : (AcrossMm, AlongMm);

        /// <summary>
        /// Puts the staircase in the room: its lowest corner at (<paramref name="originX"/>, <paramref name="originY"/>), the first flight climbing towards
        /// <paramref name="direction"/>, starting from the floor at <paramref name="baseZ"/> on level <paramref name="fromLevel"/>.
        /// </summary>
        public StairRun Place(Side direction, int originX, int originY, int fromLevel, int baseZ, int landingThicknessMm)
        {
            var flights = new List<StairFlight>(LocalFlights.Count);
            var landings = new List<FloorSurface>(LocalLandings.Count);

            foreach ((RectMm area, bool forward, int baseOffset, int rise, int steps) in LocalFlights)
            {
                Side climb = forward ? direction : GridMath.Opposite(direction);
                flights.Add(new StairFlight(Map(area, direction, originX, originY), climb, baseZ + baseOffset, rise, steps, baseZ));
            }

            foreach ((RectMm area, int zOffset) in LocalLandings)
            {
                landings.Add(new FloorSurface(Map(area, direction, originX, originY), baseZ + zOffset, landingThicknessMm, fromLevel, true));
            }

            return new StairRun(fromLevel, flights, landings);
        }

        private RectMm Map(RectMm local, Side direction, int ox, int oy)
        {
            switch (direction)
            {
                case Side.East: return new RectMm(ox + local.MinX, oy + local.MinY, ox + local.MaxX, oy + local.MaxY);
                case Side.West: return new RectMm(ox + AlongMm - local.MaxX, oy + local.MinY, ox + AlongMm - local.MinX, oy + local.MaxY);
                case Side.North: return new RectMm(ox + local.MinY, oy + local.MinX, ox + local.MaxY, oy + local.MaxX);
                default: return new RectMm(ox + local.MinY, oy + AlongMm - local.MaxX, ox + local.MaxY, oy + AlongMm - local.MinX);
            }
        }
    }

    /// <summary>
    /// The shared staircase construction: sizes straight flights and turning landings from physical dimensions. Any generator, including
    /// a future dedicated stairwell, uses this rather than building steps itself.
    /// </summary>
    public static class StairPlanner
    {
        /// <summary>
        /// Plans a staircase climbing <paramref name="riseMm"/> in <paramref name="flightCount"/> flights, or returns null if the numbers are unusable.
        /// Every riser is at most <see cref="RoomGenerationSettings.StepHeightMm"/> and every tread exactly <see cref="RoomGenerationSettings.StepDepthMm"/> deep.
        /// </summary>
        public static StairTemplate Plan(RoomGenerationSettings settings, int riseMm, int flightCount)
        {
            if (riseMm <= 0 || flightCount < 1 || settings.StepHeightMm <= 0 || settings.StepDepthMm <= 0 || settings.StairWidthMm <= 0)
            {
                return null;
            }

            int width = settings.StairWidthMm;
            int landing = settings.LandingDepthMm;
            var flights = new List<(RectMm area, bool forward, int baseOffset, int rise, int steps)>();
            var landings = new List<(RectMm area, int zOffset)>();

            int cursor = 0;
            bool forward = true;
            for (int k = 0; k < flightCount; k++)
            {
                int baseOffset = (int)((long)riseMm * k / flightCount);
                int topOffset = (int)((long)riseMm * (k + 1) / flightCount);
                int rise = topOffset - baseOffset;
                int steps = Math.Max(1, (rise + settings.StepHeightMm - 1) / settings.StepHeightMm);
                int run = steps * settings.StepDepthMm;
                int v0 = k * width;

                int far = forward ? cursor + run : cursor - run;
                flights.Add((new RectMm(Math.Min(cursor, far), v0, Math.Max(cursor, far), v0 + width), forward, baseOffset, rise, steps));

                if (k < flightCount - 1)
                {
                    int landingEnd = forward ? far + landing : far - landing;
                    landings.Add((new RectMm(Math.Min(far, landingEnd), v0, Math.Max(far, landingEnd), v0 + 2 * width), topOffset));
                }

                cursor = far;
                forward = !forward;
            }

            int minU = int.MaxValue, maxU = int.MinValue;
            foreach (var flight in flights)
            {
                minU = Math.Min(minU, flight.area.MinX);
                maxU = Math.Max(maxU, flight.area.MaxX);
            }

            foreach (var item in landings)
            {
                minU = Math.Min(minU, item.area.MinX);
                maxU = Math.Max(maxU, item.area.MaxX);
            }

            for (int i = 0; i < flights.Count; i++)
            {
                var f = flights[i];
                flights[i] = (f.area.Translate(-minU, 0), f.forward, f.baseOffset, f.rise, f.steps);
            }

            for (int i = 0; i < landings.Count; i++)
            {
                landings[i] = (landings[i].area.Translate(-minU, 0), landings[i].zOffset);
            }

            return new StairTemplate(flightCount, maxU - minU, flightCount * width, flights, landings);
        }
    }
}
