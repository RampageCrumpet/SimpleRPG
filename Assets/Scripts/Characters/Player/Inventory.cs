using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// The datacontainer for an inventory.
    /// </summary>
    public class Inventory : NetworkBehaviour
    {
        /// <summary>
        /// The total size of our inventory grid.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The total size of the inventory grid.")]
        public Vector2Int InventorySize {get; private set; }

        /// <summary>
        /// The contents of our inventory grid.
        /// </summary>
        Item[,] inventory;

        public void Awake()
        {
            inventory = new Item[InventorySize.x, InventorySize.y];
        }

        /// <summary>
        /// Add an item to the inventory at a given location.
        /// </summary>
        /// <param name="item"> The <see cref="Item"/> we want to add.</param>
        /// <param name="location"> The location within the inventory to add the item.</param>
        private void PlaceItem(Item item, Vector2Int location)
        {
            for(int x = location.x; x < location.x + item.ItemSize.x; x++)
            {
                for(int y = location.y; y < location.y + item.ItemSize.y; y++)
                {
                    inventory[x,y] = item;
                }
            }
        }

        /// <summary>
        /// Remove an item from the inventory.
        /// </summary>
        /// <param name="item"> The <see cref="Item"/> to remove from the inventory.</param>
        public void RemoveItem(Item item)
        {
            for(int x = 0; x < inventory.GetLength(0); x++)
            {
                for(int y = 0; y < inventory.GetLength(1); y++)
                {
                    if (inventory[x,y] == item)
                    {
                        inventory[x,y] = null;
                    }
                }
            }
        }

        /// <summary>
        /// Adds an item to the given location in the inventory.
        /// </summary>
        /// <param name="item"> The item we want to place.</param>
        /// <param name="location"> The location we want to place the item.</param>
        /// <returns> Returns true if the item can be placed, false if it cant</returns>
        public bool AddItem(Item item, Vector2Int location)
        {
            if(ValidateItemPlacement(item, location))
            {
                PlaceItem(item, location);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Validates an items placement ensuring that it's within the boundaries of the inventory and doesn't overlap other items.
        /// </summary>
        /// <param name="item"> The item we're trying to place.</param>
        /// <param name="location"> The location in the <see cref="Inventory"/> we're trying to place the item at.</param>
        /// <returns> False if the item is outside the bounds of the inventory or overlaps another item, true otherwise.</returns>
        public bool ValidateItemPlacement(Item item, Vector2Int location)
        {
            for (int x = location.x; x < location.x + item.ItemSize.x; x++)
            {
                for (int y = location.y; y < location.y + item.ItemSize.y; y++)
                {
                    // We're trying to place the item outside of the boundaries of the inventory and shouldn't be allowed to do so.
                    if (x >= InventorySize.x || x < 0 || y >= InventorySize.y || y < 0)
                        return false;

                    // A slot the item would have filled is occupied by a different.
                    if (inventory[x, y] != null && inventory[x, y] != item)
                        return false;
                }
            }

            // No problems were found with the items placement. It must be a valid placement.
            return true;
        }

        /// <summary>
        /// Gets all items in the inventory along with their positions.
        /// </summary>
        /// <returns> An enumerable of tuples containing the item and its position.</returns>
        public IEnumerable<(Item item, Vector2Int position)> GetItems()
        {
            var addedItems = new HashSet<Item>();

            // By starting at 0, 0 we can ensure we always find the origin location of the item first.
            for (int x = 0; x < inventory.GetLength(0); x++)
            {
                for (int y = 0; y < inventory.GetLength(1); y++)
                {
                    var item = inventory[x, y];
                    if (item != null && !addedItems.Contains(item))
                    {
                        yield return (item, new Vector2Int(x, y));
                        addedItems.Add(item);
                    }
                }
            }
        }
    }
}
