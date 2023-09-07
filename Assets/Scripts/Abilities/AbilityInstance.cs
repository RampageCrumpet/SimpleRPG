using SimpleRPG.Abilities;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    public class AbilityInstance
    {
        private IInvokeableAbilityBehaviour invokeableAbility;

        /// <summary>
        /// The data container for our ability.
        /// </summary>
        public Ability Ability
        {
            get;
        }

        public float CooldownTimeLeft
        {
            get
            {
                return LastActivationTime + Ability.cooldownTime - Time.time;
            }
        }

        /// <summary>
        /// The last time this abillity was used.
        /// </summary>
        public float LastActivationTime
        {
            get;
            private set;
        }

        /// <summary>
        /// Create a new instance of the <see cref="AbilityInstance"/> class.
        /// </summary>
        /// <param name="ability"> The ability we want to create a specific instance of.--</param>
        /// <param name="sourceGameObject"></param>
        public AbilityInstance(Ability ability, Character sourceGameObject)
        {
            this.Ability = ability;
            this.invokeableAbility = sourceGameObject.GetComponent(ability.AbilityBehavior) as IInvokeableAbilityBehaviour;

            if (invokeableAbility == null)
            {
                Debug.LogError(sourceGameObject.gameObject.name + " has no InvokableAbility for " + ability.abillityName + ".");
            }
        }

        /// <summary>
        /// Activate the abillity.
        /// </summary>
        public void Activate()
        {
            if (Time.time > LastActivationTime + this.Ability.cooldownTime)
            {
                LastActivationTime = Time.time;
                invokeableAbility.Invoke(this);
            }
        }
    }
}