using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.UI;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Lets a character use graph-based abilities. A player's request arrives from their client and the server validates it: the character
    /// must be alive, must have the ability, and the ability's cooldown must have passed. The ability then runs on the server, so a client
    /// can only ask; it can never supply damage, targets or timing. Monsters and other server-side users call
    /// <see cref="TryActivateOnServer"/> directly.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public sealed class AbilityUser : NetworkBehaviour, IAbilityActivator, IBuffSource
    {
        /// <summary>
        /// Ticks of slack on the cooldown, so network jitter between a client's press and the server's clock doesn't refuse a use that was ready
        /// on the client. About 0.1 seconds at the default tick rate.
        /// </summary>
        private const long CooldownToleranceTicks = 3;

        /// <summary>
        /// Where projectiles start relative to this character, in local space. Keeps them clear of the caster's body.
        /// </summary>
        [SerializeField]
        [Tooltip("Where projectiles start, in this character's local space.")]
        private Vector3 muzzleOffset = new Vector3(0f, 1.2f, 0.8f);

        /// <summary>
        /// One status on this character as every machine sees it: which status, how many stacks, and when it ends. Short ids only; the
        /// definition is looked up in the <see cref="DefinitionRegistry"/> on each machine.
        /// </summary>
        public struct ActiveStatusEntry : INetworkSerializable, IEquatable<ActiveStatusEntry>
        {
            public uint Group;
            public uint StatusShortId;
            public int Stacks;

            /// <summary>The network tick the status ends at (its last stack's, when stacks run out separately).</summary>
            public long ExpiryTick;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Group);
                serializer.SerializeValue(ref StatusShortId);
                serializer.SerializeValue(ref Stacks);
                serializer.SerializeValue(ref ExpiryTick);
            }

            public bool Equals(ActiveStatusEntry other) =>
                Group == other.Group && StatusShortId == other.StatusShortId && Stacks == other.Stacks && ExpiryTick == other.ExpiryTick;

            public override bool Equals(object obj) => obj is ActiveStatusEntry other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Group, StatusShortId, Stacks, ExpiryTick);
        }

        private Character character;

        /// <summary>
        /// The statuses on this character. Written only by the server from the status events; every client, late joiners included, reads it.
        /// </summary>
        private readonly NetworkList<ActiveStatusEntry> activeStatuses = new NetworkList<ActiveStatusEntry>();

        private GameplayEventDispatcher statusEvents;

        private readonly List<SubscriptionHandle> statusSubscriptions = new List<SubscriptionHandle>();

        private CooldownTracker cooldowns;

        private ItemAbilityCaster itemCaster;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        /// <summary>
        /// Raised on every machine when this character's statuses change.
        /// </summary>
        public event Action StatusesChanged;

        /// <summary>
        /// Lists the statuses on this character for the buff bar. Every machine reads the replicated list, so a client sees what the server decided.
        /// </summary>
        public void GetBuffs(List<BuffEntry> into)
        {
            into.Clear();
            if (!IsSpawned || DefinitionRegistry.Instance == null)
            {
                return;
            }

            // Polled every frame by the buff bar, so read the replicated list directly instead of building another one.
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (TryResolveStatus(activeStatuses[i], out StatusDefinition status))
                {
                    into.Add(new BuffEntry(status.StatusName, status.Icon, activeStatuses[i].Stacks));
                }
            }
        }

        private static bool TryResolveStatus(ActiveStatusEntry entry, out StatusDefinition status)
        {
            if (DefinitionRegistry.Instance.TryGetByShortId(entry.StatusShortId, out GameplayDefinition definition) && definition is StatusDefinition resolved)
            {
                status = resolved;
                return true;
            }

            status = null;
            return false;
        }

        /// <summary>
        /// Gets the statuses on this character with the network tick each ends at, from the replicated list.
        /// </summary>
        public IReadOnlyList<(StatusDefinition Status, int Stacks, long ExpiryTick)> GetActiveStatuses()
        {
            List<(StatusDefinition, int, long)> result = new List<(StatusDefinition, int, long)>();
            if (!IsSpawned || DefinitionRegistry.Instance == null)
            {
                return result;
            }

            foreach (ActiveStatusEntry entry in activeStatuses)
            {
                if (TryResolveStatus(entry, out StatusDefinition status))
                {
                    result.Add((status, entry.Stacks, entry.ExpiryTick));
                }
            }

            return result;
        }

        public override void OnNetworkSpawn()
        {
            activeStatuses.OnListChanged += HandleStatusListChanged;

            if (IsServer)
            {
                cooldowns = new CooldownTracker(AbilityServices.Current.Clock, CooldownToleranceTicks);
                itemCaster = new ItemAbilityCaster(AbilityServices.Current.Executor, GameplayServices.Current.ItemStates, cooldowns);
                BeginReplicatingStatuses();
            }

            StatusesChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            activeStatuses.OnListChanged -= HandleStatusListChanged;
            EndReplicatingStatuses();
        }

        private void HandleStatusListChanged(NetworkListEvent<ActiveStatusEntry> changeEvent) => StatusesChanged?.Invoke();

        // ----- Status replication (server) -----

        private void BeginReplicatingStatuses()
        {
            EndReplicatingStatuses();
            statusEvents = GameplayServices.Current.Events;
            statusSubscriptions.Add(statusEvents.Subscribe<StatusAppliedEvent>(e => SyncStatus(e.Target, e.Group)));
            statusSubscriptions.Add(statusEvents.Subscribe<StatusStackAddedEvent>(e => SyncStatus(e.Target, e.Group)));
            statusSubscriptions.Add(statusEvents.Subscribe<StatusStackExpiredEvent>(e => SyncStatus(e.Target, e.Group)));
            statusSubscriptions.Add(statusEvents.Subscribe<StatusRefreshedEvent>(e => SyncStatus(e.Target, e.Group)));
            statusSubscriptions.Add(statusEvents.Subscribe<StatusRemovedEvent>(e => SyncStatus(e.Target, e.Group)));

            // Statuses already on the character when it spawned.
            foreach ((StatusDefinition _, StatusSnapshot snapshot) in AbilityServices.Current.Statuses.GetAllStatuses(character.Id))
            {
                SyncStatus(character.Id, snapshot.Group);
            }
        }

        private void EndReplicatingStatuses()
        {
            if (statusEvents != null)
            {
                foreach (SubscriptionHandle handle in statusSubscriptions) statusEvents.Unsubscribe(handle);
            }

            statusSubscriptions.Clear();
            statusEvents = null;
        }

        /// <summary>
        /// Makes the replicated entry for one status group match the status manager as it is now: added, updated or removed. It reads the
        /// current state rather than the event, so several events in a row always converge on the truth.
        /// </summary>
        private void SyncStatus(EntityId target, StatusInstanceId group)
        {
            if (!IsServer || !IsSpawned || target != character.Id)
            {
                return;
            }

            StatusSnapshot current = null;
            foreach ((StatusDefinition _, StatusSnapshot snapshot) in AbilityServices.Current.Statuses.GetAllStatuses(target))
            {
                if (snapshot.Group == group)
                {
                    current = snapshot;
                    break;
                }
            }

            // A status that is not in the registry has no short id to send, so it stays on the server.
            uint shortId = 0;
            if (current != null && (DefinitionRegistry.Instance == null || !DefinitionRegistry.Instance.TryGetShortId(current.Status, out shortId)))
            {
                current = null;
            }

            int index = IndexOfStatus(group.Value);
            if (current == null)
            {
                if (index >= 0) activeStatuses.RemoveAt(index);
                return;
            }

            ActiveStatusEntry entry = new ActiveStatusEntry
            {
                Group = group.Value,
                StatusShortId = shortId,
                Stacks = current.StackCount,
                ExpiryTick = current.SharedExpiryTick >= 0 ? current.SharedExpiryTick : LastStackExpiry(current),
            };

            if (index < 0) activeStatuses.Add(entry);
            else if (!activeStatuses[index].Equals(entry)) activeStatuses[index] = entry;
        }

        private static long LastStackExpiry(StatusSnapshot snapshot)
        {
            long last = 0;
            foreach (StackSnapshot stack in snapshot.Stacks)
            {
                if (stack.ExpiryTick > last) last = stack.ExpiryTick;
            }

            return last;
        }

        private int IndexOfStatus(uint group)
        {
            for (int i = 0; i < activeStatuses.Count; i++)
            {
                if (activeStatuses[i].Group == group) return i;
            }

            return -1;
        }


        /// <summary>
        /// Asks to use one of the abilities the item in a slot provides. On a client the request names the slot and the ability's position in the
        /// item's list and nothing else; the server finds the item itself, checks everything and spends the charges.
        /// </summary>
        /// <returns>False if the request could not be made at all.</returns>
        public bool RequestActivateItem(EquipmentSlot slot, int abilityIndex)
        {
            if (IsServer)
            {
                return TryActivateItemOnServer(slot, abilityIndex) == ItemCastResult.Cast;
            }

            if (!IsOwner)
            {
                return false;
            }

            ActivateItemServerRpc((int)slot, abilityIndex);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ActivateItemServerRpc(int slot, int abilityIndex)
        {
            // Only a slot and a position are taken from the client: not the item, not the cost, not the charges.
            if (System.Enum.IsDefined(typeof(EquipmentSlot), slot))
            {
                TryActivateItemOnServer((EquipmentSlot)slot, abilityIndex);
            }
        }

        /// <summary>
        /// Uses one of the abilities the item in a slot provides, if the character may. Server only.
        /// </summary>
        public ItemCastResult TryActivateItemOnServer(EquipmentSlot slot, int abilityIndex)
        {
            if (!IsServer || !IsSpawned || itemCaster == null)
            {
                return ItemCastResult.NotAuthority;
            }

            Equipment equipment = GetComponent<Equipment>();
            Item item = equipment != null ? equipment.Get(slot) : null;
            if (item == null)
            {
                return ItemCastResult.NoItem;
            }

            if (abilityIndex < 0 || abilityIndex >= item.GrantedAbilities.Count)
            {
                return ItemCastResult.NoSuchAbility;
            }

            Vector3 origin = transform.TransformPoint(muzzleOffset);
            Vector3 direction = transform.forward;
            ItemCastRequest request = new ItemCastRequest(
                character.Id,
                equipment.GetItemInstanceId(slot),
                item.GrantedAbilities[abilityIndex],
                character.Stats.Attributes,
                new Position3(origin.x, origin.y, origin.z),
                new Position3(direction.x, direction.y, direction.z));

            return itemCaster.Cast(request, character.IsAlive);
        }

        /// <inheritdoc/>
        public bool RequestActivate(AbilityDefinition ability, bool freshAllowed = true)
        {
            if (ability == null)
            {
                return false;
            }

            if (IsServer)
            {
                return TryActivateOnServer(ability);
            }

            if (!IsOwner || DefinitionRegistry.Instance == null || !DefinitionRegistry.Instance.TryGetShortId(ability.Id, out uint shortId))
            {
                return false;
            }

            // A melee press is shown at once, guessing what the server will do, and carries an id so the server answer can be matched to the guess.
            IMeleeSwingHost melee = ability.HasNode(NodeTypes.MeleeSwing) ? GetComponent<IMeleeSwingHost>() : null;
            int pressId = melee != null ? melee.BeginClientPress(freshAllowed) : 0;

            // The tick lets the server credit the press with the time it was made, not the later time it arrived.
            ActivateServerRpc(shortId, NetworkManager.NetworkTickSystem.ServerTime.Tick, pressId);
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ActivateServerRpc(uint shortId, long pressTick, int pressId)
        {
            // The client names an ability, when it pressed and which press it was; the server looks the ability up and decides everything else.
            if (DefinitionRegistry.Instance != null && DefinitionRegistry.Instance.TryGetByShortId(shortId, out GameplayDefinition definition) && definition is AbilityDefinition ability)
            {
                bool ran = Run(ability, pressTick, pressId);

                // The client may be showing a swing for a press that did nothing; tell it to take that back.
                if (!ran && pressId != 0 && ability.HasNode(NodeTypes.MeleeSwing))
                {
                    GetComponent<IMeleeSwingHost>()?.RejectClientPress(pressId);
                }
            }
        }

        /// <summary>
        /// Uses an ability for this character if it may. Server only.
        /// </summary>
        /// <returns>True if the ability ran.</returns>
        public bool TryActivateOnServer(AbilityDefinition ability)
        {
            return Run(ability, pressTick: -1, pressId: 0);
        }

        private bool Run(AbilityDefinition ability, long pressTick, int pressId)
        {
            if (!IsServer || !IsSpawned || cooldowns == null || ability == null)
            {
                return false;
            }

            if (!character.IsAlive)
            {
                return false;
            }

            if (ability.HasNode(NodeTypes.StatusTick))
            {
                Debug.LogWarning(ability.name + " starts from a Status Tick node, so only a status can run it.");
                return false;
            }

            if (!Contains(character.Loadout.Abilities, ability))
            {
                Debug.LogWarning(name + " asked to use " + ability.name + ", which it does not have.");
                return false;
            }

            CompileResult compiled = ability.Compile();
            if (!compiled.Success)
            {
                Debug.LogError(ability.name + " has errors and cannot run:\n" + compiled.Report);
                return false;
            }

            // A melee ability's press is judged by the character's melee mechanics: it may start a use (which costs the cooldown) or continue
            // one already paid for, such as the next swing of a combo. Other abilities are always a fresh use.
            IMeleeSwingHost melee = ability.HasNode(NodeTypes.MeleeSwing) ? GetComponent<IMeleeSwingHost>() : null;
            if (melee != null)
            {
                MeleePressOutcome press = melee.Press(pressTick, cooldowns.IsReady(ability.Id), pressId);
                if (press == MeleePressOutcome.Rejected)
                {
                    return false;
                }

                // The server is the authority on cooldowns; the client's own cooldown display is only a prediction.
                if (press == MeleePressOutcome.Fresh && !cooldowns.TryStart(ability.Id, ability.CooldownSeconds))
                {
                    melee.CancelPress();
                    return false;
                }
            }
            else if (!cooldowns.TryStart(ability.Id, ability.CooldownSeconds))
            {
                return false;
            }

            Vector3 origin = transform.TransformPoint(muzzleOffset);
            Vector3 direction = transform.forward;
            ExecutionRequest request = new ExecutionRequest(
                SourceHandle.FromEntity(character.Id),
                character.Id,
                new Position3(origin.x, origin.y, origin.z),
                new Position3(direction.x, direction.y, direction.z));

            ExecutionResult result = AbilityServices.Current.Executor.Execute(compiled.Ability, request);

            // If the ability never went on to start the swing it was promised, the promise is dropped.
            melee?.CancelPress();
            return result.Completed;
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<AbilityDefinition> abilities, AbilityDefinition ability)
        {
            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] == ability) return true;
            }

            return false;
        }
    }
}
