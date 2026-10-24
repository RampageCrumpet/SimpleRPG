using UnityEngine;

namespace SimpleRPG.Articulation
{
    /// <summary>
    /// The articulation tuning of a kind of humanoid character, kept as an asset so it can be tuned in play mode and shared. Immutable at runtime:
    /// the solver reads it and keeps its own state.
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleRPG/Articulation Profile", fileName = "ArticulationProfile")]
    public sealed class ArticulationProfile : ScriptableObject
    {
        [SerializeField]
        private ArticulationSettings settings = new ArticulationSettings();

        public ArticulationSettings Settings => settings;
    }
}
