using System;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>The kind of physical contact a sound belongs to.</summary>
    [Flags]
    public enum AudioContactKind
    {
        None = 0,
        Footstep = 1,
        Impact = 2,
        Any = Footstep | Impact,
    }

    /// <summary>
    /// A sound an object adds to contacts with it, on top of its surface's own sound: the creak of an old bridge.
    /// </summary>
    [Serializable]
    public struct ContactSound
    {
        public AudioEventBase sound;

        public AudioContactKind kinds;

        [Range(0f, 1f)]
        public float chance;
    }

    /// <summary>
    /// Says what an object sounds like to walk on or hit. Put it on a collider's object (or a parent: the nearest one up the hierarchy counts).
    /// A trigger collider with one is a layer lying over the ground, such as shallow water over stone, rather than the ground itself.
    /// The surface is separate from any visual material, and the contact sounds let one object add behavior (a bridge's creak) without a
    /// surface type of its own.
    /// </summary>
    public sealed class AcousticSurfaceSource : MonoBehaviour
    {
        [SerializeField] private AcousticSurface surface;

        [SerializeField] private ContactSound[] contactSounds = Array.Empty<ContactSound>();

        public AcousticSurface Surface
        {
            get => surface;
            set => surface = value;
        }

        public ContactSound[] ContactSounds
        {
            get => contactSounds;
            set => contactSounds = value ?? Array.Empty<ContactSound>();
        }
    }

    /// <summary>
    /// What a contact turned out to touch.
    /// </summary>
    public struct SurfaceSample
    {
        /// <summary>The ground itself.</summary>
        public AcousticSurface Surface;

        /// <summary>A layer lying over it (shallow water), or null.</summary>
        public AcousticSurface Overlay;

        /// <summary>The object that supplied the ground surface, for its contact sounds. May be null.</summary>
        public AcousticSurfaceSource BaseSource;

        /// <summary>The object that supplied the overlay, for its contact sounds. May be null.</summary>
        public AcousticSurfaceSource OverlaySource;
    }

    /// <summary>
    /// Finds the acoustic surface involved in a contact. Order: the object's own <see cref="AcousticSurfaceSource"/>, then its physics material
    /// through the routing asset, then the routing asset's default surface, then none.
    /// </summary>
    public static class SurfaceQuery
    {
        private const int MaxHits = 8;

        private static readonly RaycastHit[] Hits = new RaycastHit[MaxHits];

        /// <summary>
        /// The surface a collider stands for.
        /// </summary>
        public static SurfaceSample FromCollider(Collider collider)
        {
            SurfaceSample sample = default;
            AudioRouting routing = AudioRouting.Instance;
            if (collider != null)
            {
                AcousticSurfaceSource source = collider.GetComponentInParent<AcousticSurfaceSource>();
                if (source != null && source.Surface != null)
                {
                    sample.Surface = source.Surface;
                    sample.BaseSource = source;
                    return sample;
                }

                if (routing != null)
                {
                    sample.Surface = routing.SurfaceFor(collider.sharedMaterial);
                }
            }

            if (sample.Surface == null && routing != null)
            {
                sample.Surface = routing.DefaultSurface;
            }

            return sample;
        }

        /// <summary>
        /// The surface under a point: casts down, takes the first solid collider as the ground and any surface-carrying triggers above it as overlays.
        /// </summary>
        /// <param name="ignore">A hierarchy to skip, normally the walker's own.</param>
        public static SurfaceSample Below(Vector3 point, float reach, int layerMask, Transform ignore = null)
        {
            Vector3 origin = point + Vector3.up * 0.25f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, reach + 0.25f, layerMask, QueryTriggerInteraction.Collide);
            SortByDistance(count);

            SurfaceSample sample = default;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider == null || (ignore != null && collider.transform.IsChildOf(ignore)))
                {
                    continue;
                }

                if (collider.isTrigger)
                {
                    AcousticSurfaceSource layer = collider.GetComponentInParent<AcousticSurfaceSource>();
                    if (layer != null && layer.Surface != null && sample.Overlay == null)
                    {
                        sample.Overlay = layer.Surface;
                        sample.OverlaySource = layer;
                    }

                    continue;
                }

                SurfaceSample ground = FromCollider(collider);
                sample.Surface = ground.Surface;
                sample.BaseSource = ground.BaseSource;
                return sample;
            }

            AudioRouting routing = AudioRouting.Instance;
            if (routing != null)
            {
                sample.Surface = routing.DefaultSurface;
            }

            return sample;
        }

        private static void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                int j = i - 1;
                while (j >= 0 && Hits[j].distance > hit.distance)
                {
                    Hits[j + 1] = Hits[j];
                    j--;
                }

                Hits[j + 1] = hit;
            }
        }

        /// <summary>
        /// Plays the contact sounds an object adds for this kind of contact. Each is subject to its own chance.
        /// </summary>
        public static void PlayContactSounds(AcousticSurfaceSource source, AudioContactKind kind, in AudioContext context, Vector3 position, IAudioRandom random)
        {
            if (source == null || random == null)
            {
                return;
            }

            ContactSound[] sounds = source.ContactSounds;
            for (int i = 0; i < sounds.Length; i++)
            {
                ContactSound entry = sounds[i];
                if (entry.sound == null || (entry.kinds & kind) == AudioContactKind.None)
                {
                    continue;
                }

                if (entry.chance < 1f && random.Value() >= entry.chance)
                {
                    continue;
                }

                GameAudio.Play(entry.sound, context, position);
            }
        }
    }
}
