using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// One affix an item can roll: a prefix or suffix of the item's name that does one or more things (its <see cref="Effects"/>), each rolling its
    /// own value. Affixes of one <see cref="Family"/> are its tiers: they exclude one another, and a higher <see cref="Tier"/> needs a higher item
    /// level. Like every definition it is never changed while the game runs; what an item actually rolled is stored on the item (see
    /// <see cref="ItemAffixes"/>). Run <b>SimpleRPG &gt; Definitions &gt; Rebuild Definition Registry</b> after creating one, so it has a network id.
    /// </summary>
    [CreateAssetMenu(fileName = "Affix", menuName = "Items/Affix")]
    public class AffixDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("The word it adds to the item's name, for example 'Riveted' (a prefix) or 'of the Veteran' (a suffix).")]
        private string displayName = "";

        [SerializeField]
        private AffixKind kind = AffixKind.Prefix;

        [SerializeField]
        [Tooltip("The family it belongs to. Two affixes of one family never roll on one item, so the tiers of a family are alternatives. Empty means no family.")]
        private string family = "";

        [SerializeField]
        [Min(1)]
        [Tooltip("Which tier of its family this is (1 is the weakest). Shown, and used to read the catalog; the item level it needs is the next field.")]
        private int tier = 1;

        [SerializeField]
        [Min(1)]
        [Tooltip("The lowest item level this can roll on.")]
        private int minimumItemLevel = ItemLevel.Minimum;

        [SerializeField]
        [Min(0f)]
        [Tooltip("How likely this affix is to be picked, relative to the others in the pool. 0 never.")]
        private float weight = 1f;

        [SerializeField]
        [Tooltip("The slots this affix can roll on. Empty means any.")]
        private List<EquipmentSlot> allowedSlots = new List<EquipmentSlot>();

        [SerializeReference]
        [Tooltip("What the affix does. Each effect rolls its own value; there is no limit to how many.")]
        private List<AffixEffect> effects = new List<AffixEffect>();

        public string DisplayName => displayName;

        public AffixKind Kind => kind;

        public string Family => family;

        public int Tier => tier;

        public int MinimumItemLevel => minimumItemLevel;

        public float Weight => weight;

        public IReadOnlyList<AffixEffect> Effects => effects;

        public bool AllowsSlot(EquipmentSlot slot) => allowedSlots == null || allowedSlots.Count == 0 || allowedSlots.Contains(slot);

        public bool AllowsItemLevel(int itemLevel) => itemLevel >= minimumItemLevel;

        /// <summary>How many values an instance of this affix rolls: the total over its effects.</summary>
        public int ValueCount
        {
            get
            {
                int total = 0;
                foreach (AffixEffect effect in effects) total += effect.ValueCount;
                return total;
            }
        }

        /// <summary>The range of each rolled value, in order (the first effect's values, then the second's, and so on).</summary>
        public List<AffixValueRange> Ranges()
        {
            var ranges = new List<AffixValueRange>(ValueCount);
            foreach (AffixEffect effect in effects)
            {
                for (int i = 0; i < effect.ValueCount; i++) ranges.Add(effect.RangeOf(i));
            }

            return ranges;
        }

        /// <summary>What this affix does to the wearer at rolled values (one per <see cref="ValueCount"/>, in order), added to <paramref name="into"/>.</summary>
        /// <param name="itemOwnGrants">What the item's asset grants on its own, for effects that scale the item itself.</param>
        public void AddGrants(IReadOnlyList<GrantSpec> itemOwnGrants, IReadOnlyList<float> values, List<GrantSpec> into)
        {
            int offset = 0;
            foreach (AffixEffect effect in effects)
            {
                effect.AddGrants(itemOwnGrants, Slice(values, offset, effect.ValueCount), into);
                offset += effect.ValueCount;
            }
        }

        /// <summary>The triggered effects this affix brings to its item while worn, added to <paramref name="into"/>.</summary>
        public void AddTriggeredEffects(List<TriggeredEffect> into)
        {
            foreach (AffixEffect effect in effects) effect?.AddTriggeredEffects(into);
        }

        /// <summary>The lines a player reads for this affix at rolled values, one per effect that has something to say.</summary>
        public List<string> Describe(IReadOnlyList<float> values)
        {
            var lines = new List<string>(effects.Count);
            int offset = 0;
            foreach (AffixEffect effect in effects)
            {
                string line = effect.Describe(Slice(values, offset, effect.ValueCount));
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
                offset += effect.ValueCount;
            }

            return lines;
        }

        /// <summary>Why this affix is not correctly authored, or null.</summary>
        public string Problem()
        {
            if (effects == null || effects.Count == 0) return name + " has no effects.";
            foreach (AffixEffect effect in effects)
            {
                if (effect == null) return name + " has an empty effect.";
                string problem = effect.Problem();
                if (problem != null) return name + ": " + problem + ".";
            }

            return null;
        }

        /// <summary>
        /// Whether every effect changes something in the game today. An affix that is not live must not be in a pool a player can roll from.
        /// </summary>
        public bool IsLive(out string reason)
        {
            reason = null;
            foreach (AffixEffect effect in effects)
            {
                if (effect != null && !effect.IsLive(out string why))
                {
                    reason = name + ": " + why + ".";
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<float> Slice(IReadOnlyList<float> values, int offset, int count)
        {
            var part = new float[count];
            for (int i = 0; i < count; i++) part[i] = values[offset + i];
            return part;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            string problem = Problem();
            if (problem != null) Debug.LogWarning(problem, this);
        }
#endif
    }
}
