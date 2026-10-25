using UnityEngine;

namespace SimpleRPG.Articulation
{
    /// <summary>
    /// Twists and tilts a humanoid's spine chain on top of whatever pose the animator just produced. Call <see cref="Apply"/> once per frame from
    /// LateUpdate, after animation: the offset is added to the animated pose, so every authored clip keeps working and the arms, head and
    /// weapon, which hang below the spine in the hierarchy, follow without being touched.
    /// </summary>
    public sealed class SpineRig
    {
        private readonly Transform[] bones;
        private readonly float[] yawShare;
        private readonly float[] pitchShare;

        // What the animator last left in each bone and what this rig turned it into, to undo our own offset if the animator did not run in between.
        private readonly Quaternion[] animatedLocal;
        private readonly Quaternion[] appliedLocal;
        private readonly bool[] hasApplied;

        public SpineRig(Animator animator, ArticulationSettings settings)
        {
            int count = settings.bones.Length;
            bones = new Transform[count];
            yawShare = new float[count];
            pitchShare = new float[count];
            for (int i = 0; i < count; i++)
            {
                bones[i] = animator.isHuman ? animator.GetBoneTransform(settings.bones[i].bone) : null;
                yawShare[i] = settings.bones[i].yaw;
                pitchShare[i] = settings.bones[i].pitch;
            }

            animatedLocal = new Quaternion[count];
            appliedLocal = new Quaternion[count];
            hasApplied = new bool[count];
        }

        /// <summary>
        /// Builds a rig over explicit bones, for characters without a humanoid avatar and for tests.
        /// </summary>
        public SpineRig(Transform[] bones, float[] yawShare, float[] pitchShare)
        {
            this.bones = bones;
            this.yawShare = yawShare;
            this.pitchShare = pitchShare;
            animatedLocal = new Quaternion[bones.Length];
            appliedLocal = new Quaternion[bones.Length];
            hasApplied = new bool[bones.Length];
        }

        /// <summary>True if at least one bone was found, so applying does something.</summary>
        public bool IsUsable
        {
            get
            {
                foreach (Transform bone in bones)
                {
                    if (bone != null) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Adds a twist and tilt to the spine.
        /// </summary>
        /// <param name="frame">The character's root. Its up axis is the twist axis and its right axis, turned by the twist, the tilt axis.</param>
        /// <param name="twist">Degrees to twist, positive to the right.</param>
        /// <param name="pitch">Degrees to tilt, positive forward.</param>
        public void Apply(Transform frame, float twist, float pitch)
        {
            Vector3 up = frame.up;
            Vector3 right = frame.right;
            float twistSoFar = 0f;

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    continue;
                }

                // If the animator did not rewrite this bone since our last offset, undo that offset first so offsets never pile up.
                if (hasApplied[i] && Same(bone.localRotation, appliedLocal[i]))
                {
                    bone.localRotation = animatedLocal[i];
                }

                animatedLocal[i] = bone.localRotation;

                float yaw = twist * yawShare[i];
                twistSoFar += yaw;
                Quaternion turn = Quaternion.AngleAxis(yaw, up);
                Quaternion tilt = Quaternion.AngleAxis(pitch * pitchShare[i], Quaternion.AngleAxis(twistSoFar, up) * right);

                // Rotating in world space about the bone's own pivot: ancestors already moved this frame are included, children follow.
                bone.rotation = tilt * turn * bone.rotation;

                appliedLocal[i] = bone.localRotation;
                hasApplied[i] = true;
            }
        }

        private static bool Same(Quaternion a, Quaternion b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
        }
    }
}
