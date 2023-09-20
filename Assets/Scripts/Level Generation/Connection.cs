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
}
