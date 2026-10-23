using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// One input of a derived stat, as authored: a source attribute key and the curve it passes through.
    /// </summary>
    [Serializable]
    public struct StatContributionData
    {
        [Tooltip("The attribute that feeds this stat: a core attribute or another derived stat, by key (for example simplerpg.attr.agility).")]
        public string sourceKey;

        public CurveKind curve;

        [Tooltip("Linear: the value gained per point of the source. Diminishing: the most this input can ever contribute.")]
        public double scale;

        [Tooltip("Diminishing only: the source value at which half of the maximum is reached.")]
        public double halfPoint;
    }

    /// <summary>
    /// One derived stat, as authored.
    /// </summary>
    [Serializable]
    public struct StatRuleData
    {
        [Tooltip("The stat's key, for example simplerpg.stat.max_health.")]
        public string statKey;

        [Tooltip("The value before contributions. On a direct profile this is the stat's value.")]
        public double baseValue;

        [Tooltip("Derived profiles only: what the stat gains from other attributes.")]
        public List<StatContributionData> contributions;
    }

    /// <summary>
    /// The stat configuration of one kind of character, as an authored asset. A player profile derives its stats from the core attributes;
    /// an enemy profile states them directly. Characters build their <see cref="AttributeSet"/> from it, so there is one resolved value per stat.
    /// Immutable at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "StatProfile", menuName = "SimpleRPG/Stat Profile")]
    public class StatProfile : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Derived: stats are computed from the core attributes. Direct: stats are the authored values and no core attributes are needed.")]
        private StatOwnership ownership = StatOwnership.Derived;

        [SerializeField]
        private List<StatRuleData> rules = new List<StatRuleData>();

        private StatSheet sheet;

        public StatOwnership Ownership => ownership;

        /// <summary>
        /// The runtime form of this profile. Built on first use and shared by every character that uses the profile.
        /// </summary>
        public StatSheet Sheet => sheet ??= Build(ownership, rules);

        /// <summary>
        /// Creates the attribute set for a new character of this kind.
        /// </summary>
        public AttributeSet CreateSet() => Sheet.CreateSet();

        /// <summary>
        /// Sets the profile up in code. Used by tools and tests that build profiles without the inspector.
        /// </summary>
        public void Configure(StatOwnership ownership, IEnumerable<StatRuleData> rules)
        {
            this.ownership = ownership;
            this.rules = new List<StatRuleData>(rules);
            sheet = null;
        }

        /// <summary>
        /// Builds the runtime sheet; throws if the authored data is invalid (a primary attribute as a stat, a duplicate stat, an unusable curve).
        /// </summary>
        public static StatSheet Build(StatOwnership ownership, IEnumerable<StatRuleData> data)
        {
            List<StatRule> built = new List<StatRule>();
            foreach (StatRuleData rule in data)
            {
                List<StatContribution> contributions = new List<StatContribution>();
                if (rule.contributions != null)
                {
                    foreach (StatContributionData input in rule.contributions)
                    {
                        contributions.Add(new StatContribution(new AttributeId(input.sourceKey), new ContributionCurve(input.curve, input.scale, input.halfPoint)));
                    }
                }

                built.Add(new StatRule(new AttributeId(rule.statKey), rule.baseValue, contributions));
            }

            return new StatSheet(ownership, built);
        }

        private void OnValidate()
        {
            sheet = null;
        }
    }
}
