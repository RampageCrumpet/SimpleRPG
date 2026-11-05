using SimpleRPG.Controls;
using SimpleRPG.InventorySystem;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// The tooltip for an item: its name with its affixes, what it grants, what each affix rolled and its reactions (see <see cref="ItemTooltip"/>).
    /// One panel is built in code under the canvas the first time it is needed, follows the pointer while it is shown, and never takes a click.
    /// </summary>
    public sealed class ItemTooltipView : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.05f, 0.05f, 0.07f, 0.95f);

        private static readonly Vector2 PointerOffset = new Vector2(18f, -18f);

        private static ItemTooltipView current;

        private RectTransform rect;
        private RectTransform canvasRect;
        private TextMeshProUGUI text;

        /// <summary>What the panel says right now, or empty while hidden. For tests.</summary>
        public static string CurrentText => current != null && current.gameObject.activeSelf ? current.text.text : string.Empty;

        /// <summary>Shows the tooltip of an item, near the pointer, under the canvas that <paramref name="from"/> is on.</summary>
        public static void Show(Item item, Transform from)
        {
            if (item == null || from == null)
            {
                return;
            }

            Canvas canvas = from.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas = canvas.rootCanvas;
            if (current == null || current.canvasRect != (RectTransform)canvas.transform)
            {
                current = Create(canvas);
            }

            current.text.text = Compose(ItemTooltip.Build(item));
            current.gameObject.SetActive(true);
            current.transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(current.rect);
            current.Follow();
        }

        /// <summary>Hides the tooltip.</summary>
        public static void Hide()
        {
            if (current != null)
            {
                current.gameObject.SetActive(false);
            }
        }

        /// <summary>The lines as one block of text, each coloured for its kind.</summary>
        public static string Compose(System.Collections.Generic.IReadOnlyList<TooltipLine> lines)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) builder.Append('\n');
                string color;
                switch (lines[i].Kind)
                {
                    case TooltipLineKind.Name: color = "#FFFFFF"; break;
                    case TooltipLineKind.Affix: color = "#7DA8FF"; break;
                    case TooltipLineKind.Reaction: color = "#FFB060"; break;
                    default: color = "#B8B8B8"; break;
                }

                string line = lines[i].Kind == TooltipLineKind.Name ? "<b>" + lines[i].Text + "</b>" : lines[i].Text;
                builder.Append("<color=").Append(color).Append('>').Append(line).Append("</color>");
            }

            return builder.ToString();
        }

        private static ItemTooltipView Create(Canvas canvas)
        {
            GameObject root = new GameObject("ItemTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(ItemTooltipView));
            root.transform.SetParent(canvas.transform, false);

            ItemTooltipView view = root.GetComponent<ItemTooltipView>();
            view.canvasRect = (RectTransform)canvas.transform;
            view.rect = (RectTransform)root.transform;
            view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0.5f);
            view.rect.pivot = new Vector2(0f, 1f);

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            root.GetComponent<Image>().color = Background;
            root.GetComponent<Image>().raycastTarget = false;

            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            view.text = label.GetComponent<TextMeshProUGUI>();
            view.text.fontSize = 18f;
            view.text.raycastTarget = false;
            view.text.richText = true;

            root.SetActive(false);
            return view;
        }

        private void LateUpdate() => Follow();

        // Sits next to the pointer, kept inside the canvas.
        private void Follow()
        {
            if (canvasRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, GameInput.PointerPosition, null, out Vector2 local))
            {
                return;
            }

            Vector2 size = rect.rect.size;
            Vector2 half = canvasRect.rect.size * 0.5f;
            Vector2 position = local + PointerOffset;

            // Flip to the other side of the pointer where the panel would leave the canvas.
            if (position.x + size.x > half.x) position.x = local.x - PointerOffset.x - size.x;
            if (position.y - size.y < -half.y) position.y = local.y - PointerOffset.y + size.y;
            rect.anchoredPosition = position;
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
