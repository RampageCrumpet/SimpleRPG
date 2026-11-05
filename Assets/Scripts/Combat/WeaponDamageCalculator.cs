using System;

namespace SimpleRPG
{
    /// <summary>
    /// How much a weapon blow does before the combo scalar and the target's defences: the weapon's own damage plus the flat bonuses, all
    /// multiplied by the weapon damage multiplier. Bonuses of one kind are summed before the kinds are multiplied together:
    /// <list type="bullet">
    /// <item><description>Additive: the weapon's base, its item-local Enhanced Damage (a percentage of that base, already a flat amount), and flat bonuses from worn items.</description></item>
    /// <item><description>Multiplicative: the multiplier, which is 1 plus the sum of the percentage Weapon Damage bonuses of worn items.</description></item>
    /// </list>
    /// Only weapon attacks use it. The attack's ability then applies the combo scalar and Physical Power (1 + 0.05 per point, a constant in the
    /// ability asset where it is tuned), and the combat resolver applies mitigation, exactly as before. Ability and magical damage never pass through here.
    /// </summary>
    public static class WeaponDamageCalculator
    {
        public static float Compute(float weaponDamage, double flatBonuses, double multiplier)
        {
            double total = (weaponDamage + flatBonuses) * Math.Max(0d, multiplier);
            return (float)Math.Max(0d, total);
        }
    }
}
