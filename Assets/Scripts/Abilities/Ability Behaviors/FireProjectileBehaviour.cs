using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// This class handles the execution of all <see cref="FireProjectileAbility"/> instances.
    /// </summary>
    public class FireProjectileBehaviour : NetworkBehaviour, IInvokeableAbilityBehaviour
    {
        Ability IInvokeableAbilityBehaviour.Ability { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        /// <inheritdoc/>
        void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
        {
            Debug.Log("Invoked ability " + abilityInstace.Ability.abillityName);
        }

        [ServerRpc]
        void FireProjectileServerRPC()
        {
            FireProjectileClientRPC();
        }

        [ClientRpc]
        void FireProjectileClientRPC()
        {

        }
    }
}
