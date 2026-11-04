using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// What a combatant adds to the damage it takes before it is stored: the fraction of a blow its defences remove. <see cref="CombatResolver"/>
    /// asks a target that implements this, once per request, after the struck region has had its say.
    /// </summary>
    public interface IDamageMitigator
    {
        /// <summary>The fraction of damage of this type that is removed, from 0 (none) up to <see cref="DamageMitigation.MaxReduction"/>.</summary>
        double Reduction(DamageType type);
    }

    /// <summary>
    /// The rules for turning a blow into the damage that lands. Plain code with no engine types, so the arithmetic is tested exactly.
    /// <list type="bullet">
    /// <item>Which defence a blow meets is its <see cref="DamageType"/>, and nothing else: how it was delivered (a swing, a projectile, a tick) does
    /// not matter. A request that skips the defences says so with <see cref="DamageRequest.BypassesMitigation"/>.</item>
    /// <item>A reduction is the fraction of damage removed. It is kept between 0 and <see cref="MaxReduction"/>, so no defence makes a character immune.</item>
    /// <item>The struck region's factor and the reduction are applied together and the result is rounded once, so rounding never compounds.</item>
    /// <item>A blow that was above zero and lands at all does at least 1: a low factor or a high reduction cannot turn a hit into a miss. A region
    /// that takes no damage (a factor of 0) is the one way a blow does nothing.</item>
    /// </list>
    /// </summary>
    public static class DamageMitigation
    {
        /// <summary>The most damage a defence can remove, as a fraction. 0.9 leaves one tenth of a blow.</summary>
        public const double MaxReduction = 0.9d;

        /// <summary>
        /// The fraction of a request that its target's defences remove: none if it bypasses them, otherwise the target's reduction for its type.
        /// </summary>
        public static double ReductionFor(in DamageRequest request, IDamageMitigator defences)
        {
            if (request.BypassesMitigation || defences == null)
            {
                return 0d;
            }

            return Clamp(defences.Reduction(request.Type));
        }

        /// <summary>A reduction held to the range a defence can have. Not a number, or negative, is none.</summary>
        public static double Clamp(double reduction)
        {
            if (double.IsNaN(reduction) || reduction <= 0d)
            {
                return 0d;
            }

            return Math.Min(reduction, MaxReduction);
        }

        /// <summary>
        /// The damage that lands.
        /// </summary>
        /// <param name="amount">The blow before the target's body and defences.</param>
        /// <param name="regionFactor">What the struck region is worth; 1 where no region was struck. A factor of 0 or less takes no damage.</param>
        /// <param name="reduction">The fraction the target's defences remove; see <see cref="Clamp"/>.</param>
        public static int Final(int amount, double regionFactor, double reduction)
        {
            if (amount <= 0 || double.IsNaN(regionFactor) || regionFactor <= 0d)
            {
                return 0;
            }

            double landed = amount * regionFactor * (1d - Clamp(reduction));
            return Math.Max(1, (int)Math.Round(landed, MidpointRounding.AwayFromZero));
        }
    }
}
