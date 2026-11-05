using UnityEngine;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// A character's body that can be searched once it is dead. The loot table is rolled once per life, on the server, the first time the body is
    /// searched (so what changes a roll in the meantime still counts), and the body opens like any other <see cref="LootContainer"/>. While the character is alive it cannot be interacted with.
    /// Bodies are kept for the whole session.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class LootableCorpse : LootContainer
    {
        private Character character;

        /// <summary>The life whose body has been marked to fill, so a life is only ever filled once.</summary>
        private int rolledLife = -1;

        /// <inheritdoc/>
        protected override bool RollsWhenOpened => true;

        /// <inheritdoc/>
        public override bool CanBeInteractedWith => character != null && !character.IsAlive;

        /// <summary>
        /// A body has many colliders (one per hit region), and looking at any of them counts as looking at the body.
        /// </summary>
        public override bool IsLookedAtVia(Collider collider)
        {
            return collider != null && collider.transform.IsChildOf(transform);
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            character = GetComponent<Character>();

            if (IsServer)
            {
                character.OnDeath += HandleDeath;
            }
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            if (character != null)
            {
                character.OnDeath -= HandleDeath;
            }

            base.OnNetworkDespawn();
        }

        private void HandleDeath()
        {
            if (rolledLife == character.LifeId)
            {
                return;
            }

            // A new death means a new body to fill the next time it is searched.
            rolledLife = character.LifeId;
            ResetRoll();
        }
    }
}
