using System.Collections.Generic;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Everything a generator is told about scale and architecture. Three independent ideas live here and are never derived from each other:
    /// the dungeon's layout cell (<see cref="CellSizeMm"/>, which fixes where doors stand), the precision generators plan at
    /// (<see cref="PlanningResolutionMm"/>), and physical architectural dimensions (wall thickness, passage width, stair steps, ...) in real millimetres.
    /// A door always lands exactly where the layout says, whatever the planning resolution is.
    /// </summary>
    [System.Serializable]
    public sealed class RoomGenerationSettings
    {
        // ---- Layout scale (from the roomset) ----

        /// <summary>
        /// World size of one layout cell. Doors and footprints are whole numbers of these.
        /// </summary>
        public int CellSizeMm = 2000;

        /// <summary>
        /// Height between the floors of a multi-floor room (the roomset's floor height). Only needed by rooms with more than one level.
        /// </summary>
        public int FloorHeightMm = 8000;

        // ---- Resolution ----

        /// <summary>
        /// The step generators snap their free choices (pillar spacing, stair positions, partitions) to. Never used for doors.
        /// </summary>
        public int PlanningResolutionMm = 500;

        /// <summary>
        /// The raster step of the structural validator. Smaller is more exact and slower; dimensions that are multiples of it are checked exactly,
        /// others are checked conservatively.
        /// </summary>
        public int ValidationResolutionMm = 100;

        // ---- Architectural dimensions ----

        public int WallThicknessMm = 300;
        public int FloorThicknessMm = 200;
        public int CeilingThicknessMm = 200;

        /// <summary>
        /// From a level's floor to the underside of the ceiling over the highest level used.
        /// </summary>
        public int CeilingHeightMm = 4000;

        /// <summary>
        /// Height of a doorway opening above its floor.
        /// </summary>
        public int OpeningHeightMm = 3000;

        /// <summary>
        /// The narrowest passage a walker can use, as the side of the square that has to fit through.
        /// </summary>
        public int MinPassageWidthMm = 1200;

        /// <summary>
        /// Headroom that must be free above any walkable surface.
        /// </summary>
        public int ClearanceHeightMm = 2000;

        /// <summary>
        /// How far in front of each opening (measured from the wall's inner face) stays clear.
        /// </summary>
        public int ApproachDepthMm = 1500;

        // ---- Stairs ----

        public int StairWidthMm = 2000;

        /// <summary>
        /// The tallest riser. A flight uses the fewest steps that keep every riser at or under this.
        /// </summary>
        public int StepHeightMm = 200;

        public int StepDepthMm = 300;

        /// <summary>
        /// Depth of the landing where a flight turns back on itself.
        /// </summary>
        public int LandingDepthMm = 2000;

        /// <summary>
        /// The most straight flights a staircase may be split into (with turning landings) to fit a footprint.
        /// </summary>
        public int MaxStairFlights = 4;

        public RoomGenerationSettings Clone() => (RoomGenerationSettings)MemberwiseClone();

        /// <summary>
        /// Describes every way these settings are unusable. Empty when they are fine.
        /// </summary>
        public List<string> Check()
        {
            var problems = new List<string>();
            Positive(problems, CellSizeMm, nameof(CellSizeMm));
            Positive(problems, PlanningResolutionMm, nameof(PlanningResolutionMm));
            Positive(problems, ValidationResolutionMm, nameof(ValidationResolutionMm));
            Positive(problems, WallThicknessMm, nameof(WallThicknessMm));
            Positive(problems, FloorThicknessMm, nameof(FloorThicknessMm));
            Positive(problems, CeilingThicknessMm, nameof(CeilingThicknessMm));
            Positive(problems, CeilingHeightMm, nameof(CeilingHeightMm));
            Positive(problems, OpeningHeightMm, nameof(OpeningHeightMm));
            Positive(problems, MinPassageWidthMm, nameof(MinPassageWidthMm));
            Positive(problems, ClearanceHeightMm, nameof(ClearanceHeightMm));
            Positive(problems, ApproachDepthMm, nameof(ApproachDepthMm));
            Positive(problems, StairWidthMm, nameof(StairWidthMm));
            Positive(problems, StepHeightMm, nameof(StepHeightMm));
            Positive(problems, StepDepthMm, nameof(StepDepthMm));
            Positive(problems, LandingDepthMm, nameof(LandingDepthMm));
            Positive(problems, MaxStairFlights, nameof(MaxStairFlights));

            if (problems.Count > 0)
            {
                return problems;
            }

            if (OpeningHeightMm < ClearanceHeightMm)
            {
                problems.Add($"OpeningHeightMm ({OpeningHeightMm}) is lower than ClearanceHeightMm ({ClearanceHeightMm}).");
            }

            if (CeilingHeightMm < OpeningHeightMm)
            {
                problems.Add($"CeilingHeightMm ({CeilingHeightMm}) is lower than OpeningHeightMm ({OpeningHeightMm}).");
            }

            if (StairWidthMm < MinPassageWidthMm)
            {
                problems.Add($"StairWidthMm ({StairWidthMm}) is narrower than MinPassageWidthMm ({MinPassageWidthMm}).");
            }

            if (LandingDepthMm < MinPassageWidthMm)
            {
                problems.Add($"LandingDepthMm ({LandingDepthMm}) is narrower than MinPassageWidthMm ({MinPassageWidthMm}).");
            }

            return problems;
        }

        private static void Positive(List<string> problems, int value, string name)
        {
            if (value <= 0)
            {
                problems.Add($"{name} must be greater than zero (it is {value}).");
            }
        }
    }

    /// <summary>
    /// How a chamber arranges supporting pillars.
    /// </summary>
    public enum PillarLayout
    {
        None = 0,

        /// <summary>
        /// Pillars on a regular lattice across the whole floor.
        /// </summary>
        Grid = 1,

        /// <summary>
        /// Two rows of pillars along the room's long axis, like a colonnade.
        /// </summary>
        Rows = 2,
    }

    /// <summary>
    /// Settings only the <see cref="ChamberGenerator"/> reads. An empty chamber (no pillars, no partitions) is a normal result.
    /// </summary>
    [System.Serializable]
    public sealed class ChamberSettings
    {
        public PillarLayout Pillars = PillarLayout.None;

        public int PillarSizeMm = 600;

        /// <summary>
        /// Centre to centre distance between neighbouring pillars. Snapped to the planning resolution.
        /// </summary>
        public int PillarSpacingMm = 4000;

        /// <summary>
        /// Free-standing partial walls, each attached to a perimeter wall and stopping short of the opposite side so the room stays connected.
        /// </summary>
        public int Partitions = 0;

        /// <summary>
        /// How many complete tries (each with the features above) the chamber makes before it falls back to an empty chamber.
        /// </summary>
        public int FeatureAttempts = 4;

        /// <summary>
        /// How many tries are made at placing stairs, each from the generator's own stream, before the room is reported as impossible.
        /// </summary>
        public int StairAttempts = 24;

        public ChamberSettings Clone() => (ChamberSettings)MemberwiseClone();

        public ChamberSettings WithoutFeatures()
        {
            ChamberSettings plain = Clone();
            plain.Pillars = PillarLayout.None;
            plain.Partitions = 0;
            return plain;
        }
    }
}
