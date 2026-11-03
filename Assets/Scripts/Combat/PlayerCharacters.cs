using Unity.Netcode;

namespace SimpleRPG
{
    /// <summary>
    /// Finds the character a connected player controls.
    /// </summary>
    public static class PlayerCharacters
    {
        /// <summary>
        /// The character controlled by <paramref name="client"/>, or null if the client is unknown, has no player object, or its player has no
        /// character yet.
        /// </summary>
        public static Character Of(NetworkClient client)
        {
            NetworkObject playerObject = client != null ? client.PlayerObject : null;
            return playerObject != null && playerObject.TryGetComponent(out IPlayerCharacterSource source) ? source.Character : null;
        }
    }
}
