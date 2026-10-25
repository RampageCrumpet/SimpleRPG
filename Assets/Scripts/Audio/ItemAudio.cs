using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>The inventory transactions an item can have a sound for.</summary>
    public enum InventoryAudioAction
    {
        Pickup,
        Place,
        Move,
        Stack,
        Drop,
    }

    /// <summary>
    /// Plays inventory sounds for meaningful transactions (an item picked up, placed, moved, dropped), never for an item sliding across grid cells.
    /// </summary>
    public static class InventoryAudio
    {
        /// <summary>
        /// The sound for an item and action, or null if the item has no profile or none covers the action.
        /// </summary>
        public static AudioEventBase SoundFor(Item item, InventoryAudioAction action)
        {
            ItemAudioProfile profile = item != null ? item.AudioProfile : null;
            return profile != null ? profile.Get(action) : null;
        }

        /// <summary>Plays the item's sound for an action as an interface sound with no position.</summary>
        public static AudioHandle Play(Item item, InventoryAudioAction action)
        {
            return GameAudio.Play2D(SoundFor(item, action), new AudioContext { Subject = item != null ? item.AudioSubject : null });
        }

        /// <summary>Plays the item's sound for an action in the world.</summary>
        public static AudioHandle PlayAt(Item item, InventoryAudioAction action, Vector3 position)
        {
            return GameAudio.Play(SoundFor(item, action), new AudioContext { Subject = item != null ? item.AudioSubject : null }, position);
        }
    }
}
