using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// Maps items to and from the compact network id inventories and equipment replicate. Items are gameplay definitions, so an item's id is
    /// its entry in the <see cref="DefinitionRegistry"/>: assigned once, never reused, and valid for saved data. Zero means "no item".
    /// </summary>
    public static class ItemIds
    {
        /// <summary>
        /// The network id of an item, or of the asset a cloned item was created from.
        /// </summary>
        /// <returns>The id, or 0 if the item is null, has no permanent id, or isn't in the registry.</returns>
        public static uint GetId(Item item)
        {
            DefinitionRegistry registry = DefinitionRegistry.Instance;
            if (item == null || registry == null)
            {
                return 0;
            }

            DefinitionId id = item.SourceAsset.Id;
            return !id.IsNone && registry.TryGetShortId(id, out uint shortId) ? shortId : 0;
        }

        /// <summary>
        /// The item asset for a network id, or null if the id is 0, unknown, or belongs to something that isn't an item.
        /// </summary>
        public static Item GetItem(uint id)
        {
            DefinitionRegistry registry = DefinitionRegistry.Instance;
            return id != 0 && registry != null && registry.TryGetByShortId(id, out GameplayDefinition definition) ? definition as Item : null;
        }

        /// <summary>
        /// True if the item can be found by clients: it is in the registry.
        /// </summary>
        public static bool IsReplicable(Item item) => GetId(item) != 0;
    }
}
