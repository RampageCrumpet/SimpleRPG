using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// When something happens to an entity, and every one of its conditions holds, maybe run an ability. A flaming sword that has a one in ten
    /// chance to cast Fireball when it hits is one triggered effect; so is a sword that does a great deal of extra damage on a killing blow to the
    /// head, and another, separate one for a blow to the limbs. The ability is an ordinary ability, the same asset a player can cast; the effect only
    /// decides when it runs, and at whom.
    /// <para>
    /// An effect belongs to no kind of thing. Whatever brings it (a worn item, an affix an item rolled, a status a spell applied) is its host, and
    /// it is active for as long as its host is (see <see cref="TriggeredEffectEngine"/>).
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "TriggeredEffect", menuName = "SimpleRPG/Triggered Effect")]
    public class TriggeredEffect : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("What makes this effect run.")]
        private EffectTrigger trigger = EffectTrigger.OnHit;

        [SerializeField]
        [Tooltip("Whose actions can trigger it: anything the owner does, or only what its own host did (the sword that carries it).")]
        private EffectScope scope = EffectScope.Owner;

        [SerializeReference]
        [Tooltip("Every one of these must hold for the effect to run. Empty: it always runs.")]
        private List<EffectCondition> conditions = new List<EffectCondition>();

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The chance the effect runs each time it is triggered and its conditions hold: 0 never, 1 always. Rolled from the gameplay random stream.")]
        private float chance = 1f;

        [SerializeField]
        [Tooltip("Who the ability is aimed at: the owner, or the other party of the trigger (the one struck).")]
        private EffectTarget target = EffectTarget.Other;

        [SerializeField]
        [Tooltip("The ability to run. It runs with the host as its source and the owner as its instigator.")]
        private AbilityDefinition ability;

        [SerializeField]
        [TextArea(1, 3)]
        [Tooltip("What the player reads where this effect is shown, for example 'Chance to cause bleeding on hit'. Empty: it is not shown.")]
        private string description = "";

        public EffectTrigger Trigger => trigger;

        public EffectScope Scope => scope;

        public IReadOnlyList<EffectCondition> Conditions => conditions;

        public float Chance => chance;

        public EffectTarget Target => target;

        public AbilityDefinition Ability => ability;

        public string Description => description;

        /// <summary>
        /// True if a trigger that happened to <paramref name="host"/>'s owner is one this effect reacts to: the right trigger, the right actor, and
        /// every condition passing. Does not roll the chance.
        /// </summary>
        public bool Matches(in TriggerContext context, SourceHandle host)
        {
            if (context.Trigger != trigger)
            {
                return false;
            }

            if (scope == EffectScope.Host && context.Source != host)
            {
                return false;
            }

            foreach (EffectCondition condition in conditions)
            {
                if (condition != null && !condition.Passes(context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Why this effect is not correctly authored, or null.</summary>
        public string Problem()
        {
            if (ability == null) return name + " has no ability to run.";
            foreach (EffectCondition condition in conditions)
            {
                string problem = condition?.Problem();
                if (problem != null) return name + ": " + problem + ".";
            }

            return null;
        }

        /// <summary>
        /// Sets the effect up in code. Used by tools and tests that build effects without the inspector.
        /// </summary>
        public void Configure(AbilityDefinition abilityToRun, float chanceToRun = 1f, EffectTrigger effectTrigger = EffectTrigger.OnHit,
            EffectScope effectScope = EffectScope.Owner, EffectTarget effectTarget = EffectTarget.Other, params EffectCondition[] effectConditions)
        {
            ability = abilityToRun;
            chance = Mathf.Clamp01(chanceToRun);
            trigger = effectTrigger;
            scope = effectScope;
            target = effectTarget;
            conditions = new List<EffectCondition>(effectConditions ?? new EffectCondition[0]);
        }
    }
}
