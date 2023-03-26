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
    public Ability ability;

    /// <summary>
    /// The last time this abillity was used.
    /// </summary>
    public float LastActivationTime
    {
        get;
        private set;
    }

    /// <summary>
    /// Creates a new instance of the AbilityInstance object.
    /// </summary>
    /// <param name="ability"> The ability we want an instance of.</param>
    /// <param name="owningCharacter"></param>
    public AbilityInstance(Ability ability)
    {
        this.ability = ability;
    }

    public void Activate()
    {
        if (Time.time > LastActivationTime + ability.cooldownTime)
        {
            ability.Activate();
            LastActivationTime = Time.time;
        }
    }
}
