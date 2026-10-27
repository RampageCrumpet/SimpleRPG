using SimpleRPG.Articulation;
using SimpleRPG.Controls;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This script turns input into player movement for a first person control scheme.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(UpperBodyArticulation))]
public class FirstPersonCharacterController : NetworkBehaviour, IViewSink
{
    /// <summary>
    /// The rotation of the player in degrees per second.
    /// </summary>
    [SerializeField]
    [Tooltip("The rotation of our player in degrees/second.")]
    private float rotationSpeed;

    /// <summary>
    /// The character controller we want to move around.
    /// </summary>
    private CharacterController characterController;

    /// <summary>
    /// The distance below the horizon line our character can look.
    /// </summary>
    [SerializeField]
    [Tooltip("The furthest down our character can look.")]
    private float maximumPitch = 60;

    /// <summary>
    /// The distance above our horizon line the character can look.
    /// </summary>
    [SerializeField]
    [Tooltip("The furthest up our character can look.")]
    private float minimumPitch = -60;

    /// <summary>
    /// A point on the head bone at the character's eyes. The animator's pose of it is measured, never copied to the camera: see
    /// <see cref="LateUpdate"/>.
    /// </summary>
    [SerializeField]
    [Tooltip("A transform under the head bone at the character's eyes. Its animated motion is not followed; only the lean the view gives it is.")]
    private Transform eye;

    [SerializeField]
    [Tooltip("Where the eye sits in the character root's space when standing at rest. The camera is held here, plus the lean the view gives the spine, so walking and other clips do not bob it.")]
    private Vector3 restEye = new Vector3(0f, 1.043f, 0.12f);

    [SerializeField]
    [Tooltip("Seconds for the camera to hand over from the held eye to the animated head when the character dies.")]
    private float deathBlendSeconds = 0.25f;

    [SerializeField]
    [Range(40f, 100f)]
    [Tooltip("The owner camera's vertical field of view in degrees. The hands hang well below the eye, so a wider view keeps more of the swing on screen; the aim is unaffected.")]
    private float fieldOfView = 75f;

    [SerializeField]
    [Tooltip("The owner camera's near clipping plane in metres. Small, because the sword passes within centimetres of the eye.")]
    private float nearClipPlane = 0.04f;

    /// <summary>
    /// The camera we want our script to be actively controlling.
    /// </summary>
    private Camera activeCamera;

    /// <summary>
    /// 1 while the camera is held at the rest eye, 0 when it follows the animated head (a dead character's falling body).
    /// </summary>
    private float stability = 1f;

    /// <summary>
    /// Applies mouse look to the owner's camera. Only exists on the owner.
    /// </summary>
    private FirstPersonLook look;

    /// <summary>
    /// The character this controller moves. A dead character neither moves nor turns.
    /// </summary>
    private SimpleRPG.Character character;

    /// <summary>
    /// Turns the player's view into the pose of the character's upper body, and the pelvis that follows it. The server owns that pose.
    /// </summary>
    private UpperBodyArticulation articulation;

    /// <summary>
    /// Start is called before the first frame update.
    /// </summary>
    public void Start()
    {
        this.activeCamera = Camera.main;
        this.characterController = this.GetComponent<CharacterController>();
        this.character = this.GetComponent<SimpleRPG.Character>();
        this.articulation = this.GetComponent<UpperBodyArticulation>();

        if (this.IsOwner)
        {
            if (this.activeCamera == null)
            {
                Debug.LogError("FirstPersonCharacterController: no active camera tagged MainCamera was found, so this character has no camera or mouse look.", this);
                return;
            }

            if (this.eye == null)
            {
                Debug.LogError("FirstPersonCharacterController: no eye transform is assigned, so the camera has nowhere to sit.", this);
                return;
            }

            // The camera hangs off the root only so it goes where the character goes; LateUpdate places it.
            FirstPersonLookMath.AttachCamera(activeCamera.transform, this.transform);
            this.articulation.TrackPoint(this.eye);
            this.activeCamera.nearClipPlane = this.nearClipPlane;
            this.activeCamera.fieldOfView = this.fieldOfView;

            // Start from the yaw the articulation already holds, not the transform's: the two are read at different moments and a respawn or
            // placement in between left the view and the server's body a fixed angle apart, so movement and the camera disagreed with the body.
            this.look = new FirstPersonLook(this, rotationSpeed, minimumPitch, maximumPitch, this.articulation.ViewYaw);
            this.articulation.ViewReset += this.look.ResetYaw;

            // Lock the cursor so it doesn't go wiggling everywhere.
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    /// <summary>
    /// FixedUpdate runs on the physics step and is used for movement only.
    /// </summary>
    public void FixedUpdate()
    {
        if (this.IsOwner)
        {
            // Calculate and enact our players movement.
            Vector3 movement = this.CalculateMovement();
            this.MoveCharacterServerRPC(movement);
        }
        else if (this.IsServer && !this.IsAlive && this.characterController.enabled)
        {
            // Nobody is steering a dead character, but gravity still applies to it.
            this.characterController.SimpleMove(Vector3.zero);
        }
    }

    /// <summary>
    /// Update runs once every frame and is used for look input, so mouse input is not tied to the physics step.
    /// </summary>
    public void Update()
    {
        // Only the owner reads input and moves its own camera. Other clients and the server never touch it.
        if (this.look != null && this.IsAlive && FirstPersonLookMath.ShouldLook(this.IsOwner, Application.isFocused))
        {
            Vector2 lookDelta = GameInput.Look.ReadValue<Vector2>();
            this.look.Look(lookDelta.x, lookDelta.y, Time.deltaTime);
        }
    }

    /// <summary>
    /// Places the camera. Its orientation is the view's, so it points exactly where the mouse does and the head's animation cannot tilt it. Its
    /// position is the rest eye on the character root plus how far the articulation (the lean into the view) moved the eye off the animated pose,
    /// so the walk's bob and sway, which live in the animated pose, never reach it. Runs after the articulation (this component's execution order is
    /// later), reading its result from this frame, so nothing lags and nothing is fed back: the camera is never read by anything that poses a bone.
    /// </summary>
    public void LateUpdate()
    {
        if (this.look == null)
        {
            return;
        }

        this.stability = Mathf.MoveTowards(this.stability, this.IsAlive ? 1f : 0f, this.deathBlendSeconds > 0f ? Time.deltaTime / this.deathBlendSeconds : 1f);
        this.activeCamera.transform.SetPositionAndRotation(
            FirstPersonLookMath.StableEyePosition(this.transform.TransformPoint(this.restEye), this.articulation.TrackedDisplacement, this.eye.position, this.stability),
            Quaternion.Euler(this.look.Pitch, this.look.Yaw, 0f));
    }

    public override void OnDestroy()
    {
        if (this.look != null && this.articulation != null)
        {
            this.articulation.ViewReset -= this.look.ResetYaw;
        }

        base.OnDestroy();
    }

    /// <summary>
    /// True while the character this controller moves is alive.
    /// </summary>
    private bool IsAlive => this.character == null || this.character.IsAlive;

    /// <summary>
    /// Calculates the direction of the players intended movement.
    /// </summary>
    /// <returns> Returns a <see cref="Vector3"/> representing the direction we want to move.</returns>
    private Vector3 CalculateMovement()
    {
        if (!this.IsAlive)
        {
            return Vector3.zero;
        }

        // Movement is relative to where the player looks. The body trails the view, so its own axes would steer the wrong way mid-turn.
        float viewYaw = this.look != null ? this.look.Yaw : this.transform.eulerAngles.y;
        FirstPersonLookMath.ViewAxes(viewYaw, out Vector3 forward, out Vector3 right);
        Vector2 move = GameInput.Move.ReadValue<Vector2>();
        Vector3 movementDirection = FirstPersonLookMath.MovementDirection(forward, right, move.y, move.x);

        return movementDirection;
    }

    /// <summary>
    /// Moves this player in the given direction.
    /// </summary>
    [ServerRpc]
    private void MoveCharacterServerRPC(Vector3 direction)
    {
        // The server decides whether the character may move, whatever the client sends.
        if (!this.IsAlive)
        {
            direction = Vector3.zero;
        }

        // Normalize the direction value given by the player to ensure it has no magnitude.
        direction.Normalize();
        
        // How fast the character moves is the server's resolved Movement Speed; the client only ever supplies a direction.
        float speed = this.character != null ? this.character.MovementSpeed : 0f;
        characterController.SimpleMove(speed * direction);
    }

    /// <summary>
    /// Tells the character's upper body where the view points. The articulation component sends it to the server.
    /// </summary>
    /// <param name="yawDelta"> Degrees the view turned this frame.</param>
    /// <param name="pitch"> Where the view now points vertically in degrees.</param>
    void IViewSink.Look(float yawDelta, float pitch)
    {
        this.articulation.SubmitLook(yawDelta, pitch);
    }
}
