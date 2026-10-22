using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// A clock that only moves when told to. It is the clock for tests and for any simulation that steps itself.
    /// </summary>
    public sealed class ManualGameplayClock : IGameplayClock
    {
        public ManualGameplayClock(int tickRate = 30, long startTick = 0)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate), "The tick rate must be positive.");

            TickRate = tickRate;
            Tick = startTick;
        }

        /// <inheritdoc/>
        public long Tick { get; private set; }

        /// <inheritdoc/>
        public int TickRate { get; }

        /// <inheritdoc/>
        public double ElapsedSeconds => GameplayTime.TicksToSeconds(Tick, TickRate);

        /// <summary>
        /// Moves the clock forward. Time never runs backwards.
        /// </summary>
        public void Advance(long ticks)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), "A clock cannot go backwards.");

            Tick += ticks;
        }

        /// <summary>
        /// Moves the clock forward by a duration, rounded up to whole ticks.
        /// </summary>
        public void AdvanceSeconds(double seconds)
        {
            Advance(GameplayTime.SecondsToTicks(seconds, TickRate));
        }
    }
}
