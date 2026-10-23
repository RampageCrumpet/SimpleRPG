using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// A stable reference to an attribute (Strength, MagicPower and so on). Abilities refer to attributes by this key rather than by any character class.
    /// </summary>
    public readonly struct AttributeId : IEquatable<AttributeId>
    {
        public AttributeId(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An attribute needs a key.", nameof(key));

            // Content saved before Wisdom became Willpower still names the old key; it is the same attribute.
            Key = key == PrimaryAttributes.LegacyWisdomKey ? PrimaryAttributes.WillpowerKey : key;
        }

        /// <summary>
        /// The stable key, for example "simplerpg.attr.strength". Null for the default (no attribute) value.
        /// </summary>
        public string Key { get; }

        public bool IsNone => Key == null;

        public bool Equals(AttributeId other) => string.Equals(Key, other.Key, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AttributeId other && Equals(other);

        public override int GetHashCode() => Key == null ? 0 : StringComparer.Ordinal.GetHashCode(Key);

        public override string ToString() => Key ?? "(no attribute)";

        public static bool operator ==(AttributeId a, AttributeId b) => a.Equals(b);

        public static bool operator !=(AttributeId a, AttributeId b) => !a.Equals(b);
    }

    /// <summary>
    /// The six primary attributes. Derived attributes (health, magic power and so on) are registered on an <see cref="AttributeSchema"/>.
    /// </summary>
    public static class PrimaryAttributes
    {
        public const string WillpowerKey = "simplerpg.attr.willpower";

        /// <summary>
        /// The key Willpower had when it was called Wisdom. <see cref="AttributeId"/> maps it to Willpower so saved content keeps working.
        /// </summary>
        public const string LegacyWisdomKey = "simplerpg.attr.wisdom";

        public static readonly AttributeId Strength = new AttributeId("simplerpg.attr.strength");
        public static readonly AttributeId Constitution = new AttributeId("simplerpg.attr.constitution");
        public static readonly AttributeId Dexterity = new AttributeId("simplerpg.attr.dexterity");
        public static readonly AttributeId Agility = new AttributeId("simplerpg.attr.agility");
        public static readonly AttributeId Willpower = new AttributeId(WillpowerKey);
        public static readonly AttributeId Intelligence = new AttributeId("simplerpg.attr.intelligence");

        /// <summary>
        /// All six, in a fixed order.
        /// </summary>
        public static readonly AttributeId[] All = { Strength, Constitution, Dexterity, Agility, Willpower, Intelligence };
    }
}
