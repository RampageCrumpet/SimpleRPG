using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    /// <summary>
    /// The maximum health of this character.
    /// </summary>
    [SerializeField]
    [Tooltip("The maximum health of this character.")]
    private int maxHealth = 100;

    [SerializeField]
    [Tooltip("The list of abilities this character has.")]
    private List<Ability> abilities;

    /// <summary>
    /// Private backer for our <see cref="AbilityInstances"/> collection.
    /// </summary>
    private List<AbilityInstance> abilityInstances;

    /// <summary>
    /// A public read only collection of our abilities we can pass out safe in the knowledge that it wont be modified.
    /// </summary>
    public IReadOnlyCollection<AbilityInstance> AbilityInstances
    {
        get => abilityInstances.AsReadOnly();
    }

    /// <summary>
    /// The current health of this character.
    /// </summary>
    private int health;

    // Start is called before the first frame update
    void Start()
    {
        // Set the health to maximum.
        health = maxHealth;

        // Populate our list of activatable abilities.
        abilityInstances = new List<AbilityInstance>();
        foreach(Ability ability in abilities)
        {
            abilityInstances.Add(new AbilityInstance(ability));
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
