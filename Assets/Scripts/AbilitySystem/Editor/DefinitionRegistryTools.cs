using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// Editor commands and checks for gameplay definition IDs and the <see cref="DefinitionRegistry"/> asset.
    /// </summary>
    public static class DefinitionRegistryTools
    {
        public const string RegistryAssetPath = "Assets/Resources/" + DefinitionRegistry.ResourcePath + ".asset";

        /// <summary>
        /// Every gameplay definition asset in the project.
        /// </summary>
        public static List<GameplayDefinition> FindAllDefinitions()
        {
            return AssetDatabase.FindAssets("t:GameplayDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameplayDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(d => d != null)
                .ToList();
        }

        /// <summary>
        /// Describes a definition by its asset path, for error messages.
        /// </summary>
        public static string Describe(GameplayDefinition definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(path) ? definition.name : path;
        }

        /// <summary>
        /// Loads the registry asset, creating an empty one if there isn't one yet.
        /// </summary>
        public static DefinitionRegistry LoadOrCreateRegistry()
        {
            DefinitionRegistry registry = AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(RegistryAssetPath);
            if (registry == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RegistryAssetPath));
                registry = ScriptableObject.CreateInstance<DefinitionRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            return registry;
        }

        /// <summary>
        /// Gives every definition without an ID one, then rebuilds the registry from all definitions.
        /// </summary>
        [MenuItem("SimpleRPG/Definitions/Rebuild Definition Registry")]
        public static void RebuildRegistry()
        {
            List<GameplayDefinition> all = FindAllDefinitions();
            foreach (GameplayDefinition definition in all.Where(d => d.EnsureId()))
            {
                EditorUtility.SetDirty(definition);
            }

            DefinitionRegistry registry = LoadOrCreateRegistry();
            RegistryReport report = registry.Rebuild(all, Describe);
            if (!report.Success)
            {
                Debug.LogError("The definition registry was not rebuilt:\n" + string.Join("\n", report.Errors));
                return;
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Debug.Log($"Definition registry rebuilt: {report.Added} added, {report.Removed} removed, {registry.Count} total.");
        }

        /// <summary>
        /// Reports every definition problem and whether the registry is up to date, without changing anything.
        /// </summary>
        [MenuItem("SimpleRPG/Definitions/Validate Definitions")]
        public static void ValidateMenu()
        {
            List<string> problems = ValidateForBuild();
            if (problems.Count == 0)
            {
                Debug.Log("All gameplay definitions are valid and the registry is up to date.");
            }
            else
            {
                Debug.LogError(problems.Count + " definition problem(s):\n" + string.Join("\n", problems));
            }
        }

        /// <summary>
        /// The problems that would stop a build: missing or duplicate IDs, a missing registry, a registry that is out of date, or an ability
        /// whose graph does not compile.
        /// </summary>
        public static List<string> ValidateForBuild()
        {
            List<GameplayDefinition> all = FindAllDefinitions();
            DefinitionRegistry registry = AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(RegistryAssetPath);
            if (registry == null)
            {
                List<string> problems = DefinitionRegistry.Validate(all, Describe);
                problems.Add("The definition registry asset is missing. Run SimpleRPG > Definitions > Rebuild Definition Registry.");
                problems.AddRange(AbilityGraphProblems(all));
                return problems;
            }

            List<string> errors = registry.CheckUpToDate(all, Describe).Errors;
            if (errors.Count > 0)
            {
                errors.Add("Run SimpleRPG > Definitions > Rebuild Definition Registry to fix this.");
            }

            errors.AddRange(AbilityGraphProblems(all));
            return errors;
        }

        /// <summary>
        /// Compiles every ability and returns each error with the asset, node and port it belongs to. Warnings do not stop a build.
        /// </summary>
        public static List<string> AbilityGraphProblems(IEnumerable<GameplayDefinition> definitions)
        {
            List<string> problems = new List<string>();
            foreach (AbilityDefinition ability in definitions.OfType<AbilityDefinition>())
            {
                foreach (ValidationMessage error in ability.Compile().Report.Errors)
                {
                    string where = Describe(ability)
                        + (error.NodeId != null ? ", node '" + error.NodeId + "'" : string.Empty)
                        + (error.PortId != null ? ", port '" + error.PortId + "'" : string.Empty);
                    problems.Add(where + ": " + error.Text);
                }
            }

            return problems;
        }
    }

    /// <summary>
    /// Repairs IDs of newly created or duplicated definitions as soon as the assets are imported.
    /// </summary>
    public class DefinitionIdPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            string[] paths = importedAssets.Where(p => p.EndsWith(".asset")).ToArray();
            if (paths.Length == 0)
            {
                return;
            }

            // Assets can't be edited safely in the middle of an import, so the repair waits until it has finished.
            EditorApplication.delayCall += () => Repair(paths);
        }

        private static void Repair(string[] paths)
        {
            List<GameplayDefinition> imported = paths
                .Select(AssetDatabase.LoadAssetAtPath<GameplayDefinition>)
                .Where(d => d != null)
                .ToList();
            if (imported.Count == 0)
            {
                return;
            }

            List<GameplayDefinition> changed = DefinitionIdFixer.Fix(imported, DefinitionRegistryTools.FindAllDefinitions());
            foreach (GameplayDefinition definition in changed)
            {
                EditorUtility.SetDirty(definition);
                Debug.Log("Gave " + DefinitionRegistryTools.Describe(definition) + " a new definition ID.", definition);
            }

            if (changed.Count > 0)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }

    /// <summary>
    /// Fails a build when any gameplay definition has a missing or duplicate ID, or the definition registry is out of date,
    /// so a build never ships definitions the server and clients could disagree about.
    /// </summary>
    public class DefinitionBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> problems = DefinitionRegistryTools.ValidateForBuild();
            if (problems.Count == 0)
            {
                return;
            }

            throw new BuildFailedException(
                $"{problems.Count} gameplay definition problem(s) found.\n" +
                string.Join("\n", problems.Take(20)) +
                (problems.Count > 20 ? $"\n...and {problems.Count - 20} more." : string.Empty));
        }
    }
}
