using SimpleRPG.InventorySystem;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// One square on the paper doll: the icon of what is worn in a slot, or the slot's name while it is empty.
    /// It holds no equipment state; <see cref="PaperDollUI"/> tells it what to show and what a click means.
    /// </summary>
    public class EquipmentSlotView : MonoBehaviour, IPointerClickHandler
    {
        private static readonly Color EmptyColor = new Color(0.16f, 0.16f, 0.16f, 0.9f);

        private static readonly Color FilledColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);

        /// <summary>
        /// The slot this view stands for.
        /// </summary>
        public EquipmentSlot Slot { get; private set; }

        /// <summary>
        /// The item currently shown, or null while the slot is empty.
        /// </summary>
        public Item DisplayedItem { get; private set; }

        /// <summary>
        /// Raised when the player clicks the slot with the left or right mouse button.
        /// </summary>
        public event Action<EquipmentSlotView> Clicked;

        private Image background;

        private Image icon;

        private TextMeshProUGUI label;

        /// <summary>
        /// True while the empty-slot indicator (the slot's name) is showing.
        /// </summary>
        public bool ShowsEmptyIndicator => label != null && label.gameObject.activeSelf;

        /// <summary>
        /// Creates the view's squares under <paramref name="parent"/>.
        /// </summary>
        public static EquipmentSlotView Create(RectTransform parent, EquipmentSlot slot, Vector2 anchoredPosition, float size)
        {
            GameObject root = new GameObject(slot + "Slot", typeof(RectTransform), typeof(Image), typeof(EquipmentSlotView));
            root.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);

            EquipmentSlotView view = root.GetComponent<EquipmentSlotView>();
            view.Slot = slot;
            view.background = root.GetComponent<Image>();

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);
            RectTransform iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(6f, 6f);
            iconRect.offsetMax = new Vector2(-6f, -6f);
            view.icon = iconObject.GetComponent<Image>();
            view.icon.preserveAspect = true;
            view.icon.raycastTarget = false;

            GameObject labelObject = new GameObject("EmptyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            view.label = labelObject.GetComponent<TextMeshProUGUI>();
            view.label.text = char.ToUpper(slot.ToString()[0]) + slot.ToString().Substring(1);
            view.label.alignment = TextAlignmentOptions.Center;
            view.label.fontSize = 18f;
            view.label.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            view.label.raycastTarget = false;

            view.SetItem(null);
            return view;
        }

        /// <summary>
        /// Positions and sizes the square, from the centre of its parent.
        /// </summary>
        public void Place(Vector2 anchoredPosition, float size)
        {
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);
            label.fontSize = Mathf.Max(14f, size * 0.2f);
        }

        /// <summary>
        /// Shows an item in the slot, or the empty indicator for null.
        /// </summary>
        public void SetItem(Item item)
        {
            DisplayedItem = item;
            icon.sprite = item != null ? item.Icon : null;
            icon.enabled = item != null;
            label.gameObject.SetActive(item == null);
            background.color = item != null ? FilledColor : EmptyColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left || eventData.button == PointerEventData.InputButton.Right)
            {
                Clicked?.Invoke(this);
            }
        }

        /// <summary>
        /// Clicks the slot as the player would. Lets tests and tools drive the view without a pointer.
        /// </summary>
        public void Click()
        {
            Clicked?.Invoke(this);
        }
    }
}
