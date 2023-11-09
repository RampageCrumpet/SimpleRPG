using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    /// <summary>
    /// The maximum distance the camera can be from the <see cref="GameObject"></see> it's orbiting.
    /// </summary>
    [SerializeField]
    [Tooltip("The maximum distance the camera can be from the .")]
    private float maximumCameraDistance;

    /// <summary>
    /// The minimum distance the camera can be from the <see cref="GameObject"></see> it's orbiting.
    /// </summary>
    [SerializeField]
    [Tooltip("The minimum distance the camera can be from the target character.")]
    private float minimumCameraDistance;

    [SerializeField]
    [Tooltip("The game object we want to orbit.")]
    private GameObject targetObject;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
