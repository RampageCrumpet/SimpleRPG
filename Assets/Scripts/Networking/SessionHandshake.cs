using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Networking
{
    /// <summary>
    /// What a connecting client is checked against before the server gives it a character: the same game build and a free place.
    /// Without this anyone who can reach the port is let in, whatever build they run and however many are already connected. The rules are
    /// plain functions so they can be tested without a network; <see cref="InstallOnServer"/> and <see cref="PrepareClient"/> attach them to a manager.
    /// </summary>
    public static class SessionHandshake
    {
        /// <summary>
        /// Bumped when the wire contract changes in a way two builds cannot be expected to survive, even if their version strings match.
        /// </summary>
        public const int ProtocolVersion = 1;

        private const string Prefix = "SimpleRPG";
        private const char Separator = '|';

        // Connection data travels in the connect message, which is small; a longer request is not one of ours.
        private const int MaxRequestBytes = 256;

        /// <summary>The server's answer to a connection request.</summary>
        public readonly struct Decision
        {
            public Decision(bool approved, string reason)
            {
                Approved = approved;
                Reason = reason;
            }

            public bool Approved { get; }

            /// <summary>Why the request was refused, shown to the refused client. Empty when approved.</summary>
            public string Reason { get; }
        }

        /// <summary>The connection data a client of this build sends.</summary>
        public static byte[] BuildRequest(string buildVersion)
        {
            return Encoding.UTF8.GetBytes(Prefix + Separator + ProtocolVersion + Separator + buildVersion);
        }

        /// <summary>
        /// Decides whether a client may join.
        /// </summary>
        /// <param name="request">The connection data the client sent.</param>
        /// <param name="serverBuildVersion">The build version of the server.</param>
        /// <param name="connectedCount">How many clients, a host included, are already connected.</param>
        /// <param name="maxPlayers">The most clients the session takes, a host included. Zero or less means no limit.</param>
        public static Decision Evaluate(byte[] request, string serverBuildVersion, int connectedCount, int maxPlayers)
        {
            if (request == null || request.Length == 0 || request.Length > MaxRequestBytes)
            {
                return new Decision(false, "Not a SimpleRPG client.");
            }

            string[] parts = Encoding.UTF8.GetString(request).Split(Separator);
            if (parts.Length != 3 || parts[0] != Prefix || !int.TryParse(parts[1], out int protocol))
            {
                return new Decision(false, "Not a SimpleRPG client.");
            }

            if (protocol != ProtocolVersion)
            {
                return new Decision(false, "Protocol mismatch: the server speaks " + ProtocolVersion + " and this client " + protocol + ".");
            }

            if (parts[2] != serverBuildVersion)
            {
                return new Decision(false, "Version mismatch: the server runs " + serverBuildVersion + " and this client " + parts[2] + ".");
            }

            if (maxPlayers > 0 && connectedCount >= maxPlayers)
            {
                return new Decision(false, "The server is full.");
            }

            return new Decision(true, string.Empty);
        }

        /// <summary>
        /// Makes a server or host check every client that connects. Call before starting it.
        /// </summary>
        public static void InstallOnServer(NetworkManager manager, int maxPlayers)
        {
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback = (request, response) =>
            {
                // A host connects to itself through the same path; it is always let in.
                Decision decision = request.ClientNetworkId == NetworkManager.ServerClientId
                    ? new Decision(true, string.Empty)
                    : Evaluate(request.Payload, Application.version, manager.ConnectedClientsIds.Count, maxPlayers);

                response.Approved = decision.Approved;
                response.CreatePlayerObject = decision.Approved;
                response.Pending = false;
                response.Reason = decision.Reason;
            };
        }

        /// <summary>
        /// Makes a client announce its build when it connects. Call before starting it. Approval is a setting both sides must agree on,
        /// so it is switched on here too.
        /// </summary>
        public static void PrepareClient(NetworkManager manager)
        {
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ConnectionData = BuildRequest(Application.version);
        }
    }
}
