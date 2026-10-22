using System;
using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The attributes of one entity (or one item with fixed stats): base values plus the modifiers currently applied to them.
    /// Values are always computed from the current modifiers, so removing a contribution leaves exactly what the others supply and nothing drifts.
    /// </summary>
    public sealed class AttributeSet : IAttributeReader
    {
        private readonly AttributeSchema schema;

        private readonly Dictionary<AttributeId, double> baseValues = new Dictionary<AttributeId, double>();

        private readonly List<Entry> modifiers = new List<Entry>();

        private readonly Dictionary<AttributeId, double> cache = new Dictionary<AttributeId, double>();

        private readonly HashSet<AttributeId> evaluating = new HashSet<AttributeId>();

        private uint lastHandle;

        public AttributeSet(AttributeSchema schema = null)
        {
            this.schema = schema ?? new AttributeSchema();
        }

        /// <summary>
        /// Increases whenever any base value or modifier changes, so a holder of a computed value can tell it may be stale.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Sets the base value of an attribute. For a derived attribute the base is ignored; its formula supplies the starting value.
        /// </summary>
        public void SetBase(AttributeId attribute, double value)
        {
            baseValues[attribute] = value;
            Changed();
        }

        /// <summary>
        /// Gets the base value of an attribute before any modifier, or zero if none was set.
        /// </summary>
        public double GetBase(AttributeId attribute) => baseValues.TryGetValue(attribute, out double value) ? value : 0d;

        /// <summary>
        /// Adds a contribution and returns the handle that removes exactly it.
        /// </summary>
        public ModifierHandle AddModifier(AttributeModifier modifier)
        {
            if (modifier.Attribute.IsNone) throw new ArgumentException("A modifier needs an attribute.", nameof(modifier));
            if (double.IsNaN(modifier.Value) || double.IsInfinity(modifier.Value)) throw new ArgumentException("A modifier value must be a finite number.", nameof(modifier));

            ModifierHandle handle = new ModifierHandle(++lastHandle);
            modifiers.Add(new Entry(handle, modifier));
            Changed();
            return handle;
        }

        /// <summary>
        /// Removes one contribution. Other modifiers, including identical ones from other sources, are untouched.
        /// </summary>
        /// <returns>True if the handle was still active.</returns>
        public bool RemoveModifier(ModifierHandle handle)
        {
            int index = modifiers.FindIndex(x => x.Handle == handle);
            if (index < 0) return false;

            modifiers.RemoveAt(index);
            Changed();
            return true;
        }

        /// <summary>
        /// Removes every contribution supplied by a source, for example when an item is unequipped.
        /// </summary>
        /// <returns>How many modifiers were removed.</returns>
        public int RemoveAllFromSource(SourceHandle source)
        {
            int removed = modifiers.RemoveAll(x => x.Modifier.Source == source);
            if (removed > 0) Changed();
            return removed;
        }

        /// <summary>
        /// The number of modifiers currently applied.
        /// </summary>
        public int ModifierCount => modifiers.Count;

        /// <inheritdoc/>
        /// <remarks>
        /// A result that is not a finite number (a formula that divides by zero) reads as zero rather than spreading NaN through damage.
        /// A derived attribute whose formula reads itself, directly or through others, is a definition error and throws.
        /// </remarks>
        public double Get(AttributeId attribute)
        {
            if (attribute.IsNone) return 0d;
            if (cache.TryGetValue(attribute, out double cached)) return cached;

            if (!evaluating.Add(attribute))
            {
                throw new InvalidOperationException("The derived attribute " + attribute + " depends on itself.");
            }

            double result;
            try
            {
                result = Evaluate(attribute);
            }
            finally
            {
                evaluating.Remove(attribute);
            }

            if (double.IsNaN(result) || double.IsInfinity(result)) result = 0d;

            cache[attribute] = result;
            return result;
        }

        private double Evaluate(AttributeId attribute)
        {
            double start = schema.TryGetFormula(attribute, out Func<IAttributeReader, double> formula) ? formula(this) : GetBase(attribute);

            double add = 0d;
            double percent = 0d;
            double factor = 1d;
            foreach (Entry entry in modifiers)
            {
                if (entry.Modifier.Attribute != attribute) continue;

                switch (entry.Modifier.Layer)
                {
                    case ModifierLayer.Add: add += entry.Modifier.Value; break;
                    case ModifierLayer.PercentAdd: percent += entry.Modifier.Value; break;
                    case ModifierLayer.Multiply: factor *= entry.Modifier.Value; break;
                }
            }

            return (start + add) * (1d + percent) * factor;
        }

        private void Changed()
        {
            cache.Clear();
            Version++;
        }

        private readonly struct Entry
        {
            public Entry(ModifierHandle handle, AttributeModifier modifier)
            {
                Handle = handle;
                Modifier = modifier;
            }

            public ModifierHandle Handle { get; }

            public AttributeModifier Modifier { get; }
        }
    }
}
