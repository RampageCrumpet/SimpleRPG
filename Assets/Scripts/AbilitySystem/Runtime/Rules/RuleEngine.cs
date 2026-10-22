using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Tells the rule engine which rules an item brings. The game integration implements it over its items.
    /// </summary>
    public interface IItemRuleProvider
    {
        /// <summary>
        /// Gets the rules of an equipped item.
        /// </summary>
        /// <returns>False if the item is unknown or has none.</returns>
        bool TryGetRules(EntityId wearer, ItemInstanceId item, out IReadOnlyList<GameplayRuleDefinition> rules);

        /// <summary>
        /// Every item that is equipped right now, so a rule engine created late can pick up equipment that was put on before it existed.
        /// </summary>
        IEnumerable<KeyValuePair<EntityId, ItemInstanceId>> EnumerateEquipped();
    }

    /// <summary>
    /// Runs item rules. Server only. A rule is subscribed when its item is equipped and dropped when the item is taken off or its wearer
    /// leaves, and a trigger only looks at the rules of the entity it happened to, never at every item in the world.
    /// </summary>
    public sealed class RuleEngine : IDisposable, IDeathSourceSnapshotter
    {
        private sealed class ActiveRule
        {
            public GameplayRuleDefinition Rule;
            public ItemInstanceId Item;
        }

        private readonly GameplayEventDispatcher events;
        private readonly AbilityExecutor executor;
        private readonly IGameplayRandom random;
        private readonly ICombatantRegistry combatants;
        private readonly IItemRuleProvider provider;
        private readonly Dictionary<EntityId, List<ActiveRule>> byOwner = new Dictionary<EntityId, List<ActiveRule>>();
        private readonly List<SubscriptionHandle> subscriptions = new List<SubscriptionHandle>();
        private readonly HashSet<GameplayRuleDefinition> reported = new HashSet<GameplayRuleDefinition>();
        private readonly Dictionary<EntityId, List<ActiveRule>> captured = new Dictionary<EntityId, List<ActiveRule>>();
        private readonly HashSet<DeathTransitionId> reactedDeaths = new HashSet<DeathTransitionId>();

        public RuleEngine(GameplayEventDispatcher events, AbilityExecutor executor, IGameplayRandom random, ICombatantRegistry combatants, IItemRuleProvider provider)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

            subscriptions.Add(events.Subscribe<ItemEquippedEvent>(OnItemEquipped));
            subscriptions.Add(events.Subscribe<ItemUnequippedEvent>(OnItemUnequipped));
            subscriptions.Add(events.Subscribe<EntityRemovedEvent>(OnEntityRemoved));
            subscriptions.Add(events.Subscribe<WeaponHitConfirmedEvent>(OnWeaponHitConfirmed));
            subscriptions.Add(events.Subscribe<EntityDiedEvent>(OnEntityDied));
            subscriptions.Add(events.Subscribe<DeathFinalizeEvent>(OnDeathFinalize));

            // Equipment put on before this engine existed still counts.
            foreach (KeyValuePair<EntityId, ItemInstanceId> equipped in provider.EnumerateEquipped())
            {
                Activate(equipped.Key, equipped.Value);
            }
        }

        /// <summary>
        /// Raised with a description when a rule cannot run, for example its ability has errors.
        /// </summary>
        public event Action<string> Diagnostic;

        /// <summary>
        /// The number of rules currently subscribed for an entity.
        /// </summary>
        public int ActiveRuleCount(EntityId owner) => byOwner.TryGetValue(owner, out List<ActiveRule> rules) ? rules.Count : 0;

        public void Dispose()
        {
            foreach (SubscriptionHandle handle in subscriptions)
            {
                events.Unsubscribe(handle);
            }

            subscriptions.Clear();
            byOwner.Clear();
            captured.Clear();
            reactedDeaths.Clear();
        }

        private void OnItemEquipped(ItemEquippedEvent e) => Activate(e.Wearer, e.Item);

        private void Activate(EntityId wearer, ItemInstanceId item)
        {
            if (!provider.TryGetRules(wearer, item, out IReadOnlyList<GameplayRuleDefinition> rules))
            {
                return;
            }

            foreach (GameplayRuleDefinition rule in rules)
            {
                if (rule == null)
                {
                    continue;
                }

                if (!byOwner.TryGetValue(wearer, out List<ActiveRule> active))
                {
                    active = new List<ActiveRule>();
                    byOwner.Add(wearer, active);
                }

                // The same item is never subscribed twice, however it arrives.
                if (!active.Exists(x => x.Item == item && x.Rule == rule))
                {
                    active.Add(new ActiveRule { Rule = rule, Item = item });
                }
            }
        }

        private void OnItemUnequipped(ItemUnequippedEvent e)
        {
            if (!byOwner.TryGetValue(e.Wearer, out List<ActiveRule> active))
            {
                return;
            }

            active.RemoveAll(x => x.Item == e.Item);
            if (active.Count == 0)
            {
                byOwner.Remove(e.Wearer);
            }
        }

        private void OnEntityRemoved(EntityRemovedEvent e)
        {
            byOwner.Remove(e.Entity);

            // The entity is gone for good, so its death records can no longer be announced again.
            reactedDeaths.RemoveWhere(transition => transition.Entity == e.Entity);
        }

        // A snapshot taken for a death that was never reacted to (its event was dropped) would otherwise wait forever.
        private void OnDeathFinalize(DeathFinalizeEvent e) => captured.Remove(e.Context.Victim);

        private void OnWeaponHitConfirmed(WeaponHitConfirmedEvent e)
        {
            if (!byOwner.TryGetValue(e.Attacker, out List<ActiveRule> active))
            {
                return;
            }

            // A rule's ability may change this entity's equipment, so the rules are walked from a copy.
            foreach (ActiveRule entry in active.ToArray())
            {
                Fire(entry, e);
            }
        }

        private void Fire(ActiveRule active, WeaponHitConfirmedEvent e)
        {
            GameplayRuleDefinition rule = active.Rule;
            if (rule.Trigger != RuleTrigger.WeaponHitConfirmed)
            {
                return;
            }

            // Only the kinds of damage the rule names can trigger it, so what a rule causes cannot trigger it again.
            if ((e.Provenance & rule.RequiredProvenance) != rule.RequiredProvenance)
            {
                return;
            }

            Run(rule, () => BuildRequest(active.Item, e));
        }

        /// <summary>
        /// Rolls a rule's chance and, if it succeeds, runs its ability. The request is only built if the rule actually runs.
        /// </summary>
        private void Run(GameplayRuleDefinition rule, Func<ExecutionRequest> buildRequest)
        {
            // One roll per rule per trigger, and only when the outcome is in doubt, so a certain rule never changes what later rules roll.
            if (rule.Chance <= 0f)
            {
                return;
            }

            if (rule.Chance < 1f && random.NextDouble() >= rule.Chance)
            {
                return;
            }

            if (rule.Ability == null)
            {
                Report(rule, rule.name + " has no ability to run.");
                return;
            }

            CompileResult compiled = rule.Ability.Compile();
            if (!compiled.Success)
            {
                Report(rule, rule.name + "'s ability " + rule.Ability.name + " has errors and cannot run:\n" + compiled.Report);
                return;
            }

            executor.Execute(compiled.Ability, buildRequest());
        }

        // ----- Death -----

        /// <summary>
        /// Called as a death is committed, before anything is cleaned up: records which of the victim's worn items have a rule for their wearer's
        /// death, so those rules still run when the death is announced even if the items are taken off in the meantime.
        /// </summary>
        public void Capture(EntityId victim, List<SourceHandle> sources)
        {
            if (!byOwner.TryGetValue(victim, out List<ActiveRule> active))
            {
                return;
            }

            List<ActiveRule> qualifying = active.FindAll(x => x.Rule.Trigger == RuleTrigger.OwnerDeath);
            if (qualifying.Count == 0)
            {
                return;
            }

            captured[victim] = qualifying;
            foreach (ActiveRule rule in qualifying)
            {
                SourceHandle source = SourceHandle.FromItem(rule.Item);
                if (!sources.Contains(source)) sources.Add(source);
            }
        }

        private void OnEntityDied(EntityDiedEvent e)
        {
            DeathContext context = e.Context;

            // A death is reacted to once, however many times it is announced.
            if (!reactedDeaths.Add(context.Transition))
            {
                return;
            }

            List<ActiveRule> rules;
            if (captured.Remove(context.Victim, out List<ActiveRule> snapshot))
            {
                rules = snapshot;
            }
            else if (byOwner.TryGetValue(context.Victim, out List<ActiveRule> active))
            {
                // No snapshot was taken (nothing asked the engine to capture), so use what is subscribed now.
                rules = active.FindAll(x => x.Rule.Trigger == RuleTrigger.OwnerDeath);
            }
            else
            {
                return;
            }

            foreach (ActiveRule entry in rules)
            {
                ActiveRule rule = entry;
                Run(rule.Rule, () => new ExecutionRequest(SourceHandle.FromItem(rule.Item), context.Victim, context.Position, default));
            }
        }

        /// <summary>
        /// Aims the ability from the wielder at the target they hit: it starts a little in front of the wielder, at chest height.
        /// </summary>
        private ExecutionRequest BuildRequest(ItemInstanceId item, WeaponHitConfirmedEvent e)
        {
            Position3 origin = default;
            Position3 direction = new Position3(0f, 0f, 1f);

            if (combatants.TryGet(e.Attacker, out ICombatant attacker) && combatants.TryGet(e.Victim, out ICombatant victim))
            {
                Position3 from = attacker.Position;
                Position3 to = victim.Position;
                float dx = to.X - from.X;
                float dz = to.Z - from.Z;
                float length = (float)Math.Sqrt(dx * dx + dz * dz);
                float ux = length > 0.0001f ? dx / length : 0f;
                float uz = length > 0.0001f ? dz / length : 1f;

                origin = new Position3(from.X + ux * 0.8f, from.Y + 1.2f, from.Z + uz * 0.8f);
                direction = new Position3(to.X - origin.X, to.Y + 1f - origin.Y, to.Z - origin.Z);
            }

            return new ExecutionRequest(SourceHandle.FromItem(item), e.Attacker, origin, direction);
        }

        private void Report(GameplayRuleDefinition rule, string message)
        {
            // A broken rule would otherwise repeat its message on every hit.
            if (reported.Add(rule))
            {
                Diagnostic?.Invoke(message);
            }
        }
    }
}
