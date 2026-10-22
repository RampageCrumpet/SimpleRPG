namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The random numbers gameplay rules use. Gameplay never uses the engine's random, so a fixed seed replays exactly the same rolls in a test.
    /// </summary>
    public interface IGameplayRandom
    {
        /// <summary>
        /// The next number from 0 up to, but not including, 1.
        /// </summary>
        double NextDouble();
    }

    /// <summary>
    /// A deterministic random stream (SplitMix64). The same seed always gives the same sequence on every platform.
    /// </summary>
    public sealed class SeededRandom : IGameplayRandom
    {
        private ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed;
        }

        public SeededRandom(int seed) : this(unchecked((ulong)(uint)seed))
        {
        }

        /// <inheritdoc/>
        public double NextDouble()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;

                // The top 53 bits fill a double's mantissa exactly.
                return (z >> 11) * (1.0 / 9007199254740992.0);
            }
        }
    }
}
