using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Registers test items the way the game does: items are definitions, so they live in the <see cref="DefinitionRegistry"/>.
    /// </summary>
    internal static class ItemTestRegistry
    {
        /// <summary>
        /// A copy of the live registry that also lists the given items. The copy keeps every ability and status at the id it has in the game,
        /// so a test that swaps it in still finds them. Assign it to <see cref="DefinitionRegistry.Instance"/> and destroy it afterwards.
        /// </summary>
        public static DefinitionRegistry With(IEnumerable<Item> items)
        {
            DefinitionRegistry live = DefinitionRegistry.Instance;
            DefinitionRegistry registry = live != null ? Object.Instantiate(live) : ScriptableObject.CreateInstance<DefinitionRegistry>();
            List<GameplayDefinition> all = live != null ? live.Definitions.ToList() : new List<GameplayDefinition>();

            foreach (Item item in items)
            {
                item.EnsureId();
                if (!all.Contains(item)) all.Add(item);
            }

            RegistryReport report = registry.Rebuild(all);
            if (!report.Success)
            {
                throw new System.InvalidOperationException("The test item registry could not be built: " + string.Join("; ", report.Errors));
            }

            return registry;
        }
    }
}
