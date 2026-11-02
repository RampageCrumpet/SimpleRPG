namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// What a dead player leaves behind: a container holding everything they carried and wore (see <c>DeathDrop</c>). Anyone in reach can take
    /// from it, which is the point of dying with your loot. The server removes it once it has held items and been emptied.
    /// </summary>
    public sealed class LootBag : LootContainer
    {
        // How often the server checks whether the bag has been emptied; nothing needs it sooner.
        private const float CheckSeconds = 1f;

        private float nextCheck;
        private bool hasHeldItems;

        private void Update()
        {
            if (!IsServer || !IsSpawned || Inventory == null || UnityEngine.Time.time < nextCheck)
            {
                return;
            }

            nextCheck = UnityEngine.Time.time + CheckSeconds;

            // The bag is spawned before its contents are moved in, so only a bag that has held something can count as emptied.
            if (Inventory.GetItems().GetEnumerator().MoveNext())
            {
                hasHeldItems = true;
            }
            else if (hasHeldItems)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}
