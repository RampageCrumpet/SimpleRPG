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
        MeleeAttackServerRpc(abilityInstace.Ability.AbillityName);
    }

    /// <summary>
    /// Tell the player to swing their weapon across the network.
    /// </summary>
    /// <param name="abilityName"> The Ability name of the <see cref="MeleeAttackAbility"/> we want to trigger.</param>
    [Rpc(SendTo.Server)]
    void MeleeAttackServerRpc(string abilityName)
    {
        MeleeAttackAbility abilityInstance = (MeleeAttackAbility)this.character.PersonalAbilities.Select(x => x.Ability).Single(x => x.AbillityName == abilityName);

        // If we weren't able to find an ability return and log an error.
        if (abilityInstance == null)
        {
            Debug.LogError("Unable to find an ability with the name " + abilityName + " on character " + this.gameObject.name + ".");
            return;
        }

        MeleeAttackClientRpc();
    }

        /// <summary>
    /// Triggers a melee attack on all clients.
    /// </summary>
    /// <param name="weaponData"> The weapon data for the weapon being used to attackl </param>
    [Rpc(SendTo.Everyone)]
    public void MeleeAttackClientRpc()
    {
        weapon.StartSwinging(weaponData);
        MeleeAttackStarted.Invoke();
    }

    /// <summary>
    /// Damage multiplier for each attack in the longsword chain, indexed by attack number minus one.
    /// Based on each attack clip's length relative to Attack 1 (Attack 1 = 1.0). Tune these for balance.
    /// </summary>
    [Tooltip("Damage multiplier for each longsword attack. Index 0 is Attack 1. Based on clip length relative to Attack 1.")]
    [SerializeField]
    private float[] comboDamageScalars = { 1.00f, 0.79f, 0.84f };

    /// <summary>
    /// Activates the weapons hitbox. Called by the attack clip's animation event; the event's int parameter is the attack number.
    /// </summary>
    /// <param name="attackNumber">The attack in the longsword chain (1, 2 or 3).</param>
    public void ActivateWeapon(int attackNumber)
    {
        weapon.ActivateHitbox(GetComboDamageScalar(attackNumber));
    }

    /// <summary>
    /// Gets the damage multiplier for an attack, defaulting to 1 if none is set.
    /// </summary>
    private float GetComboDamageScalar(int attackNumber)
    {
        int index = attackNumber - 1;
        return index >= 0 && index < comboDamageScalars.Length ? comboDamageScalars[index] : 1f;
    }

    /// <summary>
    /// Deactivates the weapons hitbox.
    /// </summary>
    public void DeactivateWeaponH()
    {
        weapon.DeactivateHitbox();
    }
}
