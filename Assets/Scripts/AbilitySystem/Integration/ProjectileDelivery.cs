using SimpleRPG.Gameplay;
using Unity.Netcode;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Launches real networked projectiles for abilities. It owns spawning, the caster-collision rule and reporting how each projectile ends;
    /// what a hit means is up to the ability that launched it. Server only.
    /// </summary>
    public sealed class ProjectileDelivery : IProjectileDelivery
    {
        /// <inheritdoc/>
        public bool Launch(in ProjectileLaunch launch, IProjectileEvents events)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                Debug.LogError("Projectiles can only be launched on the server.");
                return false;
            }

            GameObject prefab = launch.Prefab;
            if (prefab == null || !prefab.TryGetComponent(out Projectile projectilePrefab) || !prefab.TryGetComponent(out NetworkObject _))
            {
                Debug.LogError("The projectile prefab '" + (prefab != null ? prefab.name : "(none)") + "' needs a Projectile and a NetworkObject.", prefab);
                return false;
            }

            Vector3 direction = new Vector3(launch.Direction.X, launch.Direction.Y, launch.Direction.Z);
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector3.forward;
            }

            // The projectile lives in the world, not under the caster, so it doesn't inherit the caster's movement.
            Vector3 origin = new Vector3(launch.Origin.X, launch.Origin.Y, launch.Origin.Z);
            Projectile spawned = Object.Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
            spawned.GetComponent<NetworkObject>().Spawn(true);

            Character shooter = FindCharacter(manager, launch.Instigator);
            if (shooter != null)
            {
                // Stop the projectile colliding with any part of the caster, including hurtboxes on child objects.
                foreach (Collider casterCollider in shooter.GetComponentsInChildren<Collider>())
                {
                    foreach (Collider projectileCollider in spawned.GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(casterCollider, projectileCollider);
                    }
                }
            }

            ContinuationToken token = launch.Token;
            spawned.Impacted += impact => events.Impact(token, impact);
            spawned.Expired += () => events.Expired(token);
            spawned.Launch(shooter, direction);
            return true;
        }

        private static Character FindCharacter(NetworkManager manager, EntityId entity)
        {
            if (!entity.IsValid || !manager.SpawnManager.SpawnedObjects.TryGetValue(entity.Value, out NetworkObject spawnedObject))
            {
                return null;
            }

            return spawnedObject.GetComponent<Character>();
        }
    }
}
