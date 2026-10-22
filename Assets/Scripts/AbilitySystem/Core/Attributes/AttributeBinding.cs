namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Finds the attributes of a source. The game integration implements it over its characters and items.
    /// </summary>
    public interface IAttributeLookup
    {
        /// <summary>
        /// Gets the attributes of a source, if it has any.
        /// </summary>
        bool TryGetAttributes(SourceHandle source, out IAttributeReader attributes);
    }

    /// <summary>
    /// Where a bound value comes from.
    /// </summary>
    public enum AttributeBindingKind : byte
    {
        /// <summary>A fixed number. A brazier's damage is bound this way.</summary>
        Constant = 0,

        /// <summary>An attribute of the instigator, the entity credited with the execution (a wizard's or monster's own stat).</summary>
        Instigator = 1,

        /// <summary>An attribute of the source (a staff with its own fixed stats).</summary>
        Source = 2,
    }

    /// <summary>
    /// States where one formula input comes from, so the same ability reads a player's stat, a monster's stat, an item's fixed stat or a constant
    /// without knowing which it is. Combining several inputs is done by the expressions that consume them.
    /// </summary>
    public readonly struct AttributeBinding
    {
        public AttributeBinding(AttributeBindingKind kind, AttributeId attribute, double value)
        {
            Kind = kind;
            Attribute = attribute;
            Value = value;
        }

        public AttributeBindingKind Kind { get; }

        /// <summary>
        /// The attribute to read, for <see cref="AttributeBindingKind.Instigator"/> and <see cref="AttributeBindingKind.Source"/>.
        /// </summary>
        public AttributeId Attribute { get; }

        /// <summary>
        /// The number for a constant binding, or the fallback used when the instigator or source has no attributes (for example a hazard with no instigator).
        /// </summary>
        public double Value { get; }

        public static AttributeBinding Constant(double value) => new AttributeBinding(AttributeBindingKind.Constant, default, value);

        public static AttributeBinding FromInstigator(AttributeId attribute, double fallback = 0d) => new AttributeBinding(AttributeBindingKind.Instigator, attribute, fallback);

        public static AttributeBinding FromSource(AttributeId attribute, double fallback = 0d) => new AttributeBinding(AttributeBindingKind.Source, attribute, fallback);

        /// <summary>
        /// Reads the bound value.
        /// </summary>
        /// <param name="lookup">Finds attributes for the instigator and the source.</param>
        /// <param name="source">The execution's source.</param>
        /// <param name="instigator">The execution's instigator, if there is one.</param>
        /// <param name="value">The value, or the fallback if the instigator or source has no attributes.</param>
        /// <returns>True if the value was read from an attribute or is a constant; false if the fallback stood in for something missing.</returns>
        public bool TryResolve(IAttributeLookup lookup, SourceHandle source, EntityId instigator, out double value)
        {
            value = Value;

            switch (Kind)
            {
                case AttributeBindingKind.Constant:
                    return true;

                case AttributeBindingKind.Instigator:
                    return instigator.IsValid && Read(lookup, SourceHandle.FromEntity(instigator), out value);

                case AttributeBindingKind.Source:
                    return Read(lookup, source, out value);

                default:
                    return false;
            }
        }

        private bool Read(IAttributeLookup lookup, SourceHandle from, out double value)
        {
            if (lookup != null && lookup.TryGetAttributes(from, out IAttributeReader attributes) && attributes != null)
            {
                value = attributes.Get(Attribute);
                return true;
            }

            value = Value;
            return false;
        }
    }
}
