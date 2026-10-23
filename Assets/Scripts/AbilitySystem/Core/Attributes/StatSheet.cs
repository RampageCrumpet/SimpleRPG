using System;
using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// How a source attribute's value turns into a contribution to a derived stat.
    /// </summary>
    public enum CurveKind : byte
    {
        /// <summary><c>scale * x</c>.</summary>
        Linear = 0,

        /// <summary><c>scale * x / (x + halfPoint)</c>: rises towards <c>scale</c> and reaches half of it when x equals <c>halfPoint</c>. Zero for x at or below zero.</summary>
        Diminishing = 1,
    }

    /// <summary>
    /// A declarative contribution curve. The parameters are authored data; no balance numbers live in code.
    /// </summary>
    public readonly struct ContributionCurve
    {
        public ContributionCurve(CurveKind kind, double scale, double halfPoint = 0d)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale)) throw new ArgumentException("A curve scale must be a finite number.", nameof(scale));
            if (kind == CurveKind.Diminishing && !(halfPoint > 0d)) throw new ArgumentException("A diminishing curve needs a positive half point.", nameof(halfPoint));

            Kind = kind;
            Scale = scale;
            HalfPoint = halfPoint;
        }

        public CurveKind Kind { get; }

        public double Scale { get; }

        public double HalfPoint { get; }

        public static ContributionCurve Linear(double scale) => new ContributionCurve(CurveKind.Linear, scale);

        public static ContributionCurve Diminishing(double maximum, double halfPoint) => new ContributionCurve(CurveKind.Diminishing, maximum, halfPoint);

        public double Evaluate(double x)
        {
            switch (Kind)
            {
                case CurveKind.Diminishing:
                    return x <= 0d ? 0d : Scale * x / (x + HalfPoint);
                default:
                    return Scale * x;
            }
        }
    }

    /// <summary>
    /// One input of a derived stat: a source attribute (primary or derived) passed through a curve.
    /// </summary>
    public readonly struct StatContribution
    {
        public StatContribution(AttributeId source, ContributionCurve curve)
        {
            if (source.IsNone) throw new ArgumentException("A contribution needs a source attribute.", nameof(source));

            Source = source;
            Curve = curve;
        }

        public AttributeId Source { get; }

        public ContributionCurve Curve { get; }
    }

    /// <summary>
    /// One derived stat on a sheet: a base value plus contributions. On a direct sheet only <see cref="BaseValue"/> is used.
    /// </summary>
    public sealed class StatRule
    {
        public StatRule(AttributeId stat, double baseValue, IEnumerable<StatContribution> contributions = null)
        {
            if (stat.IsNone) throw new ArgumentException("A rule needs a stat.", nameof(stat));
            if (Array.IndexOf(PrimaryAttributes.All, stat) >= 0) throw new ArgumentException(stat + " is a primary attribute and cannot be derived.", nameof(stat));
            if (double.IsNaN(baseValue) || double.IsInfinity(baseValue)) throw new ArgumentException("A base value must be a finite number.", nameof(baseValue));

            Stat = stat;
            BaseValue = baseValue;
            Contributions = new List<StatContribution>(contributions ?? Array.Empty<StatContribution>()).AsReadOnly();
        }

        public AttributeId Stat { get; }

        public double BaseValue { get; }

        public IReadOnlyList<StatContribution> Contributions { get; }
    }

    /// <summary>
    /// Who supplies the values of the derived stats.
    /// </summary>
    public enum StatOwnership : byte
    {
        /// <summary>The stats are computed from the core attributes (and each other) through the sheet's curves. Players are built this way.</summary>
        Derived = 0,

        /// <summary>The stats are authored base values; no core attributes are needed. Enemies are built this way.</summary>
        Direct = 1,
    }

    /// <summary>
    /// The stat configuration of one kind of character: which stats it has, and whether they are computed or authored directly.
    /// It builds the <see cref="AttributeSchema"/> and <see cref="AttributeSet"/> the rest of the game already uses, so modifiers, grants and
    /// ability bindings all see the one resolved value. A sheet is immutable and shared by every character that uses it.
    /// </summary>
    public sealed class StatSheet
    {
        private AttributeSchema schema;

        public StatSheet(StatOwnership ownership, IEnumerable<StatRule> rules)
        {
            Ownership = ownership;
            List<StatRule> list = new List<StatRule>(rules ?? Array.Empty<StatRule>());

            HashSet<AttributeId> seen = new HashSet<AttributeId>();
            foreach (StatRule rule in list)
            {
                if (rule == null) throw new ArgumentException("A sheet cannot contain a null rule.", nameof(rules));
                if (!seen.Add(rule.Stat)) throw new ArgumentException(rule.Stat + " is defined twice.", nameof(rules));
                if (ownership == StatOwnership.Direct && rule.Contributions.Count > 0)
                {
                    throw new ArgumentException(rule.Stat + " has contributions, but a direct sheet takes authored values only.", nameof(rules));
                }
            }

            Rules = list.AsReadOnly();
        }

        public StatOwnership Ownership { get; }

        public IReadOnlyList<StatRule> Rules { get; }

        /// <summary>
        /// The schema that computes this sheet's derived stats. Empty for a direct sheet. Built once and shared.
        /// </summary>
        public AttributeSchema Schema
        {
            get
            {
                if (schema == null)
                {
                    AttributeSchema built = new AttributeSchema();
                    if (Ownership == StatOwnership.Derived)
                    {
                        foreach (StatRule rule in Rules)
                        {
                            StatRule captured = rule;
                            built.Define(captured.Stat, reader =>
                            {
                                double value = captured.BaseValue;
                                foreach (StatContribution contribution in captured.Contributions)
                                {
                                    value += contribution.Curve.Evaluate(reader.Get(contribution.Source));
                                }

                                return value;
                            });
                        }
                    }

                    schema = built;
                }

                return schema;
            }
        }

        /// <summary>
        /// Creates an attribute set for a character of this kind. A direct sheet's authored values become its base values.
        /// </summary>
        public AttributeSet CreateSet()
        {
            AttributeSet set = new AttributeSet(Schema);
            if (Ownership == StatOwnership.Direct)
            {
                foreach (StatRule rule in Rules)
                {
                    set.SetBase(rule.Stat, rule.BaseValue);
                }
            }

            return set;
        }
    }
}
