using SimpleRPG;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace SimpleRPG.HitDetection
{
    /// <summary>
    /// A weapon's live blade. Each physics step it asks which hurtboxes the blade overlapped since the previous step, at the poses along the way
    /// and at the step's own pose, so a fast swing cannot step over a thin target between two steps. For each character it touches it reports one
    /// hurtbox, chosen by the rule on <see cref="HitCandidate"/>, through <see cref="HitboxCollisionEvent"/>, and it reports that character only
    /// once until it is switched off and on again. Which region of a character was struck therefore never depends on the order physics lists
    /// its colliders in.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        /// <summary>
        /// The most poses between two physics steps that are tested. A blade moving further than this covers is checked at the poses nearest to its path.
        /// </summary>
        private const int MaxSweepSamples = 12;

        private static readonly Collider[] OverlapBuffer = new Collider[64];
        private static readonly RaycastHit[] RayBuffer = new RaycastHit[16];

        [SerializeField]
        [Tooltip("The physics layer of the level's solid geometry (walls, doors, pillars). A weapon's blade cannot pass through it: the swing bounces off and hits nothing past it. Floors are not on it, so a low chop is not stopped by the ground.")]
        private string obstructionLayer = "World";

        private int obstructionMask;
        private bool blocked;

        private readonly HashSet<Character> reported = new HashSet<Character>();
        private readonly List<HitCandidate> found = new List<HitCandidate>();
        private LayerMask layerMask;
        private Collider shape;
        private bool hasPreviousPose;
        private Vector3 previousPosition;
        private Quaternion previousRotation;

        private void OnEnable()
        {
            reported.Clear();
            hasPreviousPose = false;
            blocked = false;
            obstructionMask = string.IsNullOrEmpty(obstructionLayer) ? 0 : LayerMask.GetMask(obstructionLayer);

            layerMask = LayerMask.GetMask("HurtBox");
            shape = GetComponent<Collider>();
            if (layerMask == 0)
            {
                Debug.LogError($"Hitbox on '{name}' could not find the 'HurtBox' layer. Add it under Tags and Layers; hits will not register without it.", this);
            }

            if (!(shape is BoxCollider || shape is SphereCollider || shape is CapsuleCollider))
            {
                Debug.LogError($"Hitbox on '{name}' needs a box, sphere or capsule collider; a {shape.GetType().Name} cannot be tested for hits.", this);
            }
        }

        private void OnDisable() => reported.Clear();

        public UnityEvent<Character, BodyLocation> HitboxCollisionEvent { get; set; } = new UnityEvent<Character, BodyLocation>();

        /// <summary>
        /// Where a line to the blade starts: the wielder's body. While this gives a point and the line from it to the blade's far end crosses solid
        /// level geometry, the blade is behind a wall: the hitbox reports <see cref="Obstructed"/> once and finds nothing more until it is switched
        /// off and on again. Null, or a function returning null, switches the check off (a projectile, or a weapon with no wielder).
        /// </summary>
        public System.Func<Vector3?> ObstructionOrigin { get; set; }

        /// <summary>
        /// Raised once per window, with the point and surface normal where the blade met the wall.
        /// </summary>
        public event System.Action<Vector3, Vector3> Obstructed;

        // Runs before the physics step, so the blade's transform is where the last frame left it, final pose included. The previous step's pose is
        // remembered here, not read from physics, so the path between them is the blade's real path whatever moved it.
        private void FixedUpdate()
        {
            if (!enabled || shape == null || layerMask == 0)
            {
                return;
            }

            Transform blade = transform;
            int samples = hasPreviousPose ? SampleCount(previousPosition, previousRotation, blade.position, blade.rotation) : 1;

            // Hurtboxes ride animated bones too; bring their colliders up to this frame before asking what overlaps them.
            Physics.SyncTransforms();

            for (int i = 1; i <= samples && enabled; i++)
            {
                float t = i / (float)samples;
                Vector3 position = hasPreviousPose ? Vector3.Lerp(previousPosition, blade.position, t) : blade.position;
                Quaternion rotation = hasPreviousPose ? Quaternion.Slerp(previousRotation, blade.rotation, t) : blade.rotation;

                // A blade behind a wall hits nothing: the wall is checked first, so a target on the far side is never reached.
                if (blocked || IsObstructed(position, rotation))
                {
                    break;
                }

                Probe(position, rotation);
            }

            previousPosition = blade.position;
            previousRotation = blade.rotation;
            hasPreviousPose = true;
        }

        // True, and reported once, if solid geometry lies between the wielder and the blade's far end at this pose.
        private bool IsObstructed(Vector3 position, Quaternion rotation)
        {
            if (obstructionMask == 0 || ObstructionOrigin == null)
            {
                return false;
            }

            Vector3? start = ObstructionOrigin();
            if (!start.HasValue)
            {
                return false;
            }

            Vector3 origin = start.Value;
            Vector3 toEnd = FarEnd(position, rotation, origin) - origin;
            float distance = toEnd.magnitude;
            if (distance < 0.01f)
            {
                return false;
            }

            int count = Physics.RaycastNonAlloc(origin, toEnd / distance, RayBuffer, distance, obstructionMask, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                if (nearest < 0 || RayBuffer[i].distance < RayBuffer[nearest].distance)
                {
                    nearest = i;
                }
            }

            if (nearest < 0)
            {
                return false;
            }

            blocked = true;
            Obstructed?.Invoke(RayBuffer[nearest].point, RayBuffer[nearest].normal);
            return true;
        }

        // The end of the blade furthest from the wielder, which is the part that reaches through a wall first.
        private Vector3 FarEnd(Vector3 position, Quaternion rotation, Vector3 from)
        {
            Vector3 scale = Abs(transform.lossyScale);
            switch (shape)
            {
                case BoxCollider box:
                {
                    Vector3 centre = position + rotation * Vector3.Scale(box.center, scale);
                    Vector3 half = Vector3.Scale(box.size, scale) * 0.5f;
                    int longest = half.x >= half.y && half.x >= half.z ? 0 : half.y >= half.z ? 1 : 2;
                    Vector3 along = rotation * (longest == 0 ? Vector3.right : longest == 1 ? Vector3.up : Vector3.forward) * half[longest];
                    return Farther(centre + along, centre - along, from);
                }
                case CapsuleCollider capsule:
                {
                    CapsuleEnds(capsule, position, rotation, scale, out Vector3 a, out Vector3 b, out float _);
                    return Farther(a, b, from);
                }
                case SphereCollider sphere:
                    return position + rotation * Vector3.Scale(sphere.center, scale);
                default:
                    return position;
            }
        }

        private static Vector3 Farther(Vector3 a, Vector3 b, Vector3 from) => (a - from).sqrMagnitude >= (b - from).sqrMagnitude ? a : b;

        // How many poses the blade's path between two steps is tested at: spaced by the blade's thinnest extent, so no gap a thin target could hide in.
        private int SampleCount(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation)
        {
            float reach = shape.bounds.extents.magnitude;
            float travel = (toPosition - fromPosition).magnitude + Mathf.Deg2Rad * Quaternion.Angle(fromRotation, toRotation) * reach;
            float spacing = Mathf.Max(Thickness(), 0.02f);
            return Mathf.Clamp(Mathf.CeilToInt(travel / spacing), 1, MaxSweepSamples);
        }

        // Reports the best hurtbox of each character the blade overlaps at this pose and has not reported already.
        private void Probe(Vector3 position, Quaternion rotation)
        {
            int count = Overlap(position, rotation, out Vector3 centre);
            found.Clear();
            for (int c = 0; c < count; c++)
            {
                Collider other = OverlapBuffer[c];
                if (!other.TryGetComponent(out HurtBox hurtBox))
                {
                    continue;
                }

                Character owner = hurtBox.character;
                if (owner == null || reported.Contains(owner))
                {
                    continue;
                }

                HitCandidate candidate = new HitCandidate(
                    owner,
                    hurtBox.BodyLocation,
                    hurtBox.Priority,
                    hurtBox.Key,
                    Vector3.Distance(other.ClosestPoint(centre), centre),
                    Vector3.Distance(other.bounds.center, centre));
                Offer(candidate);
            }

            // Characters struck at this pose are reported in a fixed order too: the one the blade is nearest first.
            found.Sort((a, b) => a.IsPreferredTo(b) ? -1 : b.IsPreferredTo(a) ? 1 : 0);
            for (int i = 0; i < found.Count && enabled; i++)
            {
                HitCandidate hit = found[i];
                if (reported.Add(hit.Character))
                {
                    HitboxCollisionEvent.Invoke(hit.Character, hit.Location);
                }
            }
        }

        private void Offer(in HitCandidate candidate)
        {
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].Character == candidate.Character)
                {
                    if (candidate.IsPreferredTo(found[i]))
                    {
                        found[i] = candidate;
                    }

                    return;
                }
            }

            found.Add(candidate);
        }

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

        private int Overlap(Vector3 position, Quaternion rotation, out Vector3 centre)
        {
            Vector3 scale = Abs(transform.lossyScale);
            switch (shape)
            {
                case BoxCollider box:
                    centre = position + rotation * Vector3.Scale(box.center, scale);
                    return Physics.OverlapBoxNonAlloc(centre, Vector3.Scale(box.size, scale) * 0.5f, OverlapBuffer, rotation, layerMask, QueryTriggerInteraction.Collide);
                case SphereCollider sphere:
                    centre = position + rotation * Vector3.Scale(sphere.center, scale);
                    return Physics.OverlapSphereNonAlloc(centre, sphere.radius * MaxComponent(scale), OverlapBuffer, layerMask, QueryTriggerInteraction.Collide);
                case CapsuleCollider capsule:
                    CapsuleEnds(capsule, position, rotation, scale, out Vector3 a, out Vector3 b, out float radius);
                    centre = (a + b) * 0.5f;
                    return Physics.OverlapCapsuleNonAlloc(a, b, radius, OverlapBuffer, layerMask, QueryTriggerInteraction.Collide);
                default:
                    centre = position;
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
