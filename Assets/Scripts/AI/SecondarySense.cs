using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG
{
    public abstract class SecondarySense : MonoBehaviour
    {
        /// <summary>
        /// Can this sense detect the given target?
        /// </summary>
        /// <param name="target"> The target we want to sense.</param>
        /// <returns></returns>
        public abstract bool CanSense(Vector3 target);

        /// <summary>
        /// Pulse the sense to actively look for <see cref="Character"/>'s within the sense range.
        /// </summary>
        /// <returns> A collection of <see cref="Character"/>'s within the sense range.</returns>
        public abstract IEnumerable<Vector3> Sense();
    }
}
