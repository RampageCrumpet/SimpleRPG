using GluonGui.WorkspaceWindow.Views.WorkspaceExplorer;
using Inventory;
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

        /// <summary>
        /// The original position of this ItemIcon before we started dragging.
        /// </summary>
        private Vector3 originalPosition;

        /// <summary>
        /// The offset relative to the world origin from the mouse position when this ItemIcon is dragged.
        /// </summary>
        private Vector3 dragOffset;

        [SerializeField]
        /// <summary>
        /// The InventoryUI that owns this <see cref="ItemIcon"/>.
        /// </summary>
        private InventoryUI inventoryUI;

        ///<inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            originalParent = transform.parent;
            originalPosition = transform.localPosition;

            transform.SetParent(transform.root);
            iconImage.raycastTarget = false;

            // Calculate the offset from the mouse position to the icon's top-left corner
            dragOffset = transform.position- Input.mousePosition;
        }

        public void Update()
        {
            Debug.Log("MousePos:" + Input.mousePosition);

        }

        ///<inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            transform.position = Input.mousePosition + dragOffset;
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
            InventoryUI targetInventory = null;
            InventorySlot targetSlot = null;

            // Iterate over all inventory views
            foreach (InventoryUI inventory in FindObjectsByType<InventoryUI>(FindObjectsSortMode.None))
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

            //If the user dropped the ItemIcon on a valid slot and the Icon can be added there add it.
            if (targetSlot != null && targetInventory.inventory.AddItem(Item, targetSlot.position))
            {
                if (inventoryUI != targetInventory)
                {
                    inventoryUI.RemoveItemIcon(this); // Remove the item from the original inventory
                }

                //Find the size of the image so we can properly place it.
                Vector3 imageOffset = new Vector3(iconImage.rectTransform.rect.size.x, iconImage.rectTransform.rect.size.y, 0)/2;

                // Move the ItemIcon to the target slots position and make the itemIcon a child of the target inventory.
                this.transform.SetParent(targetInventory.transform, false);
                this.transform.position = targetSlot.transform.position + imageOffset;
            }
            //Otherwise send the item icon back to where it started from.
            else
            {
                transform.SetParent(originalParent);
                transform.localPosition = originalPosition;
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
