using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectionBlueprint : MonoBehaviour
{
    /// <summary>
    /// The length of the connection in unity units away from the room it exists on..
    /// </summary>
    [Tooltip("The length of the connection in unity units away from the room it exists on.")]
    [field: SerializeField]
    public float ConnectionLength { get; set; }

    [Tooltip("The width of the connection in unity units.")]
    [field:SerializeField]
    public float ConnectionWidth { get; set; }

    [Tooltip("The object that will be used to seal the connection when it is closed.")]
    [field: SerializeField]
    public GameObject SealingObject;

    [Tooltip("When on, this door must sit exactly Door Inset Cells in from the edge it faces. Off lets the door be placed freely.")]
    public bool EnforceDoorInset;

    [Tooltip("How many cells in from the edge this door sits, measured from the room's lowest corner. Only checked when Enforce Door Inset is on.")]
    public int DoorInsetCells = 2;

    /// <summary>
    /// Gets a <see cref="Vector2Int"/> representing which direction this connection is pointing.
    /// </summary>
    public Vector2Int Forward
    {
        get
        {
            return new Vector2Int(
                Mathf.RoundToInt(this.gameObject.transform.forward.x),
                Mathf.RoundToInt(this.gameObject.transform.forward.z)
            );
        }
    }

    /// <summary>
    /// The location of the connection relative to it's parent.
    /// </summary>
    public Vector2Int location;

    /// <summary>
    /// Seal the connection with a wall.
    /// </summary>
    public void Seal()
    {
        SealingObject.SetActive(true);  
    }

    void OnDrawGizmosSelected()
    {
        // Draws a 5 unit long red line in front of the object
        Gizmos.color = Color.red;
        Vector3 direction = transform.TransformDirection(Vector3.forward) * 5;

        Gizmos.DrawRay(transform.position, direction);
    }

    /// <summary>
    /// Returns true if a connection is facing in the opposite direction as another connection.
    /// </summary>
    /// <param name="otherConnection"> The connection we want to see if we can connect to.</param>
    /// <returns> True if the connections are facing opposite directions, false otherwise.</returns>
    public bool CanConnect(ConnectionBlueprint otherConnection)
    {
        if (this.Forward * -1 == otherConnection.Forward)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
