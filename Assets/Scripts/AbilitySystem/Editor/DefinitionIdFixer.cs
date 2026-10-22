using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// Repairs definition IDs after assets are created or duplicated: a definition with no ID gets one, and a newly imported definition
    /// that shares an ID with an existing one (a duplicate, which copies the serialized ID) gets a new one. The original keeps its ID.
    /// </summary>
    public static class DefinitionIdFixer
    {
        /// <summary>
        /// Fixes the IDs of newly imported definitions.
        /// </summary>
        /// <param name="imported">The definitions that were just created or imported.</param>
        /// <param name="all">Every definition in the project, including the imported ones.</param>
        /// <returns>The definitions whose IDs were changed, which the caller must save.</returns>
        public static List<GameplayDefinition> Fix(IReadOnlyCollection<GameplayDefinition> imported, IReadOnlyCollection<GameplayDefinition> all)
        {
            List<GameplayDefinition> changed = new List<GameplayDefinition>();
            HashSet<GameplayDefinition> importedSet = new HashSet<GameplayDefinition>(imported);

            // IDs already held by definitions that were not just imported are taken; an imported definition may not reuse them.
            HashSet<string> taken = new HashSet<string>(
                all.Where(d => d != null && !importedSet.Contains(d) && d.HasId).Select(d => d.Id.ToString()));

            foreach (GameplayDefinition definition in imported.Where(d => d != null).OrderBy(d => d.name, System.StringComparer.Ordinal))
            {
                if (!definition.HasId || taken.Contains(definition.Id.ToString()))
                {
                    definition.AssignNewId();
                    changed.Add(definition);
                }

                taken.Add(definition.Id.ToString());
            }

            return changed;
        }
    }
}
