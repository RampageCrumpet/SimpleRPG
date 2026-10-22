using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The authoritative time source for gameplay rules: cooldowns, status expiry, delays and scheduled ticks.
    /// Gameplay code reads time from here, never from Time.time, so timing is the same under test and on the network.
    /// </summary>
    public interface IGameplayClock
    {
        /// <summary>
        /// The current simulation tick.
        /// </summary>
        long Tick { get; }

        /// <summary>
        /// Simulation ticks per second.
        /// </summary>
        int TickRate { get; }

        /// <summary>
        /// Time in seconds on this clock. Presentation and animation timing read this; rules that must agree exactly use <see cref="Tick"/>.
        /// </summary>
        double ElapsedSeconds { get; }
    }

    /// <summary>
    /// Conversions between seconds and ticks.
    /// </summary>
    public static class GameplayTime
    {
        /// <summary>
        /// Converts a duration to ticks, rounding up so a timed effect never ends earlier than its authored duration.
        /// A positive duration is at least one tick; zero or negative is zero.
        /// </summary>
        public static long SecondsToTicks(double seconds, int tickRate)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate), "The tick rate must be positive.");
            if (seconds <= 0) return 0;

            // The small tolerance stops binary error rounding an exact multiple up a tick. Authored durations are single-precision floats, so
            // 0.1f is really 0.10000000149 and 0.1f * 30 comes out a hair above 3; that is still three ticks, not four.
            return Math.Max(1, (long)Math.Ceiling(seconds * tickRate - 1e-4));
        }

        /// <summary>
        /// Converts ticks to seconds.
        /// </summary>
        public static double TicksToSeconds(long ticks, int tickRate)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate), "The tick rate must be positive.");

            return (double)ticks / tickRate;
        }
    }
}
