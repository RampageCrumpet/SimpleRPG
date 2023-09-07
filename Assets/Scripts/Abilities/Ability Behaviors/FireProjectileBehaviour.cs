using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    public class FireProjectileBehaviour : NetworkBehaviour, IInvokeableAbilityBehaviour
    {
        Ability IInvokeableAbilityBehaviour.Ability { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
        {
            Debug.Log("Invoked ability " + abilityInstace.Ability.abillityName);
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}