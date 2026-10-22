namespace SimpleRPG.Abilities
{
    /// <summary>
    /// The timing of one combo step and nothing else, in seconds from the step's start. This is all the combo logic needs, so the logic
    /// never touches animation clips or other engine types.
    /// </summary>
    public readonly struct ComboStepTiming
    {
        public ComboStepTiming(float duration, float hitStart, float hitEnd, float chainOpen, float chainClose)
        {
            Duration = duration;
            HitStart = hitStart;
            HitEnd = hitEnd;
            ChainOpen = chainOpen;
            ChainClose = chainClose;
        }

        public float Duration { get; }

        public float HitStart { get; }

        public float HitEnd { get; }

        public float ChainOpen { get; }

        public float ChainClose { get; }

        /// <summary>
        /// True if a press can chain out of this step at all. The last step of a combo has no chain window.
        /// </summary>
        public bool HasChainWindow => ChainClose > ChainOpen;

        /// <summary>
        /// Checks whether a press this long after the step started chains into the next step. The window includes its opening time
        /// and excludes its closing time, so two windows that meet never both accept the same instant.
        /// </summary>
        /// <param name="elapsed">Seconds since the step started.</param>
        public bool IsInChainWindow(double elapsed)
        {
            return HasChainWindow && elapsed >= ChainOpen && elapsed < ChainClose;
        }
    }
}
