using SimpleRPG;
using Unity.VisualScripting;
using UnityEngine;


namespace HitDetection
{
    [RequireComponent(typeof(Collider))]
    public class HurtBox : MonoBehaviour
    {

        [field: SerializeField]
        public BodyLocation BodyLocation { get; set; }

        /// <summary>
        /// The character this <see cref="Hitbox"/> is attached to.
        /// </summary>
        public Character character { get; set; }

        private void Start()
        {
            character = this.GetComponentInParent<Character>();
        }
    }
}
