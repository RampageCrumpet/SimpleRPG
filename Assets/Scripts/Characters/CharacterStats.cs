using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG
{
    /// <summary>
    /// A character's attributes and derived stats: the six primaries plus whatever worn items grant, resolved into maximum health, movement speed
    /// and so on. Only the server has them; they are what abilities read. <see cref="Character"/> owns the character's identity and lifecycle and
    /// initialises this when it spawns on the server, so this has no network behaviour of its own.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class CharacterStats : MonoBehaviour
    {
        [Header("Attributes")]
        [SerializeField] private int strength = 10;
        [SerializeField] private int constitution = 10;
        [SerializeField] private int dexterity = 10;
        [SerializeField] private int agility = 10;
        [UnityEngine.Serialization.FormerlySerializedAs("wisdom")]
        [SerializeField] private int willpower = 10;
        [SerializeField] private int intelligence = 10;

        /// <summary>
        /// How this character's derived stats (maximum health, movement speed and so on) are resolved: from the core attributes for a player,
        /// or authored directly for an enemy. With none, the character has only its core attributes and nothing derived.
        /// </summary>
        [SerializeField]
        [Tooltip("How this character's derived stats are resolved. Leave empty for a character that has none.")]
        private StatProfile statProfile;

        /// <summary>
        /// This character's attributes: the six primaries plus whatever worn items grant. Only the server has them.
        /// Null before the character spawns and on clients.
        /// </summary>
        public AttributeSet Attributes { get; private set; }

        /// <summary>
        /// Raised on the server whenever an attribute changes, so what depends on a derived stat (maximum health) can follow it.
        /// </summary>
        public event Action Modified;

        /// <summary>
        /// The movement speed the server moves this character at: its resolved Movement Speed, in units per second. Zero on a character with no Movement Speed stat
        /// and on clients, which never decide it.
        /// </summary>
        public float MovementSpeed => Attributes != null ? (float)Mathf.Max(0f, (float)Attributes.Get(DerivedStats.MovementSpeed)) : 0f;

        /// <summary>
        /// True if this character's stat profile defines the derived stat, so a stat sheet can leave out stats the character does not have.
        /// </summary>
        public bool HasDerivedStat(AttributeId stat)
        {
            if (statProfile == null)
            {
                return false;
            }

            foreach (var rule in statProfile.Sheet.Rules)
            {
                if (rule.Stat == stat)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Maximum Health as the server resolves it: the stat rounded to a whole number and at least 1. False if the character has no such stat,
        /// or no attributes yet, in which case the character's own fallback applies.
        /// </summary>
        public bool TryResolveMaxHealth(out int maxHealth)
        {
            if (Attributes != null && HasDerivedStat(DerivedStats.MaximumHealth))
            {
                maxHealth = Mathf.Max(1, Mathf.RoundToInt((float)Attributes.Get(DerivedStats.MaximumHealth)));
                return true;
            }

            maxHealth = 0;
            return false;
        }

        /// <summary>
        /// Creates this character's attributes and registers them under its identity, so abilities and worn items can find them. Server only;
        /// called by <see cref="Character"/> as it spawns.
        /// </summary>
        public void Initialize(EntityId id)
        {
            Attributes = statProfile != null ? statProfile.CreateSet() : new AttributeSet();
            GameplayServices.Current.Attributes.Register(SourceHandle.FromEntity(id), Attributes);

            // A character whose stats are authored directly has no core attributes to set.
            if (statProfile == null || statProfile.Ownership == StatOwnership.Derived)
            {
                Attributes.SetBase(PrimaryAttributes.Strength, strength);
                Attributes.SetBase(PrimaryAttributes.Constitution, constitution);
                Attributes.SetBase(PrimaryAttributes.Dexterity, dexterity);
                Attributes.SetBase(PrimaryAttributes.Agility, agility);
                Attributes.SetBase(PrimaryAttributes.Willpower, willpower);
                Attributes.SetBase(PrimaryAttributes.Intelligence, intelligence);
            }

            Attributes.Modified += RaiseModified;
        }

        /// <summary>
        /// Drops this character's attributes and unregisters them. Server only; called by <see cref="Character"/> as it despawns, under the id it
        /// registered with because the network id is already gone by then.
        /// </summary>
        public void Release(EntityId id)
        {
            if (Attributes != null)
            {
                Attributes.Modified -= RaiseModified;
            }

            GameplayServices.Current.Attributes.Unregister(SourceHandle.FromEntity(id));
            Attributes = null;
        }

        private void RaiseModified() => Modified?.Invoke();
    }
}
