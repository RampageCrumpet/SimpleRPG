using System.Collections;
using System.Collections.Generic;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.Experimental.AI;

public class Doodad : MonoBehaviour
{
    // The meshes this object can randomly select from.
    [SerializeField]
    [Tooltip("The list of meshes this object can use.")]
    private List<Mesh> meshes = new List<Mesh>();

    /// <summary>
    /// The number of degrees we want to rotate by.
    /// </summary>
    [Header("Rotation Settings")]
    [Range(1f, 180f)]
    [SerializeField]
    [Tooltip("The number of degrees we want to rotate by when we have a locked increment.")]
    private float rotationIncrement;

    /// <summary>
    /// Allow the X axis to be rotated.
    /// </summary>
    [SerializeField]
    [Tooltip("Apply a random initial rotation to the X axis.")]
    private bool rotateXAxis = false;

    /// <summary>
    /// Allow the Y axis to be rotated.
    /// </summary>
    [SerializeField]
    [Tooltip("Apply a random initial rotation to the Y axis.")]
    private bool rotateYAxis = false;

    /// <summary>
    /// Allow the Z axis to be rotated.
    /// </summary>
    [SerializeField]
    [Tooltip("Apply a random initial rotation to the Y axis.")]
    private bool rotateZAxis = false;

    // Start is called before the first frame update
    void Start()
    {
        ApplyRotation(rotationIncrement);
        ApplyMesh();
    }

    /// <summary>
    /// Apply a rotation that rotates by a fixed number of degrees.
    /// </summary>
    /// <param name="rotationIncrement"> The number of degrees per increment.</param>
    private void ApplyRotation(float rotationIncrement)
    {
        Quaternion newRotation = new Quaternion();

        // Find the number of steps needed to rotate the object halfway around.
        int rotationSteps = (int)(360f / rotationIncrement)/2;

        if (rotateXAxis)
        {
            newRotation.x = Random.Range(-rotationSteps, rotationSteps) * rotationIncrement;
        }

        if (rotateYAxis)
        {
            newRotation.y = Random.Range(-rotationSteps, rotationSteps) * rotationIncrement;
        }

        if (rotateZAxis)
        {
            newRotation.z = Random.Range(-rotationSteps, rotationSteps) * rotationIncrement;
        }

        this.transform.rotation = newRotation;
    }

    private void ApplyMesh()
    {
        if (meshes.Count != 0)
        {
            this.GetComponent<MeshFilter>().mesh = meshes[Random.Range(0, meshes.Count - 1)];
        }
    }
}
