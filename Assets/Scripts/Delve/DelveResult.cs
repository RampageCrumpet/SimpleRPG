using System.Collections.Generic;

namespace SimpleRPG.Delve
{
    /// <summary>
    /// How a delve ended, handed to whatever rewards or records it. Items that were carried out are the survivors' own inventories at this moment;
    /// what the dead carried is in the loot bags they left behind and is not theirs any more.
    /// </summary>
    public sealed class DelveResult
    {
        public DelveResult(DelvePhase phase, double durationSeconds, IReadOnlyList<ulong> survivors, IReadOnlyList<ulong> dead)
        {
            Phase = phase;
            DurationSeconds = durationSeconds;
            Survivors = survivors;
            Dead = dead;
        }

        public DelvePhase Phase { get; }

        public double DurationSeconds { get; }

        /// <summary>The clients whose characters were alive at the end.</summary>
        public IReadOnlyList<ulong> Survivors { get; }

        /// <summary>The clients whose characters died during the delve.</summary>
        public IReadOnlyList<ulong> Dead { get; }
    }
}
