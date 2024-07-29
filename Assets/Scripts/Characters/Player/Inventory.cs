using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using static UnityEditor.FilePathAttribute;

namespace Inventory
{
    /// <summary>
    /// The datacontainer for an inventory.
    /// </summary>
    public class Inventory : NetworkBehaviour
    {
        /// <summary>
        /// The total size of our inventory grid.
        /// </summary>
        [SerializeField]
        [Tooltip("The total size of the inventory grid.")]
        Vector2Int inventorySize;

        /// <summary>
        /// The contents of our inventory grid.
        /// </summary>
        Item[,] inventory;

        public void Start()
        {
            inventory = new Item[inventorySize.x, inventorySize.y];
        }

        /// <summary>
        /// Add an item to the inventory at a given location.
        /// </summary>
        /// <param name="item"> The <see cref="Item"/> we want to add.</param>
        /// <param name="location"> The location within the inventory to add the item.</param>
        public void AddItem(Item item, Vector2Int location)
        {
            for(int x = location.x; x < location.x + item.itemSize.x; x++) 
            { 
                for(int y = location.y; y < location.y + item.itemSize.y; y++)
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

        public void MoveItem(Item item, Vector2Int location)
        {
        }

        /// <summary>
        /// Validates an items placement ensuring that it's within the boundaries of the inventory and doesn't overlap other items.
        /// </summary>
        /// <param name="item"> The item we're trying to place.</param>
        /// <param name="location"> The location in the <see cref="Inventory"/> we're trying to place the item at.</param>
        /// <returns> False if the item is outside the bounds of the inventory or overlaps another item, true otherwise.</returns>
        public bool ValidateItemPlacement(Item item, Vector2Int location)
        {
            for (int x = location.x; x < location.x + item.itemSize.x; x++)
            {
                for (int y = location.y; y < location.y + item.itemSize.y; y++)
                {
                    // We're trying to place the item outside of the boundaries of the inventory and shouldn't be allowed to do so.
                    if (x >= inventorySize.x || x < 0 || y >= inventorySize.y || y < 0)
                        return false;

                    // A slot the item would have filled is occupied and we can't place an item here.
                    if (inventory[x, y] != null)
                        return false;
                }
            }

            // No problems were found with the items placement. It must be a valid placement.
            return true;
        }
    }
}
