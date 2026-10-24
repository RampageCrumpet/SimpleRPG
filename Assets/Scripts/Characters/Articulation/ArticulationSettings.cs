using System;
using UnityEngine;

namespace SimpleRPG.Articulation
{
    /// <summary>
    /// Where a character is in an attack, as far as upper-body control is concerned. Decides how fast the torso may follow the view.
    /// </summary>
    public enum ArticulationPhase
    {
        /// <summary>Not attacking: idle, walking, or any time no swing is running.</summary>
        Free,

        /// <summary>A swing has started and its blade is not yet live. The player may still aim it.</summary>
        Windup,

        /// <summary>The blade is live. The torso is committed and can only follow the view slowly.</summary>
        Active,

        /// <summary>The blade has passed. Control returns to normal.</summary>
        Recovery,
    }

    /// <summary>
    /// Anything that knows which <see cref="ArticulationPhase"/> a character is in. Implemented by a character's melee mechanics, so articulation
    /// never reads animation clips or combo data itself.
    /// </summary>
    public interface IArticulationPhaseSource
    {
        /// <summary>
        /// The phase right now, on whatever clock this machine keeps for the character (server clock on the server, the owner's prediction on the owning client).
        /// </summary>
        ArticulationPhase CurrentPhase { get; }
    }

    /// <summary>
    /// How fast the torso may turn and tilt toward the view, in degrees per second, for one <see cref="ArticulationPhase"/>.
    /// </summary>
    [Serializable]
    public struct PhaseSpeedLimit
    {
        [Tooltip("The fastest the torso may turn toward the view horizontally, in degrees per second.")]
        public float yawSpeed;

        [Tooltip("The fastest the torso may tilt toward the view vertically, in degrees per second.")]
        public float pitchSpeed;

        public PhaseSpeedLimit(float yawSpeed, float pitchSpeed)
        {
            this.yawSpeed = yawSpeed;
            this.pitchSpeed = pitchSpeed;
        }
    }

    /// <summary>
    /// How much of the torso twist and tilt each humanoid bone takes. The weights of a column should add up to one; the solver does not enforce it
    /// so a profile can deliberately under- or over-rotate the top of the chain.
    /// </summary>
    [Serializable]
    public struct BoneShare
    {
        public HumanBodyBones bone;

        [Tooltip("Share of the torso twist this bone takes.")]
        public float yaw;

        [Tooltip("Share of the torso tilt this bone takes.")]
        public float pitch;

        public BoneShare(HumanBodyBones bone, float yaw, float pitch)
        {
            this.bone = bone;
            this.yaw = yaw;
            this.pitch = pitch;
        }
    }

    /// <summary>
    /// Every tunable of view-driven upper-body articulation. Plain data with no engine objects, so the solver and its tests can build one directly.
    /// Angles are degrees; pitch is positive downward, like the camera's.
    /// </summary>
    [Serializable]
    public sealed class ArticulationSettings
    {
        [Header("Anatomical limits")]
        [Tooltip("The most the torso may twist away from the pelvis, either way.")]
        public float maxTwist = 70f;

        [Tooltip("The most the torso may tilt forward when the view looks down.")]
        public float maxPitchDown = 45f;

        [Tooltip("The most the torso may tilt backward when the view looks up.")]
        public float maxPitchUp = 35f;

        [Tooltip("How much of the view's pitch the torso takes on. The camera is limited by its own range; this scales it into the torso.")]
        public float pitchGain = 0.8f;

        [Header("Pelvis follow")]
        [Tooltip("Twist inside which the pelvis only drifts toward the torso slowly.")]
        public float pelvisDeadZone = 15f;

        [Tooltip("How fast the pelvis turns toward the torso inside the dead zone, in degrees per second.")]
        public float pelvisRecenterSpeed = 90f;

        [Tooltip("How fast the pelvis turns toward the torso beyond the dead zone, in degrees per second.")]
        public float pelvisFollowSpeed = 300f;

        [Header("Speed limits by attack phase")]
        public PhaseSpeedLimit free = new PhaseSpeedLimit(20000f, 20000f);

        public PhaseSpeedLimit windup = new PhaseSpeedLimit(540f, 360f);

        public PhaseSpeedLimit active = new PhaseSpeedLimit(180f, 120f);

        public PhaseSpeedLimit recovery = new PhaseSpeedLimit(360f, 240f);

        [Tooltip("Seconds over which a speed limit that relaxes (the strike ends) eases toward the new phase's value, so control returns smoothly. A limit that tightens applies at once.")]
        public float limitBlendSeconds = 0.12f;

        [Header("Pose")]
        [Tooltip("The bones that carry the twist and tilt, from the pelvis up. Bones the rig lacks are skipped.")]
        public BoneShare[] bones =
        {
            new BoneShare(HumanBodyBones.Spine, 0.2f, 0.2f),
            new BoneShare(HumanBodyBones.Chest, 0.3f, 0.3f),
            new BoneShare(HumanBodyBones.UpperChest, 0.3f, 0.3f),
            new BoneShare(HumanBodyBones.Neck, 0.1f, 0.1f),
            new BoneShare(HumanBodyBones.Head, 0.1f, 0.1f),
        };

        [Tooltip("Seconds over which the pose fades out on death and in on respawn.")]
        public float deathFadeSeconds = 0.25f;

        [Header("Network")]
        [Tooltip("Seconds of smoothing other players' replicated pose gets. The server's own pose, which decides hits, is never smoothed.")]
        public float remoteSmoothingSeconds = 0.06f;

        [Tooltip("The smallest change in twist or tilt, in degrees, the server bothers to replicate.")]
        public float replicationThreshold = 0.25f;

        [Tooltip("Largest view change one look message may carry, in degrees. Anything larger is clamped by the server.")]
        public float maxYawPerMessage = 180f;

        /// <summary>
        /// The speed limits for a phase.
        /// </summary>
        public PhaseSpeedLimit LimitFor(ArticulationPhase phase)
        {
            switch (phase)
            {
                case ArticulationPhase.Windup: return windup;
                case ArticulationPhase.Active: return active;
                case ArticulationPhase.Recovery: return recovery;
                default: return free;
            }
        }
    }
}
