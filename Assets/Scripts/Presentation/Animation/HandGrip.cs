using UnityEngine;

namespace SimpleRPG.Animations
{
    /// <summary>Which of a character's hands a <see cref="HandGrip"/> is for.</summary>
    public enum GripHand
    {
        Left,
        Right,
    }

    /// <summary>
    /// A place on a held item where one hand takes hold. The transform's position is where the centre of the palm goes and its rotation is the
    /// orientation of the hand bone, so the item defines both, whatever rig holds it. <see cref="HandGripIK"/> moves the hand there.
    /// </summary>
    public sealed class HandGrip : MonoBehaviour
    {
        [Tooltip("Which hand takes this grip.")]
        [SerializeField] private GripHand hand = GripHand.Left;

        [Tooltip("Where the centre of the palm is, in the hand bone's own space (metres). Wrist to the middle of the closed fingers' curl.")]
        [SerializeField] private Vector3 palmInHand = new Vector3(0f, 0.09f, -0.025f);

        /// <summary>The hand this grip is for.</summary>
        public GripHand Hand => hand;

        /// <summary>The hand bone's world pose that puts the palm on this grip.</summary>
        public void GetHandPose(out Vector3 position, out Quaternion rotation)
        {
            rotation = transform.rotation;
            position = transform.position - rotation * palmInHand;
        }
    }
}
