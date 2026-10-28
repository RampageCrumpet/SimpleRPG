using SimpleRPG.LevelGeneration;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    #if UNITY_EDITOR
    /// <summary>
    /// Re-packs a room prefab when it is saved with stale packed data, so the values the generator reads never lag behind the geometry.
    /// Only rooms that belong to a roomset are touched, and only when they pass validation: a room with real problems is reported, not rewritten.
    /// Switch it off under Tools > Rooms > Auto-pack changed rooms.
    /// </summary>
    public class RoomAutoPacker : AssetPostprocessor
    {
        private const string MenuPath = "Tools/Rooms/Auto-pack changed rooms";
        private const string PreferenceKey = "SimpleRPG.AutoPackRooms";

        private static bool packing;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set => EditorPrefs.SetBool(PreferenceKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleChecked()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!Enabled || packing)
            {
                return;
            }

            List<string> prefabs = imported.Where(path => path.EndsWith(".prefab")).ToList();
            if (prefabs.Count == 0)
            {
                return;
            }

            // Packing saves assets, which must not happen in the middle of an import.
            EditorApplication.delayCall += () => PackStaleRooms(prefabs);
        }

        private static void PackStaleRooms(List<string> prefabPaths)
        {
            if (packing)
            {
                return;
            }

            packing = true;
            try
            {
                foreach (string path in prefabPaths)
                {
                    RoomBlueprint room = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<RoomBlueprint>();
                    Roomset roomset = room == null ? null : RoomsetTools.FindRoomsetContaining(room);
                    if (roomset == null || roomset.cellSize <= 0)
                    {
                        continue;
                    }

                    if (RoomValidator.CheckPackedData(room, roomset.cellSize).Count == 0)
                    {
                        continue;
                    }

                    PackResult result = RoomPacker.PackRoom(room, roomset.cellSize);
                    if (result.Packed)
                    {
                        Debug.Log($"Re-packed {room.name} because its packed data was out of date.", room);
                    }
                    else
                    {
                        foreach (RoomValidationError error in result.Errors)
                        {
                            Debug.LogError($"{room.name} changed but could not be re-packed: {error}", room);
                        }
                    }
                }
            }
            finally
            {
                packing = false;
            }
        }
    }
    #endif
}
