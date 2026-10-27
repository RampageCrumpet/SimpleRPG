using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// What one buff slot shows.
    /// </summary>
    public readonly struct BuffEntry
    {
        public BuffEntry(string name, Sprite icon, int stacks)
        {
            Name = name;
            Icon = icon;
            Stacks = stacks;
        }

        public string Name { get; }

        public Sprite Icon { get; }

        public int Stacks { get; }
    }

    /// <summary>
    /// Something on a character that knows which effects are active on it. The ability system provides it; the UI only reads it.
    /// </summary>
    public interface IBuffSource
    {
        /// <summary>
        /// Replaces the contents of the list with the effects active now.
        /// </summary>
        void GetBuffs(List<BuffEntry> into);
    }

    /// <summary>
    /// The row of active effects above the health bar. Slots exist only for effects that are active: with none, the bar is hidden.
    /// It reads the local character's <see cref="IBuffSource"/>. Statuses live in the server's status manager today, so a remote client's bar
    /// stays empty until statuses are replicated (tracked in its own bead).
    /// </summary>
    public class BuffBarUI : MonoBehaviour
    {
        public const int MaxSlots = 8;

        private const float SlotSize = 48f;

        private static readonly Color SlotColor = new Color(0.14f, 0.17f, 0.2f, 0.95f);

        [SerializeField]
        [Tooltip("The strip behind the slots, hidden while no effect is active.")]
        private GameObject strip;

        [SerializeField]
        [Tooltip("Parent of the slots. Needs a horizontal layout group.")]
        private RectTransform slotContainer;

        private readonly List<GameObject> slots = new List<GameObject>();

        private readonly List<BuffEntry> shown = new List<BuffEntry>();

        private IBuffSource source;

        private readonly List<BuffEntry> scratch = new List<BuffEntry>();

        /// <summary>
        /// The number of slots currently visible.
        /// </summary>
        public int VisibleCount { get; private set; }

        /// <summary>
        /// Points the bar at the character whose statuses it shows.
        /// </summary>
        public void SetCharacter(Character character)
        {
            source = character != null ? character.GetComponent<IBuffSource>() : null;
        }

        private void Awake()
        {
            if (strip != null)
            {
                strip.SetActive(false);
            }
        }

        private void Update()
        {
            Show(ReadEntries());
        }

        private IReadOnlyList<BuffEntry> ReadEntries()
        {
            scratch.Clear();
            source?.GetBuffs(scratch);
            return scratch;
        }

        /// <summary>
        /// Shows exactly these effects, up to <see cref="MaxSlots"/>.
        /// </summary>
        public void Show(IReadOnlyList<BuffEntry> entries)
        {
            int count = Mathf.Min(entries.Count, MaxSlots);
            bool same = count == shown.Count;
            for (int i = 0; same && i < count; i++)
            {
                same = shown[i].Name == entries[i].Name && shown[i].Icon == entries[i].Icon && shown[i].Stacks == entries[i].Stacks;
            }

            if (strip != null)
            {
                strip.SetActive(count > 0);
            }

            if (same)
            {
                return;
            }

            shown.Clear();
            for (int i = 0; i < count; i++)
            {
                shown.Add(entries[i]);
            }

            while (slots.Count < count)
            {
                slots.Add(CreateSlot());
            }

            for (int i = 0; i < slots.Count; i++)
            {
                bool active = i < count;
                slots[i].SetActive(active);
                if (active)
                {
                    Fill(slots[i], entries[i]);
                }
            }

            VisibleCount = count;
            if (strip != null)
            {
                strip.SetActive(count > 0);
            }
        }

        private GameObject CreateSlot()
        {
            GameObject slot = new GameObject("BuffSlot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            slot.transform.SetParent(slotContainer, false);
            slot.GetComponent<Image>().color = SlotColor;
            LayoutElement element = slot.GetComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = SlotSize;

            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(slot.transform, false);
            Stretch((RectTransform)icon.transform, 4f);
            icon.GetComponent<Image>().preserveAspect = true;
            icon.GetComponent<Image>().raycastTarget = false;

            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(slot.transform, false);
            Stretch((RectTransform)label.transform, 2f);
            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.raycastTarget = false;
            return slot;
        }

        private static void Fill(GameObject slot, BuffEntry entry)
        {
            Image icon = slot.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = entry.Icon;
            icon.enabled = entry.Icon != null;

            // An effect with no icon yet shows its first letter so it can still be told apart.
            TextMeshProUGUI label = slot.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            label.alignment = entry.Icon != null ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Center;
            string initial = entry.Icon != null || string.IsNullOrEmpty(entry.Name) ? string.Empty : entry.Name.Substring(0, 1);
            label.text = entry.Stacks > 1 ? initial + "x" + entry.Stacks : initial;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
