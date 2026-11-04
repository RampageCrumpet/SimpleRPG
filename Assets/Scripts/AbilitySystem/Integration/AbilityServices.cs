using SimpleRPG.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// The server's ability machinery: the clock, the executor and the real projectile delivery, wired to the gameplay services.
    /// There is one per running game. It is rebuilt if the gameplay services or the network manager are replaced, as tests do.
    /// </summary>
    public sealed class AbilityServices
    {
        private static AbilityServices current;

        private readonly GameplayServices gameplay;

        private readonly NetworkManager manager;

        private AbilityServices(GameplayServices gameplay, NetworkManager manager)
        {
            this.gameplay = gameplay;
            this.manager = manager;

            Clock = new NetworkGameplayClock(manager);
            Execution = new ExecutionServices(gameplay.Attributes, gameplay.Combat, new ProjectileDelivery(), new MeleeDeliveryRouter());
            Execution.Diagnostic += message => Debug.LogWarning(message);
            // Rolls always come from the gameplay random stream as it is now, so replacing the stream (tests use a fixed seed) takes effect at once.
            IGameplayRandom random = new CurrentRandom(gameplay);
            Execution.Targets = new CombatantTargetQuery(gameplay.Combatants);
            Execution.Spawner = new EntitySpawner();
            Execution.Random = random;

            // Statuses and the executor refer to each other: abilities apply statuses, and statuses run abilities when they tick.
            Statuses = new StatusManager(Clock, gameplay.Events, gameplay.Combatants, gameplay.Attributes);
            Statuses.Diagnostic += message => Debug.LogWarning(message);
            Execution.Statuses = Statuses;
            Executor = new AbilityExecutor(Execution);
            Statuses.Executor = Executor;
            driver = StatusDriver.Create(Statuses);

            // Triggered effects start listening now. Worn items and applied statuses are the hosts that bring them; equipment that was already
            // worn is picked up.
            EquipmentEffectProvider items = new EquipmentEffectProvider();
            Effects = new TriggeredEffectEngine(gameplay.Events, Executor, random, gameplay.Combatants);
            Effects.Diagnostic += message => Debug.LogWarning(message);
            ItemHost = new ItemEffectHost(gameplay.Events, Effects, items);
            StatusHost = new StatusEffectHost(gameplay.Events, Effects, new RegistryStatusEffectProvider());

            // A death's reactions are chosen as the death is committed, before any cleanup removes the worn items that asked for them.
            gameplay.Combat.DeathSources = Effects;

            Grants = new GrantEngine(gameplay.Events, gameplay.Attributes, items);
            Grants.Diagnostic += message => Debug.LogWarning(message);
        }

        public static AbilityServices Current
        {
            get
            {
                // The manager is compared by reference: with the domain kept between plays, a destroyed manager from the last play can compare
                // equal to the new one by Unity's operator, which would leave a clock that reads a tick rate of zero.
                if (current == null || !ReferenceEquals(current.gameplay, GameplayServices.Current) || !ReferenceEquals(current.manager, NetworkManager.Singleton))
                {
                    current?.Shutdown();
                    current = new AbilityServices(GameplayServices.Current, NetworkManager.Singleton);
                }

                return current;
            }
        }

        private sealed class CurrentRandom : IGameplayRandom
        {
            private readonly GameplayServices gameplay;

            public CurrentRandom(GameplayServices gameplay) => this.gameplay = gameplay;

            public double NextDouble() => gameplay.Random.NextDouble();
        }

        public IGameplayClock Clock { get; }

        public ExecutionServices Execution { get; }

        public AbilityExecutor Executor { get; }

        /// <summary>
        /// Runs triggered effects, such as an on-hit spell, for whatever hosts them.
        /// </summary>
        public TriggeredEffectEngine Effects { get; }

        /// <summary>
        /// Brings a worn item's effects to the engine while it is equipped.
        /// </summary>
        public ItemEffectHost ItemHost { get; }

        /// <summary>
        /// Brings an applied status's effects to the engine while it lasts.
        /// </summary>
        public StatusEffectHost StatusHost { get; }

        /// <summary>
        /// Holds the statuses on every entity and advances them each frame.
        /// </summary>
        public StatusManager Statuses { get; }

        /// <summary>
        /// Applies the attribute grants of worn items.
        /// </summary>
        public GrantEngine Grants { get; }

        private readonly StatusDriver driver;

        private void Shutdown()
        {
            if (ReferenceEquals(gameplay.Combat.DeathSources, Effects))
            {
                gameplay.Combat.DeathSources = null;
            }

            ItemHost.Dispose();
            StatusHost.Dispose();
            Effects.Dispose();
            Grants.Dispose();
            Statuses.Dispose();
            if (driver != null) driver.Remove();
        }

        // Entering play with the domain kept (as this project does) leaves the last play's services behind, wired to a destroyed network manager.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlay()
        {
            Reset();
            GameplayServices.Reset();
        }

        /// <summary>
        /// Drops the current services so the next use builds new ones.
        /// </summary>
        public static void Reset()
        {
            current?.Shutdown();
            current = null;
        }
    }
}
