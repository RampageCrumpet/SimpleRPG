using SimpleRPG.Abilities;
using System.Collections.Generic;

namespace SimpleRPG.InventorySystem
{
    /// <summary>What a tooltip line is, so a view can style it.</summary>
    public enum TooltipLineKind
    {
        Name,
        Property,
        Affix,
        Reaction,
    }

    /// <summary>One line of an item's tooltip.</summary>
    public readonly struct TooltipLine
    {
        public TooltipLine(TooltipLineKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public TooltipLineKind Kind { get; }

        public string Text { get; }
    }

    /// <summary>
    /// What an item says about itself: its name with its rolled affixes, what it grants of its own, what each affix does at the value it rolled,
    /// and the reactions it has. Plain text with a kind per line, so any view (an inventory tooltip, a loot label) can show it.
    /// </summary>
    public static class ItemTooltip
    {
        public static List<TooltipLine> Build(Item item)
        {
            var lines = new List<TooltipLine> { new TooltipLine(TooltipLineKind.Name, item.DisplayName) };

            if (item.RareName != null)
            {
                lines.Add(new TooltipLine(TooltipLineKind.Property, item.ItemName));
            }

            foreach (GrantSpec grant in item.Grants)
            {
                lines.Add(new TooltipLine(TooltipLineKind.Property, ModifierText.Describe(grant)));
            }

            foreach (TriggeredEffect effect in item.TriggeredEffects)
            {
                if (effect != null && !string.IsNullOrWhiteSpace(effect.Description)) lines.Add(new TooltipLine(TooltipLineKind.Reaction, effect.Description));
            }

            DefinitionRegistry registry = DefinitionRegistry.Instance;
            for (int i = 0; registry != null && i < item.Affixes.Count; i++)
            {
                AffixRoll roll = item.Affixes[i];
                if (!registry.TryGetByShortId(roll.AffixId, out GameplayDefinition definition) || !(definition is AffixDefinition affix) || affix.ValueCount != roll.ValueCount)
                {
                    continue;
                }

                foreach (string text in affix.Describe(roll.Values()))
                {
                    lines.Add(new TooltipLine(TooltipLineKind.Affix, text));
                }
            }

            return lines;
        }
    }
}
