using System;
using System.Collections.Generic;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// What came out of generating one room, with everything needed to reproduce a failure.
    /// </summary>
    public sealed class RoomGenerationResult
    {
        public RoomGenerationResult(GeneratedRoom room, RoomGenerationContext context, int retry, bool usedFallback, IReadOnlyList<string> diagnostics)
        {
            Room = room;
            Context = context;
            Retry = retry;
            UsedFallback = usedFallback;
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// A plan that passed validation, or a failed plan if nothing did.
        /// </summary>
        public GeneratedRoom Room { get; }

        /// <summary>
        /// The context the accepted plan was made from (its seed is the retry's, not the original).
        /// </summary>
        public RoomGenerationContext Context { get; }

        /// <summary>
        /// Which try produced the plan, from 0; equal to the retry limit for the fallback.
        /// </summary>
        public int Retry { get; }

        public bool UsedFallback { get; }

        public bool Succeeded => !Room.IsFailure;

        /// <summary>
        /// One line per rejected try, each naming the generator, the room, the seed and the reason.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; }
    }

    /// <summary>
    /// Runs a generator, validates what it returns, retries from deterministic derived seeds, and as a last resort builds a plain open chamber.
    /// A plan is never returned unless it passed <see cref="RoomPlanValidator"/>.
    /// </summary>
    public static class RoomGenerationPipeline
    {
        public const int DefaultRetries = 3;

        public static RoomGenerationResult Generate(RoomGenerationContext context, IRoomGenerator generator, int retries = DefaultRetries)
        {
            var diagnostics = new List<string>();

            for (int retry = 0; retry <= retries; retry++)
            {
                RoomGenerationContext attempt = context.With(RoomSeed.ForRetry(context.Seed, retry), context.GeneratorSettings);
                GeneratedRoom room = Run(generator, attempt, retry, diagnostics);
                if (room != null)
                {
                    return new RoomGenerationResult(room, attempt, retry, false, diagnostics);
                }
            }

            // Plain open chamber from the original seed's next derived seed, with no features, under the same validation.
            var fallbackGenerator = new ChamberGenerator();
            RoomGenerationContext fallback = context.With(RoomSeed.ForRetry(context.Seed, retries + 1), new ChamberSettings().WithoutFeatures());
            GeneratedRoom fallbackRoom = Run(fallbackGenerator, fallback, retries + 1, diagnostics);
            if (fallbackRoom != null)
            {
                return new RoomGenerationResult(fallbackRoom, fallback, retries + 1, true, diagnostics);
            }

            string reason = $"{generator.Id} and the fallback chamber both failed for {context}.";
            return new RoomGenerationResult(GeneratedRoom.Failed(generator.Id, context.Seed, reason), context, retries + 1, true, diagnostics);
        }

        private static GeneratedRoom Run(IRoomGenerator generator, RoomGenerationContext context, int retry, List<string> diagnostics)
        {
            GeneratedRoom room;
            try
            {
                room = generator.Generate(context);
            }
            catch (Exception exception)
            {
                diagnostics.Add($"{generator.Id} threw for {context} (try {retry}): {exception.GetType().Name}: {exception.Message}");
                return null;
            }

            RoomPlanReport report = RoomPlanValidator.Validate(room, context);
            if (report.IsValid)
            {
                return room;
            }

            diagnostics.Add($"{generator.Id} v{generator.Version} failed for {context} (try {retry}): {report}");
            return null;
        }
    }

    /// <summary>
    /// Finds a generator by its stable id. New algorithms are added here; existing ones are not touched.
    /// </summary>
    public static class RoomGeneratorRegistry
    {
        public static IRoomGenerator Create(string id)
        {
            switch (id)
            {
                case ChamberGenerator.GeneratorId: return new ChamberGenerator();
                default: return null;
            }
        }

        /// <summary>
        /// The ids that <see cref="Create"/> understands, for settings validation.
        /// </summary>
        public static IReadOnlyList<string> Ids { get; } = new[] { ChamberGenerator.GeneratorId };
    }
}
