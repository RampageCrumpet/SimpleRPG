using LevelGeneration;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.EditorTools
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(Roomset))]
    public class RoomsetCustomInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Roomset roomset = (Roomset)target;

            if (GUILayout.Button("Validate Roomset"))
            {
                LogValidation(RoomValidator.ValidateRoomset(roomset));
            }

            if (GUILayout.Button("Package Rooms"))
            {
                foreach (RoomBlueprint room in roomset.RoomCollection)
                {
                    PackageRoom(room, roomset.cellSize);
                }
            }

        }

        /// <summary>
        /// Batch entry point. Run with <c>-batchmode -quit -projectPath ... -executeMethod UnityEditor.EditorTools.RoomsetCustomInspector.ValidateRoomsetFromCommandLine -roomset Assets/...asset</c>.
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

            List<RoomValidationError> errors = RoomValidator.ValidateRoomset(roomset);
            LogValidation(errors);
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// Packs a room prefab. See <see cref="RoomPacker.PackRoom"/>.
        /// </summary>
        /// <returns> True if the room was packed, false if it was refused.</returns>
        public static bool PackageRoom(RoomBlueprint room, float cellSize)
        {
            return RoomPacker.PackRoom(room, cellSize);
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
