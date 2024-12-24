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
            dragOffset = transform.position - Input.mousePosition;
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
            InventoryUI targetInventory = null;
            InventorySlot targetSlot = null;

            // Iterate over all inventory views
            foreach (InventoryUI inventory in FindObjectsByType<InventoryUI>(FindObjectsSortMode.None))
            {
                RectTransform inventoryRectTransform = inventory.GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(inventoryRectTransform, Input.mousePosition, null))
                {
                    // Find the InventorySlot underneath this ItemIcon
                    targetSlot = inventory.FindInventorySlotClosestToPosition(this.transform.position);

                    if (targetSlot != null)
                    {
                        targetInventory = inventory;
                        break;
                    }
                }
            }

            //If the user dropped the ItemIcon on a valid slot and the Icon can be added there add it.
            if (targetSlot != null && targetInventory.Inventory.ValidateItemPlacement(Item, targetSlot.position))
            {
                // Move the ItemIcon to the target slots position and make the itemIcon a child of the target inventory.
                this.transform.SetParent(targetInventory.transform, false);
                this.transform.position = targetSlot.transform.position;
                inventoryUI.RemoveItemIcon(this);
                targetInventory.AddItemIcon(this, targetSlot);
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
            iconImage.sprite = Item.Icon;
        }
    }
}
