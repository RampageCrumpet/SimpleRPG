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
        public GameObject projectile;

        public override Type AbilityBehavior 
        { 
            get => typeof(FireProjectileBehaviour);
        }
    }
}