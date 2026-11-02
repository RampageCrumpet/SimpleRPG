namespace SimpleRPG.Delve
{
    /// <summary>
    /// Where a delve (one trip into the dungeon, from arriving to getting out or dying) stands. The server decides it; everyone else reads it.
    /// </summary>
    public enum DelvePhase
    {
        /// <summary>The party is in the dungeon.</summary>
        Delving = 0,

        /// <summary>The whole party reached the extraction point together and got out.</summary>
        Extracted = 1,

        /// <summary>Every player is dead.</summary>
        Wiped = 2,
    }
}
