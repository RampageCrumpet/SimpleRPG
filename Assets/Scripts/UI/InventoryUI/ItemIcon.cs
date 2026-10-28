using SimpleRPG.Controls;
using SimpleRPG.InventorySystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// The UI representation of an item in the inventory.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ItemIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        /// <summary>
        /// Right-clicking an item asks to wear it. Only armor in the player's own inventory can be; anything else is refused by the equipment rules.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && inventoryUI != null)
            {
                inventoryUI.RequestEquip(Item);
            }
        }

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
            dragOffset = transform.position - (Vector3)GameInput.PointerPosition;

            //Ensure that the target slot is rendered ontop of all of the other slots, this ensures that the item is always visible.
            this.transform.SetAsLastSibling();
        }

        ///<inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            transform.position = (Vector3)GameInput.PointerPosition + dragOffset;
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
            foreach (InventoryUI inventory in FindObjectsByType<InventoryUI>())
            {
                RectTransform inventoryRectTransform = inventory.GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(inventoryRectTransform, GameInput.PointerPosition, null))
                {
                    // The cell the item's origin lands on, judged from where the icon sits (it keeps its grab offset while dragging).
                    Vector2Int targetCellPosition = inventory.ScreenToItemOrigin(transform.position, Item.ItemSize);
                    targetInventoryUI = inventory;

                    // Ask the inventory to move the item. The icon goes back to where it started; if the move is accepted the
                    // inventories raise Changed and every view redraws its icons from the new contents.
                    inventoryUI.RequestMoveItem(this, targetInventoryUI, targetCellPosition);
                    break;
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

            iconImage.preserveAspect = true;

            //Update the Icons size to properly reflect it's new item.
            this.GetComponent<RectTransform>().sizeDelta = (inventoryUI.CellSize * item.ItemSize) - new Vector2(Padding.horizontal, Padding.vertical);
        }
    }
}
