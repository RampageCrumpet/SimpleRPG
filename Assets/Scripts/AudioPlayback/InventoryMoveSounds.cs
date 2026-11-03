using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// The sound of the local player moving an item: carried to another container is a placement, shuffled inside one is a move. Observes
    /// <see cref="Inventory.LocalItemMoved"/>, so the inventory itself knows nothing of sound.
    /// </summary>
    public static class InventoryMoveSounds
    {
        /// <summary>The sound a completed move has.</summary>
        public static InventoryAudioAction ActionFor(Inventory source, Inventory destination)
        {
            return destination == source ? InventoryAudioAction.Move : InventoryAudioAction.Place;
        }

        private static void OnItemMoved(Item item, Inventory source, Inventory destination)
        {
            InventoryAudio.Play(item, ActionFor(source, destination));
        }

        // Domain reload may be off in the editor, so the subscription is made fresh on entering play mode rather than added again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Subscribe()
        {
            Inventory.LocalItemMoved -= OnItemMoved;
            Inventory.LocalItemMoved += OnItemMoved;
        }
    }
}
