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
    /// Multiplier applied to the weapon's damage for the attack currently swinging.
    /// </summary>
    private float damageScalar = 1f;

    public void Start()
    {
        hitbox = GetComponentInChildren<Hitbox>();
    }
    public void StartSwinging(WeaponData weaponData)
    {
        this.weaponData = weaponData;
    }

    public void TargetHit(Character character, BodyLocation location)
    {
        if (this.IsServer)
        {
            if (character != null)
            {
                DamageInfo scaledDamage = new DamageInfo { Damage = ScaleDamage(weaponData.damage.Damage, damageScalar) };
                character.TakeDamageRPC(scaledDamage, location);
            }
        }
    }

    /// <summary>
    /// Scales a base damage value by an attack's damage multiplier, rounding to the nearest whole point.
    /// </summary>
    /// <param name="baseDamage">The weapon's base damage.</param>
    /// <param name="damageScalar">The damage multiplier for the attack.</param>
    /// <returns>The scaled damage.</returns>
    public static int ScaleDamage(int baseDamage, float damageScalar)
    {
        return Mathf.RoundToInt(baseDamage * damageScalar);
    }

    /// <summary>
    /// Enables the hitbox for an attack that deals damage scaled by <paramref name="damageScalar"/>.
    /// </summary>
    /// <param name="damageScalar">The damage multiplier for this attack.</param>
    public void ActivateHitbox(float damageScalar)
    {
        this.damageScalar = damageScalar;
        hitbox.enabled = true;
    }

    public void DeactivateHitbox()
    {
        hitbox.enabled = false;
    }
}
