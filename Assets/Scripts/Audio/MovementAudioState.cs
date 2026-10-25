using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// What a character currently wears that affects movement sound. Future equipment fills it by calling <see cref="SetSlot"/>; footsteps and
    /// impacts read the footwear and armor tags and the step layers from it.
    /// </summary>
    public sealed class MovementAudioState : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Footwear assumed when no equipped item sets one (bare feet, a monster's own feet).")]
        private AudioTag defaultFootwear;

        [SerializeField]
        [Tooltip("The most equipment layers that join a single step.")]
        [Min(0)]
        private int maxLayersPerStep = 2;

        private readonly Dictionary<int, MovementAudioProfile> slots = new Dictionary<int, MovementAudioProfile>();
        private readonly List<MovementLayer> layers = new List<MovementLayer>();
        private readonly MovementLayerPicker picker = new MovementLayerPicker();
        private readonly List<AudioEventBase> picked = new List<AudioEventBase>(4);

        public AudioTag DefaultFootwear
        {
            get => defaultFootwear;
            set => defaultFootwear = value;
        }

        public int MaxLayersPerStep
        {
            get => maxLayersPerStep;
            set => maxLayersPerStep = Mathf.Max(0, value);
        }

        /// <summary>The footwear of the equipped items, or the default.</summary>
        public AudioTag Footwear
        {
            get
            {
                foreach (MovementAudioProfile profile in slots.Values)
                {
                    if (profile != null && profile.Footwear != null)
                    {
                        return profile.Footwear;
                    }
                }

                return defaultFootwear;
            }
        }

        /// <summary>The armor class of the equipped items, or null.</summary>
        public AudioTag Armor
        {
            get
            {
                foreach (MovementAudioProfile profile in slots.Values)
                {
                    if (profile != null && profile.Armor != null)
                    {
                        return profile.Armor;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Sets what the item in a slot contributes, or clears the slot with null. The slot key is any stable number, normally the equipment slot.
        /// </summary>
        public void SetSlot(int slot, MovementAudioProfile profile)
        {
            if (profile == null)
            {
                slots.Remove(slot);
            }
            else
            {
                slots[slot] = profile;
            }

            layers.Clear();
            foreach (KeyValuePair<int, MovementAudioProfile> pair in slots)
            {
                MovementAudioProfile entry = pair.Value;
                if (entry.StepSound != null)
                {
                    layers.Add(new MovementLayer
                    {
                        Key = pair.Key,
                        Sound = entry.StepSound,
                        Chance = entry.Chance,
                        MinIntensity = entry.MinIntensity,
                        Cooldown = entry.Cooldown,
                        Group = entry.Group,
                    });
                }
            }
        }

        public void ClearAll()
        {
            slots.Clear();
            layers.Clear();
        }

        /// <summary>
        /// The equipment sounds that join one step. The returned list is reused: read it before the next call.
        /// </summary>
        public List<AudioEventBase> PickStepLayers(float intensity, float now, IAudioRandom random)
        {
            picked.Clear();
            picker.Pick(layers, intensity, now, random, maxLayersPerStep, picked);
            return picked;
        }
    }
}
