using SimpleRPG.Abilities;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Translates between body parts and the short ids that travel over the network.
    /// </summary>
    public interface IBodyPartCatalog
    {
        bool TryGetId(BodyPartDefinition part, out uint id);
        bool TryGetPart(uint id, out BodyPartDefinition part);
    }

    /// <summary>
    /// The catalog backed by the game's <see cref="DefinitionRegistry"/>.
    /// </summary>
    public sealed class RegistryBodyPartCatalog : IBodyPartCatalog
    {
        public bool TryGetId(BodyPartDefinition part, out uint id)
        {
            id = 0;
            DefinitionRegistry registry = DefinitionRegistry.Instance;
            return registry != null && part != null && registry.TryGetShortId(part.Id, out id);
        }

        public bool TryGetPart(uint id, out BodyPartDefinition part)
        {
            part = null;
            DefinitionRegistry registry = DefinitionRegistry.Instance;
            if (registry == null || !registry.TryGetByShortId(id, out GameplayDefinition definition)) return false;

            part = definition as BodyPartDefinition;
            return part != null;
        }
    }
}
