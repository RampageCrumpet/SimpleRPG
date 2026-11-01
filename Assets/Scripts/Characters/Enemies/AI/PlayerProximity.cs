using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.AI
{
    /// <summary>
    /// Answers "is a player near this point?" for the server's AI, so enemies far from every player can stand down instead of thinking and
    /// pathing for nobody. Many enemies ask every frame, so the players' positions are gathered once per short interval and shared.
    /// </summary>
    public static class PlayerProximity
    {
        // How stale the shared snapshot may get. An enemy waking a fraction of a second late is not noticeable; one scan per enemy is.
        private const float RefreshSeconds = 0.25f;

        private static readonly List<Vector3> LivingPlayerPositions = new List<Vector3>();
        private static bool anyPlayerCharacters;
        private static float refreshedAt = float.NegativeInfinity;
        private static NetworkManager refreshedFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            LivingPlayerPositions.Clear();
            anyPlayerCharacters = false;
            refreshedAt = float.NegativeInfinity;
            refreshedFor = null;
        }

        /// <summary>
        /// True if a living player's character is within <paramref name="distance"/> of <paramref name="point"/>. If no player character exists at
        /// all (a session still starting, or a test scene with none) this is true, so enemies are never left asleep for want of anyone to wake them.
        /// </summary>
        public static bool AnyWithin(NetworkManager manager, Vector3 point, float distance)
        {
            if (manager == null)
            {
                return true;
            }

            Refresh(manager);
            if (!anyPlayerCharacters)
            {
                return true;
            }

            float limit = distance * distance;
            for (int i = 0; i < LivingPlayerPositions.Count; i++)
            {
                if ((LivingPlayerPositions[i] - point).sqrMagnitude <= limit)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Refresh(NetworkManager manager)
        {
            if (manager == refreshedFor && Time.unscaledTime - refreshedAt < RefreshSeconds)
            {
                return;
            }

            refreshedFor = manager;
            refreshedAt = Time.unscaledTime;
            LivingPlayerPositions.Clear();
            anyPlayerCharacters = false;

            // Players are the characters a person steers; enemies, which the server owns, never have this component.
            foreach (NetworkObject spawned in manager.SpawnManager.SpawnedObjectsList)
            {
                if (spawned != null && spawned.TryGetComponent(out FirstPersonCharacterController _) && spawned.TryGetComponent(out Character character))
                {
                    anyPlayerCharacters = true;
                    if (character.IsAlive)
                    {
                        LivingPlayerPositions.Add(spawned.transform.position);
                    }
                }
            }
        }
    }
}
