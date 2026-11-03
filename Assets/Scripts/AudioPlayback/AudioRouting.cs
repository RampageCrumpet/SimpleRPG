using System;
using UnityEngine;
using UnityEngine.Audio;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// The project's audio wiring, one asset loaded from Resources: which mixer group each <see cref="AudioCategory"/> plays on, how many voices
    /// may play at once, and the shared tables used when content does not name its own. Missing pieces degrade to unrouted, table-less playback
    /// rather than errors.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioRouting", menuName = "Audio/Routing")]
    public sealed class AudioRouting : ScriptableObject
    {
        public const string ResourceName = "AudioRouting";

        /// <summary>Name of the exposed mixer parameter for a category's volume, in decibels.</summary>
        public static string VolumeParameter(AudioCategory category) => "Volume_" + category;

        [Serializable]
        public struct CategoryRoute
        {
            public AudioCategory category;
            public AudioMixerGroup group;
        }

        [Serializable]
        public struct PhysicsMaterialSurface
        {
            public PhysicsMaterial material;
            public AcousticSurface surface;
        }

        private static AudioRouting cached;
        private static bool loaded;

        [SerializeField] private AudioMixer mixer;

        [SerializeField] private CategoryRoute[] routes = Array.Empty<CategoryRoute>();

        [Tooltip("The most sounds that play at once across the whole game. Quieter and less important ones are cut off when it is reached.")]
        [Min(4)]
        [SerializeField] private int maxVoices = 40;

        [Tooltip("Voices created when the audio service starts, so early sounds allocate nothing.")]
        [Min(0)]
        [SerializeField] private int prewarmVoices = 12;

        [Header("Shared defaults")]
        [Tooltip("Footstep table used by characters that name none.")]
        [SerializeField] private AudioEventBase defaultFootstepTable;

        [Tooltip("Weapon impact table used when neither the weapon nor the target names its own impact sound.")]
        [SerializeField] private AudioEventBase defaultImpactTable;

        [Tooltip("The surface assumed for ground that carries no surface of its own.")]
        [SerializeField] private AcousticSurface defaultSurface;

        [Tooltip("Surfaces for ground identified only by its physics material.")]
        [SerializeField] private PhysicsMaterialSurface[] physicsMaterialSurfaces = Array.Empty<PhysicsMaterialSurface>();

        /// <summary>The routing asset in Resources, or null if the project has none.</summary>
        public static AudioRouting Instance
        {
            get
            {
                if (!loaded)
                {
                    cached = Resources.Load<AudioRouting>(ResourceName);
                    loaded = cached != null;
                }

                return cached;
            }
        }

        /// <summary>Creates a routing from code, for tools and tests. The real one is an asset in Resources.</summary>
        public static AudioRouting Create(AudioMixer mixer, CategoryRoute[] routes, int maxVoices, AudioEventBase footsteps = null, AudioEventBase impacts = null, AcousticSurface defaultSurface = null, PhysicsMaterialSurface[] physicsSurfaces = null)
        {
            AudioRouting routing = CreateInstance<AudioRouting>();
            routing.mixer = mixer;
            routing.routes = routes ?? Array.Empty<CategoryRoute>();
            routing.maxVoices = maxVoices;
            routing.prewarmVoices = 0;
            routing.defaultFootstepTable = footsteps;
            routing.defaultImpactTable = impacts;
            routing.defaultSurface = defaultSurface;
            routing.physicsMaterialSurfaces = physicsSurfaces ?? Array.Empty<PhysicsMaterialSurface>();
            return routing;
        }

        public AudioMixer Mixer => mixer;
        public int MaxVoices => maxVoices;
        public int PrewarmVoices => prewarmVoices;
        public AudioEventBase DefaultFootstepTable => defaultFootstepTable;
        public AudioEventBase DefaultImpactTable => defaultImpactTable;
        public AcousticSurface DefaultSurface => defaultSurface;

        /// <summary>
        /// The mixer group sounds of this category play on, or null (unrouted, straight to the master) if none is set.
        /// </summary>
        public AudioMixerGroup GroupFor(AudioCategory category)
        {
            if (routes != null)
            {
                foreach (CategoryRoute route in routes)
                {
                    if (route.category == category)
                    {
                        return route.group;
                    }
                }
            }

            return null;
        }

        public AcousticSurface SurfaceFor(PhysicsMaterial material)
        {
            if (material != null && physicsMaterialSurfaces != null)
            {
                foreach (PhysicsMaterialSurface entry in physicsMaterialSurfaces)
                {
                    if (entry.material == material)
                    {
                        return entry.surface;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Sets a category's volume on the mixer, in decibels. Does nothing if the mixer or its exposed parameter is missing.
        /// </summary>
        public bool SetCategoryVolumeDb(AudioCategory category, float decibels)
        {
            return mixer != null && mixer.SetFloat(VolumeParameter(category), decibels);
        }

        /// <summary>Forgets the loaded asset so the next access loads again. For tests.</summary>
        public static void ResetCache()
        {
            cached = null;
            loaded = false;
        }

        /// <summary>Test hook: makes <paramref name="routing"/> the routing asset.</summary>
        public static void Override(AudioRouting routing)
        {
            cached = routing;
            loaded = routing != null;
        }
    }
}
