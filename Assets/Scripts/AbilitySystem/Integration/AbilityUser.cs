using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using SimpleRPG.UI;
using System.Collections.Generic;
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

        private Character character;

        private CooldownTracker cooldowns;

        private ItemAbilityCaster itemCaster;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        /// <summary>
        /// Lists the statuses on this character for the buff bar. Only the server holds statuses, so elsewhere the list is empty.
        /// </summary>
        public void GetBuffs(List<BuffEntry> into)
        {
            into.Clear();
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            foreach ((StatusDefinition definition, StatusSnapshot snapshot) in AbilityServices.Current.Statuses.GetAllStatuses(character.Id))
            {
                into.Add(new BuffEntry(definition.StatusName, definition.Icon, snapshot.StackCount));
            }
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
