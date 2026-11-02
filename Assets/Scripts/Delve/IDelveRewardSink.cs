using UnityEngine;

namespace SimpleRPG.Delve
{
    /// <summary>
    /// Where the outcome of a delve goes when it ends. Official servers will plug in the persistent stash and character storage here; a local
    /// game uses its own, entirely separate, implementation. The dungeon itself never knows which one it has.
    /// </summary>
    public interface IDelveRewardSink
    {
        /// <summary>Called once on the server when a delve ends.</summary>
        void Deposit(DelveResult result);
    }

    /// <summary>
    /// Records the outcome in the log and keeps nothing. The default until a real store is plugged in.
    /// </summary>
    public sealed class LogDelveRewardSink : IDelveRewardSink
    {
        public void Deposit(DelveResult result)
        {
            Debug.Log($"Delve ended: {result.Phase} after {result.DurationSeconds:0}s; {result.Survivors.Count} survived, {result.Dead.Count} died.");
        }
    }
}
