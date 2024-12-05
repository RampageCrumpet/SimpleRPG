using HitDetection;
using SimpleRPG;
using SimpleRPG.Abilities;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

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

    public void Start()
    {
        character = this.GetComponent<Character>();

        //TODO: This needs to be dynamic. A player could have many weapons.
        weapon = this.GetComponentInChildren<Weapon>();
    }

    /// <inheritdoc/>
    void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
    {
        MeleeAttackServerRPC(abilityInstace.Ability.abillityName);
    }

    /// <summary>
    /// Tell the server to create the projectile and spawn it across the network.
    /// </summary>
    /// <param name="abilityName"> The Ability name of the <see cref="FireProjectileAbility"/> we want to fire.</param>
    [ServerRpc]
    void MeleeAttackServerRPC(string abilityName)
    {
        MeleeAttackAbility abilityInstance = (MeleeAttackAbility)this.character.PersonalAbilities.Select(x => x.Ability).Single(x => x.abillityName == abilityName);

        // If we weren't able to find an ability return and log an error.
        if (abilityInstance == null)
        {
            Debug.LogError("Unable to find an ability with the name " + abilityName + ".");
            return;
        }

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
