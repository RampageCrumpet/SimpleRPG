using UnityEngine;

namespace SimpleRPG.Animations
{
    /// <summary>
    /// Keeps a character's hands on the grips of what they hold. The animation moves the main hand and the item with it; a hand that has its own
    /// <see cref="HandGrip"/> on the item (the off hand of a two-handed weapon) is pulled onto it, so the grip survives a body whose arms are not the
    /// proportions the clip was made for. The arm is solved here, after the animator and the upper-body articulation have posed the character, with a
    /// two-bone solve that keeps the animated elbow's side. A grip out of reach leaves the arm straight and pointing at it.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(Animator))]
    public sealed class HandGripIK : MonoBehaviour
    {
        [Tooltip("Seconds to take a hand onto a grip, or off it when the item is put away.")]
        [SerializeField] private float blendSeconds = 0.15f;

        private Animator animator;
        private Character character;
        private Equipment equipment;
        private HandGrip[] grips = new HandGrip[0];
        private readonly float[] weights = new float[2];

        private void Awake()
        {
            animator = GetComponent<Animator>();
            character = GetComponent<Character>();
            equipment = GetComponent<Equipment>();
        }

        private void Start()
        {
            Refresh();
            if (equipment != null) equipment.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (equipment != null) equipment.Changed -= Refresh;
        }

        private void Refresh() => grips = GetComponentsInChildren<HandGrip>(true);

        private void LateUpdate()
        {
            if (animator == null || !animator.isHuman) return;

            bool alive = character == null || character.IsAlive;
            float step = blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f;
            Solve(GripHand.Left, alive, step, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            Solve(GripHand.Right, alive, step, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        }

        private void Solve(GripHand side, bool alive, float step, HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone)
        {
            HandGrip grip = alive ? GripFor(side) : null;
            float weight = Mathf.MoveTowards(weights[(int)side], grip != null ? 1f : 0f, step);
            weights[(int)side] = weight;
            if (weight <= 0f || grip == null) return;

            Transform upper = animator.GetBoneTransform(upperBone);
            Transform lower = animator.GetBoneTransform(lowerBone);
            Transform hand = animator.GetBoneTransform(handBone);
            if (upper == null || lower == null || hand == null) return;

            grip.GetHandPose(out Vector3 gripPosition, out Quaternion gripRotation);
            Vector3 target = Vector3.Lerp(hand.position, gripPosition, weight);
            Quaternion handRotation = Quaternion.Slerp(hand.rotation, gripRotation, weight);
            SolveTwoBone(upper, lower, hand, target);
            hand.rotation = handRotation;
        }

        // Bends the elbow so the wrist reaches the target, on the same side the animation had it, or stretches straight toward it when it is too far.
        private static void SolveTwoBone(Transform upper, Transform lower, Transform hand, Vector3 target)
        {
            Vector3 root = upper.position;
            Vector3 elbow = lower.position;
            Vector3 wrist = hand.position;
            float upperLength = Vector3.Distance(root, elbow);
            float lowerLength = Vector3.Distance(elbow, wrist);
            if (upperLength < 1e-4f || lowerLength < 1e-4f) return;

            Vector3 toTarget = target - root;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 1e-3f, upperLength + lowerLength - 1e-4f);
            Vector3 direction = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : (wrist - root).normalized;

            // Which way the elbow bends: the animated elbow's offset from the root-to-target line.
            Vector3 bend = Vector3.ProjectOnPlane(elbow - root, direction);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(-upper.up, direction);
            bend.Normalize();

            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float lateral = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 newElbow = root + direction * along + bend * lateral;
            Vector3 reached = root + direction * distance;

            upper.rotation = Quaternion.FromToRotation(elbow - root, newElbow - root) * upper.rotation;
            // The forearm followed the upper arm; swing it from wherever the wrist went to the reached point.
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, reached - newElbow) * lower.rotation;
        }

        private HandGrip GripFor(GripHand side)
        {
            foreach (HandGrip grip in grips)
                if (grip != null && grip.Hand == side && grip.isActiveAndEnabled) return grip;

            return null;
        }
    }
}
