using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// A condition on an attribute that a user must meet to use an item's ability, such as Intelligence of at least 12. The attribute is read live,
    /// so a worn item that raises it can make the user eligible.
    /// </summary>
    [Serializable]
    public sealed class AttributeRequirement
    {
        [Tooltip("The attribute's stable key, for example simplerpg.attr.intelligence.")]
        public string AttributeKey = "";

        [Tooltip("The lowest value the user needs.")]
        public float Minimum;
    }

    /// <summary>
    /// An ability an item lets its wielder use, and what using it costs. The item does not need the ability to be known or prepared by the wielder:
    /// a staff can hold three Firebolt charges for someone who has never learned Firebolt.
    /// </summary>
    [Serializable]
    public sealed class ItemAbilityGrant
    {
        [Tooltip("The ability the item lets its wielder use.")]
        public AbilityDefinition Ability;

        [Min(0)]
        [Tooltip("Charges from the item's pool each use spends. Zero means the use is free.")]
        public int ChargeCost = 1;

        [Tooltip("What the wielder needs to be able to use it. Every requirement must be met.")]
        public List<AttributeRequirement> Requirements = new List<AttributeRequirement>();
    }

    /// <summary>
    /// A fixed attribute an item has of its own, independent of whoever carries it: a staff's own magic power.
    /// </summary>
    [Serializable]
    public sealed class AttributeValueSpec
    {
        public string AttributeKey = "";

        public float Value;
    }

    /// <summary>
    /// What came of trying to use an item's ability.
    /// </summary>
    public enum ItemCastResult : byte
    {
        /// <summary>The ability ran and the charges were spent.</summary>
        Cast = 0,

        /// <summary>Not on the server; only the server casts.</summary>
        NotAuthority = 1,

        /// <summary>The user is dead.</summary>
        UserDead = 2,

        /// <summary>The user has no such item equipped.</summary>
        NoItem = 3,

        /// <summary>The item has no such ability.</summary>
        NoSuchAbility = 4,

        /// <summary>The user does not meet a requirement.</summary>
        NotEligible = 5,

        /// <summary>The ability is still cooling down.</summary>
        OnCooldown = 6,

        /// <summary>The item has too few charges.</summary>
        NotEnoughCharges = 7,

        /// <summary>The ability has errors and cannot run. Nothing was spent.</summary>
        AbilityInvalid = 8,
    }

    /// <summary>
    /// A request to use one of an item's abilities.
    /// </summary>
    public readonly struct ItemCastRequest
    {
        public ItemCastRequest(EntityId caster, ItemInstanceId item, ItemAbilityGrant grant, IAttributeReader casterAttributes, Position3 origin, Position3 direction)
        {
            Caster = caster;
            Item = item;
            Grant = grant;
            CasterAttributes = casterAttributes;
            Origin = origin;
            Direction = direction;
        }

        public EntityId Caster { get; }

        public ItemInstanceId Item { get; }

        public ItemAbilityGrant Grant { get; }

        /// <summary>The user's attributes, read for the requirements. Null means they have none.</summary>
        public IAttributeReader CasterAttributes { get; }

        public Position3 Origin { get; }

        public Position3 Direction { get; }
    }

    /// <summary>
    /// Decides whether an item's ability may be used and, if so, spends the charges and runs it. One transaction: check everything, then spend
    /// once, then run, so a refused use costs nothing and an accepted one is never charged twice. Server only.
    /// </summary>
    public sealed class ItemAbilityCaster
    {
        private readonly AbilityExecutor executor;
        private readonly ItemStateStore states;
        private readonly CooldownTracker cooldowns;

        /// <param name="executor">Runs the ability.</param>
        /// <param name="states">Where the item's charges live.</param>
        /// <param name="cooldowns">The user's cooldowns. Cooldowns are per ability, so using the same ability from two items shares one.</param>
        public ItemAbilityCaster(AbilityExecutor executor, ItemStateStore states, CooldownTracker cooldowns)
        {
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.states = states ?? throw new ArgumentNullException(nameof(states));
            this.cooldowns = cooldowns ?? throw new ArgumentNullException(nameof(cooldowns));
        }

        /// <summary>
        /// Uses an item's ability if everything allows it.
        /// </summary>
        public ItemCastResult Cast(in ItemCastRequest request, bool casterAlive = true)
        {
            ItemAbilityGrant grant = request.Grant;
            if (grant == null || grant.Ability == null || !request.Item.IsValid)
            {
                return ItemCastResult.NoSuchAbility;
            }

            if (!casterAlive)
            {
                return ItemCastResult.UserDead;
            }

            AbilityDefinition ability = grant.Ability;
            CompileResult compiled = ability.Compile();
            if (!compiled.Success || ability.HasNode(NodeTypes.StatusTick))
            {
                return ItemCastResult.AbilityInvalid;
            }

            if (!MeetsRequirements(grant, request.CasterAttributes))
            {
                return ItemCastResult.NotEligible;
            }

            if (!cooldowns.IsReady(ability.Id))
            {
                return ItemCastResult.OnCooldown;
            }

            // Everything is checked before anything is spent, so a use that is refused costs nothing.
            if (grant.ChargeCost > 0 && !states.TrySpend(request.Item, ItemCharges.PoolId, grant.ChargeCost))
            {
                return ItemCastResult.NotEnoughCharges;
            }

            cooldowns.TryStart(ability.Id, ability.CooldownSeconds);

            executor.Execute(compiled.Ability, new ExecutionRequest(SourceHandle.FromItem(request.Item), request.Caster, request.Origin, request.Direction));
            return ItemCastResult.Cast;
        }

        private static bool MeetsRequirements(ItemAbilityGrant grant, IAttributeReader attributes)
        {
            foreach (AttributeRequirement requirement in grant.Requirements)
            {
                if (requirement == null || string.IsNullOrWhiteSpace(requirement.AttributeKey))
                {
                    continue;
                }

                double value = attributes != null ? attributes.Get(new AttributeId(requirement.AttributeKey)) : 0d;
                if (value < requirement.Minimum)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
