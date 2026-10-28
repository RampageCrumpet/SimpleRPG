using SimpleRPG.Abilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// What a character fights with: the abilities on its bar, in order, and the weapon it uses when nothing is wielded. The ability slots are
    /// built once and kept, so a slot's cooldown and subscribers survive however often they are read.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class CombatLoadout : MonoBehaviour
    {
        /// <summary>
        /// The abilities this character starts with, in ability bar order. The server checks every activation request against this list.
        /// </summary>
        [SerializeField]
        [Tooltip("The abilities this character can use, in ability bar order.")]
        private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

        /// <summary>
        /// The weapon this character starts with, used when nothing is equipped in the main hand.
        /// </summary>
        [SerializeField]
        [Tooltip("The weapon this character fights with when nothing is equipped in the main hand.")]
        private WeaponData defaultWeapon;

        private Character character;

        private Equipment equipment;

        private List<IAbilitySlot> slots;

        /// <summary>
        /// The abilities this character can use.
        /// </summary>
        public IReadOnlyList<AbilityDefinition> Abilities => abilities;

        /// <summary>
        /// Every ability on this character's bar, in order. Built on first use and then the same instances every time.
        /// </summary>
        public IReadOnlyList<IAbilitySlot> Slots
        {
            get
            {
                if (slots == null)
                {
                    if (character == null) character = GetComponent<Character>();
                    slots = abilities.Select(x => (IAbilitySlot)new GraphAbilitySlot(x, character)).ToList();
                }

                return slots;
            }
        }

        /// <summary>
        /// The weapon used when nothing is equipped in the main hand.
        /// </summary>
        public WeaponData DefaultWeapon
        {
            get => defaultWeapon;
            set => defaultWeapon = value;
        }

        /// <summary>
        /// The weapon this character attacks with: the one equipped in the main hand, or the default weapon if there is none.
        /// </summary>
        public WeaponData Weapon
        {
            get
            {
                if (equipment == null) equipment = GetComponent<Equipment>();

                WeaponData equipped = equipment != null ? equipment.MainHandWeapon : null;
                return equipped != null ? equipped : defaultWeapon;
            }
        }
    }
}
