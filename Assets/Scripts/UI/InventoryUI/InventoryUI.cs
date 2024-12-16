using UnityEngine;
using SimpleRPG.InventorySystem;
using UnityEngine.UI;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.Graphs;

namespace SimpleRPG.UI
{
    public class InventoryUI : MonoBehaviour
    {
        /// <summary>
        /// The data container for the inventory.
        /// </summary>
        [SerializeField]
        [Tooltip("The backing inventory.")]
        public InventorySystem.Inventory inventory;

        /// <summary>
        /// The grid layout group governing control over this inventory.
        /// </summary>
        private GridLayoutGroup gridLayout;

        /// <summary>
        /// The prefab to be instantiated for every grid element.
        /// </summary>
        [SerializeField]
        [Tooltip("The icon to use for each slot in the inventory.")]
        private GameObject UISquarePrefab;

        /// <summary>
        /// The prefab to be instantiated for every ItemIcon.
        /// </summary>
        [SerializeField]
        [Tooltip("The Item Icon prefab to be used to create images of the items.")]
        private GameObject ItemIconPrefab;

        private List<InventorySlot> inventorySlots = new List<InventorySlot>();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if(inventory == null)
            {
                Debug.LogError(this.gameObject.name + " has no inventory attached to render.");
            }

            // Create a grid layout that matches the inventory layout.
            gridLayout = GetComponentInChildren<GridLayoutGroup>();
            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                gridLayout.constraintCount = inventory.InventorySize.x;
            }
            else if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                gridLayout.constraintCount = inventory.InventorySize.y;
            }
            else
            {
                Debug.LogError(this.gameObject.name + " has an unrecognized GridLayoutGroup constraint and may not layout properly."); 
            }

            CreateBackingGrid();
        }

        /// <summary>
        /// Creates a grid with the width and height of the specified inventory.
        /// </summary>
        private void CreateBackingGrid()
        {
            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                for (int x = 0; x < inventory.InventorySize.x; x++)
                {
                    for (int y = 0; y < inventory.InventorySize.y; y++)
                    {
                        GameObject newUISquarePrefab = Object.Instantiate(UISquarePrefab);
                        newUISquarePrefab.transform.SetParent(gridLayout.transform, false);

                        InventorySlot newInventorySlot = newUISquarePrefab.GetComponent<InventorySlot>();
                        inventorySlots.Add(newInventorySlot);
                        newInventorySlot.position = new Vector2Int(x, y);
                    }
                }
            }
            else if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                for  (int y = 0; y < inventory.InventorySize.y; y++)
                {
                    for (int x = 0; x < inventory.InventorySize.x; x++)
                    {
                        GameObject newUISquarePrefab = Object.Instantiate(UISquarePrefab);
                        newUISquarePrefab.transform.SetParent(gridLayout.transform, false);

                        InventorySlot newInventorySlot = newUISquarePrefab.GetComponent<InventorySlot>();
                        inventorySlots.Add(newInventorySlot);
                        newInventorySlot.position = new Vector2Int(x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Add an ItemIcon to this InventoryUI.
        /// </summary>
        /// <param name="itemIcon"> The ItemIcon we want to have to this inventory.</param>
        /// <param name="inventorySlot"> The slot we want to add the ItemIcon to.</param>
        public void AddItemIcon(ItemIcon itemIcon, InventorySlot inventorySlot)
        {
            // Add the item to the backing data model.
            inventory.AddItem(itemIcon.Item, inventorySlot.position);
        }

        /// <summary>
        /// Removes an ItemIcon from the inventory.
        /// </summary>
        /// <param name="itemIcon"> The ItemIcon we want to remove from this inventoryUI.</param>
        public void RemoveItemIcon(ItemIcon itemIcon)
        {
            // Remove the item from the backing data model.
            inventory.RemoveItem(itemIcon.Item);
        }

        /// <summary>
        /// Finds the <see cref="InventorySlot"/> closest to the given position.
        /// </summary>
        /// <param name="position"> The position we want to find the closest inventory slot to.</param>
        /// <returns> The <see cref="InventorySlot"/> closest to the given position. Returns null if there are no inventory slots.</returns>
        public InventorySlot FindInventorySlotClosestToPosition(Vector3 position)
        {
            if (inventorySlots == null || !inventorySlots.Any())
            {
                Debug.LogError(this.gameObject.name + " InventoryUI has no InventorySlots.");
                return null; 
            }

            //InventorySlot closestInventorySlot = inventorySlots.First();
            return inventorySlots.Aggregate((closestInventorySlot, nextInventorySlot) => (position - nextInventorySlot.transform.position).magnitude < (position - closestInventorySlot.transform.position).magnitude ? nextInventorySlot : closestInventorySlot);
        }
    }
}