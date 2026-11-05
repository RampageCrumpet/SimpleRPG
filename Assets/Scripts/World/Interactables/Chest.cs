namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// A container the player can loot. The server spawns the chest (see <c>RoomNetworkObjects</c>) and fills it the first time a player
    /// opens it, not before: nothing about its contents is decided until someone looks, so what changes the roll in the meantime (the area's
    /// loot level, a modifier) still counts.
    /// </summary>
    public class Chest : LootContainer
    {
        /// <inheritdoc/>
        protected override bool RollsWhenOpened => true;
    }
}
