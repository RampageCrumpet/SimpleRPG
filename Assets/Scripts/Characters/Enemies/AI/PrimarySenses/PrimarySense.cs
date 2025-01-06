using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.AI
{
    /// <summary>
    /// This class represents a sense that can directly sense a player.
    /// </summary>
    public abstract class PrimarySense : MonoBehaviour
    {
        /// <summary>
        /// Can this sense detect the given target?
        /// </summary>
        /// <param name="target"> The target we want to sense.</param>
        /// <returns></returns>
        public abstract bool CanSense(Character target);

        /// <summary>
        /// Pulse the sense to actively look for <see cref="Character"/>'s within the sense range.
        /// </summary>
        /// <returns> A collection of <see cref="Character"/>'s within the sense range.</returns>
        public abstract IEnumerable<Character> Sense();
    }
}
