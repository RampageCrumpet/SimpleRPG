using HitDetection;
using SimpleRPG;
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
    /// The total damage dealt by this projectile.
    /// </summary>
    private DamageInfo damage;

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

    [ClientRpc]
    public void FireClientRPC(Vector3 direction, DamageInfo damage)
    {
        this.damage = damage;
        direction.Normalize();
        this.GetComponent<Rigidbody>().linearVelocity = velocity * direction;
    }

    public void TargetHit(Character character, BodyLocation location)
    {
        if (this.IsServer)
        {
            if (character != null)
            {
                character.TakeDamageRPC(damage, location);
            }

            this.DestroyProjectile();
        }
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
