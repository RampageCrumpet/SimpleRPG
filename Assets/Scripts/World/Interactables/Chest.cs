using UnityEngine;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// A container the player can loot. The server spawns the chest (see <c>RoomNetworkObjects</c>) and rolls its contents as it spawns.
    /// </summary>
    public class Chest : LootContainer
    {
        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (this.IsServer)
            {
                RollLoot();
            }
        }
    }
}
