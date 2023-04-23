using SimpleRPG;
using SimpleRPG.Abilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    [CreateAssetMenu(menuName = "Abilities/FireProjectile")]
    public class FireProjectile : Ability
    {
        /// <summary>
        /// The prefab for the projectile we want to fire.
        /// </summary>
        [Tooltip("The projectile we want to fire.")]
        public GameObject projectile;

        public override void Activate(Character callingCharacter)
        {
            base.Activate(callingCharacter);
        }
    }
}