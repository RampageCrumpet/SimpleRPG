using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// Decides which hits in one melee swing may deal damage: a target is hit at most once per swing and an attacker is never a valid target.
    /// A swing runs from <see cref="Begin"/> until <see cref="End"/>; repeated Begin calls during a swing (a doubled animation event) keep the record.
    /// </summary>
    public class SwingHitTracker
    {
        private readonly HashSet<Object> hitTargets = new HashSet<Object>();

        /// <summary>
        /// True between <see cref="Begin"/> and <see cref="End"/>.
        /// </summary>
        public bool IsSwinging { get; private set; }

        /// <summary>
        /// Starts a swing with an empty record of hit targets. Does nothing if a swing is already in progress.
        /// </summary>
        public void Begin()
        {
            if (IsSwinging) return;

            hitTargets.Clear();
            IsSwinging = true;
        }

        /// <summary>
        /// Ends the current swing.
        /// </summary>
        public void End()
        {
            IsSwinging = false;
        }

        /// <summary>
        /// Records a hit on <paramref name="target"/> and reports whether it should deal damage.
        /// </summary>
        /// <param name="target">The character that was hit. Null is rejected.</param>
        /// <param name="attacker">The character swinging, which can never be hit. May be null if unknown.</param>
        /// <returns>True only for the first hit on a valid target while a swing is in progress.</returns>
        public bool TryRegisterHit(Object target, Object attacker)
        {
            if (!IsSwinging || target == null || target == attacker) return false;

            return hitTargets.Add(target);
        }
    }
}
