using SimpleRPG;
using UnityEngine;

namespace SimpleRPG.HitDetection
{
    /// <summary>
    /// One hurtbox a blade overlaps at one pose, and what the contact-resolution rule needs to choose between several on the same character.
    /// </summary>
    /// <remarks>
    /// The rule, in order, for hurtboxes of one character that the blade overlaps at the same pose:
    /// <list type="number">
    /// <item>The one whose surface is nearest the blade's centre (zero when the centre is inside it): the part the blade is most truly in.</item>
    /// <item>The one whose own centre is nearest the blade's centre, which separates two boxes that both contain it.</item>
    /// <item>The higher <see cref="HurtBox.Priority"/>, which an author sets only to settle a deliberate overlap, such as a neck between head and chest.</item>
    /// <item>The earlier <see cref="HurtBox.Key"/> in ordinal order, so the answer never depends on physics query order.</item>
    /// </list>
    /// Damage never takes part: a region is not chosen for being worth more. Poses are tried in the order the blade travels, so a character is
    /// struck at the first pose that touches any of its hurtboxes.
    /// </remarks>
    public readonly struct HitCandidate
    {
        /// <summary>Distances closer than this are the same, so floating point noise cannot flip a choice between two equal contacts.</summary>
        private const float Tolerance = 1e-4f;

        public HitCandidate(Character character, BodyLocation location, int priority, string key, float surfaceDistance, float centreDistance)
        {
            Character = character;
            Location = location;
            Priority = priority;
            Key = key ?? string.Empty;
            SurfaceDistance = surfaceDistance;
            CentreDistance = centreDistance;
        }

        public Character Character { get; }

        public BodyLocation Location { get; }

        public int Priority { get; }

        public string Key { get; }

        /// <summary>How far the blade's centre is from this hurtbox's surface. Zero when it is inside.</summary>
        public float SurfaceDistance { get; }

        /// <summary>How far the blade's centre is from this hurtbox's own centre.</summary>
        public float CentreDistance { get; }

        /// <summary>True when this contact beats <paramref name="other"/> under the rule above.</summary>
        public bool IsPreferredTo(in HitCandidate other)
        {
            float surface = SurfaceDistance - other.SurfaceDistance;
            if (Mathf.Abs(surface) > Tolerance)
            {
                return surface < 0f;
            }

            float centre = CentreDistance - other.CentreDistance;
            if (Mathf.Abs(centre) > Tolerance)
            {
                return centre < 0f;
            }

            if (Priority != other.Priority)
            {
                return Priority > other.Priority;
            }

            return string.CompareOrdinal(Key, other.Key) < 0;
        }
    }
}
