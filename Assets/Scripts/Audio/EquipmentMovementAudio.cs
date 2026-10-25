using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Keeps a character's <see cref="MovementAudioState"/> in step with what it has equipped: when the equipment changes, each equipped item's
    /// movement profile (footwear, armor class, step layers) is read from the item. This is the only link from equipment to audio; a future
    /// paper doll needs nothing more than the items it equips carrying a profile.
    /// </summary>
    [RequireComponent(typeof(Equipment))]
    [RequireComponent(typeof(MovementAudioState))]
    public sealed class EquipmentMovementAudio : MonoBehaviour
    {
        private Equipment equipment;
        private MovementAudioState state;

        private void Awake()
        {
            equipment = GetComponent<Equipment>();
            state = GetComponent<MovementAudioState>();
        }

        private void OnEnable()
        {
            equipment.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            equipment.Changed -= Refresh;
        }

        /// <summary>
        /// Rereads every slot.
        /// </summary>
        public void Refresh()
        {
            state.ClearAll();
            foreach ((EquipmentSlot slot, uint _, Item item) in equipment.Equipped)
            {
                if (item != null && item.MovementAudio != null)
                {
                    state.SetSlot((int)slot, item.MovementAudio);
                }
            }
        }
    }
}
