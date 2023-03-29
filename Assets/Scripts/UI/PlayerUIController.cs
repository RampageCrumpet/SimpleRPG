using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerUIController : MonoBehaviour
{
    /// <summary>
    /// The object we want to use as a blueprint for spawning all of our abillity buttons.
    /// </summary>
    [SerializeField]
    [Tooltip("The abillity button prefab.")]
    GameObject abilityButtonPrefab;

    [SerializeField]
    [Tooltip("The player object we're creating a UI for.")]
    Player player;

    List<AbilityButton> abilityButtons = new List<AbilityButton>();

    [SerializeField]
    [Tooltip("The layout control for the players ability buttons.")]
    private GameObject abillityBar;
    
    // Start is called before the first frame update
    void Start()
    {
        // Create a button for each of our abilities.
        foreach(Ability ability in player.character.AbilityInstances)
        {
            GameObject newAbilityButtonGameObject = Object.Instantiate(abilityButtonPrefab, abillityBar.transform);
            AbilityButton newAbilityButton = newAbilityButtonGameObject.GetComponent<AbilityButton>();
            newAbilityButton.Initialize(ability);
            abilityButtons.Add(newAbilityButton);
        }
    }
}
