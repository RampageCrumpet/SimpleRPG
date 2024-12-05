using SimpleRPG.Abilities;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MeleeAttackAbility", menuName = "Abilities/MeleeAttackAbility")]
public class MeleeAttackAbility : Ability
{
    /// <inheritdoc/>
    public override Type AbilityBehavior
    {
        get => typeof(MeleeAttackAbilityBehaviour);
    }

    /// <summary>
    /// The weapon used to attack.
    /// </summary>
    [Tooltip("The weapon used to attack.")]
    public Weapon Weapon;
}
