using GluonGui.WorkspaceWindow.Views.WorkspaceExplorer;
using SimpleRPG.InventorySystem;
using Unity.VisualScripting;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// The UI representation of an item in the inventory.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ItemIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>
        /// The item this icon represents
        /// </summary>
        public Item Item;

        /// <summary>
        /// The <see cref="Image"/> component used to display our items <see cref="Sprite"/>.
        /// </summary>
        private Image iconImage;

        /// <summary>
        /// The original parent of this ItemIcon before we started dragging.
        /// </summary>
        private Transform originalParent;

        /// <summary>
        /// The original position of this ItemIcon before we started dragging.
        /// </summary>
        private Vector3 originalPosition;

        /// <summary>
        /// The offset relative to the world origin from the mouse position when this ItemIcon is dragged.
        /// This is used to ensure that grabbed items maintain their offset as they're dragged around.
        /// </summary>
        private Vector3 dragOffset;

        /// <summary>
        /// The InventoryUI that owns this <see cref="ItemIcon"/>.
        /// </summary>
        [SerializeField]
        [Tooltip("The InventoryUI that owns this ItemIcon.")]
        private InventoryUI inventoryUI;

        /// <summary>
        /// The padding around the item icon.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The padding around the item icon.")]
        RectOffset Padding { get; set; }

        ///<inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            originalParent = transform.parent;
            originalPosition = transform.localPosition;

            transform.SetParent(transform.root);
            iconImage.raycastTarget = false;

            // Calculate the offset from the mouse position from the items origin.
            dragOffset = transform.position - Input.mousePosition;

            //Ensure that the target slot is rendered ontop of all of the other slots, this ensures that the item is always visible.
            this.transform.SetAsLastSibling();
        }

        public void Update()
        {
        }

        ///<inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            transform.position = Input.mousePosition + dragOffset;
        }

        ///<inheritdoc/>
        public void OnEndDrag(PointerEventData eventData)
        {
            InventoryUI targetInventoryUI = null;

            // The position we want this ItemIcon to snap to.
            Vector3 snapPosition = originalPosition;

            //Allow the ItemIcon to be hit by raycasts again.
            iconImage.raycastTarget = true;


            //Iterate over all of the inventory views and find the one that the user dropped the item on if any.
            foreach (InventoryUI inventory in FindObjectsByType<InventoryUI>(FindObjectsSortMode.None))
            {
                RectTransform inventoryRectTransform = inventory.GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(inventoryRectTransform, Input.mousePosition, null))
                {
                    // The cell position the ItemIcon was dropped on.
                    Vector2Int targetCellPosition = inventory.ScreenToCellPosition(Vector2Int.RoundToInt(Input.mousePosition), inventory);
                    Vector3 relativeLocalPosition = inventory.FindItemPlacementLocation(targetCellPosition, Item.ItemSize);

                    targetInventoryUI = inventory;

                    // Move the ItemIcon to the target slots position and make the itemIcon a child of the target inventor sloty.
                    inventoryUI.RemoveItem(this);
                    targetInventoryUI.AddItem(this, targetCellPosition);

                    this.transform.localPosition = relativeLocalPosition;

                    return;
                }
            }

            // If it wasn't dragged and dropped onto an inventory send it back to where it started.
            transform.SetParent(originalParent);
            transform.localPosition = originalPosition;
        }

        void OnEnable()
        {
            this.iconImage = GetComponent<Image>();

            // Ensure that the ItemIcon is always rendered ontop.
            this.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Sets this <see cref="ItemIcon"/>'s owning inventory.
        /// </summary>
        /// <param name="inventory"> The inventory we want this ItemIcon to be a part of.</param>
        public void SetParentInventory(InventoryUI inventory)
        {
            inventoryUI = inventory;
        }

        /// <summary>
        /// Sets the item this <see cref="ItemIcon"/> represents.
        /// </summary>
        /// <param name="item"> The item this Icon is going to represent.</param>
        public void SetItem(Item item)
        {
            this.Item = item;

            // Check to see if the iconImage is null.
            // Since SetItem can be called immediately after instantiation there's no opportunity to run Awake();
            if (iconImage == null)
            {
                this.iconImage = GetComponent<Image>();
            }

            //Set the sprite.
            iconImage.sprite = Item.Icon;

            // The items scale in Unity units.
            Vector2 itemScale = (iconImage.sprite.rect.size / iconImage.sprite.pixelsPerUnit);

            //Update the Icons size to properly reflect it's new item.
            this.GetComponent<RectTransform>().sizeDelta = (inventoryUI.CellSize * item.ItemSize * itemScale) - new Vector2(Padding.horizontal, Padding.vertical);
        }
    }
}
