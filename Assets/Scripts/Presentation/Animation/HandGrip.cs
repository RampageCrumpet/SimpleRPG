using UnityEngine;

namespace SimpleRPG.Animations
{
    /// <summary>Which of a character's hands a <see cref="HandGrip"/> is for.</summary>
    public enum GripHand
    {
        Left,
        Right,
    }

    /// <summary>One finger bone's local rotation in a closed hand.</summary>
    [System.Serializable]
    public struct FingerBone
    {
        public HumanBodyBones bone;
        public Quaternion localRotation;
    }

    /// <summary>
    /// A place on a held item where one hand takes hold. The transform's position is where the centre of the palm goes and its rotation is the
    /// orientation of the hand bone, so the item defines both, whatever rig holds it. <see cref="HandGripIK"/> moves the hand there and closes the
    /// fingers on the item as <see cref="FingerPose"/> says, since the clips do not curl the fingers of every body.
    /// </summary>
    public sealed class HandGrip : MonoBehaviour
    {
        [Tooltip("Which hand takes this grip.")]
        [SerializeField] private GripHand hand = GripHand.Left;

        [Tooltip("Where the centre of the palm is, in the hand bone's own space (metres). Wrist to the middle of the closed fingers' curl.")]
        [SerializeField] private Vector3 palmInHand = new Vector3(0f, 0.09f, -0.025f);

        [Tooltip("Whether the hand is moved onto this grip. Off for a hand the animation already holds the item with; its fingers are still closed.")]
        [SerializeField] private bool placeHand = true;

        [Tooltip("The closed hand on this item: the local rotation of each finger bone. Empty leaves the fingers to the animation.")]
        [SerializeField] private FingerBone[] fingerPose = new FingerBone[0];

        /// <summary>The hand this grip is for.</summary>
        public GripHand Hand => hand;

        /// <summary>Whether the hand is moved onto the grip, or only its fingers closed.</summary>
        public bool PlaceHand => placeHand;

        /// <summary>The finger bones of the closed hand.</summary>
        public System.Collections.Generic.IReadOnlyList<FingerBone> FingerPose => fingerPose;

        /// <summary>The hand bone's world pose that puts the palm on this grip.</summary>
        public void GetHandPose(out Vector3 position, out Quaternion rotation)
        {
            rotation = transform.rotation;
            position = transform.position - rotation * palmInHand;
        }
    }
}
