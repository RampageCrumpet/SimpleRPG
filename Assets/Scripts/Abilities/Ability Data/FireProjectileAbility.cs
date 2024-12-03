using SimpleRPG;
using SimpleRPG.Abilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    [CreateAssetMenu(menuName = "Abilities/FireProjectile")]
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
    }
}