using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// One rule a room broke, with the room it was found in.
    /// </summary>
    public readonly struct RoomValidationError
    {
        public RoomValidationError(string room, string rule, string message)
        {
            Room = room;
            Rule = rule;
            Message = message;
        }

        public string Room { get; }
        public string Rule { get; }
        public string Message { get; }

        public override string ToString() => $"{Room}: [{Rule}] {Message}";
    }

    /// <summary>
    /// Checks room authoring against the packing conventions, so bad data fails at pack time instead of producing
    /// doors that open onto walls. Pure: nothing is modified.
    /// Conventions come from Room_Hallway_Long: Size is the pivot span from the room's lowest corner, OriginOffset is never
    /// positive, and door locations are measured from that corner. How far a door sits in from its edge is a per-connection
    /// style choice, so it is only checked on doors that set <see cref="ConnectionBlueprint.EnforceDoorInset"/>.
    /// </summary>
    public static class RoomValidator
    {
        public const string RuleDoorOffGrid = "door-off-grid";
        public const string RuleDoorTargetInFootprint = "door-target-in-footprint";
        public const string RuleDoorInset = "door-inset";
        public const string RuleDoorDuplicate = "door-duplicate";
        public const string RuleDoorMissingSeal = "door-missing-seal";
        public const string RuleDoorStrayNested = "door-stray-nested";
        public const string RuleDoorNotAxisAligned = "door-not-axis-aligned";
        public const string RuleSizeVsGeometry = "size-vs-geometry";
        public const string RuleOriginOffset = "origin-offset";
        public const string RulePackedDoorData = "packed-door-data";
        public const string RuleRoomsetCellSize = "roomset-cell-size";
        public const string RuleRoomsetEntryNull = "roomset-entry-null";

        /// <summary>How far, in cells, a door pivot may sit from a grid point. Authored pillar doors are about 0.1 cells off.</summary>
        public const float GridTolerance = 0.25f;

        private const float AxisTolerance = 0.01f;

        /// <summary>
        /// Validates every room in a roomset, both the authored geometry and the values already packed onto it.
        /// </summary>
        /// <returns> Every problem found. Empty when the roomset is good to build.</returns>
        public static List<RoomValidationError> ValidateRoomset(Roomset roomset)
        {
            var errors = new List<RoomValidationError>();

            if (roomset.cellSize <= 0)
            {
                errors.Add(new RoomValidationError(roomset.name, RuleRoomsetCellSize, $"cellSize is {roomset.cellSize}; it must be greater than zero."));
                return errors;
            }

            foreach (RoomBlueprint room in roomset.RoomCollection)
            {
                if (room == null)
                {
                    errors.Add(new RoomValidationError(roomset.name, RuleRoomsetEntryNull, "The roomset has an empty room entry."));
                    continue;
                }

                errors.AddRange(Validate(room, roomset.cellSize));
                errors.AddRange(CheckPackedData(room, roomset.cellSize));
            }

            return errors;
        }

        /// <summary>
        /// Checks the authored geometry of a room: doors, their facing and their placement. Does not look at stored packed values.
        /// </summary>
        /// <param name="room"> The room to check.</param>
        /// <param name="cellSize"> The size of one world cell in unity units.</param>
        /// <returns> Every rule the room breaks. Empty when the room can be packed.</returns>
        public static List<RoomValidationError> Validate(RoomBlueprint room, float cellSize)
        {
            var errors = new List<RoomValidationError>();
            string roomName = room.name;

            RoomPlan plan = RoomPlanner.Plan(room, cellSize);
            Vector2Int origin = plan.OriginOffset;
            Vector2Int size = plan.Size;
            var doorLocations = new List<Vector2Int>();

            foreach (ConnectionBlueprint connection in room.GetComponentsInChildren<ConnectionBlueprint>())
            {
                bool isDirect = connection.transform.parent == room.transform;

                if (!IsAxisAligned(connection.transform.forward))
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorNotAxisAligned,
                        $"'{connection.name}' faces {connection.transform.forward}, which is not a flat axis direction."));
                }

                if (connection.SealingObject == null)
                {
                    // Nested doorway-wall blueprints are decorative and ignored by the packer.
                    if (isDirect)
                    {
                        errors.Add(new RoomValidationError(roomName, RuleDoorMissingSeal,
                            $"'{connection.name}' has no SealingObject, so it cannot be closed."));
                    }
                    continue;
                }

                if (!isDirect)
                {
                    // The packer would take this door, but it sits inside a sub-object, so its position is not what it looks like.
                    errors.Add(new RoomValidationError(roomName, RuleDoorStrayNested,
                        $"'{connection.name}' has a SealingObject but is nested under '{connection.transform.parent.name}'. Move it to the room root."));
                    continue;
                }

                Vector2 grid = RoomPlanner.GridPosition(room, connection.transform, cellSize);
                if (Mathf.Abs(grid.x - Mathf.Round(grid.x)) > GridTolerance || Mathf.Abs(grid.y - Mathf.Round(grid.y)) > GridTolerance)
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorOffGrid,
                        $"'{connection.name}' sits at {grid} cells, more than {GridTolerance} cells from the grid."));
                }

                Vector2Int location = RoomPlanner.DoorLocation(room, connection, cellSize, origin);
                Vector2Int forward = Forward(connection.transform.forward);
                doorLocations.Add(location);

                if (connection.EnforceDoorInset && !IsInsetCorrectly(location, forward, size, connection.DoorInsetCells))
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorInset,
                        $"'{connection.name}' at {location} facing {forward} should sit {connection.DoorInsetCells} cells in from its edge of size {size}."));
                }

                // The door opens onto the cell beyond it, using the same rule as Connection.FacingCell.
                int lengthCells = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int target = location + forward * lengthCells;
                Vector2Int facingCell = new Vector2Int(
                    forward.x < 0 ? target.x - 1 : target.x,
                    forward.y < 0 ? target.y - 1 : target.y);

                if (facingCell.x >= 0 && facingCell.x < size.x && facingCell.y >= 0 && facingCell.y < size.y)
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorTargetInFootprint,
                        $"'{connection.name}' opens onto cell {facingCell}, which is inside the room's own footprint of size {size}."));
                }
            }

            foreach (IGrouping<Vector2Int, Vector2Int> group in doorLocations.GroupBy(location => location).Where(group => group.Count() > 1))
            {
                errors.Add(new RoomValidationError(roomName, RuleDoorDuplicate,
                    $"{group.Count()} doors share the location {group.Key}."));
            }

            return errors;
        }

        /// <summary>
        /// Checks the values stored on a room by the packer (OriginOffset, Size and the packed door list) against the geometry.
        /// Catches prefabs that were edited after they were packed, which is how Room_Hallway_Long ended up with stale doors.
        /// </summary>
        /// <param name="room"> The room to check.</param>
        /// <param name="cellSize"> The size of one world cell in unity units.</param>
        /// <returns> Every stored value that disagrees with the geometry. Empty when the packed data is current.</returns>
        public static List<RoomValidationError> CheckPackedData(RoomBlueprint room, float cellSize)
        {
            var errors = new List<RoomValidationError>();
            string roomName = room.name;

            RoomPlan plan = RoomPlanner.Plan(room, cellSize);

            if (room.OriginOffset.x > 0 || room.OriginOffset.y > 0 || room.OriginOffset != plan.OriginOffset)
            {
                errors.Add(new RoomValidationError(roomName, RuleOriginOffset,
                    $"OriginOffset is {room.OriginOffset} but the geometry gives {plan.OriginOffset}. Pack the room again."));
            }

            if (room.Size != plan.Size)
            {
                errors.Add(new RoomValidationError(roomName, RuleSizeVsGeometry,
                    $"Size is {room.Size} but the geometry gives {plan.Size}. Pack the room again."));
            }

            Dictionary<ConnectionBlueprint, Vector2Int> expected = plan.Doors.ToDictionary(door => door.Door, door => door.Location);
            List<ConnectionBlueprint> stored = room.Connections.ToList();

            bool stale = stored.Count != expected.Count
                || stored.Any(connection => !expected.TryGetValue(connection, out Vector2Int location) || connection.location != location);

            if (stale)
            {
                errors.Add(new RoomValidationError(roomName, RulePackedDoorData,
                    $"The packed door list has {stored.Count} entries but the geometry has {expected.Count}. Pack the room again."));
            }

            return errors;
        }

        private static bool IsInsetCorrectly(Vector2Int location, Vector2Int forward, Vector2Int size, int inset)
        {
            if (forward.x > 0) return location.x == size.x - inset;
            if (forward.x < 0) return location.x == inset;
            if (forward.y > 0) return location.y == size.y - inset;
            return location.y == inset;
        }

        private static bool IsAxisAligned(Vector3 direction)
        {
            if (Mathf.Abs(direction.y) > AxisTolerance)
            {
                return false;
            }

            bool alongX = Mathf.Abs(Mathf.Abs(direction.x) - 1) < AxisTolerance && Mathf.Abs(direction.z) < AxisTolerance;
            bool alongZ = Mathf.Abs(Mathf.Abs(direction.z) - 1) < AxisTolerance && Mathf.Abs(direction.x) < AxisTolerance;
            return alongX || alongZ;
        }

        private static Vector2Int Forward(Vector3 direction)
        {
            return new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.z));
        }
    }
}
