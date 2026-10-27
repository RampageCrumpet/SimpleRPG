using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// How a paper doll arranges its slots.
    /// </summary>
    public enum PaperDollLayout
    {
        /// <summary>The slots surround a character preview in the middle of the region. For the player.</summary>
        Full,

        /// <summary>The slots sit in columns either side of the region, with no preview. For another entity's equipment.</summary>
        Compact,
    }

    /// <summary>
    /// A paper doll: one <see cref="EquipmentSlotView"/> per armor slot, showing what a character wears. A <see cref="CharacterPreviewUI"/> may sit behind it.
    /// It only reflects <see cref="Equipment"/>, redrawing whenever its Changed event fires, and forwards clicks as unequip requests.
    /// Equipment is correct whether or not this view is open or even bound. The same component serves the player and any other entity with equipment.
    /// </summary>
    public class PaperDollUI : MonoBehaviour
    {
        /// <summary>
        /// The side of a slot, in canvas units. The same as an inventory cell, so the doll and the grids read as one interface.
        /// </summary>
        public const float SlotSize = 64f;

        [SerializeField]
        [Tooltip("How the slots are arranged.")]
        private PaperDollLayout layout = PaperDollLayout.Full;

        private readonly Dictionary<EquipmentSlot, EquipmentSlotView> views = new Dictionary<EquipmentSlot, EquipmentSlotView>();

        private Equipment equipment;

        /// <summary>
        /// The equipment currently shown, or null.
        /// </summary>
        public Equipment Equipment => equipment;

        /// <summary>
        /// The view for one armor slot, or null if it is not an armor slot.
        /// </summary>
        public EquipmentSlotView GetView(EquipmentSlot slot)
        {
            EnsureBuilt();
            return views.TryGetValue(slot, out EquipmentSlotView view) ? view : null;
        }

        /// <summary>
        /// Shows the equipment of a character. Pass null to stop showing anything.
        /// </summary>
        public void Bind(Equipment newEquipment)
        {
            EnsureBuilt();

            if (equipment != null)
            {
                equipment.Changed -= Refresh;
            }

            equipment = newEquipment;

            if (equipment != null)
            {
                equipment.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (equipment != null)
            {
                equipment.Changed -= Refresh;
            }
        }

        /// <summary>
        /// Redraws every slot from the equipment.
        /// </summary>
        public void Refresh()
        {
            foreach (KeyValuePair<EquipmentSlot, EquipmentSlotView> pair in views)
            {
                pair.Value.SetItem(equipment != null ? equipment.Get(pair.Key) : null);
            }
        }

        private void HandleSlotClicked(EquipmentSlotView view)
        {
            if (equipment != null && view.DisplayedItem != null)
            {
                equipment.RequestUnequip(view.Slot);
            }
        }

        private void EnsureBuilt()
        {
            if (views.Count > 0)
            {
                return;
            }

            foreach (EquipmentSlot slot in ArmorData.ArmorSlots)
            {
                EquipmentSlotView view = EquipmentSlotView.Create((RectTransform)transform, slot, Vector2.zero, SlotSize);
                view.Clicked += HandleSlotClicked;
                views[slot] = view;
            }

            ApplyLayout();
        }

        /// <summary>
        /// Where a slot sits relative to the centre of the doll's region, with y up.
        /// The full layout follows the wireframe: head over chest, hands to the left, legs to the right, feet below.
        /// </summary>
        public static Vector2 SlotOffset(PaperDollLayout layout, EquipmentSlot slot)
        {
            if (layout == PaperDollLayout.Compact)
            {
                switch (slot)
                {
                    case EquipmentSlot.head: return new Vector2(-216f, 40f);
                    case EquipmentSlot.chest: return new Vector2(-216f, -40f);
                    case EquipmentSlot.hands: return new Vector2(216f, 40f);
                    case EquipmentSlot.legs: return new Vector2(216f, -40f);
                    default: return new Vector2(0f, -40f);
                }
            }

            switch (slot)
            {
                case EquipmentSlot.head: return new Vector2(0f, 206f);
                case EquipmentSlot.chest: return new Vector2(0f, 76f);
                case EquipmentSlot.hands: return new Vector2(-180f, 76f);
                case EquipmentSlot.legs: return new Vector2(180f, -24f);
                default: return new Vector2(0f, -146f);
            }
        }

        private void ApplyLayout()
        {
            foreach (KeyValuePair<EquipmentSlot, EquipmentSlotView> pair in views)
            {
                pair.Value.Place(SlotOffset(layout, pair.Key), SlotSize);
            }
        }
    }
}
