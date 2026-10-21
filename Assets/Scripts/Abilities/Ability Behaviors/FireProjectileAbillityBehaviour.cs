using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// This class handles the execution of all <see cref="FireProjectileAbility"/> instances.
    /// </summary>
    public class FireProjectileAbillityBehaviour : NetworkBehaviour, IInvokeableAbilityBehaviour
    {
        /// <summary>
        /// Where the projectile appears relative to this character, in local space. Keeps it clear of the caster's body.
        /// </summary>
        [SerializeField]
        [Tooltip("Where the projectile spawns, in this character's local space.")]
        private Vector3 muzzleOffset = new Vector3(0f, 1.2f, 0.8f);

        /// <summary>
        /// The character who owns this <see cref="FireProjectileAbillityBehaviour"/>
        /// </summary>
        private Character character;

        /// <summary>
        /// Server-side cooldown record used to validate incoming fire requests.
        /// </summary>
        private readonly ServerCooldownTracker cooldowns = new ServerCooldownTracker();

        public void Start()
        {
            character = this.GetComponent<Character>();
        }

        /// <inheritdoc/>
        void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
        {
            FireProjectileServerRPC(abilityInstace.Ability.AbillityName);
        }

        /// <summary>
        /// Tell the server to create the projectile and spawn it across the network.
        /// </summary>
        /// <param name="abilityName"> The Ability name of the <see cref="FireProjectileAbility"/> we want to fire.</param>
        [ServerRpc]
        void FireProjectileServerRPC(string abilityName)
        {
            FireProjectileAbility abilityInstance = this.character.PersonalAbilities.Select(x => x.Ability).OfType<FireProjectileAbility>().FirstOrDefault(x => x.AbillityName == abilityName);

            // If we weren't able to find an ability return and log an error.
            if (abilityInstance == null)
            {
                Debug.LogError("Unable to find an ability with the name " + abilityName + ".");
                return;
            }

            // A misconfigured spell is refused rather than throwing on the server.
            Projectile projectilePrefab = abilityInstance.Projectile != null ? abilityInstance.Projectile.GetComponent<Projectile>() : null;
            if (projectilePrefab == null || projectilePrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError(abilityName + " on " + this.gameObject.name + " has no Projectile prefab with a Projectile and a NetworkObject.", this);
                return;
            }

            // The server is the authority on cooldowns, the client's own cooldown check is only a prediction.
            if (!cooldowns.TryActivate(abilityName, abilityInstance.CooldownTime, Time.time))
            {
                return;
            }

            // The projectile lives in the world, not under the caster, so it doesn't inherit the caster's movement.
            Vector3 direction = this.transform.forward;
            Vector3 spawnPosition = this.transform.TransformPoint(muzzleOffset);
            Projectile spawnedProjectile = Object.Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
            spawnedProjectile.GetComponent<NetworkObject>().Spawn(true);

            // Stop the projectile colliding with any part of the caster, including hurtboxes on child objects.
            foreach (Collider casterCollider in this.GetComponentsInChildren<Collider>())
            {
                foreach (Collider projectileCollider in spawnedProjectile.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(casterCollider, projectileCollider);
                }
            }

            spawnedProjectile.Launch(character, direction, abilityInstance.Damage);
        }
    }
}
