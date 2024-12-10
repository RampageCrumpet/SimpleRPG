using HitDetection;
using SimpleRPG;
using SimpleRPG.Abilities;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static SimpleRPG.Character;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(Animator))]
public class MeleeAttackAbilityBehaviour : NetworkBehaviour, IInvokeableAbilityBehaviour
{
    /// <summary>
    /// The character who owns this <see cref="FireProjectileAbillityBehaviour"/> 
    /// </summary>
    private Character character;

    /// <summary>
    /// The animator.
    /// </summary>
    private Animator animator;

    private Weapon weapon;

    private WeaponData weaponData;

    public delegate void NotifyMeleeAttackStarted();

    public event NotifyMeleeAttackStarted MeleeAttackStarted;

    public void Start()
    {
        character = this.GetComponent<Character>();

        //TODO: This needs to be dynamic. A player could have many weapons.
        weapon = this.GetComponentInChildren<Weapon>();

        //TODO: This should be coming from the players inventory or equipment.
        this.weaponData = character.WeaponData;
    }

    /// <inheritdoc/>
    void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
    {
        MeleeAttackServerRpc(abilityInstace.Ability.abillityName);
    }

    /// <summary>
    /// Tell the player to swing their weapon across the network.
    /// </summary>
    /// <param name="abilityName"> The Ability name of the <see cref="MeleeAttackAbility"/> we want to trigger.</param>
    [Rpc(SendTo.Server)]
    void MeleeAttackServerRpc(string abilityName)
    {
        MeleeAttackAbility abilityInstance = (MeleeAttackAbility)this.character.PersonalAbilities.Select(x => x.Ability).Single(x => x.abillityName == abilityName);

        // If we weren't able to find an ability return and log an error.
        if (abilityInstance == null)
        {
            Debug.LogError("Unable to find an ability with the name " + abilityName + " on character " + this.gameObject.name + ".");
            return;
        }

        MeleeAttackClientRpc(weaponData);
    }

        /// <summary>
    /// Triggers a melee attack on all clients.
    /// </summary>
    /// <param name="weaponData"> The weapon data for the weapon being used to attackl </param>
    [Rpc(SendTo.Everyone)]
    public void MeleeAttackClientRpc(WeaponData weaponData)
    {
        Debug.Log("Executing MeleeAttackClientRPC");
        weapon.StartSwinging(weaponData);
        MeleeAttackStarted.Invoke();
    }

    /// <summary>
    /// Activates the weapons hitbox.
    /// </summary>
    public void ActivateWeapon()
    {
        weapon.ActivateHitbox();
    }

    /// <summary>
    /// Deactivates the weapons hitbox.
    /// </summary>
    public void DeactivateWeaponH()
    {
        weapon.DeactivateHitbox();
    }
}
