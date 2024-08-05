using Inventory;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// The UI representation of an item in the inventory.
    /// </summary>
    public class ItemIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
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

        [SerializeField]
        /// <summary>
        /// The InventoryUI that owns this <see cref="ItemIcon"/>.
        /// </summary>
        private InventoryUI inventoryUI;

        ///<inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            originalParent = transform.parent;
            transform.SetParent(transform.root);
            iconImage.raycastTarget = false;
        }

        ///<inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            transform.position = Input.mousePosition;
        }

        ///<inheritdoc/>
        public void OnDrop(PointerEventData eventData)
        {
            ItemIcon droppedItemIcon = eventData.pointerDrag.GetComponent<ItemIcon>();
            if (droppedItemIcon != null)
            {
                InventorySlot targetSlot = GetComponentInParent<InventorySlot>();
                InventorySlot originalSlot = droppedItemIcon.GetComponentInParent<InventorySlot>();

                if (targetSlot != null && originalSlot != null)
                {
                    //Swap the items.
                }
            }
        }

        ///<inheritdoc/>
        public void OnEndDrag(PointerEventData eventData)
        {
            Vector2 worldPosition = (Vector2)Input.mousePosition;
            InventoryUI targetInventory = null;
            InventorySlot targetSlot = null;

            // Iterate over all inventory views
            InventoryUI[] inventoryViews = FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
            foreach (InventoryUI inventory in inventoryViews)
            {
                RectTransform inventoryRectTransform = inventory.GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(inventoryRectTransform, Input.mousePosition, null))
                {
                    // The mouse is over this inventory, find the corresponding slot
                    targetSlot = inventory.FindInventorySlotAtScreenPosition(Input.mousePosition);
                    if (targetSlot != null)
                    {
                        targetInventory = inventory;
                        break;
                    }
                }
            }

            if (targetSlot != null)
            {
                if (inventoryUI != targetInventory)
                {
                    inventoryUI.RemoveItemIcon(this); // Remove the item from the original inventory
                }
                targetInventory.AddItemIconToSlot(this, targetSlot); // Add the item to the new inventory
            }
            else
            {
                transform.SetParent(originalParent);
                transform.localPosition = Vector3.zero;
            }

            iconImage.raycastTarget = true;
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            this.iconImage = GetComponent<Image>();

            // Ensure that the ItemIcon is always rendered ontop.
            this.transform.SetAsLastSibling();
        }

        public void SetParentInventory(InventoryUI inventory)
        {
            inventoryUI = inventory;
        }
    }
}
