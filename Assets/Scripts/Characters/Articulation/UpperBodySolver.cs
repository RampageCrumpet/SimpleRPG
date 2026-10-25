using UnityEngine;

namespace SimpleRPG.Articulation
{
    /// <summary>
    /// Decides how the pelvis, torso and tilt follow the view, one step at a time. Pure maths with no scene or network state, so the server, the
    /// owning client and the tests run the same code.
    /// <para>
    /// Three yaws are kept apart. The <b>view yaw</b> is where the player looks and is never limited. The <b>torso yaw</b> chases the view at
    /// the speed the attack phase allows; it is what the weapon follows. The <b>pelvis yaw</b> (the character's root) chases the torso, and is
    /// dragged along when the torso would otherwise twist past the anatomical limit. The twist the spine shows is torso minus pelvis.
    /// </para>
    /// </summary>
    public sealed class UpperBodySolver
    {
        private readonly ArticulationSettings settings;
        private float yawCap;
        private float pitchCap;

        public UpperBodySolver(ArticulationSettings settings)
        {
            this.settings = settings;
            Reset(0f, 0f);
        }

        /// <summary>The character root's yaw in degrees (what faces the legs and the movement animation).</summary>
        public float PelvisYaw { get; private set; }

        /// <summary>The world yaw in degrees the torso, and so the weapon, currently faces.</summary>
        public float TorsoYaw { get; private set; }

        /// <summary>The torso tilt in degrees, positive forward/down.</summary>
        public float TorsoPitch { get; private set; }

        /// <summary>The twist of the torso relative to the pelvis, within the anatomical limit.</summary>
        public float Twist => Clamp(Mathf.DeltaAngle(PelvisYaw, TorsoYaw), settings.maxTwist);

        /// <summary>
        /// Puts everything at rest facing a yaw and pitch, with the speed limits those of free movement. Used on spawn and respawn.
        /// </summary>
        public void Reset(float yaw, float pitch)
        {
            PelvisYaw = yaw;
            TorsoYaw = yaw;
            TorsoPitch = TargetPitch(pitch);
            yawCap = settings.free.yawSpeed;
            pitchCap = settings.free.pitchSpeed;
        }

        /// <summary>
        /// Advances by one frame.
        /// </summary>
        /// <param name="viewYaw">Where the player looks, as a world yaw.</param>
        /// <param name="viewPitch">How far the player looks down (negative: up), as the camera's pitch.</param>
        /// <param name="phase">Where the character is in an attack.</param>
        /// <param name="deltaTime">Seconds since the last step.</param>
        public void Step(float viewYaw, float viewPitch, ArticulationPhase phase, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            PhaseSpeedLimit target = settings.LimitFor(phase);
            float blend = settings.limitBlendSeconds <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / settings.limitBlendSeconds);
            yawCap = Ease(yawCap, target.yawSpeed, blend);
            pitchCap = Ease(pitchCap, target.pitchSpeed, blend);

            TorsoYaw = MoveTowardsAngle(TorsoYaw, viewYaw, yawCap * deltaTime);
            TorsoPitch = Mathf.MoveTowards(TorsoPitch, TargetPitch(viewPitch), pitchCap * deltaTime);

            // The pelvis follows the torso, slowly while the twist is small and faster once it is not, and never faster than the torso itself may turn.
            float offset = Mathf.DeltaAngle(PelvisYaw, TorsoYaw);
            float follow = Mathf.Abs(offset) > settings.pelvisDeadZone ? settings.pelvisFollowSpeed : settings.pelvisRecenterSpeed;
            PelvisYaw = MoveTowardsAngle(PelvisYaw, TorsoYaw, Mathf.Min(follow, yawCap) * deltaTime);

            // A twist past the limit drags the pelvis with it.
            float remaining = Mathf.DeltaAngle(PelvisYaw, TorsoYaw);
            if (Mathf.Abs(remaining) > settings.maxTwist)
            {
                PelvisYaw = TorsoYaw - Mathf.Sign(remaining) * settings.maxTwist;
            }

            PelvisYaw = Mathf.Repeat(PelvisYaw + 180f, 360f) - 180f;
            TorsoYaw = Mathf.Repeat(TorsoYaw + 180f, 360f) - 180f;
        }

        // A limit that tightens (a swing begins) applies at once; one that relaxes (control returns) eases in, so it never snaps back to full speed.
        private static float Ease(float current, float target, float blend)
        {
            return target < current ? target : Mathf.Lerp(current, target, blend);
        }

        private float TargetPitch(float viewPitch)
        {
            return Mathf.Clamp(viewPitch * settings.pitchGain, -settings.maxPitchUp, settings.maxPitchDown);
        }

        private static float Clamp(float value, float limit) => Mathf.Clamp(value, -limit, limit);

        private static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = Mathf.DeltaAngle(current, target);
            if (Mathf.Abs(delta) <= maxDelta)
            {
                return current + delta;
            }

            return current + Mathf.Sign(delta) * maxDelta;
        }
    }
}
