using SimpleRPG.Abilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// An affix effect that gives its item a <see cref="TriggeredEffect"/>: "on a killing blow to the head, deal extra damage", "on hit, shred armor".
    /// What it does, when and to whom is all in the triggered effect asset, which any item, spell or affix can use; this only attaches it. It
    /// rolls nothing yet, so every instance of the affix has the same behavior. An affix with several of these has several independent reactions.
    /// </summary>
    [Serializable]
    public sealed class TriggeredAffixEffect : AffixEffect
    {
        [SerializeField]
        [Tooltip("The triggered effect the item has while it is worn.")]
        private TriggeredEffect effect;

        public TriggeredAffixEffect()
        {
        }

        public TriggeredAffixEffect(TriggeredEffect effect) => this.effect = effect;

        public TriggeredEffect Effect => effect;

        public override int ValueCount => 0;

        public override AffixValueRange RangeOf(int valueIndex) => throw new ArgumentOutOfRangeException(nameof(valueIndex));

        public override void AddGrants(IReadOnlyList<SimpleRPG.Abilities.GrantSpec> itemOwnGrants, IReadOnlyList<float> values, List<SimpleRPG.Abilities.GrantSpec> into)
        {
        }

        public override void AddTriggeredEffects(List<TriggeredEffect> into)
        {
            if (effect != null) into.Add(effect);
        }

        public override string Describe(IReadOnlyList<float> values) => effect != null ? effect.Description : null;

        public override string Problem()
        {
            if (effect == null) return "a triggered effect is missing";
            return effect.Problem();
        }

        public override bool IsLive(out string reason)
        {
            // A trigger exists only once the game raises the event behind it, so any trigger the enum lists works; what can be wrong is the ability.
            reason = null;
            if (effect == null || effect.Ability == null)
            {
                reason = "it has no ability to run";
                return false;
            }

            return true;
        }
    }
}
