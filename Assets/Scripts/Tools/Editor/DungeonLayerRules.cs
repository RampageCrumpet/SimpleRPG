namespace SimpleRPG.Editor
{
    /// <summary>
    /// Which physics layer a piece of the dungeon belongs on, decided from its prefab's name. Solid structure goes on <c>World</c>, which is what
    /// a weapon's blade bounces off; surfaces to stand on go on <c>Walkable</c>; everything else is left alone. Plain code so the rule is tested.
    /// </summary>
    public static class DungeonLayerRules
    {
        public const string World = "World";
        public const string Walkable = "Walkable";

        /// <summary>The layer name for a prefab called <paramref name="prefabName"/>, or null to leave it as it is.</summary>
        public static string LayerFor(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return null;
            }

            string name = prefabName.ToLowerInvariant();

            // Ground first: stairs sit in a wall-like family of names but are walked on.
            if (name.StartsWith("floor") || name.StartsWith("stairs"))
            {
                return Walkable;
            }

            foreach (string solid in new[] { "wall", "pillar", "door", "portal", "railing", "metal_bar", "ceiling" })
            {
                if (name.Contains(solid))
                {
                    return World;
                }
            }

            return null;
        }
    }
}
