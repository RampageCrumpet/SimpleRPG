using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// A soft cap on a stat: up to the threshold the stat is what it adds up to; beyond it each further point counts only <see cref="Slope"/> of
    /// the way (a slope of 0.25 makes four points past the threshold worth one). It never stops growth outright, so stacking is discouraged, not
    /// forbidden, and any hard ceiling the stat has still applies on top. Applied to the stat's final value, after every modifier, so no
    /// combination of bonuses gets around it. A threshold of zero or less means no cap, which is what an unset cap is.
    /// </summary>
    public readonly struct SoftCap
    {
        public SoftCap(double threshold, double slope)
        {
            if (double.IsNaN(threshold) || double.IsInfinity(threshold)) throw new ArgumentException("A soft cap threshold must be a finite number.", nameof(threshold));
            if (double.IsNaN(slope) || double.IsInfinity(slope) || slope < 0d || slope > 1d) throw new ArgumentException("A soft cap slope must be between 0 and 1.", nameof(slope));

            Threshold = threshold;
            Slope = slope;
        }

        /// <summary>Where the stat starts to count for less.</summary>
        public double Threshold { get; }

        /// <summary>How much of each point past the threshold counts: 1 is no cap, 0 a hard cap.</summary>
        public double Slope { get; }

        public bool IsActive => Threshold > 0d;

        public double Apply(double value) => IsActive && value > Threshold ? Threshold + (value - Threshold) * Slope : value;
    }
}
