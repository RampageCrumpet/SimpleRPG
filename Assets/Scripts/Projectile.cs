using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Unity.Netcode.Components.NetworkRigidbody))]
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
    private int damage;

    private float spawnTime;

    // Start is called before the first frame update
    void Start()
    {
        spawnTime = Time.time;
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
    public void FireClientRPC(Vector3 direction, int damage)
    {
        this.damage = damage;
        direction.Normalize();
        this.GetComponent<Rigidbody>().linearVelocity = velocity * direction;
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (this.IsServer)
        {
            Character character = collision.gameObject.GetComponent<Character>();
            if (character != null)
            {
                character.TakeDamageClientRPC(damage);
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
            Destroy(this.gameObject);
        }
    }
}
