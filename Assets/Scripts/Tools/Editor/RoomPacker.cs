using LevelGeneration;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityEditor.EditorTools
{
    #if UNITY_EDITOR
    /// <summary>
    /// Writes packed values onto a room prefab. A room is only saved when a working copy has passed validation,
    /// has been packed in memory and has passed the packed-data check again, so a failed pack never changes the asset.
    /// </summary>
    public static class RoomPacker
    {
        /// <summary>
        /// Packs one room prefab asset.
        /// </summary>
        /// <param name="room"> The room as it appears in a roomset. Must be a prefab asset.</param>
        /// <param name="cellSize"> The size of one world cell in unity units.</param>
        /// <returns> True if the asset was saved, false if it was refused. A refused asset is left untouched.</returns>
        public static bool PackRoom(RoomBlueprint room, float cellSize)
        {
            string assetPath = AssetDatabase.GetAssetPath(room.gameObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"{room.name} is not a prefab asset, so it cannot be packed.", room);
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                RoomBlueprint working = contents.GetComponent<RoomBlueprint>();

                List<RoomValidationError> errors = RoomValidator.Validate(working, cellSize);
                if (errors.Count > 0)
                {
                    LogErrors(errors);
                    Debug.LogError($"{room.name} was not packed because it has {errors.Count} problem(s).", room);
                    return false;
                }

                RoomPlan plan = RoomPlanner.Plan(working, cellSize);
                Apply(working, plan);

                // Check what was written, not just what was planned, before anything is saved.
                errors = RoomValidator.Validate(working, cellSize);
                errors.AddRange(RoomValidator.CheckPackedData(working, cellSize));
                if (errors.Count > 0)
                {
                    LogErrors(errors);
                    Debug.LogError($"{room.name} was not saved because the packed result failed its check.", room);
                    return false;
                }

                if (!PrefabUtility.SaveAsPrefabAsset(contents, assetPath))
                {
                    Debug.LogError($"{room.name} could not be saved to {assetPath}.", room);
                    return false;
                }

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Writes a plan onto a room. Only called on a working copy that has already passed validation.
        /// </summary>
        private static void Apply(RoomBlueprint room, RoomPlan plan)
        {
            room.gameObject.transform.position = Vector3.zero;
            room.OriginOffset = plan.OriginOffset;
            room.Size = plan.Size;

            PropertyInfo connectionsProperty = typeof(RoomBlueprint).GetProperty(nameof(RoomBlueprint.Connections));
            connectionsProperty.SetValue(room, plan.Doors.Select(door => door.Door).ToList());

            foreach (DoorPlan door in plan.Doors)
            {
                door.Door.location = door.Location;
                EditorUtility.SetDirty(door.Door);
            }

            EditorUtility.SetDirty(room);
        }

        private static void LogErrors(List<RoomValidationError> errors)
        {
            foreach (RoomValidationError error in errors)
            {
                Debug.LogError(error);
            }
        }
    }
    #endif
}
