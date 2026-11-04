using SimpleRPG.Abilities;
using SimpleRPG.InventorySystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Which body parts are the look of an item, found from the parts themselves (<see cref="BodyPartDefinition.DressesItem"/>) through the
    /// definition registry. Authoring a piece of armor's look is making its parts; there is no table to keep in step. Built once per registry.
    /// </summary>
    public static class ArmorParts
    {
        private static readonly IReadOnlyList<BodyPartDefinition> None = Array.Empty<BodyPartDefinition>();

        private static Dictionary<Item, List<BodyPartDefinition>> byItem;
        private static DefinitionRegistry builtFor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            byItem = null;
            builtFor = null;
        }

        /// <summary>The parts that are the look of this item (an instance counts as the asset it was made from), or none.</summary>
        public static IReadOnlyList<BodyPartDefinition> For(Item item)
        {
            DefinitionRegistry registry = DefinitionRegistry.Instance;
            if (item == null || registry == null)
            {
                return None;
            }

            if (byItem == null || !ReferenceEquals(builtFor, registry))
            {
                Build(registry);
            }

            return byItem.TryGetValue(item.SourceAsset, out List<BodyPartDefinition> parts) ? parts : None;
        }

        private static void Build(DefinitionRegistry registry)
        {
            byItem = new Dictionary<Item, List<BodyPartDefinition>>();
            builtFor = registry;
            foreach (GameplayDefinition definition in registry.Definitions)
            {
                if (definition is BodyPartDefinition part && part.DressesItem != null)
                {
                    if (!byItem.TryGetValue(part.DressesItem, out List<BodyPartDefinition> parts))
                    {
                        parts = new List<BodyPartDefinition>();
                        byItem[part.DressesItem] = parts;
                    }

                    parts.Add(part);
                }
            }
        }
    }
}
