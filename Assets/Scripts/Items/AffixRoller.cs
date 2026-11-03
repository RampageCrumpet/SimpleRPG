using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// What one affix looks like to the roller: its identity, how likely it is, its range, and what it excludes. Plain values, so the rolling
    /// rules need no assets and can be tested exactly.
    /// </summary>
    public readonly struct AffixCandidate
    {
        public AffixCandidate(uint id, float weight, float minimum, float maximum, string group)
        {
            Id = id;
            Weight = weight;
            Minimum = minimum;
            Maximum = maximum;
            Group = group ?? string.Empty;
        }

        public uint Id { get; }

        public float Weight { get; }

        public float Minimum { get; }

        public float Maximum { get; }

        public string Group { get; }
    }

    /// <summary>
    /// Rolls the affixes of a new item instance. The draw order is fixed: one draw for how many, then for each affix one to pick it and one for
    /// its value, so the same random stream always gives the same item. An item that has nothing to roll draws nothing, which is what keeps a
    /// dungeon's loot the same for a seed when items without affixes are added to it.
    /// </summary>
    public static class AffixRoller
    {
        /// <summary>Rolls from plain candidates.</summary>
        /// <param name="candidates">What may roll.</param>
        /// <param name="minimumCount">The fewest affixes.</param>
        /// <param name="maximumCount">The most affixes (never more than <see cref="ItemAffixes.MaxAffixes"/> or the candidates there are).</param>
        public static ItemAffixes Roll(IReadOnlyList<AffixCandidate> candidates, int minimumCount, int maximumCount, IGameplayRandom random)
        {
            if (candidates == null || candidates.Count == 0 || random == null)
            {
                return default;
            }

            int most = Math.Min(Math.Min(maximumCount, ItemAffixes.MaxAffixes), candidates.Count);
            int fewest = Math.Max(0, Math.Min(minimumCount, most));
            if (most <= 0)
            {
                return default;
            }

            int count = fewest + (int)Math.Floor(random.NextDouble() * (most - fewest + 1));
            count = Math.Min(count, most);

            var picked = new List<AffixRoll>(count);
            var remaining = new List<AffixCandidate>(candidates);
            for (int n = 0; n < count && remaining.Count > 0; n++)
            {
                int index = PickWeighted(remaining, random.NextDouble());
                AffixCandidate chosen = remaining[index];
                double span = Math.Max(0d, chosen.Maximum - chosen.Minimum);
                double value = chosen.Minimum + random.NextDouble() * span;
                picked.Add(new AffixRoll(chosen.Id, AffixRoll.ToMilli(value)));

                // The same group never rolls twice, and neither does the same affix.
                string group = chosen.Group;
                remaining.RemoveAll(c => c.Id == chosen.Id || (group.Length > 0 && c.Group == group));
            }

            return ItemAffixes.From(picked);
        }

        /// <summary>
        /// Rolls from a pool for an item worn in <paramref name="slot"/>. Returns no affixes if the pool is null or nothing in it fits the slot.
        /// </summary>
        /// <param name="shortIdOf">An affix's network id, or 0 if it has none (it is not in the registry), in which case it cannot roll.</param>
        public static ItemAffixes Roll(AffixPool pool, EquipmentSlot slot, IGameplayRandom random, Func<AffixDefinition, uint> shortIdOf)
        {
            if (pool == null)
            {
                return default;
            }

            var candidates = new List<AffixCandidate>();
            foreach (AffixDefinition affix in pool.Affixes)
            {
                uint id = affix != null ? shortIdOf(affix) : 0;
                if (id != 0 && affix.Weight > 0f && affix.AllowsSlot(slot))
                {
                    candidates.Add(new AffixCandidate(id, affix.Weight, affix.Minimum, affix.Maximum, affix.ExclusiveGroup));
                }
            }

            return Roll(candidates, pool.MinimumCount, pool.MaximumCount, random);
        }

        // The index of the candidate the sample lands on, with each taking a share of the range in proportion to its weight.
        private static int PickWeighted(IReadOnlyList<AffixCandidate> candidates, double sample)
        {
            double total = 0d;
            foreach (AffixCandidate c in candidates)
            {
                total += Math.Max(0f, c.Weight);
            }

            if (total <= 0d)
            {
                return 0;
            }

            double target = sample * total;
            double running = 0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                running += Math.Max(0f, candidates[i].Weight);
                if (target < running)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
        }
    }
}
