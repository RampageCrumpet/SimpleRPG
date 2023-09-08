using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// This class handles the execution of all <see cref="FireProjectileAbility"/> instances.
    /// </summary>
    public class FireProjectileBehaviour : NetworkBehaviour, IInvokeableAbilityBehaviour
    {
        /// <summary>
        /// The character who owns this <see cref="FireProjectileBehaviour"/> 
        /// </summary>
        private Character character;

        Ability IInvokeableAbilityBehaviour.Ability { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public void Start()
        {
            character = this.GetComponent<Character>();
        }

        /// <inheritdoc/>
        void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
        {
            FireProjectileServerRPC(abilityInstace.Ability.abillityName);
        }

        [ServerRpc]
        void FireProjectileServerRPC(string abilityName)
        {
            FireProjectileClientRPC(abilityName);
        }

        [ClientRpc]
        void FireProjectileClientRPC(string abilityName)
        {
            FireProjectileAbility abilityInstance = (FireProjectileAbility)this.character.PersonalAbilities.Select(x => x.Ability).Single(x => x.abillityName == abilityName);
            Debug.Log("Invoked ability " + abilityInstance.Ability.abillityName);
        }
    }
}
