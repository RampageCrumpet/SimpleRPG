using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spiderweb : MonoBehaviour
{
    /// <summary>
    /// The last time the spiderweb was touched.
    /// </summary>
    public float LastTouchTime { get; private set; }

    /// <summary>
    /// Has the spiderweb been touched since it was last reset.
    /// </summary>
    public bool HasBeenTouched { get; private set; }

    public void ResetSpiderweb()
    {
        HasBeenTouched = false;
    }

    /// <summary>
    /// Somone has touched the spiderweb.
    /// </summary>
    /// <param name="other"> The collider of the object that entered the spiderweb.</param>
    public void OnTriggerEnter(Collider other)
    {
        HasBeenTouched = true;
        LastTouchTime = Time.time;
    }
}
