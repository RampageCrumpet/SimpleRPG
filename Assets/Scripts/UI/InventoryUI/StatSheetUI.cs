using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// The player's stat sheet: the statistics the character actually has, and nothing the game does not model.
    /// Core attributes and derived stats are the server's (<see cref="Character.Attributes"/>), so a remote client shows only vitals until they
    /// are replicated. A derived stat appears only if the character's stat profile defines it.
    /// </summary>
    public class StatSheetUI : MonoBehaviour
    {
        private static readonly Color HeaderColor = new Color(0.89f, 0.91f, 0.92f, 1f);

        private static readonly Color LabelColor = new Color(0.79f, 0.82f, 0.84f, 1f);

        private const float RowHeight = 32f;

        private static readonly (string Label, AttributeId Stat, bool Percent)[] Derived =
        {
            ("Physical Power", DerivedStats.PhysicalPower, false),
            ("Magical Power", DerivedStats.MagicalPower, false),
            ("Physical Reduction", DerivedStats.PhysicalDamageReduction, true),
            ("Magical Reduction", DerivedStats.MagicalDamageReduction, true),
            ("Movement Speed", DerivedStats.MovementSpeed, false),
            ("Attack Speed", DerivedStats.AttackSpeed, false),
            ("Casting Speed", DerivedStats.CastingSpeed, false),
            ("Interaction Speed", DerivedStats.InteractionSpeed, false),
            ("Equip Speed", DerivedStats.EquipSpeed, false),
            ("Carrying Capacity", DerivedStats.CarryingCapacity, false),
            ("Spell Preparation", DerivedStats.SpellPreparationCapacity, false),
        };

        private static readonly (string Label, AttributeId Attribute)[] Core =
        {
            ("Strength", PrimaryAttributes.Strength),
            ("Constitution", PrimaryAttributes.Constitution),
            ("Dexterity", PrimaryAttributes.Dexterity),
            ("Agility", PrimaryAttributes.Agility),
            ("Willpower", PrimaryAttributes.Willpower),
            ("Intelligence", PrimaryAttributes.Intelligence),
        };

        [SerializeField]
        [Tooltip("Parent of the rows. Needs a vertical layout group.")]
        private RectTransform content;

        private readonly List<(TextMeshProUGUI Value, System.Func<string> Read)> rows = new List<(TextMeshProUGUI, System.Func<string>)>();

        private Character character;

        /// <summary>
        /// The labels of the rows currently listed, for tests and tools.
        /// </summary>
        public List<string> RowLabels { get; } = new List<string>();

        /// <summary>
        /// Shows this character's stats. Pass null to clear the sheet.
        /// </summary>
        public void Bind(Character newCharacter)
        {
            character = newCharacter;
            Rebuild();
        }

        private void OnEnable()
        {
            // The server's attributes may not have existed when the sheet was bound.
            Rebuild();
        }

        private void Update()
        {
            foreach ((TextMeshProUGUI value, System.Func<string> read) in rows)
            {
                string text = read();
                if (value.text != text)
                {
                    value.text = text;
                }
            }
        }

        /// <summary>
        /// Lists the rows again from what the character has.
        /// </summary>
        public void Rebuild()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }

            rows.Clear();
            RowLabels.Clear();

            if (character == null)
            {
                return;
            }

            AddHeader("Vitals");
            AddRow("Health", () => character.Health + " / " + character.MaxHealth);

            AttributeSet attributes = character.Attributes;
            if (attributes == null)
            {
                return;
            }

            AddHeader("Core Attributes");
            foreach ((string label, AttributeId attribute) in Core)
            {
                AttributeId id = attribute;
                AddRow(label, () => Format(character.Attributes != null ? character.Attributes.Get(id) : 0d, false));
            }

            bool headerAdded = false;
            foreach ((string label, AttributeId stat, bool percent) in Derived)
            {
                if (!character.HasDerivedStat(stat))
                {
                    continue;
                }

                if (!headerAdded)
                {
                    AddHeader("Derived Stats");
                    headerAdded = true;
                }

                AttributeId id = stat;
                bool asPercent = percent;
                AddRow(label, () => Format(character.Attributes != null ? character.Attributes.Get(id) : 0d, asPercent));
            }
        }

        private static string Format(double value, bool percent)
        {
            return percent ? (value * 100d).ToString("0.#", CultureInfo.InvariantCulture) + "%" : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void AddHeader(string title)
        {
            TextMeshProUGUI text = NewText("Header " + title, content, 24f, HeaderColor, TextAlignmentOptions.BottomLeft);
            text.fontStyle = FontStyles.Bold;
            text.text = title;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        }

        private void AddRow(string label, System.Func<string> read)
        {
            GameObject row = new GameObject("Row " + label, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            row.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            TextMeshProUGUI name = NewText("Label", (RectTransform)row.transform, 20f, LabelColor, TextAlignmentOptions.MidlineLeft);
            name.text = label;
            TextMeshProUGUI value = NewText("Value", (RectTransform)row.transform, 20f, Color.white, TextAlignmentOptions.MidlineRight);
            value.text = read();

            rows.Add((value, read));
            RowLabels.Add(label);
        }

        private static TextMeshProUGUI NewText(string name, RectTransform parent, float size, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }
    }
}
