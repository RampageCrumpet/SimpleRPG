using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// An item worn on one of the five armor slots. It declares which slot it fits; everything else an item can do (rules, grants, audio)
    /// comes from <see cref="Item"/>. Armor statistics and fitted visuals are not part of it yet.
    /// </summary>
    [CreateAssetMenu(fileName = "ArmorData", menuName = "Items/ArmorData")]
    public class ArmorData : Item
    {
        [SerializeField]
        [Tooltip("The slot this armor is worn in. Must be Head, Chest, Hands, Legs or Feet.")]
        private EquipmentSlot slot = EquipmentSlot.chest;

        /// <summary>
        /// The slot this armor is worn in.
        /// </summary>
        public EquipmentSlot Slot => slot;

        /// <summary>
        /// The slots armor can be worn in, in the order the paper doll lists them.
        /// </summary>
        public static readonly EquipmentSlot[] ArmorSlots =
        {
            EquipmentSlot.head,
            EquipmentSlot.chest,
            EquipmentSlot.hands,
            EquipmentSlot.legs,
            EquipmentSlot.feet
        };

        /// <summary>
        /// True if the slot is one of the five armor slots.
        /// </summary>
        public static bool IsArmorSlot(EquipmentSlot slot) => System.Array.IndexOf(ArmorSlots, slot) >= 0;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (!IsArmorSlot(slot))
            {
                Debug.LogWarning($"{name}: armor must use the Head, Chest, Hands, Legs or Feet slot, not {slot}.", this);
            }
        }
#endif
    }
}
