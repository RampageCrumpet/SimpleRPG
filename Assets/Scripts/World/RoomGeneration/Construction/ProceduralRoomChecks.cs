using System.Collections.Generic;
using SimpleRPG.LevelGeneration;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Authoring-time checks for procedural shell prefabs, run by Validate Roomset and the build gate: a room that cannot generate is caught
    /// before the game starts, not when its seed comes up. Each room is trial-generated from a few fixed seeds with the same pipeline the game uses.
    /// </summary>
    public static class ProceduralRoomChecks
    {
        public const string Rule = "procedural-room";

        private const int TrialSeeds = 4;

        public static List<RoomValidationError> Check(RoomBlueprint room, Roomset roomset)
        {
            var errors = new List<RoomValidationError>();
            ProceduralRoomSettings settings = room.GetComponent<ProceduralRoomSettings>();
            if (settings == null)
            {
                return errors;
            }

            IRoomGenerator generator = settings.CreateGenerator();
            if (generator == null)
            {
                errors.Add(new RoomValidationError(room.name, Rule, $"The generator '{settings.GeneratorId}' does not exist."));
                return errors;
            }

            if (room.ExtraCells != null && room.ExtraCells.Count > 0)
            {
                errors.Add(new RoomValidationError(room.name, Rule, "Procedural rooms cannot claim cells beyond their rectangle."));
                return errors;
            }

            foreach (string problem in settings.ToGenerationSettings(roomset.cellSize, roomset.floorHeight).Check())
            {
                errors.Add(new RoomValidationError(room.name, Rule, problem));
            }

            if (errors.Count > 0)
            {
                return errors;
            }

            RoomDefinition definition = RoomDefinitionBaker.Bake(room, 0, roomset.cellSize);
            for (int seed = 0; seed < TrialSeeds; seed++)
            {
                var context = new RoomGenerationContext(definition.Name, definition.Size, definition.Height, definition.Doors, seed,
                    settings.ToGenerationSettings(roomset.cellSize, roomset.floorHeight), settings.GeneratorSettings());
                RoomGenerationResult result = RoomGenerationPipeline.Generate(context, generator, settings.Retries);
                if (!result.Succeeded)
                {
                    errors.Add(new RoomValidationError(room.name, Rule, $"Trial seed {seed} cannot be generated: {result.Room.Failure} First problem: {(result.Diagnostics.Count > 0 ? result.Diagnostics[0] : "none")}"));
                    break;
                }
            }

            return errors;
        }
    }
}
