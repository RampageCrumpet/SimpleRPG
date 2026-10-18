using UnityEngine;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Stands in for the player's melee behaviour so the longsword animation events have a receiver during tests.
    /// </summary>
    public class TestAnimationEventReceiver : MonoBehaviour
    {
        /// <summary>
        /// The attack number passed by the most recent ActivateWeapon event.
        /// </summary>
        public int LastActivatedAttack { get; private set; }

        /// <summary>
        /// The number of times the hitbox has been opened.
        /// </summary>
        public int ActivationCount { get; private set; }

        /// <summary>
        /// The number of times the hitbox has been closed.
        /// </summary>
        public int DeactivationCount { get; private set; }

        public void ActivateWeapon(int attackNumber)
        {
            LastActivatedAttack = attackNumber;
            ActivationCount++;
        }

        public void DeactivateWeaponH()
        {
            DeactivationCount++;
        }
    }
}
