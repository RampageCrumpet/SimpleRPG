using SimpleRPG.Gameplay;
using System.Collections.Generic;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Tells the status host which triggered effects a status brings, by looking the status up in the definition registry. Server only.
    /// </summary>
    public sealed class RegistryStatusEffectProvider : IStatusEffectProvider
    {
        /// <inheritdoc/>
        public bool TryGetEffects(DefinitionId status, out IReadOnlyList<TriggeredEffect> effects)
        {
            effects = null;
            DefinitionRegistry registry = DefinitionRegistry.Instance;
            if (registry == null || !registry.TryGet(status, out GameplayDefinition definition) || !(definition is StatusDefinition statusDefinition))
            {
                return false;
            }

            if (statusDefinition.TriggeredEffects.Count == 0)
            {
                return false;
            }

            effects = statusDefinition.TriggeredEffects;
            return true;
        }
    }
}
