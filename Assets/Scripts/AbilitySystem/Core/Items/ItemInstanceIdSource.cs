namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Hands out the IDs that identify item instances across machines. One source belongs to one set of <see cref="GameplayServices"/>, so a new
    /// session or test starts again from the first ID, and every inventory in a session draws from the same source because an item keeps its ID
    /// as it moves between inventories and equipment. Only the server (or offline play) allocates.
    /// </summary>
    public sealed class ItemInstanceIdSource
    {
        private uint last;

        /// <summary>
        /// The next unused ID. Never 0, which means "no ID".
        /// </summary>
        public uint Next() => ++last;
    }
}
