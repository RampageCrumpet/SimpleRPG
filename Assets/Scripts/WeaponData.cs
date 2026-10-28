using SimpleRPG.HitDetection;
using SimpleRPG.Audio;
using SimpleRPG.InventorySystem;
using SimpleRPG;
using System;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// This scriptable object represents a weapons stat sheet.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Items/WeaponData")]
    [System.Serializable]
    public class WeaponData : Item
    {
        /// <summary>
        /// The total damage dealt by this weapon.
        /// </summary>
        [SerializeField]
        public DamageInfo damage;

        /// <summary>
        /// The chain of melee attacks this weapon performs. Empty for weapons without one.
        /// </summary>
        [SerializeField]
        private SimpleRPG.Abilities.ComboDefinition combo;

        public SimpleRPG.Abilities.ComboDefinition Combo => combo;

        [Header("Audio")]
        [SerializeField]
        [Tooltip("What the weapon is made of, for impact sound tables (steel, wood). Each attack's swing sound is set on the combo step.")]
        private AudioTag audioMaterial;

        [SerializeField]
        [Tooltip("This weapon's own impact sound. Leave empty to use the shared material table.")]
        private AudioEventBase impactSound;

        public AudioTag AudioMaterial => audioMaterial;

        public AudioEventBase ImpactSound => impactSound;
    }
}
