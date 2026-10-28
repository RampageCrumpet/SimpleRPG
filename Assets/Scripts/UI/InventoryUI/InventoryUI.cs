using UnityEngine;
using SimpleRPG.InventorySystem;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

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
        public bool HasInventory => Inventory != null;



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
        [Tooltip("The inventory grid image.")]
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
        public Vector2 CellSize { get; private set; }

        /// <summary>
        /// The side of every inventory cell in canvas units, in every view, so items move between inventories at one scale.
        /// </summary>
        public const float ReferenceCellSize = 64f;

        void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (inventoryUIRectTransform != null)
            {
                return;
            }

            inventoryGridRectTransform = inventoryGridImage.gameObject.GetComponent<RectTransform>();
            inventoryGridBackdropRectTransform = inventoryGridBackdrop.gameObject.GetComponent<RectTransform>();
            inventoryUIRectTransform = this.gameObject.GetComponent<RectTransform>();
        }

        /// <summary>
        /// The size of this view in canvas units: the grid of the inventory it shows.
        /// </summary>
        public Vector2 GridSize => inventoryUIRectTransform.sizeDelta;

        /// <summary>
        /// Updates the grid size of the inventory. Where the view sits on screen is the screen layout's business, not this view's.
        /// </summary>
        /// <param name="inventory"> The inventory we want to update our grid size to match.</param>
        private void UpdateGridSize(Inventory inventory)
        {
            // Scale the tiled grid sprite so that one tile is exactly one cell.
            float referencePixelsPerUnit = inventoryGridImage.canvas.referencePixelsPerUnit;
            Sprite sprite = inventoryGridImage.sprite;
            inventoryGridImage.pixelsPerUnitMultiplier = sprite.rect.width / sprite.pixelsPerUnit * referencePixelsPerUnit / ReferenceCellSize;
            CellSize = new Vector2(ReferenceCellSize, ReferenceCellSize);

            // The size of the inventory we want to display.
            Vector2 gridInventorySize = (Vector2)inventory.InventorySize * CellSize;

            inventoryGridRectTransform.sizeDelta = gridInventorySize;
            inventoryGridBackdropRectTransform.anchorMin = Vector2.zero;
            inventoryGridBackdropRectTransform.anchorMax = Vector2.one;
            inventoryGridBackdropRectTransform.offsetMin = Vector2.zero;
            inventoryGridBackdropRectTransform.offsetMax = Vector2.zero;
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
            Vector2Int cellPosition = Vector2Int.FloorToInt((localPosition - targetUI.inventoryGridRectTransform.rect.min) / targetUI.CellSize);
            return cellPosition;
        }

        /// <summary>
        /// Gets the cell an item's origin (its smallest x/y cell) lands on when its centre is at the given screen position,
        /// so a drop is judged by where the item sits rather than by the cell under the cursor.
        /// </summary>
        public Vector2Int ScreenToItemOrigin(Vector2 itemCentreScreenPosition, Vector2Int itemSize)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(inventoryGridRectTransform, itemCentreScreenPosition, null, out Vector2 localPosition);
            return Vector2Int.RoundToInt(((localPosition - inventoryGridRectTransform.rect.min) / CellSize) - ((Vector2)itemSize / 2f));
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
            Vector2 screenPosition = inventoryGridRectTransform.rect.min + (((Vector2)cellPosition + ((Vector2)itemSize / 2)) * CellSize);
            return screenPosition;
        }

        /// <summary>
        /// Asks for the item shown by an <see cref="ItemIcon"/> to be moved into another inventory view.
        /// The inventory validates the move (on the server for networked inventories) and raises its Changed event when it
        /// succeeds, at which point every view redraws. Nothing is moved on screen here, so a rejected move leaves the UI untouched.
        /// </summary>
        /// <param name="itemIcon"> The ItemIcon being moved.</param>
        /// <param name="target"> The view of the inventory the item was dropped on.</param>
        /// <param name="position"> The cell the item was dropped on.</param>
        public void RequestMoveItem(ItemIcon itemIcon, InventoryUI target, Vector2Int position)
        {
            Inventory.RequestMoveItem(itemIcon.Item, target.Inventory, position);
        }

        /// <summary>
        /// Asks for an item in this inventory to be worn by the character that owns it. Inventories without equipment (a chest) do nothing.
        /// </summary>
        public void RequestEquip(Item item)
        {
            if (Inventory != null && Inventory.TryGetComponent(out Equipment equipment))
            {
                equipment.RequestEquip(item);
            }
        }

        /// <summary>
        /// Sets the <see cref="Inventory"/> the UI is represnting and updates the UI to display it.
        /// </summary>
        /// <param name="inventory"> The Inventory we want to display.</param>
        public void SetInventory(Inventory inventory)
        {
            if (this.Inventory != null)
            {
                this.Inventory.Changed -= RefreshItemIcons;
            }

            EnsureReferences();
            this.Inventory = inventory;
            inventory.Changed += RefreshItemIcons;
            UpdateGridSize(inventory);
            CreateItemIcons(inventory);
        }

        /// <summary>
        /// Redraws the item icons after the inventory's contents changed.
        /// </summary>
        private void RefreshItemIcons()
        {
            CreateItemIcons(Inventory);
        }

        private void OnDestroy()
        {
            if (Inventory != null)
            {
                Inventory.Changed -= RefreshItemIcons;
            }
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
                itemIcon.transform.localPosition = itemPosition;

                itemIcons.Add(itemIcon);
            }
        }
    }
}
