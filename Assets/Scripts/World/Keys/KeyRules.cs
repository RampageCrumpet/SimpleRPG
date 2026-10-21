using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Keys
{
    /// <summary>
    /// What happened when a player tried to use a key on a locked door.
    /// </summary>
    public enum KeyUseResult
    {
        Opened,
        NotHeld,
        AlreadyOpen,
        TooFar,
    }

    /// <summary>
    /// The server's rules for picking up keys and opening locked doors. Pure, so they can be tested without a network.
    /// </summary>
    public static class KeyRules
    {
        /// <summary>
        /// How far beyond an interactable's own range the server still accepts a request, to absorb the difference between
        /// the client's and the server's view of the player's position.
        /// </summary>
        public const float ServerSlack = 2f;

        /// <summary>
        /// True if a player standing <paramref name="distance"/> away may pick up or use something with the given interaction range.
        /// </summary>
        public static bool InRange(float distance, float interactionRange)
        {
            return distance <= interactionRange + ServerSlack;
        }

        /// <summary>
        /// Decides whether a player may open the lock that takes <paramref name="keyId"/>.
        /// </summary>
        /// <param name="held"> The keys the party holds.</param>
        /// <param name="opened"> The locks already opened, by key id.</param>
        /// <param name="keyId"> The lock being opened.</param>
        /// <param name="distance"> How far the player is from the door.</param>
        /// <param name="interactionRange"> How close a player must be to the door.</param>
        public static KeyUseResult CheckOpen(IEnumerable<int> held, IEnumerable<int> opened, int keyId, float distance, float interactionRange)
        {
            if (opened.Contains(keyId))
            {
                return KeyUseResult.AlreadyOpen;
            }

            if (!held.Contains(keyId))
            {
                return KeyUseResult.NotHeld;
            }

            return InRange(distance, interactionRange) ? KeyUseResult.Opened : KeyUseResult.TooFar;
        }
    }
}
