using SimpleRPG.Abilities;
using System;
using System.Collections.Generic;

namespace SimpleRPG.InventorySystem
{
    /// <summary>The range one rolled value is drawn from.</summary>
    public readonly struct AffixValueRange
    {
        public AffixValueRange(float minimum, float maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public float Minimum { get; }

        public float Maximum { get; }
    }

    /// <summary>
    /// One thing an affix does. An <see cref="AffixDefinition"/> has any number of these, each rolling its own values independently. The effect
    /// kinds are the extension point: a stat modifier is the first, and a granted ability or a triggered effect are further kinds that reuse the
    /// grant and ability machinery an item already has, with no change to the roller, the storage or the affix.
    /// Subclasses are serialized by reference, so adding a kind is adding a class. Like the affix they are immutable while the game runs.
    /// </summary>
    [Serializable]
    public abstract class AffixEffect
    {
        /// <summary>How many values this effect rolls. Zero for an effect with nothing to roll.</summary>
        public abstract int ValueCount { get; }

        /// <summary>The range of the value at <paramref name="valueIndex"/> (0 up to <see cref="ValueCount"/>).</summary>
        public abstract AffixValueRange RangeOf(int valueIndex);

        /// <summary>
        /// Adds what this effect does to the wearer at the rolled values. <paramref name="itemOwnGrants"/> is what the item's asset grants on its own,
        /// for effects that scale the item itself.
        /// </summary>
        public abstract void AddGrants(IReadOnlyList<GrantSpec> itemOwnGrants, IReadOnlyList<float> values, List<GrantSpec> into);

        /// <summary>The line a player reads at the rolled values, such as "+3 Strength".</summary>
        public abstract string Describe(IReadOnlyList<float> values);

        /// <summary>Why the effect is not correctly authored, or null.</summary>
        public abstract string Problem();

        /// <summary>
        /// Whether the effect changes anything in the game today. False for an effect nothing reads (see <see cref="SimpleRPG.Gameplay.AttributeConsumers"/>);
        /// <paramref name="reason"/> says why. An affix with such an effect must not be in a pool a player can roll from.
        /// </summary>
        public abstract bool IsLive(out string reason);
    }
}
