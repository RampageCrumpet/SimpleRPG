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

    /// <summary>
    /// An ability behaviour that can take a press while its previous use is still running, instead of waiting for the cooldown.
    /// </summary>
    public interface IQueueableAbilityBehaviour
    {
        /// <summary>
        /// Try to take a press as a queue request. Returns true if the press was consumed, so it must not start a new use.
        /// </summary>
        public bool TryQueue();
    }
}