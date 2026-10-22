using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The charge pool of an item instance, such as the three Firebolt charges in a staff. It is one counter in the item's state, so each
    /// instance has its own, and it can be spent but never refilled: recharging between dungeon runs is not built yet.
    /// </summary>
    public static class ItemCharges
    {
        /// <summary>
        /// The counter every item's charge pool uses. A fixed ID, so the same item always finds its pool.
        /// </summary>
        public static readonly DefinitionId PoolId = DefinitionId.FromGuid(new Guid("5f3a7c1e-9b24-4d6a-8e01-c7a2b9d4f6e3"));

        /// <summary>
        /// Gives an item instance its starting charges if it has no pool yet. An existing pool is never reset, so equipping, unequipping, trading or
        /// reloading an item cannot refill it.
        /// </summary>
        /// <returns>True if the pool was created.</returns>
        public static bool Ensure(ItemStateStore states, ItemInstanceId item, int startingCharges) =>
            startingCharges > 0 && states.EnsureCounter(item, PoolId, startingCharges);

        /// <summary>
        /// The charges an item instance has left, or -1 if it has no pool.
        /// </summary>
        public static int Remaining(ItemStateStore states, ItemInstanceId item) =>
            states.TryGetCounter(item, PoolId, out int value) ? value : -1;
    }
}
