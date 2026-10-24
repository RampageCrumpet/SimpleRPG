using SimpleRPG;
using UnityEngine;
using UnityEngine.Events;


namespace HitDetection
{
    /// <summary>
    /// A weapon's live blade. Reports the hurtboxes it touches. Touches come from two places that agree on the geometry: Unity's trigger messages,
    /// which see the blade where it is at each physics step, and a sweep over the way the blade travelled since the previous step, so a fast swing
    /// cannot step over a thin target between two steps. Both feed the same <see cref="HitboxCollisionEvent"/>, so a target is still reported once.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        /// <summary>
        /// The most poses between two physics steps the sweep tests. A blade moving further than this covers is checked at the poses nearest to its path.
        /// </summary>
        private const int MaxSweepSamples = 12;

        private static readonly Collider[] SweepBuffer = new Collider[32];

        LayerMask layerMask;
        private readonly System.Collections.Generic.HashSet<Collider> contacts = new System.Collections.Generic.HashSet<Collider>();
        private Collider shape;
        private bool hasPreviousPose;
        private Vector3 previousPosition;
        private Quaternion previousRotation;

        private void OnEnable()
        {
            contacts.Clear();
            hasPreviousPose = false;
        }

        private void OnDisable() => contacts.Clear();
        private void OnTriggerExit(Collider other) => contacts.Remove(other);

        public UnityEvent<Character, BodyLocation> HitboxCollisionEvent { get; set; } = new UnityEvent<Character, BodyLocation>();

        public void Start()
        {
            layerMask = LayerMask.GetMask("HurtBox");
            shape = GetComponent<Collider>();
            if (layerMask == 0)
            {
                Debug.LogError($"Hitbox on '{name}' could not find the 'HurtBox' layer. Add it under Tags and Layers; hits will not register without it.", this);
            }
        }

        private void OnTriggerEnter(Collider other) => ReportContact(other);

        // A weapon may already overlap a hurtbox when its active window opens.
        // Weapon's swing tracker makes repeated contacts idempotent.
        private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

        private void ReportContact(Collider other)
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

        // Runs before the physics step, so the blade's transform is where the last frame left it, final pose included. The previous step's pose is
        // remembered here, not read from physics, so the path between them is the blade's real path whatever moved it.
        private void FixedUpdate()
        {
            if (!enabled || shape == null || layerMask == 0)
            {
                return;
            }

            Transform blade = transform;
            if (hasPreviousPose)
            {
                Sweep(previousPosition, previousRotation, blade.position, blade.rotation);
            }

            previousPosition = blade.position;
            previousRotation = blade.rotation;
            hasPreviousPose = true;
        }

        private void Sweep(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation)
        {
            float reach = Reach();
            float travel = (toPosition - fromPosition).magnitude + Mathf.Deg2Rad * Quaternion.Angle(fromRotation, toRotation) * reach;
            float spacing = Mathf.Max(Thickness(), 0.02f);
            int samples = Mathf.Clamp(Mathf.CeilToInt(travel / spacing), 1, MaxSweepSamples);
            if (samples < 2)
            {
                return;
            }

            // Hurtboxes ride animated bones too; bring their colliders up to this frame before asking what overlaps them.
            Physics.SyncTransforms();

            // The end pose is the physics step's to report; the poses in between are the ones it would step over.
            for (int i = 1; i < samples; i++)
            {
                float t = i / (float)samples;
                Vector3 position = Vector3.Lerp(fromPosition, toPosition, t);
                Quaternion rotation = Quaternion.Slerp(fromRotation, toRotation, t);
                int count = Overlap(position, rotation);
                for (int c = 0; c < count; c++)
                {
                    ReportContact(SweepBuffer[c]);
                }
            }
        }

        // How far from its pivot the blade reaches, for turning an angle into a distance.
        private float Reach() => shape.bounds.extents.magnitude;

        // The blade's thinnest world-space extent, which is how far it can move without leaving a gap a thin target could hide in.
        private float Thickness()
        {
            Vector3 scale = transform.lossyScale;
            switch (shape)
            {
                case BoxCollider box:
                    Vector3 size = Vector3.Scale(box.size, Abs(scale));
                    return Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                case SphereCollider sphere:
                    return sphere.radius * 2f * MaxComponent(Abs(scale));
                case CapsuleCollider capsule:
                    return capsule.radius * 2f * MaxComponent(Abs(scale));
                default:
                    return shape.bounds.size.magnitude;
            }
        }

        private int Overlap(Vector3 position, Quaternion rotation)
        {
            Vector3 scale = Abs(transform.lossyScale);
            switch (shape)
            {
                case BoxCollider box:
                    return Physics.OverlapBoxNonAlloc(position + rotation * Vector3.Scale(box.center, scale), Vector3.Scale(box.size, scale) * 0.5f, SweepBuffer, rotation, layerMask, QueryTriggerInteraction.Collide);
                case SphereCollider sphere:
                    return Physics.OverlapSphereNonAlloc(position + rotation * Vector3.Scale(sphere.center, scale), sphere.radius * MaxComponent(scale), SweepBuffer, layerMask, QueryTriggerInteraction.Collide);
                case CapsuleCollider capsule:
                    CapsuleEnds(capsule, position, rotation, scale, out Vector3 a, out Vector3 b, out float radius);
                    return Physics.OverlapCapsuleNonAlloc(a, b, radius, SweepBuffer, layerMask, QueryTriggerInteraction.Collide);
                default:
                    return 0;
            }
        }

        private static void CapsuleEnds(CapsuleCollider capsule, Vector3 position, Quaternion rotation, Vector3 scale, out Vector3 a, out Vector3 b, out float radius)
        {
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            int axisIndex = capsule.direction;
            float axisScale = scale[axisIndex];
            radius = capsule.radius * Mathf.Max(scale[(axisIndex + 1) % 3], scale[(axisIndex + 2) % 3]);
            float halfSegment = Mathf.Max(0f, capsule.height * axisScale * 0.5f - radius);
            Vector3 center = position + rotation * Vector3.Scale(capsule.center, scale);
            a = center + rotation * axis * halfSegment;
            b = center - rotation * axis * halfSegment;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static float MaxComponent(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));
    }
}
