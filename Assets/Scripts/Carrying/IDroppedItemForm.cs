namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// The form an item takes when it is put on the ground: something in the world that can be made to be one particular item instance.
    /// Dropping an item needs only this, so it does not have to know what the world makes of it; the world's pickup implements it.
    /// </summary>
    public interface IDroppedItemForm
    {
        /// <summary>
        /// Makes this the given item instance, as dropped by a player. Called by the server right after it spawns the object. Whoever picks
        /// it up then gets that same instance, under the same instance id.
        /// </summary>
        void InitializeDropped(Item item, uint instanceId);
    }
}
