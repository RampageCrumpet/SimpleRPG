using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// Why a move was accepted or refused.
    /// </summary>
    public enum MoveResult
    {
        Success,
        NoItem,
        NoDestination,
        NotAuthority,
        ItemNotInSource,
        ItemNotReplicable,
        InvalidPlacement
    }

    /// <summary>
    /// Owns the rules for moving an item between (or within) inventories. A move is checked in full before anything is
    /// changed, then committed, so it either happens completely or not at all. The UI only forwards the player's intent here.
    /// </summary>
    public static class InventoryMoveController
    {
        /// <summary>
        /// Checks a move without changing anything.
        /// </summary>
        public static MoveResult Validate(Inventory source, Item item, Inventory destination, Vector2Int location)
        {
            if (item == null)
            {
                return MoveResult.NoItem;
            }

            if (source == null || destination == null)
            {
                return MoveResult.NoDestination;
            }

            if (!source.CanMutateContents || !destination.CanMutateContents)
            {
                return MoveResult.NotAuthority;
            }

            if (!source.Contains(item))
            {
                return MoveResult.ItemNotInSource;
            }

            if (!destination.CanReplicate(item))
            {
                return MoveResult.ItemNotReplicable;
            }

            return destination.ValidateItemPlacement(item, location) ? MoveResult.Success : MoveResult.InvalidPlacement;
        }

        /// <summary>
        /// Validates and then performs a move. Nothing is touched unless the whole move is valid.
        /// </summary>
        public static MoveResult TryMove(Inventory source, Item item, Inventory destination, Vector2Int location)
        {
            MoveResult result = Validate(source, item, destination, location);
            if (result == MoveResult.Success)
            {
                source.CommitMove(item, destination, location);
            }

            return result;
        }
    }
}
