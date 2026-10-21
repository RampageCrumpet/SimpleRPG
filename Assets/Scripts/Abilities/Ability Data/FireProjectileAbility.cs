using SimpleRPG;
using SimpleRPG.Abilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    [CreateAssetMenu(menuName = "Abilities/FireProjectileAbility")]
    public class FireProjectileAbility : Ability
    {
        /// <summary>
        /// The prefab for the projectile we want to fire.
        /// </summary>
        [Tooltip("The projectile we want to fire.")]
        public GameObject Projectile;

        /// <summary>
        /// The damage this ability does if any.
        /// </summary>
        [Tooltip("The damage we want our projectile to do.")]
        public DamageInfo Damage;

        /// <inheritdoc/>
        public override Type AbilityBehavior
        {
            get => typeof(FireProjectileAbillityBehaviour);
        }

        /// <summary>
        /// Warns in the inspector about settings that would make this spell do nothing.
        /// </summary>
        private void OnValidate()
        {
            if (Projectile == null || Projectile.GetComponent<global::Projectile>() == null)
            {
                Debug.LogWarning(AbillityName + " needs a Projectile prefab with a Projectile component, or it can't be fired.", this);
            }

            if (Damage == null || Damage.Damage <= 0)
            {
                Debug.LogWarning(AbillityName + " deals no damage. Set Damage above zero if it should hurt.", this);
            }
        }
    }
}