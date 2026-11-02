using System;

namespace SimpleRPG.Delve
{
    /// <summary>
    /// The countdown to getting out. The party moves as one: it only runs while every living player is inside the extraction zone, and it starts
    /// over if anyone steps out or the last of them dies. Plain code with no engine types so the rule can be tested exactly.
    /// </summary>
    public sealed class ExtractionChannel
    {
        private float elapsed;

        public ExtractionChannel(float seconds)
        {
            if (seconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Extraction needs a positive channel time.");
            }

            Seconds = seconds;
        }

        /// <summary>How long the whole party has to stay in the zone.</summary>
        public float Seconds { get; }

        /// <summary>How far the countdown has got, from 0 to 1.</summary>
        public float Progress => Math.Min(elapsed / Seconds, 1f);

        public bool IsComplete => elapsed >= Seconds;

        /// <summary>Moves the countdown on by <paramref name="deltaSeconds"/> if the whole living party is in the zone, otherwise starts it over. A finished channel stays finished.</summary>
        /// <param name="livingPlayers">How many players are alive.</param>
        /// <param name="livingPlayersInZone">How many of those are inside the zone.</param>
        public void Step(float deltaSeconds, int livingPlayers, int livingPlayersInZone)
        {
            if (IsComplete)
            {
                return;
            }

            bool everyoneIn = livingPlayers > 0 && livingPlayersInZone >= livingPlayers;
            elapsed = everyoneIn ? Math.Min(elapsed + Math.Max(deltaSeconds, 0f), Seconds) : 0f;
        }

        public void Reset() => elapsed = 0f;
    }
}
