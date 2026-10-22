using SimpleRPG.Gameplay;
using Unity.Netcode;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// The gameplay clock backed by Netcode's tick system. It reads the server time, so the server and every client agree on the tick
    /// (a client's value is its estimate of the server's). Before the network is running it reports tick zero.
    /// </summary>
    public sealed class NetworkGameplayClock : IGameplayClock
    {
        private readonly NetworkManager manager;

        public NetworkGameplayClock(NetworkManager manager)
        {
            this.manager = manager;
        }

        /// <inheritdoc/>
        public long Tick => manager != null && manager.NetworkTickSystem != null ? manager.NetworkTickSystem.ServerTime.Tick : 0;

        /// <inheritdoc/>
        public int TickRate => manager != null ? (int)manager.NetworkConfig.TickRate : 0;

        /// <inheritdoc/>
        public double ElapsedSeconds => manager != null && manager.NetworkTickSystem != null ? manager.NetworkTickSystem.ServerTime.Time : 0d;
    }
}
