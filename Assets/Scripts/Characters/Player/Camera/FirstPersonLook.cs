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
/// Applies mouse look to a camera. Holds the owner's view yaw and pitch and tells an <see cref="IViewSink"/> what changed. The camera is
/// aimed in world space, so it never inherits the body's rotation: the body follows the view, not the other way round. Has no network dependency.
/// </summary>
public sealed class FirstPersonLook
{
    private readonly UnityEngine.Transform camera;
    private readonly IViewSink viewSink;
    private readonly float rotationSpeed;
    private readonly float minimumPitch;
    private readonly float maximumPitch;

    /// <summary>
    /// The current camera pitch in degrees.
    /// </summary>
    public float Pitch { get; private set; }

    /// <summary>
    /// The current camera yaw in degrees, in world space.
    /// </summary>
    public float Yaw { get; private set; }

    /// <summary>
    /// Creates a look controller for one camera.
    /// </summary>
    /// <param name="camera"> The camera transform that is aimed.</param>
    /// <param name="viewSink"> Receives the view change each frame.</param>
    /// <param name="rotationSpeed"> The rotation speed in degrees per second.</param>
    /// <param name="minimumPitch"> The lowest allowed pitch in degrees.</param>
    /// <param name="maximumPitch"> The highest allowed pitch in degrees.</param>
    /// <param name="initialYaw"> The world yaw the view starts at.</param>
    public FirstPersonLook(UnityEngine.Transform camera, IViewSink viewSink, float rotationSpeed, float minimumPitch, float maximumPitch, float initialYaw = 0f)
    {
        this.camera = camera;
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
        this.ApplyCamera();

        this.viewSink.Look(yawStep, this.Pitch);
    }

    /// <summary>
    /// Points the view somewhere without reporting it, because the server moved it (a respawn).
    /// </summary>
    public void ResetYaw(float yaw)
    {
        this.Yaw = yaw;
        this.Pitch = 0f;
        this.ApplyCamera();
    }

    /// <summary>
    /// Aims the camera at the view. Called again late in the frame, after anything that moved its parent.
    /// </summary>
    public void ApplyCamera()
    {
        this.camera.rotation = UnityEngine.Quaternion.Euler(this.Pitch, this.Yaw, 0f);
    }
}
