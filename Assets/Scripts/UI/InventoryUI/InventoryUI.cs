using UnityEngine;
using SimpleRPG.InventorySystem;
using UnityEngine.UI;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
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
        private InventorySystem.Inventory Inventory { get; set; }



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

        /// <summary>
        /// A collection of all InventorySlot's currently contained within this InventoryUI.
        /// </summary>
        private List<InventorySlot> inventorySlots = new List<InventorySlot>();

        /// <summary>
        /// A collection of all <see cref="ItemIcon"/>'s contained within this InventoryUI.
        /// </summary>
        private List<ItemIcon> itemIcons = new List<ItemIcon>();

        [field: SerializeField]
        [Tooltip("The inventory gird image.")]
        private Image inventoryGridImage;

        [field: SerializeField]
        [Tooltip("The inventory grid backdrop image.")] 
        private Image inventoryGridBackdrop;

        /// <summary>
        /// The RectTransform of the inventory grid image.
        /// </summary>
        private RectTransform inventoryGridRectTransform;

        /// <summary>
        /// The RectTransform of the inventory grid backdrop image.
        /// </summary>
        private RectTransform inventoryGridBackdropTransform;

        /// <summary>
        /// The size of an individual cell on our screen.
        /// </summary>
        private Vector2 cellSize;

        void Start()
        {
            inventoryGridRectTransform = inventoryGridImage.gameObject.GetComponent<RectTransform>();
            inventoryGridBackdropTransform = inventoryGridBackdrop.gameObject.GetComponent<RectTransform>();

            // Finds the size in pixels of a 1 unity unit object.
            float canvasScaleFactor = this.GetComponentsInParent<Canvas>().Select(x =>  x.referencePixelsPerUnit/ x.scaleFactor).Aggregate(1f, (acc, value) => acc * value);
           
            // CellSize is equal to the ratio between the sprite and it's pixels per unit, scaled by the canvas and image's scale factors, then scaled byt
            cellSize = (inventoryGridImage.sprite.rect.size/inventoryGridImage.sprite.pixelsPerUnit) * canvasScaleFactor / inventoryGridImage.pixelsPerUnitMultiplier;

            // Hide this InventoryUI until it's needed.
            this.gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates the grid size of the inventory.
        /// </summary>
        /// <param name="inventory"> The inventory we want to update our grid size to match.</param>
        private void UpdateGridSize(Inventory inventory)
        {
            inventoryGridRectTransform.sizeDelta = (Vector2)inventory.InventorySize * cellSize;
            inventoryGridBackdropTransform.sizeDelta = (Vector2)inventory.InventorySize * cellSize;
        }

        /// <summary>
        /// Gets the cell position at the given screen position.
        /// </summary>
        /// <param name="screenPosition"> The screen position we want to find the cel position of.</param>
        /// <returns> The X/Y position of the cell at the given screen position.</returns>
        public Vector2Int ScreenToCellPosition(Vector2Int screenPosition)
        {
            // Find the position relative to our origin.
            Vector2 localPosition = new Vector2(screenPosition.x - this.transform.position.x, screenPosition.y - this.transform.position.y);

            // Divide by the cellSize to find the cell position and round to an int to discard any remainders.
            Vector2Int cellPosition = Vector2Int.RoundToInt(localPosition / cellSize);
            return cellPosition;
        }

        /// <summary>
        /// Finds the world position of the given cell.
        /// </summary>
        /// <param name="cellPosition"> The cell we want to find the world position for.</param>
        /// <returns> The world position of the cell.</returns>
        public Vector2 CellToScreenPosition(Vector2Int cellPosition)
        {
            // Find the position relative to our origin.
            Vector2 screenPosition = (cellPosition*cellSize) + (Vector2)this.transform.position;
            return screenPosition;
        }

        /// <summary>
        /// Adds an Item to the inventory at the given position.
        /// </summary>
        /// <param name="itemIcon"> The ItemIcon we want to source the Item from..</param>
        /// <param name="position"> The position we want to add the item to.</param>
        public void AddItem(ItemIcon itemIcon, Vector2Int position)
        {
            // Add the item to the backing data model.
            Inventory.AddItem(itemIcon.Item, position);
            this.itemIcons.Add(itemIcon);
        }

        /// <summary>
        /// Removes an ItemIcon from the inventory.
        /// </summary>
        /// <param name="itemIcon"> The ItemIcon we want to remove from.</param>
        public void RemoveItem(ItemIcon itemIcon)
        {
            // Remove the item from the backing data model.
            Inventory.RemoveItem(itemIcon.Item);
            this.itemIcons.Remove(itemIcon);
        }

        /// <summary>
        /// Sets the <see cref="Inventory"/> the UI is represnting and updates the UI to display it.
        /// </summary>
        /// <param name="inventory"> The Inventory we want to display.</param>
        public void SetInventory(Inventory inventory)
        {
            this.Inventory = inventory;
            UpdateGridSize(inventory);
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
                Vector3 itemPosition = this.transform.position + (Vector3)(position*cellSize);

                // Create a new inventory icon for each item
                GameObject icon = Instantiate(ItemIconPrefab, this.transform.position, this.transform.rotation);
                ItemIcon itemIcon = icon.GetComponent<ItemIcon>();
                itemIcon.SetItem(item);
                itemIcon.SetParentInventory(this);
            }
        }
    }
}