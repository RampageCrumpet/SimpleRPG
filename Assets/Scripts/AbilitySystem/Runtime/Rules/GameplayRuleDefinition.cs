using SimpleRPG.Gameplay;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What makes a rule run.
    /// </summary>
    public enum RuleTrigger : byte
    {
        /// <summary>The rule's owner landed a weapon blow on a target.</summary>
        WeaponHitConfirmed = 0,

        /// <summary>The rule's owner died. The ability starts where they died, and it runs even if the item is taken off during the death.</summary>
        OwnerDeath = 1,
    }

    /// <summary>
    /// A rule: when something happens to the item's wearer, maybe run an ability. A flaming sword that has a one in ten chance to cast Fireball
    /// when it hits is one rule. The ability is an ordinary ability, the same asset a player can cast; the rule only decides when it runs.
    /// Rules belong to items. They apply while the item is equipped and stop when it is taken off.
    /// </summary>
    [CreateAssetMenu(fileName = "Rule", menuName = "SimpleRPG/Gameplay Rule")]
    public class GameplayRuleDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("What makes this rule run.")]
        private RuleTrigger trigger = RuleTrigger.WeaponHitConfirmed;

        [SerializeField]
        [Tooltip("The kinds of damage that can trigger this rule. A weapon hit trigger only reacts to direct weapon hits, so an effect the rule causes never triggers it again.")]
        private DamageProvenance requiredProvenance = DamageProvenance.DirectWeaponHit;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("The chance the rule runs each time it is triggered: 0 never, 1 always. Rolled from the gameplay random stream.")]
        private float chance = 1f;

        [SerializeField]
        [Tooltip("The ability to run. It runs with the item as its source and the wearer as its instigator.")]
        private AbilityDefinition ability;

        public RuleTrigger Trigger => trigger;

        public DamageProvenance RequiredProvenance => requiredProvenance;

        public float Chance => chance;

        public AbilityDefinition Ability => ability;

        /// <summary>
        /// Sets the rule up in code. Used by tools and tests that build rules without the inspector.
        /// </summary>
        public void Configure(AbilityDefinition abilityToRun, float chanceToRun = 1f, RuleTrigger ruleTrigger = RuleTrigger.WeaponHitConfirmed, DamageProvenance provenance = DamageProvenance.DirectWeaponHit)
        {
            ability = abilityToRun;
            chance = Mathf.Clamp01(chanceToRun);
            trigger = ruleTrigger;
            requiredProvenance = provenance;
        }
    }
}
