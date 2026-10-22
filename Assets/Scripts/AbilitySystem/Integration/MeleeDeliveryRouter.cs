using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Starts a melee swing on the swinging character's own melee mechanics. The character owns its weapon, animation and combo; this just
    /// finds them. Server only.
    /// </summary>
    public sealed class MeleeDeliveryRouter : IMeleeDelivery
    {
        /// <inheritdoc/>
        public bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !launch.Instigator.IsValid)
            {
                return false;
            }

            if (!manager.SpawnManager.SpawnedObjects.TryGetValue(launch.Instigator.Value, out NetworkObject swinger))
            {
                return false;
            }

            IMeleeSwingHost host = swinger.GetComponent<IMeleeSwingHost>();
            if (host == null)
            {
                Debug.LogError(swinger.name + " has no melee mechanics (MeleeDelivery), so it cannot swing.", swinger);
                return false;
            }

            return host.Begin(launch, events);
        }
    }
}
