/// <summary>
/// Pure look calculations used by <see cref="FirstPersonCharacterController"/>. Kept free of scene and network state so they can be tested directly.
/// </summary>
public static class FirstPersonLookMath
{
    /// <summary>
    /// Whether mouse look should be applied this frame.
    /// </summary>
    /// <param name="isOwner"> Whether this instance owns the character.</param>
    /// <param name="isFocused"> Whether the application currently has focus.</param>
    /// <returns> True only for the owner while the game is focused.</returns>
    public static bool ShouldLook(bool isOwner, bool isFocused)
    {
        return isOwner && isFocused;
    }

    /// <summary>
    /// Advances the camera pitch by one frame of vertical mouse movement and clamps it to the allowed range.
    /// </summary>
    /// <param name="currentPitch"> The pitch in degrees before this frame.</param>
    /// <param name="mouseY"> The vertical mouse movement for this frame.</param>
    /// <param name="rotationSpeed"> The rotation speed in degrees per second.</param>
    /// <param name="deltaTime"> The time since the last frame in seconds.</param>
    /// <param name="minimumPitch"> The lowest allowed pitch in degrees.</param>
    /// <param name="maximumPitch"> The highest allowed pitch in degrees.</param>
    /// <returns> The new pitch in degrees, between the minimum and maximum.</returns>
    public static float StepPitch(float currentPitch, float mouseY, float rotationSpeed, float deltaTime, float minimumPitch, float maximumPitch)
    {
        return UnityEngine.Mathf.Clamp(currentPitch + mouseY * rotationSpeed * deltaTime, minimumPitch, maximumPitch);
    }

    /// <summary>
    /// Moves the camera under the anchor with its local position reset, so it follows the character.
    /// </summary>
    /// <param name="camera"> The camera transform to attach.</param>
    /// <param name="anchor"> The transform the camera should be parented to.</param>
    public static void AttachCamera(UnityEngine.Transform camera, UnityEngine.Transform anchor)
    {
        camera.SetParent(anchor);
        camera.localPosition = UnityEngine.Vector3.zero;
    }

    /// <summary>
    /// Builds the flat, normalized movement direction from the character's axes and the player's input.
    /// </summary>
    /// <param name="forward"> The character's forward axis.</param>
    /// <param name="right"> The character's right axis.</param>
    /// <param name="vertical"> The forward/back input.</param>
    /// <param name="horizontal"> The strafe input.</param>
    /// <returns> A horizontal unit vector, or zero when there is no input.</returns>
    public static UnityEngine.Vector3 MovementDirection(UnityEngine.Vector3 forward, UnityEngine.Vector3 right, float vertical, float horizontal)
    {
        UnityEngine.Vector3 direction = forward * vertical + right * horizontal;
        direction.y = 0;
        return direction.normalized;
    }

    /// <summary>
    /// Converts one frame of horizontal mouse movement into a body yaw amount.
    /// </summary>
    /// <param name="mouseX"> The horizontal mouse movement for this frame.</param>
    /// <param name="rotationSpeed"> The rotation speed in degrees per second.</param>
    /// <param name="deltaTime"> The time since the last frame in seconds.</param>
    /// <returns> The yaw in degrees to rotate the body by.</returns>
    public static float YawStep(float mouseX, float rotationSpeed, float deltaTime)
    {
        return mouseX * rotationSpeed * deltaTime;
    }
}
