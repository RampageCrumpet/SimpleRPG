using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
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
    /// positive, and door locations are measured from that corner.
    /// </summary>
    public static class RoomValidator
    {
        public const string RuleDoorOffGrid = "door-off-grid";
        public const string RuleDoorTargetInFootprint = "door-target-in-footprint";
        public const string RuleDoorDuplicate = "door-duplicate";
        public const string RuleDoorMissingSeal = "door-missing-seal";
        public const string RuleDoorStrayNested = "door-stray-nested";
        public const string RuleDoorNotAxisAligned = "door-not-axis-aligned";
        public const string RuleSizeVsGeometry = "size-vs-geometry";
        public const string RuleOriginOffset = "origin-offset";
        public const string RulePackedDoorData = "packed-door-data";
        public const string RuleRoomsetCellSize = "roomset-cell-size";
        public const string RuleRoomsetEntryNull = "roomset-entry-null";
        public const string RuleRootTransform = "root-transform";
        public const string RuleDoorLength = "door-length";
        public const string RuleDoorWidth = "door-width";
        public const string RuleSealOutsideRoom = "seal-outside-room";
        public const string RuleSealStartsActive = "seal-starts-active";
        public const string RuleLockOutsideRoom = "lock-outside-room";
        public const string RuleLockStartsActive = "lock-starts-active";
        public const string RuleRoomsetDoorNoLock = "roomset-door-no-lock";
        public const string RuleStrayPivot = "stray-pivot";
        public const string RuleGeometryOutsideFootprint = "geometry-outside-footprint";
        public const string RuleGeometryShortOfFootprint = "geometry-short-of-footprint";
        public const string RuleClaimedCellInsideFootprint = "claimed-cell-inside-footprint";
        public const string RuleClaimedCellDetached = "claimed-cell-detached";
        public const string RulePackedClaimedCells = "packed-claimed-cells";
        public const string RuleDoorFlushNeedsDoorObject = "door-flush-needs-door-object";
        public const string RuleDoorObjectOutsideRoom = "door-object-outside-room";
        public const string RuleRoomsetEmpty = "roomset-empty";
        public const string RuleRoomsetDuplicate = "roomset-duplicate";
        public const string RuleRoomsetRoomWeight = "roomset-room-weight";
        public const string RuleRoomsetMinCount = "roomset-min-count";
        public const string RuleRoomsetExitDoors = "roomset-exit-doors";
        public const string RuleRoomsetRoomNoDoors = "roomset-room-no-doors";
        public const string RuleRoomsetNoDeadEnd = "roomset-no-dead-end";
        public const string RuleRoomsetNothingPlaceable = "roomset-nothing-placeable";
        public const string RuleRoomsetDoorLengths = "roomset-door-lengths";
        public const string RuleRoomsetConnectionTypeClash = "roomset-connection-type-clash";
        public const string RuleRoomFloors = "room-floors";
        public const string RuleDoorLevel = "door-level";
        public const string RuleRoomsetFloorHeight = "roomset-floor-height";
        public const string RuleRoomsetVerticalUnpaired = "roomset-vertical-unpaired";

        /// <summary>
        /// How far, in cells, a door pivot may sit from a grid point. The packer snaps the door to the point, so whatever is left over is
        /// a seam where two joined rooms meet; keep it to modelling noise.
        /// </summary>
        public const float GridTolerance = 0.02f;

        /// <summary>
        /// How far, in cells, the room's drawn geometry may differ from its footprint on any side. Past the footprint it overlaps the
        /// neighbouring room and z-fights; short of it, it leaves a gap.
        /// </summary>
        public const float GeometryTolerance = 0.02f;

        private const float AxisTolerance = 0.01f;

        /// <summary>How far, in cells, a door length may be from a whole number of cells.</summary>
        private const float LengthTolerance = 0.01f;

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

            if (roomset.RoomCollection == null || roomset.RoomCollection.Count == 0)
            {
                errors.Add(new RoomValidationError(roomset.name, RuleRoomsetEmpty, "The roomset has no rooms."));
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

            bool spansFloors = roomset.RoomCollection.Any(room => room != null
                && (room.Floors > 1 || RoomPlanner.SealedDoors(room).Any(door => door.FloorDelta != 0)));
            if (spansFloors && roomset.floorHeight <= 0)
            {
                errors.Add(new RoomValidationError(roomset.name, RuleRoomsetFloorHeight,
                    $"floorHeight is {roomset.floorHeight}; a roomset with rooms or doors that span floors needs it greater than zero."));
            }

            errors.AddRange(CheckRoomsetComposition(roomset));
            return errors;
        }

        /// <summary>
        /// Lists the doors that have no <see cref="ConnectionBlueprint.LockObject"/>. This is a warning, never an error: the dungeon
        /// still builds, because a locked door without its own look falls back to a plain barrier. It is kept out of
        /// <see cref="ValidateRoomset"/> so it cannot fail a build. A LockObject that is set but wrong is an error in <see cref="Validate"/>.
        /// </summary>
        /// <returns> One warning per room that has doors without a lock object. Empty when every door has one.</returns>
        public static List<RoomValidationError> CheckLockWarnings(Roomset roomset)
        {
            var warnings = new List<RoomValidationError>();
            if (roomset.RoomCollection == null)
            {
                return warnings;
            }

            foreach (RoomBlueprint room in roomset.RoomCollection.Where(room => room != null))
            {
                List<string> bare = RoomPlanner.SealedDoors(room).Where(door => door.LockObject == null).Select(door => $"'{door.name}'").ToList();
                if (bare.Count > 0)
                {
                    warnings.Add(new RoomValidationError(room.name, RuleRoomsetDoorNoLock,
                        $"{bare.Count} door(s) have no LockObject ({string.Join(", ", bare)}); if the generator locks one it gets a plain barrier instead of the room's own look."));
                }
            }

            return warnings;
        }

        /// <summary>
        /// Checks that a roomset as a whole can build a dungeon: every room has doors, something can close a branch, and doors can join each other.
        /// Looks only at the rooms' own doors and policy fields, so it can run before any room is packed.
        /// </summary>
        public static List<RoomValidationError> CheckRoomsetComposition(Roomset roomset)
        {
            var errors = new List<RoomValidationError>();
            List<RoomBlueprint> rooms = roomset.RoomCollection.Where(room => room != null).ToList();
            string name = roomset.name;

            foreach (IGrouping<RoomBlueprint, RoomBlueprint> duplicate in rooms.GroupBy(room => room).Where(group => group.Count() > 1))
            {
                errors.Add(new RoomValidationError(name, RuleRoomsetDuplicate,
                    $"'{duplicate.Key.name}' is in the roomset {duplicate.Count()} times, which only makes it more likely to be picked. Use its weight instead."));
            }

            var doorLengthsByType = new Dictionary<int, HashSet<int>>();
            var typesById = new Dictionary<int, HashSet<ConnectionType>>();
            var climbsByType = new Dictionary<int, HashSet<int>>();
            bool hasPlaceableDeadEnd = false;
            bool hasPlaceableRoom = false;

            foreach (RoomBlueprint room in rooms)
            {
                if (room.SelectionWeight < 0 || room.MaxCount < 0)
                {
                    errors.Add(new RoomValidationError(room.name, RuleRoomsetRoomWeight,
                        $"SelectionWeight is {room.SelectionWeight} and MaxCount is {room.MaxCount}; neither may be negative."));
                }

                if (room.MinCount < 0 || (room.MaxCount > 0 && room.MinCount > room.MaxCount) || (room.MinCount > 0 && (room.Role & (RoomRole.Start | RoomRole.Exit)) != 0))
                {
                    errors.Add(new RoomValidationError(room.name, RuleRoomsetMinCount,
                        $"MinCount is {room.MinCount} with MaxCount {room.MaxCount}; it may not be negative or above the maximum, and a start or exit room appears once by definition."));
                }

                List<ConnectionBlueprint> doors = RoomPlanner.SealedDoors(room).ToList();
                if ((room.Role & RoomRole.Exit) != 0 && doors.Count != 1)
                {
                    errors.Add(new RoomValidationError(room.name, RuleRoomsetExitDoors,
                        $"An exit room replaces a dead end, so it needs exactly one door but has {doors.Count}."));
                }

                if (doors.Count == 0)
                {
                    errors.Add(new RoomValidationError(room.name, RuleRoomsetRoomNoDoors, "The room has no doors, so nothing can be built from or onto it."));
                }

                foreach (ConnectionBlueprint door in doors)
                {
                    int typeId = ConnectionType.IdFor(door.Type);
                    if (!doorLengthsByType.TryGetValue(typeId, out HashSet<int> lengths))
                    {
                        doorLengthsByType[typeId] = lengths = new HashSet<int>();
                        typesById[typeId] = new HashSet<ConnectionType>();
                    }

                    lengths.Add(Mathf.RoundToInt(door.ConnectionLength / roomset.cellSize));
                    typesById[typeId].Add(door.Type);

                    if (door.FloorDelta != 0)
                    {
                        if (!climbsByType.TryGetValue(typeId, out HashSet<int> climbs))
                        {
                            climbsByType[typeId] = climbs = new HashSet<int>();
                        }

                        climbs.Add(door.FloorDelta);
                    }
                }

                bool placeable = room.SelectionWeight > 0 && (room.Role & (RoomRole.Start | RoomRole.Exit)) == 0 && doors.Count > 0;
                hasPlaceableRoom |= placeable;
                hasPlaceableDeadEnd |= placeable && doors.Count == 1;
            }

            if (rooms.Count > 0 && !hasPlaceableRoom)
            {
                errors.Add(new RoomValidationError(name, RuleRoomsetNothingPlaceable,
                    "Every room is a start room, has a weight of zero or has no doors, so no room can be added after the first."));
            }
            else if (rooms.Count > 0 && !hasPlaceableDeadEnd)
            {
                errors.Add(new RoomValidationError(name, RuleRoomsetNoDeadEnd,
                    "No room with a single door can be placed, so branches can only be ended by sealing them."));
            }

            foreach (KeyValuePair<int, HashSet<int>> type in doorLengthsByType.Where(pair => pair.Value.Count > 1))
            {
                string typeName = typesById[type.Key].First()?.name ?? "standard";
                errors.Add(new RoomValidationError(name, RuleRoomsetDoorLengths,
                    $"'{typeName}' doors have different lengths ({string.Join(", ", type.Value.OrderBy(length => length))} cells). Only doors of the same type and length join, so each type needs one length."));
            }

            foreach (KeyValuePair<int, HashSet<int>> climbs in climbsByType)
            {
                // A vertical door joins a door of the same type that climbs the other way by the same amount.
                if (climbs.Value.Any(delta => !climbs.Value.Contains(-delta)))
                {
                    string typeName = typesById[climbs.Key].First()?.name ?? "standard";
                    errors.Add(new RoomValidationError(name, RuleRoomsetVerticalUnpaired,
                        $"'{typeName}' doors climb {string.Join(", ", climbs.Value.OrderBy(delta => delta))} floors, but no door climbs the opposite way, so some can never join."));
                }
            }

            foreach (KeyValuePair<int, HashSet<ConnectionType>> clash in typesById.Where(pair => pair.Value.Count > 1))
            {
                errors.Add(new RoomValidationError(name, RuleRoomsetConnectionTypeClash,
                    $"Connection types {string.Join(", ", clash.Value.Select(type => type == null ? "standard" : $"'{type.name}'"))} have the same id, so they would join each other. Rename one."));
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

            CheckRootTransform(room, errors);
            CheckStrayPivots(room, cellSize, errors);
            CheckClaimedCells(room, plan, errors);
            CheckGeometryFit(room, cellSize, plan, errors);

            if (room.Floors < 1)
            {
                errors.Add(new RoomValidationError(roomName, RuleRoomFloors, $"Floors is {room.Floors}; a room is at least one floor high."));
            }

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

                CheckDoorProperties(room, connection, cellSize, errors);

                Vector2 grid = RoomPlanner.GridPosition(room, connection.transform, cellSize);
                if (Mathf.Abs(grid.x - Mathf.Round(grid.x)) > GridTolerance || Mathf.Abs(grid.y - Mathf.Round(grid.y)) > GridTolerance)
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorOffGrid,
                        $"'{connection.name}' sits at {grid} cells, more than {GridTolerance} cells from the grid."));
                }

                Vector2Int location = RoomPlanner.DoorLocation(room, connection, cellSize, origin);
                Vector2Int forward = Forward(connection.transform.forward);
                doorLocations.Add(location);

                // The door opens onto the cell beyond it, using the same rule as Connection.FacingCell.
                int lengthCells = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int target = location + forward * lengthCells;
                Vector2Int facingCell = new Vector2Int(
                    forward.x < 0 ? target.x - 1 : target.x,
                    forward.y < 0 ? target.y - 1 : target.y);

                if ((facingCell.x >= 0 && facingCell.x < size.x && facingCell.y >= 0 && facingCell.y < size.y) || plan.ExtraCells.Contains(facingCell))
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorTargetInFootprint,
                        $"'{connection.name}' opens onto cell {facingCell}, which is inside the room's own footprint of size {size} (or a cell it claims)."));
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

            if (!new HashSet<Vector2Int>(room.ExtraCells ?? new List<Vector2Int>()).SetEquals(plan.ExtraCells))
            {
                errors.Add(new RoomValidationError(roomName, RulePackedClaimedCells,
                    $"The packed claimed cells ({room.ExtraCells?.Count ?? 0}) do not match the RoomClaimedCell markers ({plan.ExtraCells.Count}). Pack the room again."));
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

        /// <summary>
        /// The packer measures everything from the root's position and ignores its rotation and scale, so both must be neutral.
        /// </summary>
        private static void CheckRootTransform(RoomBlueprint room, List<RoomValidationError> errors)
        {
            bool turned = Quaternion.Angle(room.transform.rotation, Quaternion.identity) > 0.01f;
            bool scaled = (room.transform.lossyScale - Vector3.one).sqrMagnitude > 1e-6f;

            if (turned || scaled)
            {
                errors.Add(new RoomValidationError(room.name, RuleRootTransform,
                    $"The room root is rotated {room.transform.rotation.eulerAngles} or scaled {room.transform.lossyScale}; it must have identity rotation and scale 1."));
            }
        }

        /// <summary>
        /// A door's length has to be a whole number of cells, because doors join at whole-cell distances, and it needs a width and a wall to close it with.
        /// </summary>
        private static void CheckDoorProperties(RoomBlueprint room, ConnectionBlueprint connection, float cellSize, List<RoomValidationError> errors)
        {
            string roomName = room.name;
            float lengthInCells = connection.ConnectionLength / cellSize;

            if (lengthInCells < -LengthTolerance || Mathf.Abs(lengthInCells - Mathf.Round(lengthInCells)) > LengthTolerance)
            {
                errors.Add(new RoomValidationError(roomName, RuleDoorLength,
                    $"'{connection.name}' is {connection.ConnectionLength} units long, which is {lengthInCells} cells. It must be a whole number of cells; zero joins flush with the next room."));
            }

            // A flush join puts two doors on the same plane. Both rooms draw their own door, so one of them has to be switched off or they z-fight.
            if (Mathf.RoundToInt(lengthInCells) == 0)
            {
                GameObject doorObject = connection.DoorObject;
                if (doorObject == null)
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorFlushNeedsDoorObject,
                        $"'{connection.name}' is zero cells long, so it joins flush against another room's door. Set its DoorObject so the generator can hide one of the pair."));
                }
                else if (!doorObject.transform.IsChildOf(room.transform))
                {
                    errors.Add(new RoomValidationError(roomName, RuleDoorObjectOutsideRoom,
                        $"The DoorObject of '{connection.name}' is not part of this room, so hiding it would change something else."));
                }
            }

            if (connection.Level < 0 || connection.Level >= Mathf.Max(1, room.Floors))
            {
                errors.Add(new RoomValidationError(roomName, RuleDoorLevel,
                    $"'{connection.name}' is on level {connection.Level} but the room has {room.Floors} floor(s); the level must be from 0 to {Mathf.Max(1, room.Floors) - 1}."));
            }

            if (connection.ConnectionWidth <= 0f)
            {
                errors.Add(new RoomValidationError(roomName, RuleDoorWidth, $"'{connection.name}' has a width of {connection.ConnectionWidth}; it must be greater than zero."));
            }

            GameObject seal = connection.SealingObject;
            if (!seal.transform.IsChildOf(room.transform))
            {
                errors.Add(new RoomValidationError(roomName, RuleSealOutsideRoom,
                    $"The SealingObject of '{connection.name}' is not part of this room, so sealing the door would change something else."));
            }
            else if (seal.activeSelf)
            {
                errors.Add(new RoomValidationError(roomName, RuleSealStartsActive,
                    $"The SealingObject of '{connection.name}' starts active, which would close the door before anything seals it."));
            }

            GameObject lockObject = connection.LockObject;
            if (lockObject != null)
            {
                if (!lockObject.transform.IsChildOf(room.transform))
                {
                    errors.Add(new RoomValidationError(roomName, RuleLockOutsideRoom,
                        $"The LockObject of '{connection.name}' is not part of this room, so locking the door would change something else."));
                }
                else if (lockObject.activeSelf)
                {
                    errors.Add(new RoomValidationError(roomName, RuleLockStartsActive,
                        $"The LockObject of '{connection.name}' starts active, which would lock the door before the generator decides to."));
                }
            }
        }

        /// <summary>
        /// Size comes from the furthest transform pivots, so a stray empty object far outside the geometry inflates the room.
        /// Flags transforms that sit more than a cell beyond everything that is actually drawn.
        /// </summary>
        private static void CheckStrayPivots(RoomBlueprint room, float cellSize, List<RoomValidationError> errors)
        {
            if (!TryGetMeshBounds(room, out Bounds drawn))
            {
                return;
            }

            Vector3 root = room.transform.position;
            Vector3 min = drawn.min - Vector3.one * cellSize;
            Vector3 max = drawn.max + Vector3.one * cellSize;

            foreach (Transform child in room.GetComponentsInChildren<Transform>())
            {
                Vector3 position = child.position;
                bool outside = position.x < min.x || position.x > max.x || position.z < min.z || position.z > max.z;

                if (child != room.transform && outside && child.GetComponent<RoomClaimedCell>() == null)
                {
                    errors.Add(new RoomValidationError(room.name, RuleStrayPivot,
                        $"'{child.name}' sits at {(position - root) / cellSize} cells, more than a cell outside the room's geometry, so it stretches the room's Size."));
                }
            }
        }

        /// <summary>
        /// Claimed cells have to lie outside the rectangle (inside it they mean nothing) and join up with the room, or the room would be in two pieces.
        /// </summary>
        private static void CheckClaimedCells(RoomBlueprint room, RoomPlan plan, List<RoomValidationError> errors)
        {
            var claimed = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in plan.ExtraCells)
            {
                if (cell.x >= 0 && cell.x < plan.Size.x && cell.y >= 0 && cell.y < plan.Size.y)
                {
                    errors.Add(new RoomValidationError(room.name, RuleClaimedCellInsideFootprint,
                        $"A RoomClaimedCell stands in cell {cell}, which the room's rectangle already covers. Move it to the cell it should claim, or remove it."));
                }
                else
                {
                    claimed.Add(cell);
                }
            }

            // Grow outwards from the rectangle one touching cell at a time; whatever is left over is detached.
            var joined = new HashSet<Vector2Int>();
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (Vector2Int cell in claimed)
                {
                    if (joined.Contains(cell))
                    {
                        continue;
                    }

                    foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        Vector2Int next = cell + step;
                        bool touchesRectangle = next.x >= 0 && next.x < plan.Size.x && next.y >= 0 && next.y < plan.Size.y;
                        if (touchesRectangle || joined.Contains(next))
                        {
                            joined.Add(cell);
                            grew = true;
                            break;
                        }
                    }
                }
            }

            foreach (Vector2Int cell in claimed.Where(cell => !joined.Contains(cell)))
            {
                errors.Add(new RoomValidationError(room.name, RuleClaimedCellDetached,
                    $"The claimed cell {cell} does not touch the room's rectangle or another claimed cell, so the room would be in two pieces."));
            }
        }

        /// <summary>
        /// The packed footprint is worked out from transform pivots, so nothing else ties it to what is drawn. Every mesh has to stay inside
        /// the cells the room claims: its rectangle plus any claimed cells. A mesh that enters another cell overlaps the room placed there
        /// and z-fights. The room's geometry as a whole must also reach the rectangle's edges, or it leaves a gap between rooms.
        /// Rooms with no meshes are skipped.
        /// </summary>
        private static void CheckGeometryFit(RoomBlueprint room, float cellSize, RoomPlan plan, List<RoomValidationError> errors)
        {
            List<(string name, Bounds bounds)> meshes = GetMeshBounds(room);
            if (meshes.Count == 0)
            {
                return;
            }

            Vector3 root = room.transform.position;
            float tolerance = GeometryTolerance * cellSize;
            var claimed = new HashSet<Vector2Int>(plan.ExtraCells);

            foreach ((string name, Bounds bounds) in meshes)
            {
                // The range of cells the mesh reaches into, ignoring slivers within the tolerance of a cell edge.
                (int firstX, int lastX, int firstY, int lastY) = RoomGeometry.CellRange(bounds, root, plan.OriginOffset, cellSize, tolerance);

                bool reported = false;
                for (int x = firstX; x <= lastX && !reported; x++)
                {
                    for (int y = firstY; y <= lastY && !reported; y++)
                    {
                        bool inRectangle = x >= 0 && x < plan.Size.x && y >= 0 && y < plan.Size.y;
                        if (!inRectangle && !claimed.Contains(new Vector2Int(x, y)))
                        {
                            errors.Add(new RoomValidationError(room.name, RuleGeometryOutsideFootprint,
                                $"'{name}' reaches into cell ({x}, {y}), which the room does not claim, so it will overlap the room placed there and z-fight. Pull it back to the cell edge, or put a RoomClaimedCell in that cell if the room needs it."));
                            reported = true;
                        }
                    }
                }
            }

            Bounds drawn = meshes[0].bounds;
            foreach ((string name, Bounds bounds) in meshes)
            {
                drawn.Encapsulate(bounds);
            }

            float footprintMinX = root.x + plan.OriginOffset.x * cellSize;
            float footprintMinZ = root.z + plan.OriginOffset.y * cellSize;
            float footprintMaxX = footprintMinX + plan.Size.x * cellSize;
            float footprintMaxZ = footprintMinZ + plan.Size.y * cellSize;

            // (side, how far the geometry stops short of the footprint on that side)
            (string side, float gap)[] sides =
            {
                ("west (-x)", drawn.min.x - footprintMinX),
                ("east (+x)", footprintMaxX - drawn.max.x),
                ("south (-z)", drawn.min.z - footprintMinZ),
                ("north (+z)", footprintMaxZ - drawn.max.z),
            };

            foreach ((string side, float shortBy) in sides)
            {
                if (shortBy > tolerance)
                {
                    errors.Add(new RoomValidationError(room.name, RuleGeometryShortOfFootprint,
                        $"The geometry stops {shortBy / cellSize:0.###} cells short of the footprint on the {side} side, which leaves a gap between this room and the next. Extend it to the cell edge."));
                }
            }
        }

        /// <summary>
        /// The bounds of every mesh in the room taken together. Works on prefab assets as well as instances.
        /// </summary>
        private static bool TryGetMeshBounds(RoomBlueprint room, out Bounds bounds)
        {
            bounds = default;
            List<(string name, Bounds bounds)> meshes = GetMeshBounds(room);
            if (meshes.Count == 0)
            {
                return false;
            }

            bounds = meshes[0].bounds;
            foreach ((string name, Bounds mesh) in meshes)
            {
                bounds.Encapsulate(mesh);
            }

            return true;
        }

        private static List<(string name, Bounds bounds)> GetMeshBounds(RoomBlueprint room) => RoomGeometry.MeshBounds(room.transform);

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
