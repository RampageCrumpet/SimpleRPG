namespace SimpleRPG.Audio
{
    /// <summary>
    /// Source of randomness for sound variation. Separate from the gameplay stream on purpose: which clip is heard must never change what happens.
    /// </summary>
    public interface IAudioRandom
    {
        /// <summary>A value in [0, 1).</summary>
        float Value();
    }

    public sealed class SystemAudioRandom : IAudioRandom
    {
        private readonly System.Random random;

        public SystemAudioRandom(int seed) => random = new System.Random(seed);

        public SystemAudioRandom() => random = new System.Random();

        public float Value() => (float)random.NextDouble();
    }
}
