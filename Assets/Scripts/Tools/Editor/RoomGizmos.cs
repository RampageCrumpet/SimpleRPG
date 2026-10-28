using SimpleRPG.LevelGeneration;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    #if UNITY_EDITOR
    /// <summary>
    /// Draws what the packer will store for a selected room: its footprint, and for each door the point it stands on,
    /// the way it faces, how far it reaches and the cells just past it that must be empty or the room it joins.
    /// Drawn from the geometry (not the stored values), so it also shows what a re-pack would change.
    /// </summary>
    public static class RoomGizmos
    {
        private static readonly Color FootprintColor = new Color(0.2f, 0.8f, 1f, 1f);
        private static readonly Color DoorColor = new Color(1f, 0.6f, 0.1f, 1f);
        private static readonly Color BeyondColor = new Color(1f, 0.2f, 0.2f, 0.9f);

        // Looking up the roomset searches the project, so remember the answer until the project changes.
        private static readonly System.Collections.Generic.Dictionary<RoomBlueprint, float> CellSizes = new System.Collections.Generic.Dictionary<RoomBlueprint, float>();

        static RoomGizmos()
        {
            EditorApplication.projectChanged += CellSizes.Clear;
        }

        private static float CellSizeFor(RoomBlueprint room)
        {
            if (!CellSizes.TryGetValue(room, out float cellSize))
            {
                Roomset roomset = RoomsetTools.FindRoomsetContaining(AssetDatabase.Contains(room) ? room : PrefabSource(room));
                cellSize = roomset != null && roomset.cellSize > 0 ? roomset.cellSize : 0f;
                CellSizes[room] = cellSize;
            }

            return cellSize;
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawRoom(RoomBlueprint room, GizmoType gizmoType)
        {
            float cellSize = CellSizeFor(room);
            if (cellSize <= 0f)
            {
                return;
            }

            RoomPlan plan = RoomPlanner.Plan(room, cellSize);
            Vector3 root = room.transform.position;
            Vector3 footprintMin = root + new Vector3(plan.OriginOffset.x, 0, plan.OriginOffset.y) * cellSize;

            Gizmos.color = FootprintColor;
            Vector3 center = footprintMin + new Vector3(plan.Size.x, 0, plan.Size.y) * cellSize * 0.5f;
            Gizmos.DrawWireCube(center, new Vector3(plan.Size.x * cellSize, 0.1f, plan.Size.y * cellSize));

            foreach (DoorPlan door in plan.Doors)
            {
                DrawDoor(door, footprintMin, cellSize);
            }
        }

        private static void DrawDoor(DoorPlan door, Vector3 footprintMin, float cellSize)
        {
            ConnectionBlueprint connection = door.Door;
            Side side = GridMath.FromDirection(connection.Forward);
            int length = Mathf.Max(1, Mathf.RoundToInt(connection.ConnectionLength / cellSize));
            Vector3 point = footprintMin + new Vector3(door.Location.x, 0, door.Location.y) * cellSize;
            Vector2Int direction = GridMath.Direction(side);
            Vector3 along = new Vector3(direction.x, 0, direction.y);

            Gizmos.color = DoorColor;
            Gizmos.DrawSphere(point, cellSize * 0.1f);
            Gizmos.DrawLine(point, point + along * length * cellSize);

            // The first row of cells past the door's end: where a door of the room it joins stands, or where nothing may be.
            int width = Mathf.Max(2, 2 * Mathf.CeilToInt(connection.ConnectionWidth / cellSize / 2f));
            Gizmos.color = BeyondColor;
            foreach (Vector2Int cell in GridMath.CellsAcross(door.Location, side, length, width))
            {
                Vector3 cellCenter = footprintMin + new Vector3(cell.x + 0.5f, 0, cell.y + 0.5f) * cellSize;
                Gizmos.DrawWireCube(cellCenter, new Vector3(cellSize, 0.1f, cellSize));
            }
        }

        /// <summary>
        /// A scene instance or prefab-stage object's prefab asset, so the roomset lookup can find it.
        /// </summary>
        private static RoomBlueprint PrefabSource(RoomBlueprint room)
        {
            return PrefabUtility.GetCorrespondingObjectFromSource(room) ?? room;
        }
    }
    #endif
}
