using System;
using System.Collections.Generic;
using SimpleRPG.LevelGeneration;
using UnityEngine;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Everything a generator needs, as plain data: no prefab, scene object or placement. The room is generated in its own unturned frame
    /// (lowest corner at the origin); where it stands and how it is turned is applied to the finished geometry by the caller.
    /// Connections are the layout's own <see cref="DoorDefinition"/>s, so points, sides, widths, levels and types mean exactly what they do there.
    /// </summary>
    public sealed class RoomGenerationContext
    {
        public RoomGenerationContext(string name, Vector2Int footprintCells, int levels, IReadOnlyList<DoorDefinition> connections, int seed,
            RoomGenerationSettings settings, object generatorSettings = null)
        {
            Name = name ?? string.Empty;
            FootprintCells = footprintCells;
            Levels = Math.Max(1, levels);
            Connections = connections ?? new DoorDefinition[0];
            Seed = seed;
            Settings = settings ?? new RoomGenerationSettings();
            GeneratorSettings = generatorSettings;
        }

        /// <summary>
        /// For diagnostics only.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Width and height of the rectangular footprint in layout cells.
        /// </summary>
        public Vector2Int FootprintCells { get; }

        /// <summary>
        /// How many floors high the room is (<c>RoomDefinition.Height</c>). A door's <see cref="DoorDefinition.Level"/> is below this.
        /// </summary>
        public int Levels { get; }

        /// <summary>
        /// The assigned connections, in the room's own coordinates. Their order and indices never change.
        /// </summary>
        public IReadOnlyList<DoorDefinition> Connections { get; }

        /// <summary>
        /// The deterministic seed for this room (see <see cref="RoomSeed"/>).
        /// </summary>
        public int Seed { get; }

        public RoomGenerationSettings Settings { get; }

        /// <summary>
        /// Settings for one generator (for example <see cref="ChamberSettings"/>), or null for its defaults.
        /// </summary>
        public object GeneratorSettings { get; }

        /// <summary>
        /// The footprint in millimetres.
        /// </summary>
        public RectMm FootprintMm => new RectMm(0, 0, FootprintCells.x * Settings.CellSizeMm, FootprintCells.y * Settings.CellSizeMm);

        /// <summary>
        /// The elevation of a level's floor above the room's base.
        /// </summary>
        public int LevelElevation(int level) => level * Settings.FloorHeightMm;

        public T GetSettings<T>() where T : class, new() => GeneratorSettings as T ?? new T();

        public RoomGenerationContext With(int seed, object generatorSettings) =>
            new RoomGenerationContext(Name, FootprintCells, Levels, Connections, seed, Settings, generatorSettings);

        public override string ToString() => $"room '{Name}' {FootprintCells.x}x{FootprintCells.y} cells, {Levels} level(s), {Connections.Count} connection(s), seed {Seed}";
    }

    /// <summary>
    /// Builds the geometry of one room from its context. Implementations are pure functions of the context: same context, same plan,
    /// on every machine, with no use of global random state or of Unity objects. A generator that cannot realise a valid arrangement
    /// returns <see cref="GeneratedRoom.Failed"/> with the reason rather than a broken room.
    /// </summary>
    public interface IRoomGenerator
    {
        /// <summary>
        /// A stable name, part of every room seed.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Raise this whenever a change would give an existing seed different geometry. Part of every room seed and of the roomset fingerprint.
        /// </summary>
        int Version { get; }

        GeneratedRoom Generate(RoomGenerationContext context);
    }
}
