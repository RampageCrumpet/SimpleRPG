using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Runs triggered effects. Server only. The engine knows nothing about what brings an effect: a host (a worn item, a status a spell applied)
    /// activates its effects for an owner with <see cref="Activate"/> and removes them with <see cref="Deactivate"/>, and the engine reacts to
    /// damage and deaths. A trigger only looks at the effects of the entity it happened to, never at every item in the world.
    /// </summary>
    public sealed class TriggeredEffectEngine : IDisposable, IDeathSourceSnapshotter
    {
        private sealed class ActiveEffect
        {
            public TriggeredEffect Effect;
            public SourceHandle Host;
        }

        private readonly GameplayEventDispatcher events;
        private readonly AbilityExecutor executor;
        private readonly IGameplayRandom random;
        private readonly ICombatantRegistry combatants;
        private readonly Dictionary<EntityId, List<ActiveEffect>> byOwner = new Dictionary<EntityId, List<ActiveEffect>>();
        private readonly List<SubscriptionHandle> subscriptions = new List<SubscriptionHandle>();
        private readonly HashSet<TriggeredEffect> reported = new HashSet<TriggeredEffect>();
        private readonly Dictionary<EntityId, List<ActiveEffect>> captured = new Dictionary<EntityId, List<ActiveEffect>>();
        private readonly HashSet<DeathTransitionId> reactedDeaths = new HashSet<DeathTransitionId>();

        public TriggeredEffectEngine(GameplayEventDispatcher events, AbilityExecutor executor, IGameplayRandom random, ICombatantRegistry combatants)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));

            subscriptions.Add(events.Subscribe<EntityRemovedEvent>(OnEntityRemoved));
            subscriptions.Add(events.Subscribe<DamageResolvedEvent>(OnDamageResolved));
            subscriptions.Add(events.Subscribe<EntityDiedEvent>(OnEntityDied));
            subscriptions.Add(events.Subscribe<DeathFinalizeEvent>(OnDeathFinalize));
        }

        /// <summary>
        /// Raised with a description when an effect cannot run, for example its ability has errors.
        /// </summary>
        public event Action<string> Diagnostic;

        /// <summary>
        /// The number of effects currently active for an entity.
        /// </summary>
        public int ActiveEffectCount(EntityId owner) => byOwner.TryGetValue(owner, out List<ActiveEffect> effects) ? effects.Count : 0;

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

        // ----- Hosts -----

        /// <summary>
        /// Makes a host's effects active for an owner. The same host is never active twice for an owner, however it arrives, and an empty entry is skipped.
        /// </summary>
        public void Activate(EntityId owner, SourceHandle host, IReadOnlyList<TriggeredEffect> effects)
        {
            if (effects == null)
            {
                return;
            }

            foreach (TriggeredEffect effect in effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (!byOwner.TryGetValue(owner, out List<ActiveEffect> active))
                {
                    active = new List<ActiveEffect>();
                    byOwner.Add(owner, active);
                }

                if (!active.Exists(x => x.Host == host && x.Effect == effect))
                {
                    active.Add(new ActiveEffect { Effect = effect, Host = host });
                }
            }
        }

        /// <summary>
        /// Removes every effect a host brought to an owner.
        /// </summary>
        public void Deactivate(EntityId owner, SourceHandle host)
        {
            if (!byOwner.TryGetValue(owner, out List<ActiveEffect> active))
            {
                return;
            }

            active.RemoveAll(x => x.Host == host);
            if (active.Count == 0)
            {
                byOwner.Remove(owner);
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

        // ----- Damage -----

        private void OnDamageResolved(DamageResolvedEvent e)
        {
            DamageRequest request = e.Request;
            int dealt = Math.Max(0, e.HealthBefore - e.HealthAfter);

            // Only a direct weapon blow is a hit, so what an effect itself causes (a proc, a bleed) cannot trigger a hit effect again.
            if (request.Instigator.IsValid && (request.Provenance & DamageProvenance.DirectWeaponHit) != 0)
            {
                Dispatch(new TriggerContext(EffectTrigger.OnHit, request.Instigator, request.Target, request.Source, request.BodyLocation, dealt, e.Fatal, default));
            }

            if (request.Target.IsValid)
            {
                Dispatch(new TriggerContext(EffectTrigger.OnTakeDamage, request.Target, request.Instigator, request.Source, request.BodyLocation, dealt, e.Fatal, default));
            }
        }

        // Walks the owner's effects from a copy, because an effect's ability may change what the owner has on.
        private void Dispatch(in TriggerContext context)
        {
            if (!byOwner.TryGetValue(context.Owner, out List<ActiveEffect> active))
            {
                return;
            }

            foreach (ActiveEffect entry in active.ToArray())
            {
                if (!entry.Effect.Matches(context, entry.Host))
                {
                    continue;
                }

                TriggerContext triggered = context;
                Run(entry.Effect, () => BuildRequest(entry, triggered));
            }
        }

        /// <summary>
        /// Rolls an effect's chance and, if it succeeds, runs its ability. The request is only built if the effect actually runs.
        /// </summary>
        private void Run(TriggeredEffect effect, Func<ExecutionRequest> buildRequest)
        {
            // One roll per effect per trigger, and only when the outcome is in doubt, so a certain effect never changes what later effects roll.
            if (effect.Chance <= 0f)
            {
                return;
            }

            if (effect.Chance < 1f && random.NextDouble() >= effect.Chance)
            {
                return;
            }

            if (effect.Ability == null)
            {
                Report(effect, effect.name + " has no ability to run.");
                return;
            }

            CompileResult compiled = effect.Ability.Compile();
            if (!compiled.Success)
            {
                Report(effect, effect.name + "'s ability " + effect.Ability.name + " has errors and cannot run:\n" + compiled.Report);
                return;
            }

            executor.Execute(compiled.Ability, buildRequest());
        }

        /// <summary>
        /// Aims the ability from the owner at the other party: it starts a little in front of the owner, at chest height, and is told who the
        /// other party is, where they were struck and who it is aimed at.
        /// </summary>
        private ExecutionRequest BuildRequest(ActiveEffect active, in TriggerContext context)
        {
            Position3 origin = default;
            Position3 direction = new Position3(0f, 0f, 1f);

            if (combatants.TryGet(context.Owner, out ICombatant owner) && combatants.TryGet(context.Other, out ICombatant other))
            {
                Position3 from = owner.Position;
                Position3 to = other.Position;
                float dx = to.X - from.X;
                float dz = to.Z - from.Z;
                float length = (float)Math.Sqrt(dx * dx + dz * dz);
                float ux = length > 0.0001f ? dx / length : 0f;
                float uz = length > 0.0001f ? dz / length : 1f;

                origin = new Position3(from.X + ux * 0.8f, from.Y + 1.2f, from.Z + uz * 0.8f);
                direction = new Position3(to.X - origin.X, to.Y + 1f - origin.Y, to.Z - origin.Z);
            }

            EntityId target = active.Effect.Target == EffectTarget.Other ? context.Other : context.Owner;
            int bodyLocation = context.BodyLocation >= 0 ? context.BodyLocation : 0;
            return new ExecutionRequest(active.Host, context.Owner, origin, direction, default, context.Other, bodyLocation, target);
        }

        // ----- Death -----

        /// <summary>
        /// Called as a death is committed, before anything is cleaned up: records which of the victim's hosts have an effect for their owner's
        /// death, so those effects still run when the death is announced even if the hosts are removed in the meantime.
        /// </summary>
        public void Capture(EntityId victim, List<SourceHandle> sources)
        {
            if (!byOwner.TryGetValue(victim, out List<ActiveEffect> active))
            {
                return;
            }

            List<ActiveEffect> qualifying = active.FindAll(x => x.Effect.Trigger == EffectTrigger.OnOwnerDeath);
            if (qualifying.Count == 0)
            {
                return;
            }

            captured[victim] = qualifying;
            foreach (ActiveEffect effect in qualifying)
            {
                if (!sources.Contains(effect.Host)) sources.Add(effect.Host);
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

            if (context.Killer.IsValid)
            {
                Dispatch(new TriggerContext(EffectTrigger.OnKill, context.Killer, context.Victim, context.KillingSource, TriggerContext.NoBodyLocation, 0, true, context.Position));
            }

            List<ActiveEffect> effects;
            if (captured.Remove(context.Victim, out List<ActiveEffect> snapshot))
            {
                effects = snapshot;
            }
            else if (byOwner.TryGetValue(context.Victim, out List<ActiveEffect> active))
            {
                // No snapshot was taken (nothing asked the engine to capture), so use what is active now.
                effects = active.FindAll(x => x.Effect.Trigger == EffectTrigger.OnOwnerDeath);
            }
            else
            {
                return;
            }

            foreach (ActiveEffect entry in effects)
            {
                ActiveEffect effect = entry;
                Run(effect.Effect, () => new ExecutionRequest(effect.Host, context.Victim, context.Position, default));
            }
        }

        private void Report(TriggeredEffect effect, string message)
        {
            // A broken effect would otherwise repeat its message on every trigger.
            if (reported.Add(effect))
            {
                Diagnostic?.Invoke(message);
            }
        }
    }
}
