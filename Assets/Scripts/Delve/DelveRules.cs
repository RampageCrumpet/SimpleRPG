namespace SimpleRPG.Delve
{
    /// <summary>
    /// When a delve ends. Plain code so it can be tested without a network.
    /// </summary>
    public static class DelveRules
    {
        /// <summary>
        /// The phase a delve that is still running is in now.
        /// </summary>
        /// <param name="anyPlayerSeen">True once at least one player has been in the delve, so an empty session is not a wipe.</param>
        /// <param name="livingPlayers">How many players are alive.</param>
        /// <param name="extractionComplete">True once the party has finished the extraction channel.</param>
        public static DelvePhase Evaluate(bool anyPlayerSeen, int livingPlayers, bool extractionComplete)
        {
            // Getting out wins: the channel only completes while someone is alive.
            if (extractionComplete && livingPlayers > 0)
            {
                return DelvePhase.Extracted;
            }

            return anyPlayerSeen && livingPlayers <= 0 ? DelvePhase.Wiped : DelvePhase.Delving;
        }
    }
}
