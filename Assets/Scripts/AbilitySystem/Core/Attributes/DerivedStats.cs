using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The derived statistics every character can have. Which of them a character has, and how, is chosen by its <see cref="StatSheet"/>.
    /// Cataloguing a stat does not make anything read it: a stat takes effect only where a system consumes it.
    /// </summary>
    public static class DerivedStats
    {
        public static readonly AttributeId MaximumHealth = new AttributeId("simplerpg.stat.max_health");
        public static readonly AttributeId PhysicalPower = new AttributeId("simplerpg.stat.physical_power");
        public static readonly AttributeId MagicalPower = new AttributeId("simplerpg.stat.magical_power");
        public static readonly AttributeId PhysicalDamageReduction = new AttributeId("simplerpg.stat.physical_damage_reduction");
        public static readonly AttributeId MagicalDamageReduction = new AttributeId("simplerpg.stat.magical_damage_reduction");
        public static readonly AttributeId MovementSpeed = new AttributeId("simplerpg.stat.movement_speed");
        public static readonly AttributeId AttackSpeed = new AttributeId("simplerpg.stat.attack_speed");
        public static readonly AttributeId CastingSpeed = new AttributeId("simplerpg.stat.casting_speed");
        public static readonly AttributeId InteractionSpeed = new AttributeId("simplerpg.stat.interaction_speed");
        public static readonly AttributeId EquipSpeed = new AttributeId("simplerpg.stat.equip_speed");
        public static readonly AttributeId CarryingCapacity = new AttributeId("simplerpg.stat.carrying_capacity");
        public static readonly AttributeId SpellPreparationCapacity = new AttributeId("simplerpg.stat.spell_preparation_capacity");

        /// <summary>
        /// All twelve, in a fixed order.
        /// </summary>
        public static readonly AttributeId[] All =
        {
            MaximumHealth, PhysicalPower, MagicalPower, PhysicalDamageReduction, MagicalDamageReduction, MovementSpeed,
            AttackSpeed, CastingSpeed, InteractionSpeed, EquipSpeed, CarryingCapacity, SpellPreparationCapacity,
        };

        private static readonly HashSet<AttributeId> AllSet = new HashSet<AttributeId>(All);

        /// <summary>
        /// True if the attribute is one of the catalogued derived statistics.
        /// </summary>
        public static bool IsCatalogued(AttributeId attribute) => AllSet.Contains(attribute);
    }
}
