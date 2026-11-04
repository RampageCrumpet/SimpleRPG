using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;

namespace SimpleRPG.InventorySystem
{
    /// <summary>How a modifier reads to the player, shared by everything that shows one (affix lines, an item's own grants).</summary>
    public static class ModifierText
    {
        /// <summary>The amount with its sign or unit: "+3", "+10%", "x1.5".</summary>
        public static string Amount(ModifierLayer layer, float value)
        {
            switch (layer)
            {
                case ModifierLayer.PercentAdd:
                    return (value >= 0f ? "+" : "") + (value * 100f).ToString("0.#") + "%";
                case ModifierLayer.Multiply:
                    return "x" + value.ToString("0.##");
                default:
                    return (value >= 0f ? "+" : "") + value.ToString("0.##");
            }
        }

        /// <summary>A grant as a line: "+28 Armor Rating".</summary>
        public static string Describe(GrantSpec grant) => Amount(grant.Layer, grant.Value) + " " + AttributeLabels.For(grant.AttributeKey);
    }

    /// <summary>
    /// The names the player reads for attributes. Derived from the stable key ("simplerpg.stat.armor_rating" is "Armor Rating"), so a new
    /// attribute reads sensibly without a table to keep in step.
    /// </summary>
    public static class AttributeLabels
    {
        public static string For(string attributeKey)
        {
            if (string.IsNullOrEmpty(attributeKey)) return "";
            string tail = attributeKey.Substring(attributeKey.LastIndexOf('.') + 1);
            var words = tail.Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            }

            return string.Join(" ", words);
        }
    }
}
