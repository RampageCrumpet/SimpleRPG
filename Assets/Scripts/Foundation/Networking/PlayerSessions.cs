using System.Collections.Generic;
using Unity.Netcode;

namespace SimpleRPG.Networking
{
    /// <summary>
    /// Which player each connected client is, as the server learned when it approved the connection. A client id changes every time someone
    /// connects; a player id does not. Server only; one per network manager so two sessions in one process never share it.
    /// </summary>
    public sealed class PlayerSessions
    {
        private static readonly Dictionary<NetworkManager, PlayerSessions> All = new Dictionary<NetworkManager, PlayerSessions>();

        private readonly Dictionary<ulong, string> playerByClient = new Dictionary<ulong, string>();

        /// <summary>The sessions of this manager, made on first use.</summary>
        public static PlayerSessions For(NetworkManager manager)
        {
            if (!All.TryGetValue(manager, out PlayerSessions sessions))
            {
                sessions = new PlayerSessions();
                All[manager] = sessions;
            }

            return sessions;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll() => All.Clear();

        public void Set(ulong clientId, string playerId) => playerByClient[clientId] = playerId;

        public bool TryGetPlayerId(ulong clientId, out string playerId) => playerByClient.TryGetValue(clientId, out playerId);

        public void Remove(ulong clientId) => playerByClient.Remove(clientId);
    }
}
