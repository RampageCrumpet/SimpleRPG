using HitDetection;
using SimpleRPG;
using SimpleRPG.Abilities;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Unity.Netcode.Components.NetworkRigidbody))]
[RequireComponent(typeof(Hitbox))]
public class Projectile : NetworkBehaviour
{
    /// <summary>
    /// The constant velocity of this projectile in Unity units/second.
    /// </summary>
    [SerializeField]
    [Tooltip("The constant velocity of this projectile in Unity units/second")]
    float velocity;

    /// <summary>
    /// The lifespan of this projectile in seconds.
    /// </summary>
    [SerializeField]
    [Tooltip("The life span of this projectile in seconds.")]
    float lifeSpan;

    /// <summary>
    /// The total damage dealt by this projectile. Set on the server in <see cref="Launch"/> and mirrored on clients.
    /// </summary>
    private DamageInfo damage = new DamageInfo();

    /// <summary>
    /// The character that fired this projectile. Contact with it is ignored so a shot never hurts its caster.
    /// </summary>
    private Character owner;

    /// <summary>
    /// True once this projectile has damaged a target, so a second hitbox overlap in the same step can't hit again.
    /// </summary>
    private bool hasHit;

    private float spawnTime;

    /// <summary>
    /// The hitbox used to determine if this projectile hit anything.
    /// </summary>
    private Hitbox hitBox;

    // Start is called before the first frame update
    void Start()
    {
        spawnTime = Time.time;
        this.hitBox = this.GetComponent<Hitbox>();
        hitBox.HitboxCollisionEvent.AddListener(TargetHit);
    }

    // Update is called once per frame
    void Update()
    {
        // If the projectile has outlived it's lifespan.
        if (Time.time - spawnTime > lifeSpan)
        {
            this.DestroyProjectile();
        }
    }

    /// <summary>
    /// Server only. Records who fired the projectile and how much damage it does, starts it moving, and tells clients to do the same.
    /// The server must set these itself: a <see cref="ClientRpc"/> body doesn't run on a dedicated server.
    /// </summary>
    /// <param name="shooter">The character that fired this projectile.</param>
    /// <param name="direction">The direction to fire in.</param>
    /// <param name="damageInfo">The damage this projectile does on a hit.</param>
    public void Launch(Character shooter, Vector3 direction, DamageInfo damageInfo)
    {
        if (!this.IsServer)
        {
            Debug.LogWarning(this.gameObject.name + " can only be launched on the server.");
            return;
        }

        owner = shooter;
        ApplyLaunch(direction, damageInfo);
        FireClientRPC(direction, damageInfo);
    }

    [ClientRpc]
    public void FireClientRPC(Vector3 direction, DamageInfo damage)
    {
        ApplyLaunch(direction, damage);
    }

    /// <summary>
    /// Copies the damage and sets the velocity. Runs on the server and on every client.
    /// </summary>
    private void ApplyLaunch(Vector3 direction, DamageInfo damageInfo)
    {
        // Copy the damage so the projectile doesn't hold a reference to the ability asset's own DamageInfo.
        this.damage = new DamageInfo { Damage = damageInfo != null ? damageInfo.Damage : 0 };
        this.GetComponent<Rigidbody>().linearVelocity = ProjectileRules.LaunchVelocity(direction, velocity);
    }

    /// <summary>
    /// Server only. Damages the touched character once and then destroys the projectile. Contact with the caster is ignored.
    /// </summary>
    public void TargetHit(Character character, BodyLocation location)
    {
        if (!this.IsServer)
        {
            return;
        }

        if (character != null && character == owner)
        {
            return;
        }

        if (ProjectileRules.ShouldApplyHit(character != null, hasHit))
        {
            hasHit = true;
            character.TakeDamage(damage, location);
        }

        this.DestroyProjectile();
    }

    /// <summary>
    /// Destroy the given projectile.
    /// </summary>
    void DestroyProjectile()
    {
        if (this.IsServer)
        {
            hitBox.HitboxCollisionEvent.RemoveListener(TargetHit);
            Destroy(this.gameObject);
        }
    }
}
