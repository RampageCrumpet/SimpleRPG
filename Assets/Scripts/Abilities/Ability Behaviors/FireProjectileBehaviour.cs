using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// This class handles the execution of all <see cref="FireProjectileAbility"/> instances.
    /// </summary>
    public class FireProjectileBehaviour : NetworkBehaviour, IInvokeableAbilityBehaviour
    {
        /// <summary>
        /// The character who owns this <see cref="FireProjectileBehaviour"/> 
        /// </summary>
        private Character character;

        Ability IInvokeableAbilityBehaviour.Ability { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public void Start()
        {
            character = this.GetComponent<Character>();
        }

        /// <inheritdoc/>
        void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
        {
            FireProjectileServerRPC(abilityInstace.Ability.abillityName);
        }

        /// <summary>
        /// Tell the server to create the projectile and spawn it across the network.
        /// </summary>
        /// <param name="abilityName"> The Ability name of the <see cref="FireProjectileAbility"/> we want to fire.</param>
        [ServerRpc]
        void FireProjectileServerRPC(string abilityName)
        {
            FireProjectileAbility abilityInstance = (FireProjectileAbility)this.character.PersonalAbilities.Select(x => x.Ability).Single(x => x.abillityName == abilityName);

            // If we weren't able to find an ability return and log an error.
            if (abilityInstance == null)
            {
                Debug.LogError("Unable to find an ability with the name " + abilityName + ".");
                return;
            }

            Projectile spawnedProjectile = Object.Instantiate(abilityInstance.Projectile, this.transform).GetComponent<Projectile>();
            spawnedProjectile.gameObject.GetComponent<NetworkObject>().Spawn(true);

            Physics.IgnoreCollision(this.GetComponent<Collider>(), spawnedProjectile.gameObject.GetComponent<Collider>());

            spawnedProjectile.FireClientRPC(this.gameObject.transform.forward, abilityInstance.Damage);
        }
    }
}
