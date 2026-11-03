using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// The respawn policy of each running session, found through the session's <see cref="NetworkManager"/> so that two sessions in one
    /// process (a test with a host and a client) keep their own. Where no policy is registered, respawning is always allowed, so nothing that
    /// does not use one changes.
    /// </summary>
    public static class RespawnPolicies
    {
        private static readonly Dictionary<NetworkManager, IRespawnPolicy> Active = new Dictionary<NetworkManager, IRespawnPolicy>();

        /// <summary>Makes <paramref name="policy"/> the rule for the session run by <paramref name="manager"/>.</summary>
        public static void Register(NetworkManager manager, IRespawnPolicy policy)
        {
            if (manager != null) Active[manager] = policy;
        }

        /// <summary>Removes <paramref name="policy"/> if it is still the session's rule; a newer one that replaced it is left alone.</summary>
        public static void Unregister(NetworkManager manager, IRespawnPolicy policy)
        {
            if (manager != null && Active.TryGetValue(manager, out IRespawnPolicy registered) && registered == policy)
            {
                Active.Remove(manager);
            }
        }

        /// <summary>The policy of the session run by <paramref name="manager"/>, or null if there is none.</summary>
        public static IRespawnPolicy Find(NetworkManager manager)
        {
            return manager != null && Active.TryGetValue(manager, out IRespawnPolicy policy) ? policy : null;
        }

        /// <summary>Whether a dead character may start its next life now. True where no policy is registered.</summary>
        public static bool AllowsRespawn(Character character)
        {
            IRespawnPolicy policy = Find(character != null ? character.NetworkManager : null);
            return policy == null || policy.AllowsRespawn(character);
        }

        // Domain reload may be off in the editor, so nothing registered in one play session may be carried into the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();
    }
}
