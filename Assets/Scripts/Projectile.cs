using SimpleRPG.HitDetection;
using SimpleRPG;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
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
        /// The character that fired this projectile. Contact with it is ignored so a shot never hurts its caster.
        /// </summary>
        private Character owner;

        /// <summary>
        /// True once this projectile has reported what it hit, so a second overlap in the same step can't report again.
        /// </summary>
        private bool hasHit;

        private float spawnTime;

        /// <summary>How long this projectile lives before it expires, in seconds.</summary>
        public float LifeSpan => lifeSpan;

        /// <summary>How long this projectile has existed, in seconds.</summary>
        public float Age => Time.time - spawnTime;

        /// <summary>
        /// The hitbox used to determine if this projectile hit anything.
        /// </summary>
        private Hitbox hitBox;

        /// <summary>
        /// Raised on the server, once, when this projectile hits a character or level geometry. The projectile does no damage itself;
        /// whatever launched it decides what the hit means.
        /// </summary>
        public event Action<ProjectileImpact> Impacted;

        /// <summary>
        /// Raised on the server when this projectile ends without hitting anything, because it ran out of lifetime.
        /// </summary>
        public event Action Expired;

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
                if (IsServer && !hasHit)
                {
                    hasHit = true;
                    Expired?.Invoke();
                }

                this.DestroyProjectile();
            }
        }

        public override void OnNetworkDespawn()
        {
            // A projectile removed without hitting anything or running out of time (a scene change, a shutdown) still tells the ability that fired it.
            if (IsServer && !hasHit)
            {
                hasHit = true;
                Expired?.Invoke();
            }

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server only. Records who fired the projectile, starts it moving, and tells clients to do the same.
        /// The server must set these itself: a <see cref="ClientRpc"/> body doesn't run on a dedicated server.
        /// </summary>
        /// <param name="shooter">The character that fired this projectile, or null for a projectile with no shooter.</param>
        /// <param name="direction">The direction to fire in.</param>
        public void Launch(Character shooter, Vector3 direction)
        {
            if (!this.IsServer)
            {
                Debug.LogWarning(this.gameObject.name + " can only be launched on the server.");
                return;
            }

            owner = shooter;
            ApplyLaunch(direction);
            FireClientRPC(direction);
        }

        [ClientRpc]
        public void FireClientRPC(Vector3 direction)
        {
            ApplyLaunch(direction);
        }

        /// <summary>
        /// Sets the velocity. Runs on the server and on every client.
        /// </summary>
        private void ApplyLaunch(Vector3 direction)
        {
            Rigidbody body = this.GetComponent<Rigidbody>();

            // A client's copy follows the server's transform and is kinematic, which cannot take a velocity.
            if (body.isKinematic)
            {
                return;
            }

            body.linearVelocity = ProjectileRules.LaunchVelocity(direction, velocity);
        }

        /// <summary>
        /// Server only. Reports the touched character once and then destroys the projectile. Contact with the shooter is ignored.
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
                Impacted?.Invoke(new ProjectileImpact(character.Id, (int)location, ToPosition(transform.position), false));
            }

            this.DestroyProjectile();
        }

        /// <summary>
        /// Server only. A projectile whose collider is a trigger reaches level geometry as a trigger overlap.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            ReportWorldImpact(other, transform.position);
        }

        /// <summary>
        /// Server only. A projectile with a solid collider reaches level geometry as a collision.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            ReportWorldImpact(collision.collider, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        /// <summary>
        /// A projectile that touches level geometry stops there and reports a world impact.
        /// Triggers, characters (their hurtboxes report those) and the shooter's own colliders are not geometry.
        /// </summary>
        private void ReportWorldImpact(Collider other, Vector3 point)
        {
            if (!IsServer || hasHit || !ProjectileRules.IsLevelGeometry(other.isTrigger, other.GetComponentInParent<Character>() != null))
            {
                return;
            }

            hasHit = true;
            Impacted?.Invoke(new ProjectileImpact(default, 0, ToPosition(point), true));
            DestroyProjectile();
        }

        private static Position3 ToPosition(Vector3 position) => new Position3(position.x, position.y, position.z);

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
}
