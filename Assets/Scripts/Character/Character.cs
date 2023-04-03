using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Character : NetworkBehaviour
{
    /// <summary>
    /// The maximum health of this character.
    /// </summary>
    [SerializeField]
    [Tooltip("The maximum health of this character.")]
    private int maxHealth = 100;

    /// <summary>
    /// The list of serialized abilities this player has.
    /// </summary>
    [SerializeField]
    [Tooltip("The list of abilities this character starts with.")]
    private List<Ability> abilities;

    /// <summary>
    /// Private backer for our <see cref="abilities"/> collection.
    /// </summary>
    private List<Ability> personalAbilityCollection = new List<Ability>();

    /// <summary>
    /// A public read only collection of our abilities with properties un
    /// </summary>
    public IReadOnlyCollection<Ability> PersonalAbilities
    {
        get => personalAbilityCollection.AsReadOnly();
    }

    /// <summary>
    /// Adds an ability 
    /// </summary>
    public void AddAbilitry(Ability ability)
    {
        personalAbilityCollection.Add((Ability)ScriptableObject.CreateInstance(ability.GetType()));
        abilities.Add(ability);
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
        foreach(Ability ability in abilities)
        {
            personalAbilityCollection.Add(Instantiate(ability));
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
