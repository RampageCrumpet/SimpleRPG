using SimpleRPG.Controls;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This script turns input into player movement for a first person control scheme.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonCharacterController : NetworkBehaviour, IBodyYawSink
{
    /// <summary>
    /// The movement of the player in unity units per second.
    /// </summary>
    [SerializeField]
    [Tooltip("The movment of the Player in unity units/second.")]
    private float movementSpeed;

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
    /// Start is called before the first frame update.
    /// </summary>
    public void Start()
    {
        this.activeCamera = Camera.main;
        this.characterController = this.GetComponent<CharacterController>();
        this.character = this.GetComponent<SimpleRPG.Character>();

        if (this.IsOwner)
        {
            if (this.activeCamera == null)
            {
                Debug.LogError("FirstPersonCharacterController: no active camera tagged MainCamera was found, so this character has no camera or mouse look.", this);
                return;
            }

            // Move the main camera to this character.
            FirstPersonLookMath.AttachCamera(activeCamera.transform, cameraTransform);

            this.look = new FirstPersonLook(activeCamera.transform, this, rotationSpeed, minimumPitch, maximumPitch);

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

        Vector3 movementDirection = FirstPersonLookMath.MovementDirection(this.transform.forward, this.transform.right, GameInput.Move.ReadValue<Vector2>().y, GameInput.Move.ReadValue<Vector2>().x);

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
        characterController.SimpleMove((this.movementSpeed * direction));
    }

    /// <summary>
    /// Turns this character by the given yaw amount on the server, so the rotation replicates to everyone.
    /// </summary>
    /// <param name="yaw"> The yaw in degrees to rotate by.</param>
    [ServerRpc]
    private void RotateCharacterServerRPC(float yaw)
    {
        if (!this.IsAlive)
        {
            return;
        }

        this.gameObject.transform.Rotate(0, yaw, 0);
    }

    /// <summary>
    /// Sends the body yaw from mouse look to the server.
    /// </summary>
    /// <param name="yaw"> The yaw in degrees to rotate by.</param>
    void IBodyYawSink.Turn(float yaw)
    {
        this.RotateCharacterServerRPC(yaw);
    }
}
