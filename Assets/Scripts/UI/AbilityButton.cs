using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This script handles controller the cooldown display for an abillity.
/// </summary>
public class AbilityButton : MonoBehaviour
{
    /// <summary>
    /// The button axis we use to trigger the abillity associated with this AbillityCooldown.
    /// </summary>
    [SerializeField]
    [Tooltip("The button axis we use to trigger the abillity associated with this AbillityCooldown.")]
    public string abillityButtonAxisName = "Fire1";

    [SerializeField]
    [Tooltip("The mask used to darken this abillity when it's cooling down.")]
    public Image darkMask;

    [SerializeField]
    [Tooltip("The Text Mesh we use to display the cooldown time.")]
    public TMP_Text cooldownText;

    private Image abilityIconImage;
    private Image darkAbillityIcon;


    private Ability ability;

    public void Initialize(Ability abilityInstance)
    {
        abilityIconImage = this.GetComponent<Image>();
        darkAbillityIcon = this.GetComponentInChildren<Image>();

        this.ability = abilityInstance;
        abilityIconImage.sprite = ability.abillitySprite;
        darkAbillityIcon.sprite = ability.abillitySprite;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateCooldown();
    }

    /// <summary>
    /// Updates the cooldown display.
    /// </summary>
    private void UpdateCooldown()
    {
        float cooldownTimeLeft = ability.LastActivationTime - Time.time;
        float percentCooledDown = (ability.cooldownTime - cooldownTimeLeft) / ability.cooldownTime;

        //Scale the dark mask so the abillity is properly visible behind it.
        darkAbillityIcon.fillAmount = percentCooledDown;

        // If the ability is cooling down we want the text to be visible.
        if(cooldownTimeLeft >= 0)
        {
            cooldownText.text = Mathf.Round(cooldownTimeLeft).ToString();
        }
        else
        {
            cooldownText.text = "";
        }
    }
}
