using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Connection : MonoBehaviour
{
    /// <summary>
    /// Gets a <see cref="Vector2Int"/> representing which direction this connection is pointing.
    /// </summary>
    public Vector2Int Forward
    {
        get
        {
            return new Vector2Int((int)this.gameObject.transform.forward.x, (int)this.gameObject.transform.forward.z);
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
    public bool CanConnect(Connection otherConnection)
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
