using UnityEngine;
using Inventory;
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
        public Inventory.Inventory inventory;

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
                gridLayout.constraintCount = inventory.inventorySize.x;
            }
            else if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                gridLayout.constraintCount = inventory.inventorySize.y;
            }
            else
            {
                Debug.LogError(this.gameObject.name + " has an unrecognized GridLayoutGroup constraint."); 
            }

            CreateBackingGrid();
        }

        /// <summary>
        /// Creates a grid with the width and height of the specified inventory.
        /// </summary>
        private void CreateBackingGrid()
        {
            for (int x = 0; x < inventory.inventorySize.x; x++)
            {
                for (int y = 0; y < inventory.inventorySize.y; y++)
                {
                    GameObject newUISquarePrefab = Object.Instantiate(UISquarePrefab);
                    newUISquarePrefab.transform.SetParent(gridLayout.transform, false);

                    InventorySlot newInventorySlot = newUISquarePrefab.GetComponent<InventorySlot>();
                    inventorySlots.Add(newInventorySlot);
                    newInventorySlot.position = new Vector2Int(x, y);
                }
            }
        }

        public void RemoveItemIcon(ItemIcon itemIcon)
        {
            itemIcon.transform.SetParent(null);
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
                return null; // Handle the case where there are no inventory slots.
            }

            //InventorySlot closestInventorySlot = inventorySlots.First();
            return inventorySlots.Aggregate((closestInventorySlot, nextInventorySlot) => (position - nextInventorySlot.transform.position).magnitude < (position - closestInventorySlot.transform.position).magnitude ? nextInventorySlot : closestInventorySlot);
        }
    }
}