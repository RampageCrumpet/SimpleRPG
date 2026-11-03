namespace SimpleRPG
{
    /// <summary>
    /// Where a combatant's wielded weapon comes from. Combat asks for the weapon in the main hand without knowing how items are carried,
    /// worn or equipped; whatever holds the equipment implements this.
    /// </summary>
    public interface IEquippedWeaponSource
    {
        /// <summary>The weapon in the main hand, or null if the main hand is empty or holds something that isn't a weapon.</summary>
        WeaponData MainHandWeapon { get; }
    }
}
