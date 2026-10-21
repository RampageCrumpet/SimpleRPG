using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Pure rules for projectile abilities. They take plain values rather than scene objects so they can be tested in EditMode.
    /// </summary>
    public static class ProjectileRules
    {
        /// <summary>
        /// The velocity a projectile travels at when fired along a direction. A zero direction gives no movement instead of NaN.
        /// </summary>
        /// <param name="direction">The direction to fire in. It does not need to be normalized.</param>
        /// <param name="speed">The projectile's speed in units per second.</param>
        public static Vector3 LaunchVelocity(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return direction.normalized * speed;
        }

        /// <summary>
        /// Whether a projectile should damage the character it touched. Each projectile damages at most one target, and only once.
        /// </summary>
        /// <param name="hasTarget">True if the contact was with a character.</param>
        /// <param name="alreadyHit">True if this projectile has already damaged a target.</param>
        public static bool ShouldApplyHit(bool hasTarget, bool alreadyHit)
        {
            return hasTarget && !alreadyHit;
        }
    }
}
