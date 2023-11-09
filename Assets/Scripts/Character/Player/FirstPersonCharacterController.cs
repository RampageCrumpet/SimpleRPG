using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This script turns input into player movement for a first person control scheme.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonCharacterController : NetworkBehaviour
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
    /// The minimum X rotation our camera can have.
    /// </summary>
    [SerializeField]
    [Tooltip("The furthest down our character can look.")]
    private float bottomRotationLimit = 60;

    /// <summary>
    /// The maximum X rotation our camera can have.
    /// </summary>
    [SerializeField]
    [Tooltip("The furthest up our character can look.")]
    private float topRotationLimit = -60;

    /// <summary>
    /// Start is called before the first frame update.
    /// </summary>
    public void Start()
    {
        this.characterController = this.GetComponent<CharacterController>();

        if (this.IsOwner)
        {
            // Move the main camera to this character.
            Camera.main.transform.SetParent(this.gameObject.transform);
            Camera.main.transform.position = this.gameObject.transform.position;

            //TODO: This doesn't belong here, delete it.
            this.gameObject.GetComponent<MeshRenderer>().forceRenderingOff = true;
        }
    }

    /// <summary>
    /// Update runs once every frame.
    /// </summary>
    public void Update()
    {
        if (this.IsOwner)
        {
            // Calculate and enact our players movement.
            Vector3 movement = this.CalculateMovement();
            this.MoveCharacterServerRPC(movement);

            // Only try to rotate the character if the camera is focused on the game.
            if (Application.isFocused)
            {
                this.RotateCharacterServerRPC(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }
        }
    }

    /// <summary>
    /// Calculates the direction of the players intended movement.
    /// </summary>
    /// <returns> Returns a <see cref="Vector3"/> representing the direction we want to move.</returns>
    private Vector3 CalculateMovement()
    {
        Vector3 movementDirection = Vector3.zero;
        movementDirection += this.transform.forward * Input.GetAxis("Vertical");
        movementDirection += this.transform.right * Input.GetAxis("Horizontal");

        Camera.main.transform.TransformDirection(movementDirection);
        movementDirection.y = 0;
        movementDirection.Normalize();

        return movementDirection;
    }

    /// <summary>
    /// Moves this player in the given direction.
    /// </summary>
    [ServerRpc]
    private void MoveCharacterServerRPC(Vector3 direction)
    {
        // Normalize the direction value given by the player to ensure it has no magnitude.
        direction.Normalize();
        characterController.Move((this.movementSpeed * Time.fixedDeltaTime * direction));
    }

    /// <summary>
    /// Rotates the character towards the mouse position.
    /// </summary>
    /// <param name="xMove"> The horizontal movement.</param>
    /// <param name="yMove"> The vertical movement.</param>
    [ServerRpc]
    private void RotateCharacterServerRPC(float xMove, float yMove)
    {
        float clampedYRotation = Mathf.Clamp(yMove * rotationSpeed, topRotationLimit, bottomRotationLimit);

        Camera.main.transform.Rotate(clampedYRotation, 0 , 0);
        this.transform.Rotate(0, xMove * rotationSpeed, 0);
    }

}
