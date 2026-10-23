namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Asks for an ability to be used on behalf of a character. The game integration implements it; the request is only a request, and the
    /// server decides whether the character has the ability, whether it is ready and what it does.
    /// </summary>
    public interface IAbilityActivator
    {
        /// <summary>
        /// Requests that the ability be used. On a client the request is sent to the server. For a melee ability this is also how the next
        /// swing of a combo is asked for: the server decides whether the press starts a chain, continues one, or does nothing.
        /// </summary>
        /// <param name="freshAllowed">What the requester believes about whether a fresh use is allowed, such as whether the cooldown it shows has
        /// passed. Only a client prediction uses it; the server decides for itself.</param>
        /// <returns>False if the request could not be made at all.</returns>
        bool RequestActivate(AbilityDefinition ability, bool freshAllowed = true);
    }
}
