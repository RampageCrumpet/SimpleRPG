using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// The sounds of handling an item. A profile can stand for a whole family (gems, coins, heavy weapons) and an individual item's profile
    /// names the family as its <see cref="Parent"/> and overrides only what differs; any action it leaves empty falls back up the chain.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemAudioProfile", menuName = "Audio/Item Profile")]
    public sealed class ItemAudioProfile : ScriptableObject
    {
        [Tooltip("The family to fall back to for any action left empty here.")]
        [SerializeField] private ItemAudioProfile parent;

        [SerializeField] private AudioEventBase pickup;
        [SerializeField] private AudioEventBase place;
        [SerializeField] private AudioEventBase move;
        [SerializeField] private AudioEventBase stack;
        [SerializeField] private AudioEventBase drop;

        public ItemAudioProfile Parent => parent;

        /// <summary>Creates a profile from code, for tools and tests. Authored profiles are assets.</summary>
        public static ItemAudioProfile Create(string name, ItemAudioProfile parent = null, AudioEventBase pickup = null, AudioEventBase place = null)
        {
            ItemAudioProfile profile = CreateInstance<ItemAudioProfile>();
            profile.name = name;
            profile.parent = parent;
            profile.pickup = pickup;
            profile.place = place;
            return profile;
        }

        /// <summary>
        /// The sound for an action, from this profile or the nearest family above it that has one, or null.
        /// </summary>
        public AudioEventBase Get(InventoryAudioAction action)
        {
            ItemAudioProfile current = this;
            for (int steps = 0; current != null && steps < AudioTag.MaxChain; steps++)
            {
                AudioEventBase found = current.Own(action);
                if (found != null)
                {
                    return found;
                }

                current = current.parent;
            }

            return null;
        }

        private AudioEventBase Own(InventoryAudioAction action)
        {
            switch (action)
            {
                case InventoryAudioAction.Pickup: return pickup;
                case InventoryAudioAction.Place: return place;
                case InventoryAudioAction.Move: return move;
                case InventoryAudioAction.Stack: return stack;
                case InventoryAudioAction.Drop: return drop;
                default: return null;
            }
        }
    }
}
