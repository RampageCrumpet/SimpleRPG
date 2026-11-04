using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// What one affix looks like to the roller: its identity, how likely it is, whether it is a prefix or suffix, the family it excludes, and the
    /// range of each value it rolls. Plain values, so the rolling rules need no assets and can be tested exactly.
    /// </summary>
    public readonly struct AffixCandidate
    {
        public AffixCandidate(uint id, float weight, AffixKind kind, string family, IReadOnlyList<AffixValueRange> ranges)
        {
            Id = id;
            Weight = weight;
            Kind = kind;
            Family = family ?? string.Empty;
            Ranges = ranges ?? Array.Empty<AffixValueRange>();
        }

        public uint Id { get; }

        public float Weight { get; }

        public AffixKind Kind { get; }

        public string Family { get; }

        public IReadOnlyList<AffixValueRange> Ranges { get; }
    }

    /// <summary>How many affixes an instance rolls, and how many of each kind.</summary>
    public readonly struct AffixLimits
    {
        public AffixLimits(int minimumCount, int maximumCount, int maximumPrefixes, int maximumSuffixes)
        {
            MinimumCount = minimumCount;
            MaximumCount = maximumCount;
            MaximumPrefixes = maximumPrefixes;
            MaximumSuffixes = maximumSuffixes;
        }

        public int MinimumCount { get; }

        public int MaximumCount { get; }

        public int MaximumPrefixes { get; }

        public int MaximumSuffixes { get; }
    }

    /// <summary>
    /// Rolls the affixes of a new item instance. The draw order is fixed: one draw for how many, then for each affix one to pick it and one for
    /// each of its values in order, so the same random stream always gives the same item. An item that has nothing to roll draws nothing, which is
    /// what keeps a dungeon's loot the same for a seed when items without affixes are added to it.
    /// </summary>
    public static class AffixRoller
    {
        /// <summary>Rolls from plain candidates.</summary>
        public static ItemAffixes Roll(IReadOnlyList<AffixCandidate> candidates, AffixLimits limits, IGameplayRandom random)
        {
            if (candidates == null || candidates.Count == 0 || random == null)
            {
                return default;
            }

            int most = Math.Min(limits.MaximumCount, candidates.Count);
            int fewest = Math.Max(0, Math.Min(limits.MinimumCount, most));
            if (most <= 0)
            {
                return default;
            }

            int count = fewest + (int)Math.Floor(random.NextDouble() * (most - fewest + 1));
            count = Math.Min(count, most);

            var picked = new List<AffixRoll>(count);
            var remaining = new List<AffixCandidate>(candidates);
            int prefixes = 0;
            int suffixes = 0;
            int words = 0;
            for (int n = 0; n < count; n++)
            {
                // What can still be added: the kind has room and the roll fits in the space an item has.
                var open = remaining.FindAll(c =>
                    (c.Kind == AffixKind.Prefix ? prefixes < limits.MaximumPrefixes : suffixes < limits.MaximumSuffixes) &&
                    words + ItemAffixes.Footprint(c.Ranges.Count) <= ItemAffixes.WordCapacity);
                if (open.Count == 0)
                {
                    break;
                }

                AffixCandidate chosen = open[PickWeighted(open, random.NextDouble())];
                var values = new int[chosen.Ranges.Count];
                for (int v = 0; v < values.Length; v++)
                {
                    double span = Math.Max(0d, chosen.Ranges[v].Maximum - chosen.Ranges[v].Minimum);
                    values[v] = AffixRoll.ToMilli(chosen.Ranges[v].Minimum + random.NextDouble() * span);
                }

                picked.Add(new AffixRoll(chosen.Id, values));
                words += ItemAffixes.Footprint(values.Length);
                if (chosen.Kind == AffixKind.Prefix) prefixes++;
                else suffixes++;

                // The same affix never rolls twice, and neither does another of its family.
                string family = chosen.Family;
                remaining.RemoveAll(c => c.Id == chosen.Id || (family.Length > 0 && c.Family == family));
            }

            return ItemAffixes.From(picked);
        }

        /// <summary>
        /// Rolls from a pool for an item worn in <paramref name="slot"/> that came from a source of <paramref name="itemLevel"/>. Returns no affixes
        /// if the pool is null or nothing in it fits the slot and level.
        /// </summary>
        /// <param name="shortIdOf">An affix's network id, or 0 if it has none (it is not in the registry), in which case it cannot roll.</param>
        public static ItemAffixes Roll(AffixPool pool, EquipmentSlot slot, int itemLevel, IGameplayRandom random, Func<AffixDefinition, uint> shortIdOf)
        {
            if (pool == null)
            {
                return default;
            }

            var candidates = new List<AffixCandidate>();
            foreach (AffixDefinition affix in pool.Affixes)
            {
                uint id = affix != null ? shortIdOf(affix) : 0;
                if (id != 0 && affix.Weight > 0f && affix.AllowsSlot(slot) && affix.AllowsItemLevel(itemLevel) && affix.Problem() == null)
                {
                    candidates.Add(new AffixCandidate(id, affix.Weight, affix.Kind, affix.Family, affix.Ranges()));
                }
            }

            return Roll(candidates, new AffixLimits(pool.MinimumCount, pool.MaximumCount, pool.MaximumPrefixes, pool.MaximumSuffixes), random);
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
