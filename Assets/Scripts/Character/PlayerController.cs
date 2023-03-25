using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script manages player movement.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
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
    /// The rigidbody controlling this players physics interactions.
    /// </summary>
    new private Rigidbody rigidbody;

    // Start is called before the first frame update
    void Start()
    {
        this.rigidbody = this.GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Calculate and enact our players movement.
        Vector3 movement = CalculateMovement();
        MoveCharacter(movement);

        RotateCharacter();
    }

    /// <summary>
    /// Calculates the magnitude and direction of the players intended movement.
    /// </summary>
    /// <returns> Returns the global position we want to move to.</returns>
    private Vector3 CalculateMovement()
    {
        Vector3 movementDirection = Vector3.zero;
        movementDirection += Vector3.forward * Input.GetAxis("Vertical");
        movementDirection += Vector3.right * Input.GetAxis("Horizontal");

        Camera.main.transform.TransformDirection(movementDirection);
        movementDirection.y = 0;
        movementDirection.Normalize();

        movementDirection *=  movementSpeed;
        return movementDirection;
    }


    /// <summary>
    /// Moves this player in the given direction.
    /// </summary>
    private void MoveCharacter(Vector3 direction)
    {
        // Make the movement relative to the cameras local coordinates.
        //direction = Camera.main.transform.TransformDirection(direction);
        
        rigidbody.MovePosition(this.transform.position + (direction * Time.deltaTime));
    }

    /// <summary>
    /// Rotates the character towards the mouse position.
    /// </summary>
    private void RotateCharacter()
    {
        // The mouses position in 3d space.
        Vector3 mousePosition = FindMousePosition();

        //The final rotation we want to end up at.
        Quaternion targetRotation = Quaternion.LookRotation(mousePosition - this.transform.position, Vector3.up);

        // Rotate the player towards our target.
        Quaternion newRotation = Quaternion.RotateTowards(this.rigidbody.rotation, targetRotation, rotationSpeed*Time.deltaTime);
        newRotation.x = this.rigidbody.rotation.x;
        newRotation.z = this.rigidbody.rotation.z;
        
        this.rigidbody.MoveRotation(newRotation);
    }

    /// <summary>
    /// Finds the mouse position in world space.
    /// </summary>
    /// <returns> The mouse position in world space.</returns>
    private Vector3 FindMousePosition()
    {
        // The ray we want to draw from the mouse position on the screen through the camera.
        // It's hit location will be the location of the mouse in 3d space.
        Ray cameraRay = Camera.main.ScreenPointToRay(Input.mousePosition);

        // The details about what our ray hit and where.
        RaycastHit hit;

        if(Physics.Raycast(cameraRay, out hit, Mathf.Infinity))
        {
            return hit.point;
        }

        // If we didn't hit anything we need to figure out what direction to look.
        Debug.LogWarning("We're not hitting anything when trying to find a mouse position.");
        return Camera.main.transform.position;
    }
}
