using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// The outcome of checking or rebuilding a <see cref="DefinitionRegistry"/>.
    /// </summary>
    public sealed class RegistryReport
    {
        public List<string> Errors { get; } = new List<string>();

        /// <summary>Definitions newly registered by a rebuild.</summary>
        public int Added { get; internal set; }

        /// <summary>Entries dropped by a rebuild because their definition no longer exists.</summary>
        public int Removed { get; internal set; }

        public bool Success => Errors.Count == 0;
    }

    /// <summary>
    /// Finds gameplay definitions by ID at runtime, in the editor, in player builds and on a dedicated server, without the asset database.
    /// It lives in Resources and holds a reference to every definition, so everything in it ships in the build.
    /// Each definition also has a compact short ID for network messages. Short IDs are assigned once and never reused, so they stay valid
    /// for the life of the project even as definitions are added and removed.
    /// </summary>
    public sealed class DefinitionRegistry : ScriptableObject
    {
        /// <summary>
        /// The Resources path the registry asset is loaded from.
        /// </summary>
        public const string ResourcePath = "DefinitionRegistry";

        [Serializable]
        public struct Entry
        {
            public string id;
            public uint shortId;
            public GameplayDefinition definition;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        [SerializeField]
        private uint nextShortId = 1;

        private static DefinitionRegistry instance;

        private Dictionary<DefinitionId, Entry> byId;

        private Dictionary<uint, Entry> byShortId;

        /// <summary>
        /// The registry used by the game. Loaded from Resources on first use; null if the asset hasn't been created.
        /// </summary>
        public static DefinitionRegistry Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<DefinitionRegistry>(ResourcePath);
                }

                return instance;
            }
            set
            {
                instance = value;
            }
        }

        /// <summary>
        /// The number of registered definitions.
        /// </summary>
        public int Count => entries.Count;

        private void OnEnable()
        {
            byId = null;
            byShortId = null;
        }

        /// <summary>
        /// Finds a definition by its permanent ID.
        /// </summary>
        public bool TryGet(DefinitionId id, out GameplayDefinition definition)
        {
            EnsureIndexes();
            bool found = byId.TryGetValue(id, out Entry entry) && entry.definition != null;
            definition = found ? entry.definition : null;
            return found;
        }

        /// <summary>
        /// Finds a definition by the short ID used in network messages.
        /// </summary>
        public bool TryGetByShortId(uint shortId, out GameplayDefinition definition)
        {
            EnsureIndexes();
            bool found = byShortId.TryGetValue(shortId, out Entry entry) && entry.definition != null;
            definition = found ? entry.definition : null;
            return found;
        }

        /// <summary>
        /// Gets the short ID for a definition's permanent ID.
        /// </summary>
        public bool TryGetShortId(DefinitionId id, out uint shortId)
        {
            EnsureIndexes();
            bool found = byId.TryGetValue(id, out Entry entry);
            shortId = found ? entry.shortId : 0;
            return found;
        }

        /// <summary>
        /// Checks a set of definitions for problems that would make IDs unreliable: a definition with no ID, or two with the same ID.
        /// </summary>
        /// <param name="definitions">The definitions to check.</param>
        /// <param name="describe">Describes a definition in messages, for example by asset path. Defaults to its name.</param>
        public static List<string> Validate(IEnumerable<GameplayDefinition> definitions, Func<GameplayDefinition, string> describe = null)
        {
            describe ??= d => d.name;
            List<string> errors = new List<string>();
            Dictionary<DefinitionId, List<GameplayDefinition>> byDefinitionId = new Dictionary<DefinitionId, List<GameplayDefinition>>();

            foreach (GameplayDefinition definition in definitions.Where(d => d != null))
            {
                if (!definition.HasId)
                {
                    errors.Add(describe(definition) + " has no definition ID.");
                    continue;
                }

                if (!byDefinitionId.TryGetValue(definition.Id, out List<GameplayDefinition> sharing))
                {
                    sharing = new List<GameplayDefinition>();
                    byDefinitionId.Add(definition.Id, sharing);
                }

                sharing.Add(definition);
            }

            foreach (KeyValuePair<DefinitionId, List<GameplayDefinition>> pair in byDefinitionId.Where(p => p.Value.Count > 1))
            {
                errors.Add("Definition ID " + pair.Key + " is shared by " + string.Join(", ", pair.Value.Select(describe)) + ".");
            }

            return errors;
        }

        /// <summary>
        /// Checks that the registry matches a set of definitions exactly: every definition valid and registered, nothing stale.
        /// </summary>
        public RegistryReport CheckUpToDate(IEnumerable<GameplayDefinition> definitions, Func<GameplayDefinition, string> describe = null)
        {
            describe ??= d => d.name;
            List<GameplayDefinition> all = definitions.Where(d => d != null).ToList();
            RegistryReport report = new RegistryReport();
            report.Errors.AddRange(Validate(all, describe));
            if (!report.Success)
            {
                return report;
            }

            Dictionary<string, Entry> registered = entries.Where(e => e.definition != null).ToDictionary(e => e.id);
            foreach (GameplayDefinition definition in all)
            {
                if (!registered.TryGetValue(definition.Id.ToString(), out Entry entry) || entry.definition != definition)
                {
                    report.Errors.Add(describe(definition) + " is not in the definition registry.");
                }
            }

            HashSet<string> current = new HashSet<string>(all.Select(d => d.Id.ToString()));
            foreach (Entry entry in entries.Where(e => e.definition == null || !current.Contains(e.id)))
            {
                report.Errors.Add("The definition registry has a stale entry for " + entry.id + ".");
            }

            return report;
        }

        /// <summary>
        /// Makes the registry list exactly the given definitions. Editor tooling calls this; it is not for gameplay.
        /// Existing short IDs are kept, new definitions get the next unused ones, and removed definitions retire their short IDs for good.
        /// If any definition is invalid the registry is left unchanged and the report says why.
        /// </summary>
        public RegistryReport Rebuild(IEnumerable<GameplayDefinition> definitions, Func<GameplayDefinition, string> describe = null)
        {
            List<GameplayDefinition> all = definitions.Where(d => d != null).ToList();
            RegistryReport report = new RegistryReport();
            report.Errors.AddRange(Validate(all, describe));
            if (!report.Success)
            {
                return report;
            }

            Dictionary<string, uint> existingShortIds = entries.ToDictionary(e => e.id, e => e.shortId);
            List<Entry> rebuilt = new List<Entry>();
            uint next = nextShortId;

            // Ordered by ID so the result doesn't depend on the order assets were found in.
            foreach (GameplayDefinition definition in all.OrderBy(d => d.Id.ToString(), StringComparer.Ordinal))
            {
                string id = definition.Id.ToString();
                if (!existingShortIds.TryGetValue(id, out uint shortId))
                {
                    shortId = next++;
                    report.Added++;
                }

                rebuilt.Add(new Entry { id = id, shortId = shortId, definition = definition });
            }

            HashSet<string> kept = new HashSet<string>(rebuilt.Select(e => e.id));
            report.Removed = entries.Count(e => !kept.Contains(e.id));

            entries = rebuilt;
            nextShortId = next;
            byId = null;
            byShortId = null;
            return report;
        }

        private void EnsureIndexes()
        {
            if (byId != null)
            {
                return;
            }

            byId = new Dictionary<DefinitionId, Entry>();
            byShortId = new Dictionary<uint, Entry>();
            foreach (Entry entry in entries)
            {
                if (DefinitionId.TryParse(entry.id, out DefinitionId id))
                {
                    byId[id] = entry;
                    byShortId[entry.shortId] = entry;
                }
            }
        }
    }
}
