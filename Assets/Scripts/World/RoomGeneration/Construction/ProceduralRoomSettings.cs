using System;
using UnityEngine;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Marks a <see cref="LevelGeneration.RoomBlueprint"/> prefab as a room whose structure is generated when the dungeon is built, and says how.
    /// The prefab is a shell: its footprint (two pivots at the lowest and furthest corners, as the packer reads them), its doors with their sealing
    /// and door objects, and this component. Doors must share their doorway with the next room (length 0); see <see cref="ConnectionMap"/>.
    /// Authored rooms simply do not have this component.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProceduralRoomSettings : MonoBehaviour
    {
        [Tooltip("Which algorithm builds the room's structure.")]
        public ProceduralGeneratorKind Generator = ProceduralGeneratorKind.Chamber;

        [Tooltip("Architectural dimensions in millimetres. Cell size and floor height are taken from the roomset; the values here for those two are ignored.")]
        public RoomGenerationSettings Architecture = new RoomGenerationSettings();

        [Tooltip("Settings for the chamber generator.")]
        public ChamberSettings Chamber = new ChamberSettings();

        [Header("Materials")]
        public Material FloorMaterial;
        public Material WallMaterial;
        public Material CeilingMaterial;
        public Material PillarMaterial;
        public Material StairMaterial;

        [Tooltip("Used for any material left empty above. If this is empty too, a plain URP Lit material is used.")]
        public Material DefaultMaterial;

        [Tooltip("How far a texture repeats, in metres.")]
        [Min(0.1f)]
        public float MetresPerUvTile = 2f;

        [Tooltip("The number of tries (each from a seed derived from the room's) before the generator falls back to an open chamber.")]
        [Min(0)]
        public int Retries = RoomGenerationPipeline.DefaultRetries;

        public string GeneratorId
        {
            get
            {
                switch (Generator)
                {
                    default: return ChamberGenerator.GeneratorId;
                }
            }
        }

        /// <summary>
        /// The generator this component selects, or null if it names none.
        /// </summary>
        public IRoomGenerator CreateGenerator() => RoomGeneratorRegistry.Create(GeneratorId);

        /// <summary>
        /// The settings the selected generator reads besides the common ones.
        /// </summary>
        public object GeneratorSettings() => Generator == ProceduralGeneratorKind.Chamber ? Chamber : null;

        /// <summary>
        /// The architectural settings with the roomset's scale applied. A floor height of zero (not set in the roomset) stays zero,
        /// which is only a problem for rooms with more than one level.
        /// </summary>
        public RoomGenerationSettings ToGenerationSettings(float cellSize, float floorHeight)
        {
            RoomGenerationSettings settings = Architecture.Clone();
            settings.CellSizeMm = Mathf.RoundToInt(cellSize * 1000f);
            settings.FloorHeightMm = Mathf.RoundToInt(floorHeight * 1000f);
            return settings;
        }

        public Material MaterialFor(PieceKind kind)
        {
            Material material;
            switch (kind)
            {
                case PieceKind.Floor: material = FloorMaterial; break;
                case PieceKind.Ceiling: material = CeilingMaterial; break;
                case PieceKind.Wall: material = WallMaterial; break;
                case PieceKind.Pillar: material = PillarMaterial; break;
                default: material = StairMaterial; break;
            }

            return material != null ? material : DefaultMaterial;
        }

        private void Reset()
        {
            Architecture = new RoomGenerationSettings();
            Chamber = new ChamberSettings();
        }
    }

    /// <summary>
    /// The generators a room can select. Appending a value and a case in <see cref="RoomGeneratorRegistry"/> adds an algorithm.
    /// </summary>
    public enum ProceduralGeneratorKind
    {
        Chamber = 0,
    }
}
