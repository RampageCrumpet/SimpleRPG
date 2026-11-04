using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Which attributes some gameplay system actually reads today. Cataloguing a stat does not make anything read it, so a modifier on an attribute
    /// outside this list changes a number nobody uses. Content that would only look good (an affix on such an attribute) must stay out of anything
    /// a player can find; the affix definition check uses this.
    /// <para>
    /// Add an attribute here in the same change that makes a system read it, and remove it when its last reader goes. Constitution, Agility and
    /// Willpower count because the player profile derives Maximum Health, Movement Speed and the damage reductions from them. Strength and
    /// Physical Power are <b>not</b> read yet: melee damage is the weapon's damage times the combo scalar and nothing more, until the weapon damage
    /// calculation reads Physical Power. Dexterity, Intelligence (it feeds Magical Power, which no spell reads), Magical Power, Attack, Casting,
    /// Interaction and Equip Speed, Carrying Capacity and Spell Preparation Capacity are read by nothing yet.
    /// </para>
    /// </summary>
    public static class AttributeConsumers
    {
        private static readonly HashSet<string> Read = new HashSet<string>
        {
            PrimaryAttributes.Constitution.Key,
            PrimaryAttributes.Agility.Key,
            PrimaryAttributes.Willpower.Key,
            DerivedStats.MaximumHealth.Key,
            DerivedStats.PhysicalDamageReduction.Key,
            DerivedStats.MagicalDamageReduction.Key,
            DerivedStats.MovementSpeed.Key,
            DerivedStats.ArmorRating.Key,
        };

        /// <summary>True if a gameplay system reads the attribute with this key.</summary>
        public static bool IsRead(string attributeKey) => !string.IsNullOrEmpty(attributeKey) && Read.Contains(new AttributeId(attributeKey).Key);

        /// <summary>The keys that are read, for tests and tooling.</summary>
        public static IReadOnlyCollection<string> Keys => Read;
    }
}
