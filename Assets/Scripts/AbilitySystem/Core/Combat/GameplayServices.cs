namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The server's gameplay services: the event dispatcher, the combatant registry and the combat resolver.
    /// Gameplay runs only on the authoritative server, so there is one set per running game. Tests replace it with <see cref="Reset"/>.
    /// </summary>
    public sealed class GameplayServices
    {
        private static GameplayServices current;

        public GameplayServices()
        {
            Events = new GameplayEventDispatcher();
            Combatants = new CombatantRegistry();
            Combat = new CombatResolver(Events, Combatants);
            ItemStates = new ItemStateStore();
            Attributes = new AttributeSetRegistry();
            Random = new SeededRandom(System.Environment.TickCount);
        }

        /// <summary>
        /// The random stream gameplay rules roll from. Tests replace it with a fixed seed.
        /// </summary>
        public IGameplayRandom Random { get; set; }

        /// <summary>
        /// The services in use. Created on first use.
        /// </summary>
        public static GameplayServices Current => current ??= new GameplayServices();

        public GameplayEventDispatcher Events { get; }

        public CombatantRegistry Combatants { get; }

        public CombatResolver Combat { get; }

        /// <summary>
        /// The state of item instances, such as charges.
        /// </summary>
        public ItemStateStore ItemStates { get; }

        /// <summary>
        /// The attributes of entities, items and world objects, which ability bindings read.
        /// </summary>
        public AttributeSetRegistry Attributes { get; }

        /// <summary>
        /// Replaces the current services with a fresh set, dropping all subscriptions and combatants.
        /// </summary>
        public static GameplayServices Reset()
        {
            current = new GameplayServices();
            return current;
        }
    }
}
