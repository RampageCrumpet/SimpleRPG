using UnityEngine;
using SimpleRPG.InventorySystem;
using UnityEngine.UI;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphs;
using static UnityEngine.GraphicsBuffer;

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
        private RectTransform inventoryGridBackdropRectTransform;

        /// <summary>
        /// This InventoryUI's RectTransform.
        /// </summary>
        private RectTransform inventoryUIRectTransform;

        /// <summary>
        /// The size of an individual cell on our screen.
        /// </summary>
        [Tooltip("The size of an individual cell on our screen.")]
        public Vector2 CellSize { get; private set; }

        void Start()
        {
            inventoryGridRectTransform = inventoryGridImage.gameObject.GetComponent<RectTransform>();
            inventoryGridBackdropRectTransform = inventoryGridBackdrop.gameObject.GetComponent<RectTransform>();
            inventoryUIRectTransform = this.gameObject.GetComponent<RectTransform>();

            // Hide this InventoryUI until it's needed.
            this.gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates the grid size of the inventory.
        /// </summary>
        /// <param name="inventory"> The inventory we want to update our grid size to match.</param>
        private void UpdateGridSize(Inventory inventory)
        {
            // Finds the size in pixels of a 1 unity unit object.
            float canvasScaleFactor = this.GetComponentsInParent<Canvas>().Select(x => x.referencePixelsPerUnit / x.scaleFactor).Aggregate(1f, (acc, value) => acc * value);

            // CellSize is equal to the ratio between the sprite and it's pixels per unit, scaled by the canvas and image's scale factors, then scaled byt
            CellSize = (inventoryGridImage.sprite.rect.size / inventoryGridImage.sprite.pixelsPerUnit) * canvasScaleFactor / inventoryGridImage.pixelsPerUnitMultiplier;

            // The size of the inventory we want to display.
            Vector2 gridInventorySize = (Vector2)inventory.InventorySize * CellSize;

            inventoryGridRectTransform.sizeDelta = gridInventorySize;
            inventoryGridBackdropRectTransform.sizeDelta = gridInventorySize;
            inventoryUIRectTransform.sizeDelta = gridInventorySize;
        }

        /// <summary>
        /// Gets the cell position at the given screen position.
        /// </summary>
        /// <param name="screenPosition"> The screen position we want to find the cel position of.</param>
        /// <returns> The X/Y position of the cell at the given screen position.</returns>
        public Vector2Int ScreenToCellPosition(Vector2 screenPosition, InventoryUI targetUI)
        {
            Vector2 localPosition = new Vector2();
            // Find the position relative to our origin.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(targetUI.inventoryGridRectTransform, screenPosition, null,out localPosition);
            //Vector2 localPosition = new Vector2(screenPosition.x - this.transform.position.x, screenPosition.y - this.transform.position.y);

            // Divide by the cellSize to find the cell position and round to an int to discard any remainders.
            Vector2Int cellPosition = Vector2Int.FloorToInt(localPosition / CellSize);
            return cellPosition;
        }

        /// <summary>
        /// Finds the location relative to the given InventoryUI's origin where we'd want to place an item to have it match up with the given cell position.
        /// </summary>
        /// <param name="cellPosition"> The location we want to place the item in inventory cells.</param>
        /// <param name="itemSize"> The size in inventory cells we want to place the item into.</param>
        /// <returns>A vector2 telling us where the item should be placed relative to the given <see cref="InventoryUI"/> to match the cell position.</returns>
        public Vector2 FindItemPlacementLocation(Vector2Int cellPosition, Vector2Int itemSize)
        {
            // Find the position relative to our origin.
            Vector2 screenPosition = (((Vector2)cellPosition + ((Vector2)itemSize / 2)) * CellSize);
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

            // Update the visuals so the item is displayed properly.
            itemIcon.transform.SetParent(inventoryGridBackdropRectTransform);
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
                Destroy(icon.gameObject);
            }
            itemIcons.Clear();

            // Get all items from the inventory
            foreach (var (item, position) in inventory.GetItems())
            {
                Vector3 itemPosition = FindItemPlacementLocation(position, item.ItemSize);

                // Create a new inventory icon for each item
                GameObject icon = Instantiate(ItemIconPrefab, this.transform);
                icon.transform.localPosition = itemPosition;
                ItemIcon itemIcon = icon.GetComponent<ItemIcon>();
                itemIcon.SetParentInventory(this);
                itemIcon.SetItem(item);
                itemIcon.transform.SetParent(this.transform);
                itemIcon.transform.position = itemPosition;

                itemIcons.Add(itemIcon);
            }
        }
    }
}