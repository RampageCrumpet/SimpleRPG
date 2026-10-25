using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// What a worn or carried item adds to movement: the footwear it counts as, the armor class it counts as, and a sound layered onto steps
    /// (plate clinking, chainmail rustling). Assigned to an <see cref="SimpleRPG.InventorySystem.Item"/>; the equipment audio adapter reads it when
    /// the item is equipped.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementAudioProfile", menuName = "Audio/Movement Profile")]
    public sealed class MovementAudioProfile : ScriptableObject
    {
        [Tooltip("Set on boots and shoes: what the character counts as wearing on its feet. Feeds footstep tables.")]
        [SerializeField] private AudioTag footwear;

        [Tooltip("Set on body armor: the armor class, for impact tables.")]
        [SerializeField] private AudioTag armor;

        [Tooltip("Layered onto steps while the item is worn. Leave empty for an item that only sets footwear or armor.")]
        [SerializeField] private AudioEventBase stepSound;

        [Tooltip("Chance the layer joins a given step, so it is not heard on every footfall.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 0.6f;

        [Tooltip("The layer only joins steps at least this hard (0 walking slowly, 1 sprinting).")]
        [Range(0f, 1f)]
        [SerializeField] private float minIntensity;

        [Tooltip("Shortest time between two plays of this layer, in seconds.")]
        [Min(0f)]
        [SerializeField] private float cooldown = 0.25f;

        [Tooltip("Layers sharing a group name never play on the same step, so two metal items do not both clink at once. Leave empty for no group.")]
        [SerializeField] private string group;

        /// <summary>Creates a profile from code, for tools and tests. Authored profiles are assets.</summary>
        public static MovementAudioProfile Create(string name, AudioTag footwear = null, AudioTag armor = null, AudioEventBase stepSound = null, float chance = 1f, float cooldown = 0f, string group = null, float minIntensity = 0f)
        {
            MovementAudioProfile profile = CreateInstance<MovementAudioProfile>();
            profile.name = name;
            profile.footwear = footwear;
            profile.armor = armor;
            profile.stepSound = stepSound;
            profile.chance = chance;
            profile.cooldown = cooldown;
            profile.group = group;
            profile.minIntensity = minIntensity;
            return profile;
        }

        public AudioTag Footwear => footwear;
        public AudioTag Armor => armor;
        public AudioEventBase StepSound => stepSound;
        public float Chance => chance;
        public float MinIntensity => minIntensity;
        public float Cooldown => cooldown;
        public string Group => group;
    }
}
