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

    [SerializeField]
    [Tooltip("The transform we want to snap the camera to.")]
    private Transform cameraTransform;

    /// <summary>
    /// The camera we want our script to be actively controlling.
    /// </summary>
    private Camera activeCamera;

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
    /// The head bone the eye rides on, and where the camera mount sits in that bone's space, so the eye leans with the body.
    /// </summary>
    private Vector3 eyeInHead;

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

            // Move the main camera to this character.
            FirstPersonLookMath.AttachCamera(activeCamera.transform, cameraTransform);

            if (this.articulation.Head != null)
            {
                this.eyeInHead = this.articulation.Head.InverseTransformPoint(cameraTransform.position);
            }

            this.look = new FirstPersonLook(activeCamera.transform, this, rotationSpeed, minimumPitch, maximumPitch, this.transform.eulerAngles.y);
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
    /// The camera follows the view, not the body, so it is aimed again after everything that could have moved its parent this frame.
    /// </summary>
    public void LateUpdate()
    {
        this.look?.ApplyCamera();

        // Runs after the articulation has posed the spine this frame. The eye stays on its mount and moves only by how far the bend carried the
        // head, so leaning forward or back moves the view without the head's idle sway or the rig's rest pose shifting where the eye sits.
        Transform head = this.articulation.Head;
        if (this.look != null && head != null)
        {
            Vector3 before = this.articulation.HeadToWorldBeforeBend.MultiplyPoint3x4(this.eyeInHead);
            Vector3 after = head.TransformPoint(this.eyeInHead);
            this.activeCamera.transform.position = this.cameraTransform.position + (after - before);
        }
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
