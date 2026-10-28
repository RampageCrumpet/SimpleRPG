using SimpleRPG;
using UnityEngine;


namespace SimpleRPG.HitDetection
{
    /// <summary>
    /// One struck region of a character's body: a trigger collider on the HurtBox layer, parented to the bone it follows, so animation and the
    /// upper-body articulation move it. It names the body location a blade touched and the character that owns it. What a location is worth is the
    /// owner's <see cref="HitRegionProfile"/>, not this component.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HurtBox : MonoBehaviour
    {
        [field: SerializeField]
        public BodyLocation BodyLocation { get; set; }

        /// <summary>
        /// Settles two hurtboxes of one character that the blade overlaps equally (a neck shared by head and chest): the higher wins. Leave at 0 unless
        /// the boxes really are meant to overlap. See <see cref="HitCandidate"/>.
        /// </summary>
        [field: SerializeField]
        public int Priority { get; set; }

        private Character owner;
        private Collider[] shapes;
        private string key;

        /// <summary>
        /// The character this <see cref="HurtBox"/> is attached to.
        /// </summary>
        public Character character
        {
            get => owner != null ? owner : (owner = GetComponentInParent<Character>());
            set => owner = value;
        }

        /// <summary>
        /// The colliders that make up this region.
        /// </summary>
        public Collider[] Shapes => shapes ?? (shapes = GetComponents<Collider>());

        /// <summary>
        /// A name that stays the same between runs, for the last tie-break of <see cref="HitCandidate"/>: the path from the character down to this bone.
        /// </summary>
        public string Key
        {
            get
            {
                if (key == null)
                {
                    string path = name;
                    Transform root = character != null ? character.transform : null;
                    for (Transform up = transform.parent; up != null && up != root; up = up.parent)
                    {
                        path = up.name + "/" + path;
                    }

                    key = path;
                }

                return key;
            }
        }

        /// <summary>
        /// Lets this region be struck, or not. A character that has died turns all of its regions off.
        /// </summary>
        public void SetStrikable(bool strikable)
        {
            foreach (Collider shape in Shapes)
            {
                shape.enabled = strikable;
            }
        }
    }
}
