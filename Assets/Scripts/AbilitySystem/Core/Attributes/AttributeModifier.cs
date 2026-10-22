using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Where in the calculation a modifier applies. An attribute's value is <c>(base + sum of Add) * (1 + sum of PercentAdd) * product of Multiply</c>.
    /// </summary>
    public enum ModifierLayer : byte
    {
        /// <summary>A flat amount added to the base.</summary>
        Add = 0,

        /// <summary>A fraction added to a shared percentage bonus: 0.1 is +10%, and several of them add together before applying.</summary>
        PercentAdd = 1,

        /// <summary>A factor multiplied in last: 1.5 is x1.5, and several of them multiply together.</summary>
        Multiply = 2,
    }

    /// <summary>
    /// One contribution to an attribute: what it changes, how, by how much, and which source supplied it.
    /// </summary>
    public readonly struct AttributeModifier
    {
        public AttributeModifier(AttributeId attribute, ModifierLayer layer, double value, SourceHandle source)
        {
            Attribute = attribute;
            Layer = layer;
            Value = value;
            Source = source;
        }

        public AttributeId Attribute { get; }

        public ModifierLayer Layer { get; }

        public double Value { get; }

        /// <summary>
        /// The provider of this contribution, such as the equipped item. Used to withdraw everything one source supplied.
        /// </summary>
        public SourceHandle Source { get; }

        public static AttributeModifier Flat(AttributeId attribute, double amount, SourceHandle source = default) =>
            new AttributeModifier(attribute, ModifierLayer.Add, amount, source);

        public static AttributeModifier Percent(AttributeId attribute, double fraction, SourceHandle source = default) =>
            new AttributeModifier(attribute, ModifierLayer.PercentAdd, fraction, source);

        public static AttributeModifier Factor(AttributeId attribute, double factor, SourceHandle source = default) =>
            new AttributeModifier(attribute, ModifierLayer.Multiply, factor, source);
    }

    /// <summary>
    /// Identifies one added modifier so exactly that contribution can be removed. Zero is not a handle.
    /// </summary>
    public readonly struct ModifierHandle : IEquatable<ModifierHandle>
    {
        public ModifierHandle(uint id)
        {
            Id = id;
        }

        public uint Id { get; }

        public bool IsValid => Id != 0;

        public bool Equals(ModifierHandle other) => Id == other.Id;

        public override bool Equals(object obj) => obj is ModifierHandle other && Equals(other);

        public override int GetHashCode() => Id.GetHashCode();

        public override string ToString() => "Modifier#" + Id;

        public static bool operator ==(ModifierHandle a, ModifierHandle b) => a.Equals(b);

        public static bool operator !=(ModifierHandle a, ModifierHandle b) => !a.Equals(b);
    }
}
