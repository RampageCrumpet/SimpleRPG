namespace SimpleRPG
{
    /// <summary>
    /// Implemented by the object that stands for a connected player and holds the character that player controls. Code that only wants that
    /// character reads it through <see cref="PlayerCharacters"/> and does not need to know how the session creates and tracks players.
    /// </summary>
    public interface IPlayerCharacterSource
    {
        /// <summary>The character this player controls, or null while it has none (not spawned yet, or not yet known on this machine).</summary>
        Character Character { get; }
    }
}
