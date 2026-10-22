using System;
using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Reads attribute values.
    /// </summary>
    public interface IAttributeReader
    {
        /// <summary>
        /// Gets the current value of an attribute, including every active modifier. Unknown attributes read as zero.
        /// </summary>
        double Get(AttributeId attribute);
    }

    /// <summary>
    /// The set of derived attributes and how each is computed from other attributes. Registering a new derived attribute needs no change to anything else.
    /// A schema is built once and shared by every <see cref="AttributeSet"/> that uses it.
    /// </summary>
    public sealed class AttributeSchema
    {
        private readonly Dictionary<AttributeId, Func<IAttributeReader, double>> derived = new Dictionary<AttributeId, Func<IAttributeReader, double>>();

        /// <summary>
        /// Registers a derived attribute. The formula reads other attributes, primary or derived, through the reader it is given.
        /// The formula's result is then modified like any other attribute.
        /// </summary>
        /// <returns>This schema, so registrations can be chained.</returns>
        public AttributeSchema Define(AttributeId attribute, Func<IAttributeReader, double> formula)
        {
            if (attribute.IsNone) throw new ArgumentException("A derived attribute needs an ID.", nameof(attribute));
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            if (Array.IndexOf(PrimaryAttributes.All, attribute) >= 0) throw new ArgumentException(attribute + " is a primary attribute and cannot be derived.", nameof(attribute));
            if (derived.ContainsKey(attribute)) throw new ArgumentException(attribute + " is already defined.", nameof(attribute));

            derived.Add(attribute, formula);
            return this;
        }

        /// <summary>
        /// True if the attribute is computed from other attributes.
        /// </summary>
        public bool IsDerived(AttributeId attribute) => derived.ContainsKey(attribute);

        internal bool TryGetFormula(AttributeId attribute, out Func<IAttributeReader, double> formula) => derived.TryGetValue(attribute, out formula);
    }
}
