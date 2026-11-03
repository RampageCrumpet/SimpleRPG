namespace SimpleRPG
{
    /// <summary>
    /// A rule about when a dead character may start its next life. Whatever runs the session (a delve, today) implements it and registers
    /// itself with <see cref="RespawnPolicies"/>; a <see cref="Character"/> only asks, and knows nothing of what the rule is.
    /// </summary>
    public interface IRespawnPolicy
    {
        /// <summary>Whether <paramref name="character"/> may respawn now.</summary>
        bool AllowsRespawn(Character character);
    }
}
