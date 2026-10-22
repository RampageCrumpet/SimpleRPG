using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Lets a character use graph-based abilities. A player's request arrives from their client and the server validates it: the character
    /// must be alive, must have the ability, and the ability's cooldown must have passed. The ability then runs on the server, so a client
    /// can only ask; it can never supply damage, targets or timing. Monsters and other server-side users call
    /// <see cref="TryActivateOnServer"/> directly.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public sealed class AbilityUser : NetworkBehaviour, IAbilityActivator
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

        private Character character;

        private CooldownTracker cooldowns;

        private ItemAbilityCaster itemCaster;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                cooldowns = new CooldownTracker(AbilityServices.Current.Clock, CooldownToleranceTicks);
                itemCaster = new ItemAbilityCaster(AbilityServices.Current.Executor, GameplayServices.Current.ItemStates, cooldowns);
            }
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
                character.Attributes,
                new Position3(origin.x, origin.y, origin.z),
                new Position3(direction.x, direction.y, direction.z));

            return itemCaster.Cast(request, character.IsAlive);
        }

        /// <inheritdoc/>
        public bool RequestActivate(AbilityDefinition ability)
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

            ActivateServerRpc(shortId);
            return true;
        }

        /// <inheritdoc/>
        public bool TryQueue(AbilityDefinition ability)
        {
            // Only melee abilities have a use that a later press can continue.
            if (ability == null || !ability.HasNode(NodeTypes.MeleeSwing))
            {
                return false;
            }

            IMeleeSwingHost melee = GetComponent<IMeleeSwingHost>();
            return melee != null && (IsServer || IsOwner) && melee.TryQueue(ability);
        }

        /// <inheritdoc/>
        public bool ExecuteQueued(AbilityDefinition ability)
        {
            return Run(ability, chargeCooldown: false);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ActivateServerRpc(uint shortId)
        {
            // The client names an ability; the server looks it up and decides everything else.
            if (DefinitionRegistry.Instance != null && DefinitionRegistry.Instance.TryGetByShortId(shortId, out GameplayDefinition definition) && definition is AbilityDefinition ability)
            {
                TryActivateOnServer(ability);
            }
        }

        /// <summary>
        /// Uses an ability for this character if it may. Server only.
        /// </summary>
        /// <returns>True if the ability ran.</returns>
        public bool TryActivateOnServer(AbilityDefinition ability)
        {
            return Run(ability, chargeCooldown: true);
        }

        private bool Run(AbilityDefinition ability, bool chargeCooldown)
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

            if (!Contains(character.GraphAbilities, ability))
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

            if (chargeCooldown)
            {
                // A new combo chain can't start while a queued one is still running.
                if (ability.HasNode(NodeTypes.MeleeSwing) && GetComponent<IMeleeSwingHost>() is IMeleeSwingHost melee && !melee.CanStartFreshChain)
                {
                    return false;
                }

                // The server is the authority on cooldowns; the client's own cooldown display is only a prediction.
                if (!cooldowns.TryStart(ability.Id, ability.CooldownSeconds))
                {
                    return false;
                }
            }

            Vector3 origin = transform.TransformPoint(muzzleOffset);
            Vector3 direction = transform.forward;
            ExecutionRequest request = new ExecutionRequest(
                SourceHandle.FromEntity(character.Id),
                character.Id,
                new Position3(origin.x, origin.y, origin.z),
                new Position3(direction.x, direction.y, direction.z));

            ExecutionResult result = AbilityServices.Current.Executor.Execute(compiled.Ability, request);
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
