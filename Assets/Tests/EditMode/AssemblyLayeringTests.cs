using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Holds the assembly layering of Docs/CodeLayout.md in place. Reads every .asmdef under Assets and fails when a game assembly
    /// references one that is not strictly below it, when the project's own assemblies form a cycle, when a new game assembly is not placed in
    /// the table below, or when a layered assembly is auto-referenced. Only references between the project's own assemblies are checked; Unity
    /// and package references are not. The plan lives here as data and in Docs/tools/layering_check.py; change both together.
    /// </summary>
    public class AssemblyLayeringTests
    {
        // Ranks 0 and below are the existing ability assemblies, under everything. Rank 10 are the leaves: they may use every core rank and the leaves listed before them in LeafOrder.
        private static readonly Dictionary<string, int> Ranks = new Dictionary<string, int>
        {
            { "SimpleRPG.Gameplay.Core", -1 },
            { "SimpleRPG.Abilities.Runtime", 0 },
            { "SimpleRPG.Foundation", 1 },
            { "SimpleRPG.Combos", 3 },
            { "SimpleRPG.AudioModel", 2 },
            { "SimpleRPG.Items", 4 },
            { "SimpleRPG.Combat", 5 },
            { "SimpleRPG.Carrying", 6 },
            { "SimpleRPG.Melee", 6 },
            { "SimpleRPG.AudioPlayback", 10 },
            { "SimpleRPG.Presentation", 10 },
            { "SimpleRPG.Interaction", 10 },
            { "SimpleRPG.Loot", 10 },
            { "SimpleRPG.PlayerControl", 10 },
            { "SimpleRPG.AI", 10 },
            { "SimpleRPG.World", 10 },
            { "SimpleRPG.Delve", 10 },
            { "SimpleRPG.Rendering", 10 },
            { "SimpleRPG.UI", 10 },
            // Bootstraps the game and may use all of it; nothing else in the layering may use it.
            { "SimpleRPG.Session", 11 },
            // Wires the ability system to the game, so it may use any of it. Nothing in the layering may use it.
            { "SimpleRPG.Abilities.Integration", 12 },
        };

        private static readonly string[] LeafOrder =
        {
            "SimpleRPG.AudioPlayback", "SimpleRPG.Presentation", "SimpleRPG.Interaction", "SimpleRPG.Loot", "SimpleRPG.PlayerControl",
            "SimpleRPG.AI", "SimpleRPG.World", "SimpleRPG.Delve", "SimpleRPG.Rendering", "SimpleRPG.UI",
        };

        // Assemblies that existed before the split and are exempt from the autoReferenced rule. New assemblies are not.
        private static readonly HashSet<string> AutoReferencedLegacy = new HashSet<string>
        {
            "SimpleRPG.Gameplay.Core", "SimpleRPG.Abilities.Runtime", "SimpleRPG.Abilities.Integration",
        };

        [Serializable]
        private class AsmDef
        {
            public string name;
            public string[] references;
        }

        private sealed class Assembly
        {
            public string Name;
            public string Path;
            public string Guid;
            public string[] References;
            public bool AutoReferenced;
            public bool IsEditorOrTest;
        }

        private static List<Assembly> LoadProjectAssemblies()
        {
            var list = new List<Assembly>();
            foreach (string path in Directory.GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                AsmDef parsed = JsonUtility.FromJson<AsmDef>(text);
                string meta = File.ReadAllText(path + ".meta");
                Match guid = Regex.Match(meta, @"guid:\s*([0-9a-f]{32})");
                Match auto = Regex.Match(text, "\"autoReferenced\"\\s*:\\s*(true|false)");
                string normalized = path.Replace('\\', '/');
                list.Add(new Assembly
                {
                    Name = parsed.name,
                    Path = normalized.Substring(normalized.IndexOf("Assets/", StringComparison.Ordinal)),
                    Guid = guid.Success ? guid.Groups[1].Value : null,
                    References = parsed.references ?? new string[0],
                    AutoReferenced = !auto.Success || auto.Groups[1].Value == "true",
                    // Editor and test assemblies sit outside the layering; they may reference any game assembly.
                    IsEditorOrTest = Regex.IsMatch(text, "\"Editor\"") || normalized.Contains("/Tests/") || normalized.Contains("/Editor/"),
                });
            }
            return list;
        }

        /// <summary>The project's own assemblies that a reference (a name or "GUID:...") points at; references to packages resolve to none.</summary>
        private static IEnumerable<Assembly> ResolveProjectReferences(Assembly from, List<Assembly> all)
        {
            foreach (string reference in from.References)
            {
                Assembly target = reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase)
                    ? all.FirstOrDefault(a => a.Guid == reference.Substring(5))
                    : all.FirstOrDefault(a => a.Name == reference);
                if (target != null) yield return target;
            }
        }

        private static bool MayReference(string from, string to)
        {
            int rankFrom = Ranks[from], rankTo = Ranks[to];
            if (rankFrom != 10) return rankTo < rankFrom;
            if (rankTo < 10) return true;
            return rankTo == 10 && Array.IndexOf(LeafOrder, to) < Array.IndexOf(LeafOrder, from);
        }

        [Test]
        public void EveryGameAssemblyIsPlacedInTheLayering()
        {
            var unplaced = LoadProjectAssemblies().Where(a => !a.IsEditorOrTest && !Ranks.ContainsKey(a.Name)).Select(a => $"{a.Name} ({a.Path})").ToList();
            Assert.That(unplaced, Is.Empty,
                "These assemblies are not in the layering. Place each one in Ranks in AssemblyLayeringTests and in Docs/tools/layering_check.py, "
                + "and in the table in Docs/CodeLayout.md, deciding what belongs in it first:\n" + string.Join("\n", unplaced));
        }

        [Test]
        public void GameAssembliesReferenceOnlyAssembliesBelowThem()
        {
            List<Assembly> all = LoadProjectAssemblies();
            var violations = new List<string>();
            foreach (Assembly from in all.Where(a => !a.IsEditorOrTest && Ranks.ContainsKey(a.Name)))
            {
                foreach (Assembly to in ResolveProjectReferences(from, all))
                {
                    if (to.IsEditorOrTest || !Ranks.ContainsKey(to.Name)) continue;
                    if (!MayReference(from.Name, to.Name)) violations.Add($"{from.Name} (rank {Ranks[from.Name]}) -> {to.Name} (rank {Ranks[to.Name]})");
                }
            }
            Assert.That(violations, Is.Empty, "A layer may only reference layers below it:\n" + string.Join("\n", violations));
        }

        [Test]
        public void GameAssembliesDoNotReferenceEditorOrTestAssemblies()
        {
            List<Assembly> all = LoadProjectAssemblies();
            var violations = new List<string>();
            foreach (Assembly from in all.Where(a => !a.IsEditorOrTest))
                foreach (Assembly to in ResolveProjectReferences(from, all).Where(t => t.IsEditorOrTest))
                    violations.Add($"{from.Name} -> {to.Name}");
            Assert.That(violations, Is.Empty, "Game assemblies cannot depend on editor or test assemblies:\n" + string.Join("\n", violations));
        }

        [Test]
        public void ProjectAssembliesFormNoCycle()
        {
            List<Assembly> all = LoadProjectAssemblies();
            var state = new Dictionary<Assembly, int>(); // 1 = on the path, 2 = done
            var path = new List<string>();
            string cycle = null;

            void Visit(Assembly node)
            {
                if (cycle != null) return;
                if (state.TryGetValue(node, out int s))
                {
                    if (s == 1) cycle = string.Join(" -> ", path.SkipWhile(n => n != node.Name).Append(node.Name));
                    return;
                }
                state[node] = 1;
                path.Add(node.Name);
                foreach (Assembly next in ResolveProjectReferences(node, all)) Visit(next);
                path.RemoveAt(path.Count - 1);
                state[node] = 2;
            }

            foreach (Assembly a in all) Visit(a);
            Assert.That(cycle, Is.Null, "Assembly references form a cycle: " + cycle);
        }

        [Test]
        public void LayeredAssembliesAreNotAutoReferenced()
        {
            var violations = LoadProjectAssemblies()
                .Where(a => !a.IsEditorOrTest && Ranks.ContainsKey(a.Name) && !AutoReferencedLegacy.Contains(a.Name) && a.AutoReferenced)
                .Select(a => a.Name).ToList();
            Assert.That(violations, Is.Empty, "Set autoReferenced to false so nothing is referenced implicitly:\n" + string.Join("\n", violations));
        }
    }
}
