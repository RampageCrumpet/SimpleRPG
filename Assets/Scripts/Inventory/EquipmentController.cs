using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// Why an equip or unequip was accepted or refused.
    /// </summary>
    public enum EquipResult
    {
        Success,
        NoItem,
        NoEquipment,
        NotAuthority,
        ItemNotInInventory,
        NotArmor,
        ItemNotReplicable,
        SlotEmpty,
        NoRoomInInventory
    }

    /// <summary>
    /// Owns the rules for moving armor between an inventory and the equipment slots. Like <see cref="InventoryMoveController"/> a request is
    /// checked in full before anything changes, so it either happens completely or not at all, and an item is never in both places.
    /// The UI only forwards the player's intent here. Server (or offline) only.
    /// </summary>
    public static class EquipmentController
    {
        /// <summary>
        /// Checks equipping an inventory item into the slot its <see cref="ArmorData"/> declares, without changing anything.
        /// A replaced item must fit back into the inventory, in the room the new item leaves behind or anywhere else.
        /// </summary>
        public static EquipResult ValidateEquip(Inventory inventory, Equipment equipment, Item item)
        {
            if (item == null)
            {
                return EquipResult.NoItem;
            }

            if (inventory == null || equipment == null)
            {
                return EquipResult.NoEquipment;
            }

            if (!inventory.CanMutateContents || !equipment.IsServer)
            {
                return EquipResult.NotAuthority;
            }

            if (!(item is ArmorData armor) || !ArmorData.IsArmorSlot(armor.Slot))
            {
                return EquipResult.NotArmor;
            }

            if (!inventory.Contains(item))
            {
                return EquipResult.ItemNotInInventory;
            }

            if (!equipment.CanEquip(armor.Slot, item, (uint)inventory.GetInstanceId(item).Value))
            {
                return EquipResult.ItemNotReplicable;
            }

            Item previous = equipment.Get(armor.Slot);
            if (previous != null && !inventory.TryFindRoom(previous, item, out _))
            {
                return EquipResult.NoRoomInInventory;
            }

            return EquipResult.Success;
        }

        /// <summary>
        /// Validates and then equips an inventory item. A replaced item goes back into the inventory.
        /// </summary>
        public static EquipResult TryEquip(Inventory inventory, Equipment equipment, Item item)
        {
            EquipResult result = ValidateEquip(inventory, equipment, item);
            if (result != EquipResult.Success)
            {
                return result;
            }

            EquipmentSlot slot = ((ArmorData)item).Slot;
            Item previous = equipment.Get(slot);
            uint previousId = equipment.GetInstanceId(slot);

            inventory.Detach(item, out uint instanceId, out Vector2Int originalLocation);

            // Validation already covered everything Equip checks; if it still refuses, put the item back where it was.
            if (!equipment.Equip(slot, item, instanceId))
            {
                inventory.Attach(item, originalLocation, instanceId);
                return EquipResult.ItemNotReplicable;
            }

            if (previous != null)
            {
                inventory.TryFindRoom(previous, null, out Vector2Int room);
                inventory.Attach(previous, room, previousId);
            }

            return EquipResult.Success;
        }

        /// <summary>
        /// Checks taking the item out of a slot and into the inventory, without changing anything.
        /// </summary>
        public static EquipResult ValidateUnequip(Inventory inventory, Equipment equipment, EquipmentSlot slot)
        {
            if (inventory == null || equipment == null)
            {
                return EquipResult.NoEquipment;
            }

            if (!inventory.CanMutateContents || !equipment.IsServer)
            {
                return EquipResult.NotAuthority;
            }

            Item item = equipment.Get(slot);
            if (item == null || !ArmorData.IsArmorSlot(slot))
            {
                return EquipResult.SlotEmpty;
            }

            return inventory.TryFindRoom(item, null, out _) ? EquipResult.Success : EquipResult.NoRoomInInventory;
        }

        /// <summary>
        /// Validates and then moves the item in a slot into the first free place in the inventory.
        /// </summary>
        public static EquipResult TryUnequip(Inventory inventory, Equipment equipment, EquipmentSlot slot)
        {
            EquipResult result = ValidateUnequip(inventory, equipment, slot);
            if (result != EquipResult.Success)
            {
                return result;
            }

            Item item = equipment.Get(slot);
            uint instanceId = equipment.GetInstanceId(slot);
            inventory.TryFindRoom(item, null, out Vector2Int room);

            equipment.Unequip(slot);
            inventory.Attach(item, room, instanceId);
            return EquipResult.Success;
        }
    }
}
