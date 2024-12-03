using SimpleRPG.Abilities;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Base class for the behavior component of abilities.
    /// </summary>
    public interface IInvokeableAbilityBehaviour
    {
        /// <summary>
        /// Invoke the given ability.
        /// </summary>
        public void Invoke(AbilityInstance abilityInstace);
    }
}