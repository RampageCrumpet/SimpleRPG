using SimpleRPG.Gameplay;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// Presents the seeded gameplay stream as a <see cref="System.Random"/> so loot tables roll from it, never from <c>UnityEngine.Random</c>.
    /// Loot tables only call <see cref="NextDouble"/>.
    /// </summary>
    public sealed class GameplayRandomSource : System.Random
    {
        private readonly IGameplayRandom stream;

        public GameplayRandomSource(IGameplayRandom stream)
        {
            this.stream = stream;
        }

        /// <summary>A source over the current gameplay stream.</summary>
        public static GameplayRandomSource Current => new GameplayRandomSource(GameplayServices.Current.Random);

        public override double NextDouble() => stream.NextDouble();

        protected override double Sample() => stream.NextDouble();
    }
}
