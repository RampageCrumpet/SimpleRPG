using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Delve
{
    /// <summary>
    /// What a player loses on death: everything. The inventory and every worn item move into a loot bag, as the same items with the same
    /// instance IDs, so nothing is copied or invented and a looter picks up exactly what the dead player carried. Server (or offline) only.
    /// </summary>
    public static class DeathDrop
    {
        /// <summary>How many items moved and how many had no room and stayed.</summary>
        public readonly struct Counts
        {
            public Counts(int moved, int left)
            {
                Moved = moved;
                Left = left;
            }

            public int Moved { get; }

            public int Left { get; }

            public static Counts operator +(Counts a, Counts b) => new Counts(a.Moved + b.Moved, a.Left + b.Left);
        }

        /// <summary>True if the character carries or wears anything at all.</summary>
        public static bool HasAnything(Inventory inventory, Equipment equipment)
        {
            if (inventory != null && inventory.GetItems().GetEnumerator().MoveNext())
            {
                return true;
            }

            return equipment != null && equipment.Equipped.GetEnumerator().MoveNext();
        }

        /// <summary>Moves every item in <paramref name="source"/> into <paramref name="bag"/>. An item the bag has no room for stays where it was.</summary>
        public static Counts MoveInventory(Inventory source, Inventory bag)
        {
            if (source == null || bag == null)
            {
                return default;
            }

            // The grid changes as items leave, so work from a list taken first.
            var items = new List<Item>();
            foreach ((Item item, Vector2Int _) in source.GetItems())
            {
                items.Add(item);
            }

            int moved = 0;
            int left = 0;
            foreach (Item item in items)
            {
                if (bag.TryFindRoom(item, null, out Vector2Int room) && source.Detach(item, out uint instanceId, out Vector2Int _))
                {
                    bag.Attach(item, room, instanceId);
                    moved++;
                }
                else
                {
                    left++;
                }
            }

            return new Counts(moved, left);
        }

        /// <summary>Takes everything worn off and puts it in <paramref name="bag"/>. An item the bag has no room for stays worn.</summary>
        public static Counts MoveEquipment(Equipment equipment, Inventory bag)
        {
            if (equipment == null || bag == null)
            {
                return default;
            }

            var worn = new List<(EquipmentSlot slot, uint instanceId, Item item)>();
            foreach ((EquipmentSlot slot, uint instanceId, Item item) in equipment.Equipped)
            {
                worn.Add((slot, instanceId, item));
            }

            int moved = 0;
            int left = 0;
            foreach ((EquipmentSlot slot, uint instanceId, Item item) in worn)
            {
                if (bag.TryFindRoom(item, null, out Vector2Int room) && equipment.Unequip(slot))
                {
                    bag.Attach(item, room, instanceId);
                    moved++;
                }
                else
                {
                    left++;
                }
            }

            return new Counts(moved, left);
        }
    }
}
