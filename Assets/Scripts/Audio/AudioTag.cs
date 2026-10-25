using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// A named acoustic identity used to pick sounds by context: a material (bone, metal), footwear (leather boots), armor class or any other
    /// "kind of thing". Tags form a fallback chain through <see cref="Parent"/>, so "Gravel" can fall back to "Dirt" and "Dirt" to "Generic Ground"
    /// when an audio table has no entry for the more specific tag. Tags carry no sound; they only select.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioTag", menuName = "Audio/Tag")]
    public class AudioTag : ScriptableObject
    {
        /// <summary>The most parents followed, which also ends an accidental cycle.</summary>
        public const int MaxChain = 8;

        [SerializeField]
        [Tooltip("What to fall back to when a table has no entry for this tag. Leave empty for a root tag.")]
        private AudioTag parent;

        public AudioTag Parent => parent;

        /// <summary>Creates a tag from code, for tools and tests. Authored tags are assets.</summary>
        public static T Create<T>(string name, AudioTag parent = null) where T : AudioTag
        {
            T tag = CreateInstance<T>();
            tag.name = name;
            tag.parent = parent;
            return tag;
        }

        public static AudioTag Create(string name, AudioTag parent = null) => Create<AudioTag>(name, parent);

        /// <summary>
        /// How many steps up the fallback chain <paramref name="wanted"/> is: 0 when it is this tag, 1 for the parent and so on, or -1 if it is not in the chain.
        /// </summary>
        public int DistanceTo(AudioTag wanted)
        {
            AudioTag current = this;
            for (int steps = 0; current != null && steps < MaxChain; steps++)
            {
                if (current == wanted)
                {
                    return steps;
                }

                current = current.parent;
            }

            return -1;
        }
    }
}
