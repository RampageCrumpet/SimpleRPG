using HitDetection;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG
{
    public class Character : NetworkBehaviour, ICombatant
    {
        /// <summary>
        /// The maximum health of this character.
        /// </summary>
        [SerializeField]
        [Tooltip("The maximum health of this character.")]
        private int maxHealth = 100;

        /// <summary>
        /// The abilities this character starts with, in ability bar order. The server checks every activation request against this list.
        /// </summary>
        [SerializeField]
        [Tooltip("The abilities this character can use, in ability bar order.")]
        private List<AbilityDefinition> graphAbilities = new List<AbilityDefinition>();

        private List<IAbilitySlot> abilitySlots;

        /// <summary>
        /// The weapon this character starts with, used when nothing is equipped in the main hand.
        /// </summary>
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("WeaponData")]
        [Tooltip("The weapon this character fights with when nothing is equipped in the main hand.")]
        private WeaponData startingWeapon;

        private Equipment equipment;

        /// <summary>
        /// The weapon this character attacks with: the one equipped in the main hand, or the starting weapon if there is none.
        /// </summary>
        public WeaponData WeaponData
        {
            get
            {
                if (equipment == null) equipment = GetComponent<Equipment>();

                WeaponData equipped = equipment != null ? equipment.MainHandWeapon : null;
                return equipped != null ? equipped : startingWeapon;
            }
            set => startingWeapon = value;
        }

        public delegate void NotifyDamageTaken();

        public event NotifyDamageTaken OnTakeDamage;

        public delegate void NotifyDeath();

        public event NotifyDeath OnDeath;

        public delegate void NotifyRevive();

        /// <summary>
        /// Raised on every machine when a dead character starts its next life. Presentation and transient client state reset here.
        /// </summary>
        public event NotifyRevive OnRevive;

        [Header("Attributes")]
        [SerializeField] private int strength = 10;
        [SerializeField] private int constitution = 10;
        [SerializeField] private int dexterity = 10;
        [SerializeField] private int agility = 10;
        [SerializeField] private int wisdom = 10;
        [SerializeField] private int intelligence = 10;

        /// <summary>
        /// This character's attributes: the six primaries plus whatever worn items grant. Only the server has them; they are what abilities read.
        /// Null before the character spawns and on clients.
        /// </summary>
        public AttributeSet Attributes { get; private set; }

        /// <summary>
        /// The replicated health value. Only the server writes it; every client reads it, so late joiners receive the
        /// current value as part of the spawn state instead of assuming full health.
        /// </summary>
        private readonly NetworkVariable<int> networkHealth = new NetworkVariable<int>();

        /// <summary>
        /// Health to apply when the character spawns, set if <see cref="Initialize"/> is called before the network object is spawned.
        /// </summary>
        private int? pendingInitialHealth;

        /// <summary>
        /// True once this character's current life has ended. Replicated, so every machine sees the death and raises <see cref="OnDeath"/>.
        /// </summary>
        private readonly NetworkVariable<bool> networkIsDead = new NetworkVariable<bool>();

        /// <summary>
        /// How many lives have ended before the current one. The current life number is this plus one.
        /// </summary>
        private readonly NetworkVariable<int> networkLivesEnded = new NetworkVariable<int>();

        /// <summary>
        /// The ID this character is registered as a combatant under, on the server.
        /// </summary>
        private EntityId registeredId;

        /// <summary>
        /// Where the character spawned, on the server. A respawn returns the character here.
        /// </summary>
        private Vector3 respawnPosition;
        private Quaternion respawnRotation;

        /// <summary>
        /// The current health of this character. Before the character spawns on the network this reports full health.
        /// </summary>
        public int Health => IsSpawned ? networkHealth.Value : (pendingInitialHealth ?? maxHealth);

        /// <summary>
        /// The maximum health of this character.
        /// </summary>
        public int MaxHealth => maxHealth;

        /// <summary>
        /// True while this character is alive. Death is committed once by the server; AI and presentation read this instead of watching health.
        /// </summary>
        public bool IsAlive => !IsSpawned || !networkIsDead.Value;

        /// <summary>
        /// The identity of this character for gameplay rules: its network object ID, or none before it spawns.
        /// </summary>
        public EntityId Id => IsSpawned ? new EntityId(NetworkObjectId) : default;

        /// <summary>
        /// Which life this is, starting at 1. Reviving starts the next one.
        /// </summary>
        public int LifeId => IsSpawned ? networkLivesEnded.Value + 1 : 1;

        Position3 ICombatant.Position => new Position3(transform.position.x, transform.position.y, transform.position.z);

        /// <summary>
        /// The abilities this character can use.
        /// </summary>
        public IReadOnlyList<AbilityDefinition> GraphAbilities => graphAbilities;

        /// <summary>
        /// Every ability on this character's bar, in order.
        /// </summary>
        public IReadOnlyList<IAbilitySlot> AbilitySlots
        {
            get
            {
                if (abilitySlots == null)
                {
                    abilitySlots = graphAbilities.Select(x => (IAbilitySlot)new GraphAbilitySlot(x, this)).ToList();
                }

                return abilitySlots;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                networkHealth.Value = pendingInitialHealth ?? maxHealth;
                registeredId = Id;
                respawnPosition = transform.position;
                respawnRotation = transform.rotation;
                GameplayServices.Current.Combatants.Register(this);

                // The character's attributes live on the server, where abilities read them and worn items add their grants to them.
                Attributes = GameplayServices.Current.Attributes.GetOrCreate(SourceHandle.FromEntity(registeredId));
                Attributes.SetBase(PrimaryAttributes.Strength, strength);
                Attributes.SetBase(PrimaryAttributes.Constitution, constitution);
                Attributes.SetBase(PrimaryAttributes.Dexterity, dexterity);
                Attributes.SetBase(PrimaryAttributes.Agility, agility);
                Attributes.SetBase(PrimaryAttributes.Wisdom, wisdom);
                Attributes.SetBase(PrimaryAttributes.Intelligence, intelligence);
            }

            networkHealth.OnValueChanged += HandleHealthChanged;
            networkIsDead.OnValueChanged += HandleDeadChanged;
        }

        public override void OnNetworkDespawn()
        {
            networkHealth.OnValueChanged -= HandleHealthChanged;
            networkIsDead.OnValueChanged -= HandleDeadChanged;

            if (IsServer)
            {
                // The network ID is already gone by now, so unregister under the ID it was registered with.
                GameplayServices.Current.Combatants.Unregister(registeredId, this);
                GameplayServices.Current.Attributes.Unregister(SourceHandle.FromEntity(registeredId));
                Attributes = null;

                // Rules and state tied to this entity can be dropped.
                GameplayEventDispatcher events = GameplayServices.Current.Events;
                events.Enqueue(new EntityRemovedEvent(registeredId));
                if (!events.IsDraining)
                {
                    events.Drain();
                }
            }
        }

        /// <summary>
        /// Sets the health to maximum. Only the server's value matters; clients receive health through replication, so calling this on a client does nothing.
        /// </summary>
        public void Initialize()
        {
            if (!IsSpawned)
            {
                pendingInitialHealth = maxHealth;
            }
            else if (IsServer)
            {
                networkHealth.Value = maxHealth;
            }
        }

        /// <summary>
        /// Reduce the characters health by the given damage. Health is server authoritative, so calls from clients are ignored.
        /// </summary>
        /// <param name="damage">The damage to apply.</param>
        /// <param name="location">The body location that was hit.</param>
        /// <param name="instigator">The character credited with the damage, if any.</param>
        /// <param name="provenance">What kind of damage this is, so rules can tell a weapon blow from a spell.</param>
        public void TakeDamage(DamageInfo damage, BodyLocation location, Character instigator = null, DamageProvenance provenance = DamageProvenance.None)
        {
            if (!IsServer)
            {
                Debug.LogWarning(this.gameObject.name + " ignored a damage request on a client; health is server authoritative.");
                return;
            }

            DamageRequest request = new DamageRequest(Id, damage.Damage, instigator != null ? instigator.Id : default, bodyLocation: (int)location, provenance: provenance);
            DamageOutcome outcome = GameplayServices.Current.Combat.Apply(request);

            if (outcome.WasApplied)
            {
                Debug.Log(this.gameObject.name + " has taken " + damage.Damage + " damage at " + location.ToString() + ".");
            }

            if (outcome.Result == DamageResult.Killed)
            {
                Debug.Log(this.gameObject.name + " has died.");
            }
        }

        void ICombatant.CommitHealth(int health)
        {
            networkHealth.Value = health;
        }

        void ICombatant.CommitDeath()
        {
            networkIsDead.Value = true;
        }

        void ICombatant.CommitRevive(int health)
        {
            networkLivesEnded.Value++;
            networkHealth.Value = health;
            networkIsDead.Value = false;
        }

        /// <summary>
        /// Asks for a dead character to start its next life at its spawn point with full health. On the server this happens at once; the owning
        /// client asks the server, which decides. Does nothing while the character is alive.
        /// </summary>
        public void RequestRespawn()
        {
            if (!IsSpawned || IsAlive)
            {
                return;
            }

            if (IsServer)
            {
                RespawnOnServer();
            }
            else if (IsOwner)
            {
                RespawnServerRpc();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RespawnServerRpc()
        {
            RespawnOnServer();
        }

        /// <summary>
        /// Starts the next life of a dead character: back at its spawn point, with full health. Server only.
        /// </summary>
        /// <returns>False if the character is alive, not spawned or not on the server.</returns>
        public bool RespawnOnServer()
        {
            if (!IsServer || !IsSpawned || IsAlive)
            {
                return false;
            }

            Teleport(respawnPosition, respawnRotation);
            return GameplayServices.Current.Combat.Revive(Id, maxHealth);
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            CharacterController body = GetComponent<CharacterController>();
            bool bodyWasEnabled = body != null && body.enabled;
            if (bodyWasEnabled) body.enabled = false;

            if (TryGetComponent(out NetworkTransform networkTransform) && networkTransform.CanCommitToTransform)
            {
                networkTransform.Teleport(position, rotation, transform.localScale);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            if (bodyWasEnabled) body.enabled = true;
        }

        /// <summary>
        /// Raises <see cref="OnTakeDamage"/> on every machine whenever replicated health goes down.
        /// </summary>
        private void HandleHealthChanged(int previousValue, int newValue)
        {
            if (newValue < previousValue)
            {
                OnTakeDamage?.Invoke();
            }
        }

        /// <summary>
        /// Raises <see cref="OnDeath"/> on every machine when the replicated dead flag turns on. This is presentation; the death itself was committed by the server.
        /// </summary>
        private void HandleDeadChanged(bool previousValue, bool newValue)
        {
            if (newValue && !previousValue)
            {
                OnDeath?.Invoke();
            }
            else if (!newValue && previousValue)
            {
                OnRevive?.Invoke();
            }
        }
    }
}
