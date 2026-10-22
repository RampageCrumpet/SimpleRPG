using System;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// One attack in a <see cref="ComboDefinition"/>. All times are seconds from the moment the step starts.
    /// A step is plain data: the combo logic and the animation both read it, so nothing else restates its timing.
    /// </summary>
    [Serializable]
    public sealed class ComboStep
    {
        [Tooltip("The animator state the presentation plays for this step.")]
        [SerializeField] private string state;

        [Tooltip("The clip that state plays. Used to check Duration; the animator still plays the state.")]
        [SerializeField] private AnimationClip clip;

        [Tooltip("Full length of the step, windup to the end of recovery. The step ends here unless it is chained.")]
        [SerializeField, Min(0)] private float duration;

        [Tooltip("When the weapon hitbox becomes live.")]
        [SerializeField, Min(0)] private float hitStart;

        [Tooltip("When the weapon hitbox stops being live.")]
        [SerializeField, Min(0)] private float hitEnd;

        [Tooltip("When a press starts chaining into the next step. Must not be before Hit End. Leave both chain times at zero on the last step.")]
        [SerializeField, Min(0)] private float chainOpen;

        [Tooltip("When a press stops chaining into the next step.")]
        [SerializeField, Min(0)] private float chainClose;

        [Tooltip("Crossfade length when this step is entered, from locomotion or from the previous step.")]
        [SerializeField, Min(0)] private float blendIn;

        [Tooltip("Seconds of this step's clip skipped when it is entered, to hide a pose mismatch with what came before.")]
        [SerializeField, Min(0)] private float startOffset;

        [Tooltip("Multiplies the weapon's damage for this step.")]
        [SerializeField, Min(0)] private float damageScalar = 1f;

        public ComboStep(
            string state,
            AnimationClip clip,
            float duration,
            float hitStart,
            float hitEnd,
            float chainOpen = 0f,
            float chainClose = 0f,
            float blendIn = 0f,
            float startOffset = 0f,
            float damageScalar = 1f)
        {
            this.state = state;
            this.clip = clip;
            this.duration = duration;
            this.hitStart = hitStart;
            this.hitEnd = hitEnd;
            this.chainOpen = chainOpen;
            this.chainClose = chainClose;
            this.blendIn = blendIn;
            this.startOffset = startOffset;
            this.damageScalar = damageScalar;
        }

        public string State => state;

        public AnimationClip Clip => clip;

        public float Duration => duration;

        public float HitStart => hitStart;

        public float HitEnd => hitEnd;

        public float ChainOpen => chainOpen;

        public float ChainClose => chainClose;

        public float BlendIn => blendIn;

        public float StartOffset => startOffset;

        public float DamageScalar => damageScalar;

        /// <summary>
        /// The step's timing alone, which is what the combo logic runs on.
        /// </summary>
        public ComboStepTiming Timing => new ComboStepTiming(duration, hitStart, hitEnd, chainOpen, chainClose);

        /// <inheritdoc cref="ComboStepTiming.HasChainWindow"/>
        public bool HasChainWindow => Timing.HasChainWindow;

        /// <inheritdoc cref="ComboStepTiming.IsInChainWindow"/>
        public bool IsInChainWindow(float elapsed)
        {
            return Timing.IsInChainWindow(elapsed);
        }
    }
}
