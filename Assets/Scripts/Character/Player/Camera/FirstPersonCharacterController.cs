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
    /// The camera we want our script to be actively controlling.
    /// </summary>
    private Camera activeCamera;

    /// <summary>
    /// Start is called before the first frame update.
    /// </summary>
    public void Start()
    {
        this.activeCamera = Camera.main;
        this.characterController = this.GetComponent<CharacterController>();

        if (this.IsOwner)
        {
            // Move the main camera to this character.
            activeCamera.transform.SetParent(this.gameObject.transform);
            activeCamera.transform.position = this.gameObject.transform.position;

            // Lock the cursor so it doesn't go wiggling everywhere.
            Cursor.lockState = CursorLockMode.Locked;

            //TODO: This doesn't belong here, delete it.
            this.gameObject.GetComponent<MeshRenderer>().forceRenderingOff = true;

        }
    }

    /// <summary>
    /// Update runs once every frame.
    /// </summary>
    public void FixedUpdate()
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
    /// <param name="yaw"> The horizontal movement.</param>
    /// <param name="pitch"> The vertical movement.</param>
    [ServerRpc]
    private void RotateCharacterServerRPC(float yaw, float pitch)
    {
        // Calculate the new camera rotation and then ensure it's clamped between our minimum and maximum rotations.
        //Find the camera's rotation relative to the horizon line.
        float cameraPitch = activeCamera.transform.localRotation.eulerAngles.x + 180f;

        // Adjust the camera's rotation by the ammount we want to rotate.
        cameraPitch += pitch * rotationSpeed * Time.fixedDeltaTime;

        // Ensure it's between 0 and 360 degrees to be a valid angle.
        cameraPitch %= 360;

        // Ensure that it's between our minimum and maximum pitch, both adjusted to be relative to our horizon line.
        cameraPitch = Mathf.Clamp(cameraPitch, minimumPitch + 180f,maximumPitch + 180f);

        // Set the angle back to Unity's usual rotational origin and assign it to our camera.
        activeCamera.transform.localEulerAngles = new Vector3(cameraPitch - 180f, activeCamera.transform.localRotation.y, activeCamera.transform.localRotation.z);


        // Horizontal rotation.
        float cameraYaw = yaw * rotationSpeed * Time.fixedDeltaTime;
        this.gameObject.transform.Rotate(0, cameraYaw, 0);
    }

}
