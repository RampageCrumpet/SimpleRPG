using SimpleRPG.LevelGeneration;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    #if UNITY_EDITOR
    /// <summary>
    /// What happened when a room, or a whole roomset, was packed.
    /// </summary>
    public sealed class PackResult
    {
        public PackResult(RoomBlueprint room, bool packed, IReadOnlyList<RoomValidationError> errors)
        {
            Room = room;
            Packed = packed;
            Errors = errors;
        }

        /// <summary>
        /// The room this result is for. Null for problems with a roomset as a whole.
        /// </summary>
        public RoomBlueprint Room { get; }

        /// <summary>
        /// True if the asset was saved. A refused asset is left untouched.
        /// </summary>
        public bool Packed { get; }

        /// <summary>
        /// Why the room was refused. Empty when it was packed.
        /// </summary>
        public IReadOnlyList<RoomValidationError> Errors { get; }
    }

    /// <summary>
    /// Writes packed values onto room prefabs. A room is only saved when a working copy has passed validation,
    /// has been packed in memory and has passed the packed-data check again, so a failed pack never changes the asset.
    /// Reports through <see cref="PackResult"/> and never logs, so callers decide how to show problems.
    /// </summary>
    public static class RoomPacker
    {
        /// <summary>
        /// Packs one room prefab asset.
        /// </summary>
        /// <param name="room"> The room as it appears in a roomset. Must be a prefab asset.</param>
        /// <param name="cellSize"> The size of one world cell in unity units.</param>
        public static PackResult PackRoom(RoomBlueprint room, float cellSize)
        {
            string assetPath = AssetDatabase.GetAssetPath(room.gameObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                return Refused(room, new RoomValidationError(room.name, RuleNotAnAsset, $"{room.name} is not a prefab asset, so it cannot be packed."));
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                RoomBlueprint working = contents.GetComponent<RoomBlueprint>();

                List<RoomValidationError> errors = RoomValidator.Validate(working, cellSize);
                if (errors.Count > 0)
                {
                    return Refused(room, errors);
                }

                RoomPlan plan = RoomPlanner.Plan(working, cellSize);
                Apply(working, plan);

                // Check what was written, not just what was planned, before anything is saved.
                errors = RoomValidator.Validate(working, cellSize);
                errors.AddRange(RoomValidator.CheckPackedData(working, cellSize));
                if (errors.Count > 0)
                {
                    return Refused(room, errors);
                }

                if (!PrefabUtility.SaveAsPrefabAsset(contents, assetPath))
                {
                    return Refused(room, new RoomValidationError(room.name, RuleSaveFailed, $"{room.name} could not be saved to {assetPath}."));
                }

                return new PackResult(room, true, new RoomValidationError[0]);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Packs every room in a roomset, all or nothing: every room and the roomset as a whole are validated first, and if
        /// anything is wrong no asset is touched, so a roomset is never left half packed under two conventions.
        /// </summary>
        /// <returns> One result per room, in roomset order. When the roomset was refused none is packed, and a refusal that belongs to
        /// the roomset itself is reported on an extra result with no room.</returns>
        public static List<PackResult> PackRoomset(Roomset roomset)
        {
            var results = new List<PackResult>();
            List<RoomBlueprint> rooms = roomset.RoomCollection?.ToList() ?? new List<RoomBlueprint>();

            // Packed values are not checked here: they are about to be rewritten.
            var roomsetErrors = new List<RoomValidationError>();
            if (roomset.cellSize <= 0)
            {
                roomsetErrors.Add(new RoomValidationError(roomset.name, RoomValidator.RuleRoomsetCellSize, $"cellSize is {roomset.cellSize}; it must be greater than zero."));
            }
            else if (rooms.Count == 0)
            {
                roomsetErrors.Add(new RoomValidationError(roomset.name, RoomValidator.RuleRoomsetEmpty, "The roomset has no rooms."));
            }
            else
            {
                roomsetErrors.AddRange(RoomValidator.CheckRoomsetComposition(roomset));
            }

            bool refused = roomsetErrors.Count > 0;
            foreach (RoomBlueprint room in rooms)
            {
                if (room == null)
                {
                    refused = true;
                    results.Add(new PackResult(null, false, new[] { new RoomValidationError(roomset.name, RoomValidator.RuleRoomsetEntryNull, "The roomset has an empty room entry.") }));
                    continue;
                }

                List<RoomValidationError> errors = roomset.cellSize > 0 ? RoomValidator.Validate(room, roomset.cellSize) : new List<RoomValidationError>();
                refused |= errors.Count > 0;
                results.Add(new PackResult(room, false, errors));
            }

            if (roomsetErrors.Count > 0)
            {
                results.Add(new PackResult(null, false, roomsetErrors));
            }

            if (refused)
            {
                return results;
            }

            // Everything passed, so write them all. Batching keeps the asset database from reimporting after every save.
            results.Clear();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (RoomBlueprint room in rooms)
                {
                    results.Add(PackRoom(room, roomset.cellSize));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return results;
        }

        public const string RuleNotAnAsset = "not-an-asset";
        public const string RuleSaveFailed = "save-failed";

        /// <summary>
        /// Writes a plan onto a room. Only called on a working copy that has already passed validation.
        /// </summary>
        private static void Apply(RoomBlueprint room, RoomPlan plan)
        {
            room.gameObject.transform.position = Vector3.zero;
            room.OriginOffset = plan.OriginOffset;
            room.Size = plan.Size;
            room.ExtraCells = plan.ExtraCells.ToList();

            room.SetConnections(plan.Doors.Select(door => door.Door));

            foreach (DoorPlan door in plan.Doors)
            {
                door.Door.location = door.Location;
                EditorUtility.SetDirty(door.Door);
            }

            EditorUtility.SetDirty(room);
        }

        private static PackResult Refused(RoomBlueprint room, params RoomValidationError[] errors)
        {
            return new PackResult(room, false, errors);
        }

        private static PackResult Refused(RoomBlueprint room, List<RoomValidationError> errors)
        {
            return new PackResult(room, false, errors);
        }
    }
    #endif
}
