/// <summary>
/// Receives the body yaw produced by mouse look. The network-backed implementation sends it to the server; tests use a fake.
/// </summary>
public interface IBodyYawSink
{
    /// <summary>
    /// Turns the character body by the given amount.
    /// </summary>
    /// <param name="yaw"> The yaw in degrees to rotate by.</param>
    void Turn(float yaw);
}

/// <summary>
/// Applies mouse look to a camera. Holds the owner's pitch and sends yaw to an <see cref="IBodyYawSink"/>. Has no network dependency.
/// </summary>
public sealed class FirstPersonLook
{
    private readonly UnityEngine.Transform camera;
    private readonly IBodyYawSink yawSink;
    private readonly float rotationSpeed;
    private readonly float minimumPitch;
    private readonly float maximumPitch;

    /// <summary>
    /// The current camera pitch in degrees.
    /// </summary>
    public float Pitch { get; private set; }

    /// <summary>
    /// Creates a look controller for one camera.
    /// </summary>
    /// <param name="camera"> The camera transform whose pitch is controlled.</param>
    /// <param name="yawSink"> Receives the body yaw each frame.</param>
    /// <param name="rotationSpeed"> The rotation speed in degrees per second.</param>
    /// <param name="minimumPitch"> The lowest allowed pitch in degrees.</param>
    /// <param name="maximumPitch"> The highest allowed pitch in degrees.</param>
    public FirstPersonLook(UnityEngine.Transform camera, IBodyYawSink yawSink, float rotationSpeed, float minimumPitch, float maximumPitch)
    {
        this.camera = camera;
        this.yawSink = yawSink;
        this.rotationSpeed = rotationSpeed;
        this.minimumPitch = minimumPitch;
        this.maximumPitch = maximumPitch;
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
        this.camera.localEulerAngles = new UnityEngine.Vector3(this.Pitch, 0f, 0f);

        this.yawSink.Turn(FirstPersonLookMath.YawStep(mouseX, this.rotationSpeed, deltaTime));
    }
}
