namespace SimpleRPG
{
    /// <summary>
    /// Receives where the owner's view points. The network-backed implementation sends it to the server; tests use a fake.
    /// </summary>
    public interface IViewSink
    {
        /// <summary>
        /// The view moved this frame.
        /// </summary>
        /// <param name="yawDelta"> Degrees the view turned horizontally this frame.</param>
        /// <param name="pitch"> Where the view now points vertically in degrees, positive downward.</param>
        void Look(float yawDelta, float pitch);
    }

    /// <summary>
    /// Holds the owner's view yaw and pitch and tells an <see cref="IViewSink"/> what changed. It is input state only: nothing here reads or writes
    /// a camera, so the view never depends on where the camera ended up. The body follows the view, and the camera rides the head the body posed.
    /// Has no network dependency.
    /// </summary>
    public sealed class FirstPersonLook
    {
        private readonly IViewSink viewSink;
        private readonly float rotationSpeed;
        private readonly float minimumPitch;
        private readonly float maximumPitch;

        /// <summary>
        /// The current view pitch in degrees, positive downward.
        /// </summary>
        public float Pitch { get; private set; }

        /// <summary>
        /// The current view yaw in degrees, in world space.
        /// </summary>
        public float Yaw { get; private set; }

        /// <summary>
        /// Creates a look controller for one player.
        /// </summary>
        /// <param name="viewSink"> Receives the view change each frame.</param>
        /// <param name="rotationSpeed"> The rotation speed in degrees per second.</param>
        /// <param name="minimumPitch"> The lowest allowed pitch in degrees.</param>
        /// <param name="maximumPitch"> The highest allowed pitch in degrees.</param>
        /// <param name="initialYaw"> The world yaw the view starts at.</param>
        public FirstPersonLook(IViewSink viewSink, float rotationSpeed, float minimumPitch, float maximumPitch, float initialYaw = 0f)
        {
            this.viewSink = viewSink;
            this.rotationSpeed = rotationSpeed;
            this.minimumPitch = minimumPitch;
            this.maximumPitch = maximumPitch;
            this.Yaw = initialYaw;
        }

        /// <summary>
        /// Applies one frame of mouse look.
        /// </summary>
        /// <param name="mouseX"> The horizontal mouse movement for this frame.</param>
        /// <param name="mouseY"> The vertical mouse movement for this frame.</param>
        /// <param name="deltaTime"> The time since the last frame in seconds.</param>
        public void Look(float mouseX, float mouseY, float deltaTime)
        {
            this.Pitch = FirstPersonLookMath.StepPitch(this.Pitch, mouseY, this.rotationSpeed, deltaTime, this.minimumPitch, this.maximumPitch);
            float yawStep = FirstPersonLookMath.YawStep(mouseX, this.rotationSpeed, deltaTime);
            this.Yaw = UnityEngine.Mathf.Repeat(this.Yaw + yawStep + 180f, 360f) - 180f;

            this.viewSink.Look(yawStep, this.Pitch);
        }

        /// <summary>
        /// Points the view somewhere without reporting it, because the server moved it (a respawn).
        /// </summary>
        public void ResetYaw(float yaw)
        {
            this.Yaw = yaw;
            this.Pitch = 0f;
        }
    }
}
