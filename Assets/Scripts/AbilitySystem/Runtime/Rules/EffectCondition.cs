using System;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// A test a <see cref="TriggeredEffect"/> makes of a trigger before it runs; all of an effect's conditions must pass. A new kind of test is a new
    /// subclass, serialized by reference, with no change to the engine. Conditions are immutable data and read only the <see cref="TriggerContext"/>.
    /// </summary>
    [Serializable]
    public abstract class EffectCondition
    {
        /// <summary>True if the trigger meets the condition.</summary>
        public abstract bool Passes(in TriggerContext context);

        /// <summary>Why the condition is not correctly authored, or null.</summary>
        public virtual string Problem() => null;
    }

    /// <summary>Passes when the blow did (or did not) kill.</summary>
    [Serializable]
    public sealed class FatalCondition : EffectCondition
    {
        [SerializeField]
        [Tooltip("True: only a blow that kills. False: only a blow that does not.")]
        private bool fatal = true;

        public FatalCondition()
        {
        }

        public FatalCondition(bool fatal) => this.fatal = fatal;

        public override bool Passes(in TriggerContext context) => context.Fatal == fatal;
    }
}
