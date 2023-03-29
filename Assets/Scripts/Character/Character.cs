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
    /// Private backer for our <see cref="abilities"/> collection.
    /// </summary>
    private List<Ability> abilityInstances;

    /// <summary>
    /// A public read only collection of our abilities we can pass out safe in the knowledge that it wont be modified.
    /// </summary>
    public IReadOnlyCollection<Ability> AbilityInstances
    {
        get => abilityInstances.AsReadOnly();
    }

    /// <summary>
    /// Adds an ability 
    /// </summary>
    public void AddAbilitry(Ability ability)
    {
        abilityInstances.Add((Ability)ScriptableObject.CreateInstance(ability.GetType()));
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

        // Populate our list of activatable abilities from the start.
        abilityInstances = new List<Ability>();
        foreach(Ability ability in abilities)
        {
            AddAbilitry(ability);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
