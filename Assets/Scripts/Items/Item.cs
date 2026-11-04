using SimpleRPG;
using SimpleRPG.Abilities;
using SimpleRPG.Audio;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    public enum EquipmentSlot
    {
        neck,
        head,
        chest,
        legs,
        feet,
        hands,
        mainHand,
        offhand,
        Consumable
    }

    /// <summary>
    /// A serializable base class containing information about items.
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "Items/Item")]
    public class Item : GameplayDefinition
    {
        [field: SerializeField]
        [Tooltip("Networked world pickup for this catalog item.")]
        public GameObject WorldPrefab { get; set; }
        [Header("Item Settings")]
        [field: SerializeField]
        [Tooltip("The image for the item to be displayed in the users Inventory.")]
        public Sprite Icon { get; private set; }

        [field: SerializeField]
        [Tooltip("The name of the item to be displayed in game.")]
        public string ItemName { get; private set; }

        /// <summary>
        /// The total size of the item in the inventory grid.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The total size of the item in the inventory grid.")]
        public Vector2Int ItemSize { private set; get; }

        /// <summary>
        /// Triggered effects this item brings while it is equipped, such as a chance to cast a spell when its wielder lands a blow.
        /// They run on the server only, so they are not part of what is sent to clients. Affixes bring more (see <see cref="AllTriggeredEffects"/>).
        /// </summary>
        [SerializeField]
        [Tooltip("Triggered effects this item brings while it is equipped.")]
        private List<TriggeredEffect> triggeredEffects = new List<TriggeredEffect>();

        /// <summary>
        /// The triggered effects this item's asset brings while it is equipped.
        /// </summary>
        public IReadOnlyList<TriggeredEffect> TriggeredEffects => triggeredEffects;

        [Header("Audio")]
        [SerializeField]
        [Tooltip("Sounds of handling this item in the inventory. Point it at a shared family profile, or override single actions.")]
        private ItemAudioProfile audioProfile;

        [SerializeField]
        [Tooltip("Sound of wearing this item: footwear, armor class and layered step sounds (plate clinking). Used when it is equipped.")]
        private MovementAudioProfile movementAudio;

        [SerializeField]
        [Tooltip("Which kind of item this is, for sound tables that vary by item type.")]
        private AudioTag audioSubject;

        public ItemAudioProfile AudioProfile => audioProfile;

        public MovementAudioProfile MovementAudio => movementAudio;

        public AudioTag AudioSubject => audioSubject;

        /// <summary>
        /// Changes to the wearer's attributes this item brings while it is equipped. Applied on the server and released when the item comes off.
        /// </summary>
        [SerializeField]
        [Tooltip("Changes to the wearer's attributes while this item is equipped.")]
        private List<GrantSpec> grants = new List<GrantSpec>();

        /// <summary>
        /// The attribute grants this item brings while it is equipped.
        /// </summary>
        public IReadOnlyList<GrantSpec> Grants => grants;

        [SerializeField]
        [Min(0)]
        [Tooltip("Charges each instance of this item starts with. Zero means the item has no charge pool. Charges are spent by the item's abilities and are not refilled.")]
        private int startingCharges;

        [SerializeField]
        [Tooltip("Abilities this item lets its wielder use while it is equipped, and what each costs.")]
        private List<ItemAbilityGrant> grantedAbilities = new List<ItemAbilityGrant>();

        [SerializeField]
        [Tooltip("Attributes this item has of its own, such as a staff's magic power. Abilities the item provides can read them whoever wields it.")]
        private List<AttributeValueSpec> fixedAttributes = new List<AttributeValueSpec>();

        [Header("Affixes")]
        [SerializeField]
        [Tooltip("The bonuses an instance of this item can roll when it comes into the world. None means it has no affixes and the game rolls nothing for it.")]
        private AffixPool affixPool;

        /// <summary>
        /// The pool new instances of this item roll their affixes from, or null.
        /// </summary>
        public AffixPool AffixPool => affixPool;

        /// <summary>
        /// The affixes this instance rolled. Fixed when the instance was made and never changed; empty for an item asset and for an instance
        /// made from an item with no pool.
        /// </summary>
        public ItemAffixes Affixes { get; private set; }

        private string displayName;
        private List<GrantSpec> combinedGrants;
        private List<TriggeredEffect> combinedEffects;

        /// <summary>
        /// The slot this item is worn in, for choosing which affixes may roll on it. Items that are not worn report the consumable slot.
        /// </summary>
        public virtual EquipmentSlot WornSlot => EquipmentSlot.Consumable;

        /// <summary>
        /// Everything this item changes about its wearer: the grants its asset declares plus what this instance rolled. An affix that cannot
        /// be found in the registry is skipped with a warning rather than failing the item.
        /// </summary>
        public IReadOnlyList<GrantSpec> AllGrants
        {
            get
            {
                if (Affixes.IsEmpty)
                {
                    return grants;
                }

                if (combinedGrants == null)
                {
                    combinedGrants = new List<GrantSpec>(grants);
                    DefinitionRegistry registry = DefinitionRegistry.Instance;
                    for (int i = 0; i < Affixes.Count; i++)
                    {
                        AffixRoll roll = Affixes[i];
                        if (registry != null && registry.TryGetByShortId(roll.AffixId, out GameplayDefinition definition) && definition is AffixDefinition affix)
                        {
                            if (affix.ValueCount != roll.ValueCount)
                            {
                                Debug.LogWarning($"{name}: affix {affix.name} now rolls {affix.ValueCount} values but this item rolled {roll.ValueCount}, so it does nothing.", this);
                                continue;
                            }

                            // Scaled by the item's own grants (the asset's), not by the grants of other affixes.
                            affix.AddGrants(grants, roll.Values(), combinedGrants);
                        }
                        else
                        {
                            Debug.LogWarning($"{name}: affix {roll.AffixId} is not in the DefinitionRegistry, so it does nothing. Rebuild the registry (SimpleRPG > Definitions).", this);
                        }
                    }
                }

                return combinedGrants;
            }
        }

        /// <summary>
        /// The name the player reads: the prefixes this instance rolled, the item's name, then its suffixes, such as "Riveted Mail Hauberk of the
        /// Veteran". An item without affixes is just its name. Affixes are fixed for the life of an instance, so this is worked out once.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (displayName != null)
                {
                    return displayName;
                }

                if (Affixes.IsEmpty)
                {
                    return ItemName;
                }

                var prefixes = new List<string>();
                var suffixes = new List<string>();
                DefinitionRegistry registry = DefinitionRegistry.Instance;
                for (int i = 0; i < Affixes.Count; i++)
                {
                    if (registry != null && registry.TryGetByShortId(Affixes[i].AffixId, out GameplayDefinition definition) && definition is AffixDefinition affix &&
                        !string.IsNullOrWhiteSpace(affix.DisplayName))
                    {
                        (affix.Kind == AffixKind.Prefix ? prefixes : suffixes).Add(affix.DisplayName.Trim());
                    }
                }

                var words = new List<string>(prefixes) { ItemName };
                words.AddRange(suffixes);
                displayName = string.Join(" ", words);
                return displayName;
            }
        }

        /// <summary>
        /// Every triggered effect this item brings while worn: the ones its asset declares plus those its rolled affixes bring. An affix that is
        /// not in the registry is skipped (<see cref="AllGrants"/> already warned about it).
        /// </summary>
        public IReadOnlyList<TriggeredEffect> AllTriggeredEffects
        {
            get
            {
                if (Affixes.IsEmpty)
                {
                    return triggeredEffects;
                }

                if (combinedEffects == null)
                {
                    combinedEffects = new List<TriggeredEffect>(triggeredEffects);
                    DefinitionRegistry registry = DefinitionRegistry.Instance;
                    for (int i = 0; i < Affixes.Count; i++)
                    {
                        if (registry != null && registry.TryGetByShortId(Affixes[i].AffixId, out GameplayDefinition definition) && definition is AffixDefinition affix)
                        {
                            affix.AddTriggeredEffects(combinedEffects);
                        }
                    }
                }

                return combinedEffects;
            }
        }

        /// <summary>
        /// Rolls affixes for a new instance of this item from its pool. Draws nothing if it has no pool, so adding affixes to some items never
        /// changes what the rest of a dungeon's loot rolls. Server (or offline) only.
        /// </summary>
        /// <param name="itemLevel">The level of the loot source this instance comes from (see <see cref="ItemLevel"/>).</param>
        public ItemAffixes RollAffixes(SimpleRPG.Gameplay.IGameplayRandom random, int itemLevel = ItemLevel.Minimum)
        {
            if (affixPool == null)
            {
                return default;
            }

            DefinitionRegistry registry = DefinitionRegistry.Instance;
            return AffixRoller.Roll(affixPool, WornSlot, itemLevel, random, affix => registry != null && registry.TryGetShortId(affix.Id, out uint id) ? id : 0u);
        }

        /// <summary>
        /// Charges each instance of this item starts with. Zero means no charge pool.
        /// </summary>
        public int StartingCharges => startingCharges;

        /// <summary>
        /// The abilities this item lets its wielder use while it is equipped.
        /// </summary>
        public IReadOnlyList<ItemAbilityGrant> GrantedAbilities => grantedAbilities;

        /// <summary>
        /// The attributes this item has of its own.
        /// </summary>
        public IReadOnlyList<AttributeValueSpec> FixedAttributes => fixedAttributes;

        /// <summary>
        /// The item asset this instance was cloned from, or null if this is the asset itself.
        /// </summary>
        public Item Template { get; private set; }

        /// <summary>
        /// The item asset that identifies this item: the asset itself, or the asset a clone was created from.
        /// </summary>
        public Item SourceAsset => Template != null ? Template : this;

        /// <summary>
        /// Creates a runtime copy of this item (for example the copy that lives in an inventory) that remembers its source asset.
        /// </summary>
        public Item CloneInstance() => CloneInstance(default);

        /// <summary>
        /// Creates a runtime copy that carries the affixes it rolled. They are set here, once, and never again.
        /// </summary>
        public Item CloneInstance(ItemAffixes affixes)
        {
            Item clone = Instantiate(SourceAsset);
            clone.Template = SourceAsset;
            clone.Affixes = affixes;
            return clone;
        }
    }
}


