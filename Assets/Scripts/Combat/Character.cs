using SimpleRPG.HitDetection;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG
{
    /// <summary>
    /// A combatant: it has an identity, health, and a life that ends and can begin again. Its attributes live in <see cref="CharacterStats"/> and
    /// what it fights with in <see cref="CombatLoadout"/>, which it requires; what it wears is <see cref="Equipment"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    [RequireComponent(typeof(CombatLoadout))]
    public class Character : NetworkBehaviour, ICombatant, IRegionDamageModifier, IDamageMitigator
    {
        private HitAnatomy anatomy;
        private bool anatomyLooked;

        private CharacterStats stats;

        private CombatLoadout loadout;

        /// <summary>
        /// This character's attributes and derived stats.
        /// </summary>
        public CharacterStats Stats => stats != null ? stats : (stats = GetComponent<CharacterStats>());

        /// <summary>
        /// This character's abilities and default weapon.
        /// </summary>
        public CombatLoadout Loadout => loadout != null ? loadout : (loadout = GetComponent<CombatLoadout>());

        /// <summary>
        /// The maximum health of a character with no stat profile defining Maximum Health. Otherwise the resolved Maximum Health is used.
        /// </summary>
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("maxHealth")]
        [Tooltip("Maximum health for a character whose stat profile does not define Maximum Health. Ignored when it does.")]
        private int fallbackMaxHealth = 100;

        public delegate void NotifyDamageTaken();

        public event NotifyDamageTaken OnTakeDamage;

        public delegate void NotifyDeath();

        public event NotifyDeath OnDeath;

        public delegate void NotifyRevive();

        /// <summary>
        /// Raised on every machine when a dead character starts its next life. Presentation and transient client state reset here.
        /// </summary>
        public event NotifyRevive OnRevive;

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
        /// The server-resolved Maximum Health, replicated so every machine can show health as a fraction of it.
        /// </summary>
        private readonly NetworkVariable<int> networkMaxHealth = new NetworkVariable<int>();

        /// <summary>
        /// True once this character's current life has ended. Replicated, so every machine sees the death and raises <see cref="OnDeath"/>.
        /// </summary>
        private readonly NetworkVariable<bool> networkIsDead = new NetworkVariable<bool>();

        /// <summary>
        /// How many lives have ended before the current one. The current life number is this plus one.
        /// </summary>
        private readonly NetworkVariable<int> networkLivesEnded = new NetworkVariable<int>();

        /// <summary>
        /// When the current life began, in server time (see <see cref="ServerClock"/>). Replicated, so a machine that sees the character late can join
        /// its starting animation part-way through, the way a swing is joined, instead of playing it from the start.
        /// </summary>
        private readonly NetworkVariable<double> networkLifeStartedAt = new NetworkVariable<double>();

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
        public int Health => IsSpawned ? networkHealth.Value : (pendingInitialHealth ?? MaxHealth);

        /// <summary>
        /// The maximum health of this character.
        /// </summary>
        public int MaxHealth => IsSpawned ? networkMaxHealth.Value : ResolveMaxHealth();

        /// <summary>
        /// Maximum Health as the server resolves it: the stat rounded to a whole number and at least 1, or the fallback if the character has no such stat.
        /// </summary>
        private int ResolveMaxHealth()
        {
            return Stats.TryResolveMaxHealth(out int maxHealth) ? maxHealth : fallbackMaxHealth;
        }

        /// <summary>
        /// Brings the replicated maximum up to date after a stat change. Current health never rises because the maximum did,
        /// but it comes down with a lower maximum. A living character is never killed by the clamp.
        /// </summary>
        private void RefreshMaxHealth()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            int max = ResolveMaxHealth();
            if (networkMaxHealth.Value != max) networkMaxHealth.Value = max;

            int clamped = networkIsDead.Value ? 0 : Mathf.Clamp(networkHealth.Value, 1, max);
            if (!networkIsDead.Value && networkHealth.Value != clamped) networkHealth.Value = clamped;
        }

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

        /// <summary>
        /// When the current life began, in server time: when the character spawned, or when it last revived. Now before it spawns.
        /// </summary>
        public double LifeStartedAt => IsSpawned ? networkLifeStartedAt.Value : ServerClock.Now;

        /// <summary>
        /// What a weapon blow to a body location is worth against this character, which its <see cref="HitAnatomy"/> decides. 1 for a character without one.
        /// </summary>
        float IRegionDamageModifier.DamageMultiplier(int bodyLocation)
        {
            if (!anatomyLooked)
            {
                anatomy = GetComponent<HitAnatomy>();
                anatomyLooked = true;
            }

            return anatomy != null ? anatomy.DamageMultiplier(bodyLocation) : 1f;
        }

        /// <summary>
        /// What this character's defences take off damage of a type, which its stats decide. The resolver asks once per request.
        /// </summary>
        double IDamageMitigator.Reduction(DamageType type) => Stats.DamageReduction(type);

        Position3 ICombatant.Position => new Position3(transform.position.x, transform.position.y, transform.position.z);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                registeredId = Id;
                networkLifeStartedAt.Value = ServerClock.Now;
                respawnPosition = transform.position;
                respawnRotation = transform.rotation;
                GameplayServices.Current.Combatants.Register(this);

                // The character's attributes live on the server, where abilities read them and worn items add their grants to them.
                Stats.Initialize(registeredId);

                networkMaxHealth.Value = ResolveMaxHealth();
                networkHealth.Value = Mathf.Min(pendingInitialHealth ?? networkMaxHealth.Value, networkMaxHealth.Value);
                Stats.Modified += RefreshMaxHealth;
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
                Stats.Modified -= RefreshMaxHealth;
                Stats.Release(registeredId);

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
                pendingInitialHealth = ResolveMaxHealth();
            }
            else if (IsServer)
            {
                networkHealth.Value = networkMaxHealth.Value;
            }
        }

        /// <summary>
        /// Reduce the characters health by the given damage. Health is server authoritative, so calls from clients are ignored.
        /// </summary>
        /// <param name="damage">The damage to apply.</param>
        /// <param name="location">The body location that was hit.</param>
        /// <param name="instigator">The character credited with the damage, if any.</param>
        /// <param name="provenance">Where the damage came from, so rules can tell a weapon blow from a spell.</param>
        /// <param name="type">Which defence the damage meets: Physical Damage Reduction or Magical Damage Reduction.</param>
        /// <param name="bypassesMitigation">True to skip the defences altogether. Damage is reduced unless the caller says otherwise.</param>
        public void TakeDamage(DamageInfo damage, BodyLocation location, Character instigator = null, DamageProvenance provenance = DamageProvenance.None,
            DamageType type = DamageType.Physical, bool bypassesMitigation = false)
        {
            if (!IsServer)
            {
                Debug.LogWarning(this.gameObject.name + " ignored a damage request on a client; health is server authoritative.");
                return;
            }

            DamageRequest request = new DamageRequest(Id, damage.Damage, instigator != null ? instigator.Id : default, bodyLocation: (int)location, provenance: provenance, type: type, bypassesMitigation: bypassesMitigation);
            GameplayServices.Current.Combat.Apply(request);

            // The resolver publishes damage and death events; nothing is logged per hit.
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
            networkLifeStartedAt.Value = ServerClock.Now;
            networkHealth.Value = health;
            networkIsDead.Value = false;
        }

        /// <summary>
        /// Asks for a dead character to start its next life at its spawn point with full health. On the server this happens at once; the owning
        /// client asks the server, which decides. Does nothing while the character is alive.
        /// </summary>
        public void RequestRespawn()
        {
            // Whether a dead character may respawn is up to the session's IRespawnPolicy (a running delve makes it wait).
            if (!IsSpawned || IsAlive || !RespawnPolicies.AllowsRespawn(this))
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
            if (RespawnPolicies.AllowsRespawn(this))
            {
                RespawnOnServer();
            }
        }

        /// <summary>
        /// Sets how hurt a character is, for one returning to the delve as it left it. Never kills, and never goes above the maximum. Server only.
        /// </summary>
        public void RestoreHealthOnServer(int health)
        {
            if (!IsServer || !IsSpawned || networkIsDead.Value)
            {
                return;
            }

            networkHealth.Value = Mathf.Clamp(health, 1, networkMaxHealth.Value);
        }

        /// <summary>
        /// Changes where this character goes when it respawns or is returned to the start. For a character spawned somewhere other than the
        /// start, such as one returning to where it left off. Server only.
        /// </summary>
        public void SetRespawnPointOnServer(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
            {
                return;
            }

            respawnPosition = position;
            respawnRotation = rotation;
        }

        /// <summary>
        /// Puts a living character back at its spawn point, as the start of a new delve does for those who got out. Server only.
        /// </summary>
        /// <returns>False if the character is dead, not spawned or not on the server.</returns>
        public bool ReturnToSpawnOnServer()
        {
            if (!IsServer || !IsSpawned || !IsAlive)
            {
                return false;
            }

            Teleport(respawnPosition, respawnRotation);
            return true;
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
            return GameplayServices.Current.Combat.Revive(Id, networkMaxHealth.Value);
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
