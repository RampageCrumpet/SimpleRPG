using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Base of everything the <see cref="GameplayEventDispatcher"/> delivers.
    /// </summary>
    public abstract class GameplayEvent
    {
        /// <summary>
        /// Position in the order events were queued, assigned by the dispatcher.
        /// </summary>
        public long Sequence { get; internal set; }

        /// <summary>
        /// How many events deep this one was caused: zero for an event raised outside any handler, one more than the event whose handler raised it otherwise.
        /// </summary>
        public int Depth { get; internal set; }

        /// <summary>
        /// True for an event that must be delivered even when the dispatcher is over budget, because skipping it would leave state behind
        /// (cleanup after a death). Most events can be dropped.
        /// </summary>
        public virtual bool MustDeliver => false;
    }

    /// <summary>
    /// Damage was applied (or reduced a combatant to zero).
    /// </summary>
    public sealed class DamageResolvedEvent : GameplayEvent
    {
        public DamageResolvedEvent(DamageRequest request, int healthBefore, int healthAfter, bool fatal)
        {
            Request = request;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
            Fatal = fatal;
        }

        public DamageRequest Request { get; }

        public int HealthBefore { get; }

        public int HealthAfter { get; }

        /// <summary>True if this damage committed a death.</summary>
        public bool Fatal { get; }
    }

    /// <summary>
    /// What was true at the moment of death. Reactions use this, not the victim's later state, so they stay valid while cleanup removes equipment, grants and statuses.
    /// </summary>
    public sealed class DeathContext
    {
        public DeathContext(
            DeathTransitionId transition,
            EntityId killer,
            SourceHandle killingSource,
            DamageProvenance killingProvenance,
            Position3 position,
            IReadOnlyList<SourceHandle> qualifyingSources)
        {
            Transition = transition;
            Killer = killer;
            KillingSource = killingSource;
            KillingProvenance = killingProvenance;
            Position = position;
            QualifyingSources = qualifyingSources;
        }

        /// <summary>This death. It is unique per life, so it identifies a death even if the victim is revived and dies again.</summary>
        public DeathTransitionId Transition { get; }

        public EntityId Victim => Transition.Entity;

        /// <summary>The corpse is the victim's own entity, which stays in the world; there is no separate corpse object yet.</summary>
        public EntityId Corpse => Transition.Entity;

        /// <summary>The instigator of the fatal damage, if there was one.</summary>
        public EntityId Killer { get; }

        public SourceHandle KillingSource { get; }

        public DamageProvenance KillingProvenance { get; }

        /// <summary>Where the victim died.</summary>
        public Position3 Position { get; }

        /// <summary>The sources (equipped items and so on) that had an on-death effect at the moment of death.</summary>
        public IReadOnlyList<SourceHandle> QualifyingSources { get; }
    }

    /// <summary>
    /// A death was committed. Raised exactly once per death.
    /// </summary>
    public sealed class EntityDiedEvent : GameplayEvent
    {
        public EntityDiedEvent(DeathContext context)
        {
            Context = context;
        }

        public DeathContext Context { get; }
    }

    /// <summary>
    /// An entity left the game (it despawned), so rules and state tied to it can be dropped.
    /// </summary>
    public sealed class EntityRemovedEvent : GameplayEvent
    {
        public EntityRemovedEvent(EntityId entity)
        {
            Entity = entity;
        }

        public EntityId Entity { get; }
    }

    /// <summary>
    /// Raised after every <see cref="EntityDiedEvent"/> reaction for a death has run, so cleanup (releasing grants, ending statuses) happens after them.
    /// </summary>
    public sealed class DeathFinalizeEvent : GameplayEvent
    {
        public DeathFinalizeEvent(DeathContext context)
        {
            Context = context;
        }

        /// <inheritdoc/>
        public override bool MustDeliver => true;

        public DeathContext Context { get; }
    }
}
