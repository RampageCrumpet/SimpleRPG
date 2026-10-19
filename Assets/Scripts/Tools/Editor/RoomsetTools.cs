using LevelGeneration;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    #if UNITY_EDITOR
    /// <summary>
    /// Editor and command line entry points for checking and packing roomsets. They run <see cref="RoomValidator"/> and
    /// <see cref="RoomPacker"/> and decide how their results are shown; the inspector only draws the buttons.
    /// </summary>
    public static class RoomsetTools
    {
        /// <summary>
        /// Validates a roomset and logs every problem, or a confirmation when there are none.
        /// </summary>
        /// <returns> The problems found. Empty when the roomset is good to build.</returns>
        public static List<RoomValidationError> ValidateAndLog(Roomset roomset)
        {
            List<RoomValidationError> errors = RoomValidator.ValidateRoomset(roomset);
            LogValidation(errors);
            return errors;
        }

        /// <summary>
        /// Packs a roomset all or nothing and logs the outcome.
        /// </summary>
        /// <returns> True if every room was packed.</returns>
        public static bool PackAndLog(Roomset roomset)
        {
            List<PackResult> results = RoomPacker.PackRoomset(roomset);
            List<PackResult> refused = results.Where(result => !result.Packed).ToList();

            foreach (PackResult result in refused)
            {
                foreach (RoomValidationError error in result.Errors)
                {
                    Debug.LogError(error, result.Room);
                }
            }

            if (refused.Count == 0)
            {
                Debug.Log($"Packed {results.Count} room(s) from {roomset.name}.", roomset);
                return true;
            }

            Debug.LogError($"Nothing was packed from {roomset.name}: {refused.Sum(result => result.Errors.Count)} problem(s) found. See the errors above.", roomset);
            return false;
        }

        /// <summary>
        /// Every roomset asset in the project.
        /// </summary>
        public static List<Roomset> FindRoomsets()
        {
            return AssetDatabase.FindAssets("t:Roomset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<Roomset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(roomset => roomset != null)
                .ToList();
        }

        /// <summary>
        /// The roomsets that end up in a build: those the enabled build scenes depend on, directly or through prefabs.
        /// Roomsets nothing in the build refers to (old experiments, test sets) are left out, so they cannot fail a build they are not part of.
        /// </summary>
        public static List<Roomset> FindShippedRoomsets()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

            return AssetDatabase.GetDependencies(scenes, true)
                .Where(path => path.EndsWith(".asset"))
                .Select(path => AssetDatabase.LoadAssetAtPath<Roomset>(path))
                .Where(roomset => roomset != null)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// The first roomset that lists a room, or null if the room is not part of any.
        /// </summary>
        public static Roomset FindRoomsetContaining(RoomBlueprint room)
        {
            return FindRoomsets().FirstOrDefault(roomset => roomset.RoomCollection != null && roomset.RoomCollection.Contains(room));
        }

        /// <summary>
        /// Validates roomsets for a build, so stale or invalid room data cannot ship.
        /// </summary>
        /// <returns> Every problem found across the roomsets, each with its roomset named in the message. Empty when all are good.</returns>
        public static List<string> ValidateForBuild(IEnumerable<Roomset> roomsets)
        {
            var problems = new List<string>();

            foreach (Roomset roomset in roomsets)
            {
                foreach (RoomValidationError error in RoomValidator.ValidateRoomset(roomset))
                {
                    problems.Add($"{roomset.name}: {error}");
                }
            }

            return problems;
        }

        /// <summary>
        /// Batch entry point. Run with <c>-batchmode -quit -projectPath ... -executeMethod SimpleRPG.EditorTools.RoomsetTools.ValidateRoomsetFromCommandLine -roomset Assets/...asset</c>.
        /// Exits with code 0 when the roomset is valid, 1 when it has problems and 2 when the roomset could not be loaded.
        /// </summary>
        public static void ValidateRoomsetFromCommandLine()
        {
            string roomsetPath = GetCommandLineValue("-roomset");
            Roomset roomset = roomsetPath == null ? null : AssetDatabase.LoadAssetAtPath<Roomset>(roomsetPath);

            if (roomset == null)
            {
                Debug.LogError($"Could not load a Roomset from '{roomsetPath}'. Pass -roomset with the asset path.");
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(ValidateAndLog(roomset).Count == 0 ? 0 : 1);
        }

        private static void LogValidation(List<RoomValidationError> errors)
        {
            foreach (RoomValidationError error in errors)
            {
                Debug.LogError(error);
            }

            if (errors.Count == 0)
            {
                Debug.Log("Roomset validation passed.");
            }
        }

        private static string GetCommandLineValue(string flag)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
    #endif
}
