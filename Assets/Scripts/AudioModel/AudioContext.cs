using System;
using SimpleRPG.HitDetection;
namespace SimpleRPG.Audio
{
    /// <summary>
    /// The kinds of fact a sound can depend on. Add a value when a new kind of context is needed; tables only look at the facets they name.
    /// </summary>
    public enum AudioFacet
    {
        /// <summary>The surface touched: stone floor, wooden bridge.</summary>
        Surface,
        /// <summary>What is on the moving foot.</summary>
        Footwear,
        /// <summary>The material doing the hitting or moving: a sword's metal.</summary>
        Source,
        /// <summary>The material hit: a skeleton's bone.</summary>
        Target,
        /// <summary>Armor worn, for armor-dependent impacts.</summary>
        Armor,
        /// <summary>The creature, item family or object the sound is about.</summary>
        Subject,
    }

    /// <summary>
    /// Anatomical regions a sound can be limited to. Flags, so a table entry can name several.
    /// </summary>
    [Flags]
    public enum AudioRegion
    {
        None = 0,
        Head = 1,
        Torso = 2,
        Legs = 4,
        Arms = 8,
    }

    /// <summary>
    /// What is known about the moment a sound is requested. Small and copied by value: the caller fills in what it knows and a table uses what it asks for.
    /// </summary>
    public struct AudioContext
    {
        public AudioTag Surface;
        public AudioTag Footwear;
        public AudioTag Source;
        public AudioTag Target;
        public AudioTag Armor;
        public AudioTag Subject;

        /// <summary>The body region struck, or <see cref="AudioRegion.None"/> when not relevant.</summary>
        public AudioRegion Region;

        /// <summary>How hard: 0 for a light step or tap, 1 for a sprint or full-force blow.</summary>
        public float Intensity;

        public AudioTag Get(AudioFacet facet)
        {
            switch (facet)
            {
                case AudioFacet.Surface: return Surface;
                case AudioFacet.Footwear: return Footwear;
                case AudioFacet.Source: return Source;
                case AudioFacet.Target: return Target;
                case AudioFacet.Armor: return Armor;
                case AudioFacet.Subject: return Subject;
                default: return null;
            }
        }

        public AudioContext With(AudioFacet facet, AudioTag tag)
        {
            AudioContext copy = this;
            switch (facet)
            {
                case AudioFacet.Surface: copy.Surface = tag; break;
                case AudioFacet.Footwear: copy.Footwear = tag; break;
                case AudioFacet.Source: copy.Source = tag; break;
                case AudioFacet.Target: copy.Target = tag; break;
                case AudioFacet.Armor: copy.Armor = tag; break;
                case AudioFacet.Subject: copy.Subject = tag; break;
            }

            return copy;
        }

        public static AudioRegion RegionOf(BodyLocation location)
        {
            switch (location)
            {
                case BodyLocation.Head: return AudioRegion.Head;
                case BodyLocation.Torso: return AudioRegion.Torso;
                case BodyLocation.Legs: return AudioRegion.Legs;
                case BodyLocation.Arms: return AudioRegion.Arms;
                default: return AudioRegion.None;
            }
        }
    }
}
