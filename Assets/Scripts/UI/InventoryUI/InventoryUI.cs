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
        [field: SerializeField]
        [Tooltip("The backing inventory.")]
        public InventorySystem.Inventory Inventory { get; private set; }

        /// <summary>
        /// The grid layout group governing control over this inventory.
        /// </summary>
        private GridLayoutGroup gridLayout;

        /// <summary>
        /// The prefab to be instantiated for every grid element.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The icon to use for each slot in the inventory.")]
        private GameObject UISquarePrefab { get; set; }

        /// <summary>
        /// The prefab to be instantiated for every ItemIcon.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The Item Icon prefab to be used to create images of the items.")]
        private GameObject ItemIconPrefab {get; set; }

        private List<InventorySlot> inventorySlots = new List<InventorySlot>();

        private List<ItemIcon> itemIcons = new List<ItemIcon>();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if(Inventory == null)
            {
                Debug.LogError(this.gameObject.name + " has no inventory attached to render.");
            }

            // Create a grid layout that matches the inventory layout.
            gridLayout = GetComponentInChildren<GridLayoutGroup>();
            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                gridLayout.constraintCount = Inventory.InventorySize.x;
            }
            else if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                gridLayout.constraintCount = Inventory.InventorySize.y;
            }
            else
            {
                Debug.LogError(this.gameObject.name + " has an unrecognized GridLayoutGroup constraint and may not layout properly."); 
            }
        }

        /// <summary>
        /// Creates a grid with the width and height of the specified inventory.
        /// </summary>
        private void CreateBackingGrid()
        {
            if(gridLayout == null)
            {
                gridLayout = GetComponentInChildren<GridLayoutGroup>();
            }

            // Clear existing grid
            foreach (Transform child in gridLayout.transform)
            {
                Destroy(child.gameObject);
            }
            inventorySlots.Clear();

            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                for (int x = 0; x < Inventory.InventorySize.x; x++)
                {
                    for (int y = 0; y < Inventory.InventorySize.y; y++)
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
                for  (int y = 0; y < Inventory.InventorySize.y; y++)
                {
                    for (int x = 0; x < Inventory.InventorySize.x; x++)
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
            Inventory.AddItem(itemIcon.Item, inventorySlot.position);
        }

        /// <summary>
        /// Removes an ItemIcon from the inventory.
        /// </summary>
        /// <param name="itemIcon"> The ItemIcon we want to remove from this inventoryUI.</param>
        public void RemoveItemIcon(ItemIcon itemIcon)
        {
            // Remove the item from the backing data model.
            Inventory.RemoveItem(itemIcon.Item);
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

        /// <summary>
        /// Sets the <see cref="Inventory"/> the UI is represnting and updates the UI to display it.
        /// </summary>
        /// <param name="inventory"> The Inventory we want to display.</param>
        public void SetInventory(Inventory inventory)
        {
            this.Inventory = inventory;
            CreateBackingGrid();
            CreateItemIcons(inventory);
        }

        /// <summary>
        /// Removes all of the existing <see cref="ItemIcon"/>'s and recreates them based on the new inventory.
        /// </summary>
        /// <param name="inventory"> The inventory we want to draw <see cref="ItemIcon"/>'s for.</param>
        private void CreateItemIcons(Inventory inventory)
        {
            // Clear out all existing inventory icons
            foreach (var icon in itemIcons)
            {
                Destroy(icon);
            }
            itemIcons.Clear();

            // Get all items from the inventory
            foreach (var (item, position) in inventory.GetItems())
            {
                // Create a new inventory icon for each item
                GameObject icon = Instantiate(ItemIconPrefab, gridLayout.transform);
                ItemIcon itemIcon = icon.GetComponent<ItemIcon>();
                itemIcon.SetItem(item);
                itemIcon.SetParentInventory(this);

                // Find the corresponding inventory slot
                InventorySlot slot = inventorySlots.FirstOrDefault(s => s.position == position);
                if (slot != null)
                {
                    icon.transform.position = slot.transform.position;
                }

                itemIcons.Add(itemIcon);
            }
        }
    }
}