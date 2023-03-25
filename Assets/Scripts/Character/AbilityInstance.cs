using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class represents a single instance of an ability.
/// </summary>
public class AbilityInstance
{
    /// <summary>
    /// The abillity we want to invoke.
    /// </summary>
    public Ability abillity;

    /// <summary>
    /// The last time this abillity was used.
    /// </summary>
    private float lastActivationTime;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Activate()
    {
        if (Time.time > lastActivationTime + abillity.cooldownTime)
        {
            abillity.Activate();
            lastActivationTime = Time.time;
        }
    }
}
