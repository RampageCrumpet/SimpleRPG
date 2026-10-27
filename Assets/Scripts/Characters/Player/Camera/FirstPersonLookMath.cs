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
    /// Where the first-person camera sits: held at the rest eye plus the articulation's lean, or on the animated eye as the character dies.
    /// </summary>
    /// <param name="restEye"> The rest eye in world space, carried by the character root.</param>
    /// <param name="lean"> How far the articulation moved the eye from the animated pose, in world space.</param>
    /// <param name="animatedEye"> The eye where the animation and the articulation left it.</param>
    /// <param name="stability"> 1 to hold the camera at the rest eye, 0 to follow the animated eye.</param>
    public static UnityEngine.Vector3 StableEyePosition(UnityEngine.Vector3 restEye, UnityEngine.Vector3 lean, UnityEngine.Vector3 animatedEye, float stability)
    {
        return UnityEngine.Vector3.Lerp(animatedEye, restEye + lean, stability);
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
    /// The flat forward and right axes of a view yaw. Movement is relative to where the player looks, not to which way the body currently faces,
    /// because the body trails the view.
    /// </summary>
    /// <param name="yaw"> The view yaw in degrees.</param>
    /// <param name="forward"> The horizontal forward axis.</param>
    /// <param name="right"> The horizontal right axis.</param>
    public static void ViewAxes(float yaw, out UnityEngine.Vector3 forward, out UnityEngine.Vector3 right)
    {
        UnityEngine.Quaternion turn = UnityEngine.Quaternion.Euler(0f, yaw, 0f);
        forward = turn * UnityEngine.Vector3.forward;
        right = turn * UnityEngine.Vector3.right;
    }

    /// <summary>
    /// Converts one frame of horizontal mouse movement into a view yaw amount.
    /// </summary>
    /// <param name="mouseX"> The horizontal mouse movement for this frame.</param>
    /// <param name="rotationSpeed"> The rotation speed in degrees per second.</param>
    /// <param name="deltaTime"> The time since the last frame in seconds.</param>
    /// <returns> The yaw in degrees to turn the view by.</returns>
    public static float YawStep(float mouseX, float rotationSpeed, float deltaTime)
    {
        return mouseX * rotationSpeed * deltaTime;
    }
}
