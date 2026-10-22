namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Asks for an ability to be used on behalf of a character. The game integration implements it; the request is only a request, and the
    /// server decides whether the character has the ability, whether it is ready and what it does.
    /// </summary>
    public interface IAbilityActivator
    {
        /// <summary>
        /// Requests that the ability be used. On a client the request is sent to the server.
        /// </summary>
        /// <returns>False if the request could not be made at all.</returns>
        bool RequestActivate(AbilityDefinition ability);

        /// <summary>
        /// A press that continues a use already in progress instead of starting a new one, such as the next swing of a combo.
        /// It bypasses the cooldown, which was charged when the use began. On a client the request is sent to the server.
        /// </summary>
        /// <returns>True if the press was consumed and must not start a new use.</returns>
        bool TryQueue(AbilityDefinition ability);

        /// <summary>
        /// Runs the ability as the continuation of a use already in progress, so its cooldown is not charged again. Server only.
        /// </summary>
        /// <returns>True if the ability ran.</returns>
        bool ExecuteQueued(AbilityDefinition ability);
    }
}
