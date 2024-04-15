using AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderNest : MonoBehaviour
{
    /// <summary>
    /// The <see cref="SpiderAI"/> that owns this nest.
    /// </summary>
    private SpiderAI spiderAI;

    /// <summary>
    /// How suitable is this nest for the spider to inhabit? Spiders prefer to build nests at more suitable locations.
    /// </summary>
    [field: SerializeField]
    [Tooltip("How suitable this nest is for a spider to inhabit it.")]
    public float NestSuitability { get; private set; }

    /// <summary>
    /// Returns whether or not a spider has settled into this nest.
    /// </summary>
    public bool IsOwned 
    {
        get { return spiderAI != null; }
    }

    /// <summary>
    /// Activates the current nest.
    /// </summary>
    public void ActivateNest(SpiderAI spiderAI)
    {
        this.spiderAI = spiderAI;
        this.GetComponent<MeshRenderer>().enabled = true;
    }

    private void OnDrawGizmos()
    {
        // Ensure that the mesh is drawn when Gizmos are turned on. 
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        Gizmos.DrawMesh(mesh, transform.position, transform.rotation, gameObject.transform.lossyScale);
    }
}
