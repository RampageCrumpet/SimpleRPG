using System;
using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// What a fatal-damage interceptor decides.
    /// </summary>
    public enum FatalDamageDecision
    {
        /// <summary>Let the damage kill.</summary>
        Proceed = 0,

        /// <summary>Stop the death: the target survives with 1 health.</summary>
        Prevent = 1,
    }

    /// <summary>
    /// The reserved pre-death step. An interceptor sees damage that would kill and may prevent the death. Nothing implements this yet; the step
    /// exists so death-prevention mechanics have a defined place in the sequence and need no change to it.
    /// </summary>
    public interface IFatalDamageInterceptor
    {
        FatalDamageDecision Intercept(ICombatant target, in DamageRequest request);
    }

    /// <summary>
    /// Captures which sources have an on-death effect for a victim. Called as the death is committed, before any cleanup removes them.
    /// </summary>
    public interface IDeathSourceSnapshotter
    {
        void Capture(EntityId victim, List<SourceHandle> sources);
    }

    /// <summary>
    /// What happened to a damage request.
    /// </summary>
    public enum DamageResult
    {
        /// <summary>Damage was applied and the target lives.</summary>
        Applied,

        /// <summary>Damage was applied and the target died.</summary>
        Killed,

        /// <summary>The damage would have killed but an interceptor left the target at 1 health.</summary>
        Prevented,

        RejectedUnknownTarget,

        RejectedTargetDead,

        RejectedNoDamage,

        /// <summary>The request was caused by too long a chain of events and was refused.</summary>
        RejectedTooDeep,
    }

    /// <summary>
    /// The result of <see cref="CombatResolver.Apply"/>.
    /// </summary>
    public readonly struct DamageOutcome
    {
        public DamageOutcome(DamageResult result, int healthBefore, int healthAfter, DeathContext death = null)
        {
            Result = result;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
            Death = death;
        }

        public DamageResult Result { get; }

        public int HealthBefore { get; }

        public int HealthAfter { get; }

        /// <summary>The death this damage caused, if it killed.</summary>
        public DeathContext Death { get; }

        public bool WasApplied => Result == DamageResult.Applied || Result == DamageResult.Killed || Result == DamageResult.Prevented;
    }

    /// <summary>
    /// The one path damage takes. Server only. A lethal hit commits a single death, captures its context, then queues
    /// <see cref="DamageResolvedEvent"/>, <see cref="EntityDiedEvent"/> and <see cref="DeathFinalizeEvent"/> in that order.
    /// </summary>
    public sealed class CombatResolver
    {
        private readonly GameplayEventDispatcher events;

        private readonly ICombatantRegistry combatants;

        private readonly List<IFatalDamageInterceptor> interceptors = new List<IFatalDamageInterceptor>();

        public CombatResolver(GameplayEventDispatcher events, ICombatantRegistry combatants)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        }

        /// <summary>
        /// Supplies the on-death sources captured with each death. Without one, deaths capture none.
        /// </summary>
        public IDeathSourceSnapshotter DeathSources { get; set; }

        /// <summary>
        /// Adds a fatal-damage interceptor. They run in the order added and the first to prevent wins.
        /// </summary>
        public void AddFatalInterceptor(IFatalDamageInterceptor interceptor)
        {
            interceptors.Add(interceptor ?? throw new ArgumentNullException(nameof(interceptor)));
        }

        /// <summary>
        /// Applies damage. When called outside event delivery, every event it causes has been delivered by the time it returns;
        /// when called from an event handler, the events wait their turn in the queue.
        /// </summary>
        public DamageOutcome Apply(in DamageRequest incoming)
        {
            if (!combatants.TryGet(incoming.Target, out ICombatant target))
            {
                return new DamageOutcome(DamageResult.RejectedUnknownTarget, 0, 0);
            }

            int before = target.Health;

            if (!target.IsAlive)
            {
                return new DamageOutcome(DamageResult.RejectedTargetDead, before, before);
            }

            if (incoming.Amount <= 0)
            {
                return new DamageOutcome(DamageResult.RejectedNoDamage, before, before);
            }

            // The one place a struck body region changes the damage. Everything after this, including the events, sees the final amount.
            DamageRequest request = AdjustForRegion(target, incoming);
            if (request.Amount <= 0)
            {
                return new DamageOutcome(DamageResult.RejectedNoDamage, before, before);
            }

            if (events.NextEventDepth > events.MaxDepth)
            {
                events.Report("Damage of " + request.Amount + " to " + request.Target + " was dropped: its cause chain is " + events.NextEventDepth + " events deep, the limit is " + events.MaxDepth + ".");
                return new DamageOutcome(DamageResult.RejectedTooDeep, before, before);
            }

            bool lethal = request.Amount >= before;
            if (lethal && Intercept(target, request) == FatalDamageDecision.Prevent)
            {
                target.CommitHealth(1);
                events.Enqueue(new DamageResolvedEvent(request, before, 1, false));
                DrainIfOutermost();
                return new DamageOutcome(DamageResult.Prevented, before, 1);
            }

            DamageOutcome outcome;
            if (lethal)
            {
                outcome = Kill(target, request, before);
            }
            else
            {
                int after = before - request.Amount;
                target.CommitHealth(after);
                events.Enqueue(new DamageResolvedEvent(request, before, after, false));
                outcome = new DamageOutcome(DamageResult.Applied, before, after);
            }

            DrainIfOutermost();
            return outcome;
        }

        /// <summary>
        /// Starts a new life for a dead combatant. The next death is a new transition and raises its events again.
        /// </summary>
        /// <returns>False if the entity is unknown or still alive.</returns>
        public bool Revive(EntityId id, int health)
        {
            if (health <= 0) throw new ArgumentOutOfRangeException(nameof(health), "A revived combatant needs positive health.");

            if (!combatants.TryGet(id, out ICombatant target) || target.IsAlive)
            {
                return false;
            }

            target.CommitRevive(health);
            return true;
        }

        private DamageOutcome Kill(ICombatant target, in DamageRequest request, int before)
        {
            // The life that ends here names the death.
            DeathTransitionId transition = new DeathTransitionId(target.Id, target.LifeId);

            target.CommitHealth(0);
            target.CommitDeath();

            List<SourceHandle> sources = new List<SourceHandle>();
            DeathSources?.Capture(target.Id, sources);

            DeathContext context = new DeathContext(
                transition,
                request.Instigator,
                request.Source,
                request.Provenance,
                target.Position,
                sources.AsReadOnly());

            events.Enqueue(new DamageResolvedEvent(request, before, 0, true));
            events.Enqueue(new EntityDiedEvent(context));
            events.Enqueue(new DeathFinalizeEvent(context));

            return new DamageOutcome(DamageResult.Killed, before, 0, context);
        }

        /// <summary>
        /// Scales a weapon blow by the factor its target gives the struck region. A spell, tick or hazard strikes no region and is untouched.
        /// A blow that lands always does at least 1, so a low multiplier cannot turn a hit into a miss. Armour mitigation, when it exists, belongs after this.
        /// </summary>
        private static DamageRequest AdjustForRegion(ICombatant target, in DamageRequest request)
        {
            if ((request.Provenance & DamageProvenance.DirectWeaponHit) == 0 || request.BodyLocation == DamageRequest.NoBodyLocation)
            {
                return request;
            }

            if (!(target is IRegionDamageModifier anatomy))
            {
                return request;
            }

            float multiplier = anatomy.DamageMultiplier(request.BodyLocation);
            if (multiplier == 1f || float.IsNaN(multiplier) || multiplier < 0f)
            {
                return request;
            }

            int scaled = (int)Math.Round(request.Amount * (double)multiplier, MidpointRounding.AwayFromZero);
            return request.WithAmount(multiplier > 0f ? Math.Max(1, scaled) : 0);
        }

        private FatalDamageDecision Intercept(ICombatant target, in DamageRequest request)
        {
            foreach (IFatalDamageInterceptor interceptor in interceptors)
            {
                if (interceptor.Intercept(target, request) == FatalDamageDecision.Prevent)
                {
                    return FatalDamageDecision.Prevent;
                }
            }

            return FatalDamageDecision.Proceed;
        }

        private void DrainIfOutermost()
        {
            if (!events.IsDraining)
            {
                events.Drain();
            }
        }
    }
}
