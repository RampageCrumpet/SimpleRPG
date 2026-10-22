using SimpleRPG;
using UnityEngine;
using UnityEngine.Events;


namespace HitDetection
{
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        LayerMask layerMask;
        private readonly System.Collections.Generic.HashSet<Collider> contacts = new System.Collections.Generic.HashSet<Collider>();
        private void OnEnable() => contacts.Clear();
        private void OnDisable() => contacts.Clear();
        private void OnTriggerExit(Collider other) => contacts.Remove(other);

        public UnityEvent<Character, BodyLocation> HitboxCollisionEvent { get; set; } = new UnityEvent<Character, BodyLocation>();

        public void Start()
        {
            layerMask = LayerMask.GetMask("HurtBox");
            if (layerMask == 0)
            {
                Debug.LogError($"Hitbox on '{name}' could not find the 'HurtBox' layer. Add it under Tags and Layers; hits will not register without it.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || !contacts.Add(other)) return;
            if(layerMask == (layerMask | 1 << other.transform.gameObject.layer))
            {
                if (other.TryGetComponent(out HurtBox hurtBox))
                {
                    HitboxCollisionEvent.Invoke(hurtBox.character, hurtBox.BodyLocation);
                }
            }
        }

        // A weapon may already overlap a hurtbox when its active window opens.
        // Weapon's swing tracker makes repeated contacts idempotent.
        private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

    }
}
