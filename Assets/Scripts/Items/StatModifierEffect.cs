using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>Whose value a percentage modifier scales.</summary>
    public enum AffixScope
    {
        /// <summary>The wearer's whole attribute, like any other modifier.</summary>
        Wearer,

        /// <summary>Only what this item itself grants of the attribute: "+10% armor" on a cuirass is 10% of that cuirass's armor.</summary>
        ThisItem,
    }

    /// <summary>
    /// An effect that changes one attribute of the wearer by one rolled value: a flat amount, a percentage, or a final factor. With
    /// <see cref="AffixScope.ThisItem"/> a percentage is of the item's own grants of that attribute and becomes a flat amount, so it does not
    /// scale armor worn elsewhere.
    /// </summary>
    [Serializable]
    public sealed class StatModifierEffect : AffixEffect
    {
        [SerializeField]
        [Tooltip("What the player reads, for example 'Strength'. The amount is put in front: '+3 Strength', '+10% Strength'.")]
        private string label = "";

        [SerializeField]
        [Tooltip("The attribute it changes, by stable key, for example simplerpg.attr.strength.")]
        private string attributeKey = "";

        [SerializeField]
        [Tooltip("Where the change applies: a flat amount, a percentage bonus (0.1 is +10%), or a final factor.")]
        private ModifierLayer layer = ModifierLayer.Add;

        [SerializeField]
        [Tooltip("Wearer: the wearer's attribute. This Item: a percentage of what this item itself grants of the attribute (percentage only).")]
        private AffixScope scope = AffixScope.Wearer;

        [SerializeField]
        [Tooltip("The lowest value it can roll.")]
        private float minimum = 1f;

        [SerializeField]
        [Tooltip("The highest value it can roll.")]
        private float maximum = 3f;

        public StatModifierEffect()
        {
        }

        public StatModifierEffect(string label, string attributeKey, ModifierLayer layer, float minimum, float maximum, AffixScope scope = AffixScope.Wearer)
        {
            this.label = label;
            this.attributeKey = attributeKey;
            this.layer = layer;
            this.minimum = minimum;
            this.maximum = maximum;
            this.scope = scope;
        }

        public string Label => label;

        public string AttributeKey => attributeKey;

        public ModifierLayer Layer => layer;

        public AffixScope Scope => scope;

        public override int ValueCount => 1;

        public override AffixValueRange RangeOf(int valueIndex) => new AffixValueRange(minimum, maximum);

        public override void AddGrants(IReadOnlyList<GrantSpec> itemOwnGrants, IReadOnlyList<float> values, List<GrantSpec> into)
        {
            float value = values[0];
            if (scope == AffixScope.Wearer)
            {
                into.Add(new GrantSpec { AttributeKey = attributeKey, Layer = layer, Value = value });
                return;
            }

            // A percentage of the item's own flat grants of this attribute, as the flat amount it comes to.
            float own = 0f;
            foreach (GrantSpec grant in itemOwnGrants)
            {
                if (grant.Layer == ModifierLayer.Add && !string.IsNullOrEmpty(grant.AttributeKey) && new AttributeId(grant.AttributeKey) == new AttributeId(attributeKey)) own += grant.Value;
            }

            if (own != 0f) into.Add(new GrantSpec { AttributeKey = attributeKey, Layer = ModifierLayer.Add, Value = own * value });
        }

        public override string Describe(IReadOnlyList<float> values)
        {
            string amount = ModifierText.Amount(layer, values[0]);
            return amount + " " + label + (scope == AffixScope.ThisItem ? " (this item)" : "");
        }

        public override string Problem()
        {
            if (string.IsNullOrWhiteSpace(attributeKey)) return "a stat effect has no attribute key, so it changes nothing";
            if (maximum < minimum) return "'" + label + "' has a maximum below its minimum";
            if (scope == AffixScope.ThisItem && layer != ModifierLayer.PercentAdd) return "'" + label + "' is scoped to this item, which only a percentage can be";
            return null;
        }

        public override bool IsLive(out string reason)
        {
            reason = null;
            if (AttributeConsumers.IsRead(attributeKey)) return true;

            reason = "nothing in the game reads '" + attributeKey + "' yet (see AttributeConsumers)";
            return false;
        }
    }
}
