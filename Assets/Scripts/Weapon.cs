using HitDetection;
using SimpleRPG;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This script handles collision detection for weapons.
/// </summary>
public class Weapon : NetworkBehaviour
{
    /// <summary>
    /// The weapon data for the weapon we're currently attacking with.
    /// </summary>
    private WeaponData weaponData;

    private Hitbox hitbox;

    /// <summary>
    /// Triggers a melee attack on all clients.
    /// </summary>
    /// <param name="weaponData"> The weapon data for the weapon being used to attackl </param>
    [ClientRpc]
    public void MeleeAttackClientRPC(WeaponData weaponData)
    {
        this.weaponData = weaponData;
    }

    public void TargetHit(Character character, BodyLocation location)
    {
        if (this.IsServer)
        {
            if (character != null)
            {
                character.TakeDamageRPC(weaponData.damage, location);
            }
        }
    }

    public void ActivateHitbox()
    {
        hitbox.enabled = true;
    }

    public void DeactivateHitbox()
    {
        hitbox.enabled = false;
    }
}
