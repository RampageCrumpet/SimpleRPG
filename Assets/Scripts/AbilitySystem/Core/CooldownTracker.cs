using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Records when each definition (an ability, usually) may next be used, in ticks of an <see cref="IGameplayClock"/>.
    /// The server owns one per entity. A client's own cooldown display is only a prediction.
    /// </summary>
    public sealed class CooldownTracker
    {
        private readonly IGameplayClock clock;

        private readonly long toleranceTicks;

        private readonly Dictionary<DefinitionId, long> readyTicks = new Dictionary<DefinitionId, long>();

        /// <param name="clock">The clock cooldowns run on.</param>
        /// <param name="toleranceTicks">Ticks of slack allowed below the full cooldown, so network jitter between a client's press and the server's clock doesn't reject a use that was ready on the client.</param>
        public CooldownTracker(IGameplayClock clock, long toleranceTicks = 0)
        {
            this.clock = clock;
            this.toleranceTicks = toleranceTicks;
        }

        /// <summary>
        /// True if <paramref name="id"/> can be used now.
        /// </summary>
        public bool IsReady(DefinitionId id)
        {
            return !readyTicks.TryGetValue(id, out long ready) || clock.Tick >= ready - toleranceTicks;
        }

        /// <summary>
        /// Ticks until <paramref name="id"/> is ready, or zero if it already is.
        /// </summary>
        public long TicksRemaining(DefinitionId id)
        {
            return readyTicks.TryGetValue(id, out long ready) && ready > clock.Tick ? ready - clock.Tick : 0;
        }

        /// <summary>
        /// Starts the cooldown if <paramref name="id"/> is ready.
        /// </summary>
        /// <param name="id">What is being used.</param>
        /// <param name="cooldownSeconds">The cooldown to start. Zero or less leaves nothing to wait for.</param>
        /// <returns>True if the use is accepted, false if it is still cooling down.</returns>
        public bool TryStart(DefinitionId id, double cooldownSeconds)
        {
            if (!IsReady(id))
            {
                return false;
            }

            readyTicks[id] = clock.Tick + GameplayTime.SecondsToTicks(cooldownSeconds, clock.TickRate);
            return true;
        }
    }
}
