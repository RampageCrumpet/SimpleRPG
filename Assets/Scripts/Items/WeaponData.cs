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
        /// <inheritdoc/>
        public override EquipmentSlot WornSlot => EquipmentSlot.mainHand;

        /// <summary>
        /// The total damage dealt by this weapon.
        /// </summary>
        [SerializeField]
        public DamageInfo damage;

        /// <summary>
        /// A weapon's base damage is what its Enhanced Damage affix is a percentage of. It is not a grant (the weapon damage calculation adds it),
        /// so it is offered here as the number an item-scoped percentage scales.
        /// </summary>
        protected override System.Collections.Generic.IReadOnlyList<SimpleRPG.Abilities.GrantSpec> ScalableGrants
        {
            get
            {
                var scalable = new System.Collections.Generic.List<SimpleRPG.Abilities.GrantSpec>(Grants)
                {
                    new SimpleRPG.Abilities.GrantSpec
                    {
                        AttributeKey = SimpleRPG.Gameplay.DerivedStats.WeaponDamage.Key,
                        Layer = SimpleRPG.Gameplay.ModifierLayer.Add,
                        Value = damage != null ? damage.Damage : 0f,
                    },
                };
                return scalable;
            }
        }

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
