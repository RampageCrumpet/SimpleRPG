using SimpleRPG;
using UnityEngine;
using UnityEngine.Events;


namespace HitDetection
{
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        LayerMask layerMask;

        public UnityEvent<Character, BodyLocation> HitboxCollisionEvent { get; set; } = new UnityEvent<Character, BodyLocation>();

        public void Start()
        {
            layerMask = LayerMask.GetMask("HurtBox");
        }

        private void OnTriggerEnter(Collider other)
        {
            if(layerMask == (layerMask | 1 << other.transform.gameObject.layer))
            {
                if (other.TryGetComponent(out HurtBox hurtBox))
                {
                    HitboxCollisionEvent.Invoke(hurtBox.character, hurtBox.BodyLocation);
                }
            }
        }

    }
}