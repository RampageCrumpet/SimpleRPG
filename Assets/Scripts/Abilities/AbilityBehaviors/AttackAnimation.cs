namespace SimpleRPG.Abilities
{
    /// <summary>
    /// How one attack is shown. Melee mechanics describe it; the character's animation adapter plays it, so gameplay never touches an animator.
    /// An attack is either started by an animator trigger, or played directly as a named state with a crossfade.
    /// </summary>
    public readonly struct AttackAnimation
    {
        private AttackAnimation(string trigger, string state, float blendIn, float startOffset)
        {
            Trigger = trigger;
            State = state;
            BlendIn = blendIn;
            StartOffset = startOffset;
        }

        /// <summary>The animator trigger that starts the attack, or null if it is played as a state.</summary>
        public string Trigger { get; }

        /// <summary>The animator state to play, or null if the attack is started by a trigger.</summary>
        public string State { get; }

        /// <summary>Seconds to crossfade into the state.</summary>
        public float BlendIn { get; }

        /// <summary>Seconds of the state's clip to skip when it starts.</summary>
        public float StartOffset { get; }

        public bool IsState => !string.IsNullOrEmpty(State);

        public static AttackAnimation FromTrigger(string trigger)
        {
            return new AttackAnimation(trigger, null, 0f, 0f);
        }

        public static AttackAnimation FromState(string state, float blendIn, float startOffset)
        {
            return new AttackAnimation(null, state, blendIn, startOffset);
        }
    }
}
