using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleRPG.LevelGeneration;
using SimpleRPG.RoomGeneration;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Unity side of procedural rooms: the shared mesh construction, and procedural rooms spawned next to authored ones.
    /// </summary>
    public class ProceduralRoomConstructionTests
    {
        private const float CellSize = 2f;
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        private static DoorDefinition Door(int index, int x, int y, Side side, int level = 0) =>
            new DoorDefinition(index, new Vector2Int(x, y), side, 0, 2, ConnectionType.StandardId, level, 0);

        private static GeneratedRoom Chamber(int w, int h, int seed, params DoorDefinition[] doors)
        {
            var context = new RoomGenerationContext("mesh", new Vector2Int(w, h), 1, doors, seed, new RoomGenerationSettings());
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            Assert.IsFalse(room.IsFailure, room.Failure);
            return room;
        }

        // ---- Mesh construction ----

        [Test]
        public void Mesh_FillsTheFootprint_AndEveryTriangleFacesOutward()
        {
            GeneratedRoom room = Chamber(6, 6, 1, Door(0, 0, 3, Side.West), Door(1, 6, 3, Side.East));
            RoomMeshBuilder.Result built = RoomMeshBuilder.Build(room.ResolvePieces());
            created.Add(built.Mesh);
            Mesh mesh = built.Mesh;

            Assert.AreEqual(0f, mesh.bounds.min.x, 1e-4f);
            Assert.AreEqual(12f, mesh.bounds.max.x, 1e-4f);
            Assert.AreEqual(0f, mesh.bounds.min.z, 1e-4f);
            Assert.AreEqual(12f, mesh.bounds.max.z, 1e-4f);
            Assert.AreEqual(-0.2f, mesh.bounds.min.y, 1e-4f);
            Assert.AreEqual(4.2f, mesh.bounds.max.y, 1e-4f);

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            Assert.AreEqual(vertices.Length, normals.Length);
            Assert.AreEqual(vertices.Length, mesh.uv.Length);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a).normalized;
                Assert.Greater(Vector3.Dot(face, normals[triangles[i]]), 0.99f, "triangle winding must agree with its normal (Unity front faces wind clockwise)");
                Assert.AreEqual(1f, normals[triangles[i]].magnitude, 1e-4f);
            }
        }

        [Test]
        public void Mesh_HidesBuriedFaces_AndSplitsSubmeshesByKind()
        {
            GeneratedRoom room = Chamber(6, 6, 1, Door(0, 0, 3, Side.West), Door(1, 6, 3, Side.East));
            List<StructuralPiece> pieces = room.ResolvePieces();
            RoomMeshBuilder.Result built = RoomMeshBuilder.Build(pieces);
            created.Add(built.Mesh);

            Assert.Greater(built.HiddenFaceCount, 0);
            Assert.AreEqual(pieces.Count * 6, built.FaceCount + built.HiddenFaceCount);
            CollectionAssert.AreEqual(new[] { PieceKind.Floor, PieceKind.Ceiling, PieceKind.Wall }, built.SubmeshKinds);
            Assert.AreEqual(3, built.Mesh.subMeshCount);
        }

        [Test]
        public void Mesh_OfAStaircaseRoom_BuildsStepsWithoutError()
        {
            var context = new RoomGenerationContext("stairs", new Vector2Int(10, 10), 2,
                new[] { Door(0, 0, 3, Side.West, 0), Door(1, 10, 7, Side.East, 1) }, 4, new RoomGenerationSettings());
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            Assert.IsFalse(room.IsFailure, room.Failure);

            RoomMeshBuilder.Result built = RoomMeshBuilder.Build(room.ResolvePieces());
            created.Add(built.Mesh);
            Assert.IsTrue(built.SubmeshKinds.Contains(PieceKind.Stair));
            Assert.Greater(built.Mesh.triangles.Length, 0);
        }

        // ---- Spawning next to authored rooms ----

        private RoomBlueprint Shell(string name, bool procedural)
        {
            var root = new GameObject(name);
            created.Add(root);
            RoomBlueprint room = root.AddComponent<RoomBlueprint>();
            room.Size = new Vector2Int(6, 6);
            room.OriginOffset = Vector2Int.zero;

            if (procedural)
            {
                root.AddComponent<ProceduralRoomSettings>();
            }

            var connections = new List<ConnectionBlueprint>();
            AddDoor(room, connections, "West", new Vector2Int(0, 3), Vector3.left, new Vector3(0, 0, 3 * CellSize));
            AddDoor(room, connections, "East", new Vector2Int(6, 3), Vector3.right, new Vector3(6 * CellSize, 0, 3 * CellSize));
            room.SetConnections(connections);
            return room;
        }

        private void AddDoor(RoomBlueprint room, List<ConnectionBlueprint> connections, string name, Vector2Int location, Vector3 forward, Vector3 position)
        {
            var door = new GameObject(name);
            door.transform.SetParent(room.transform, false);
            door.transform.position = position;
            door.transform.rotation = Quaternion.LookRotation(forward);
            ConnectionBlueprint connection = door.AddComponent<ConnectionBlueprint>();
            connection.ConnectionLength = 0f;
            connection.ConnectionWidth = 2 * CellSize;
            connection.location = location;
            connection.SealingObject = new GameObject("Seal");
            connection.SealingObject.transform.SetParent(door.transform, false);
            connection.SealingObject.SetActive(false);
            connection.DoorObject = new GameObject("Door");
            connection.DoorObject.transform.SetParent(door.transform, false);
            connections.Add(connection);
        }

        private (Layout layout, List<RoomBlueprint> prefabs) LayoutOf(RoomBlueprint prefab, int turns, Vector2Int position)
        {
            RoomDefinition definition = RoomDefinitionBaker.Bake(prefab, 0, CellSize);
            var placed = new PlacedRoom(0, definition, position, turns);
            return (new Layout(1234, 0, new[] { placed }, new PlacedDoor[0]), new List<RoomBlueprint> { prefab });
        }

        [Test]
        public void AuthoredRoom_GetsNoGeneratedGeometry()
        {
            (Layout layout, List<RoomBlueprint> prefabs) = LayoutOf(Shell("Authored", false), 0, Vector2Int.zero);
            var spawner = new LevelSpawner(prefabs, CellSize, 8f);
            IReadOnlyList<RoomBlueprint> rooms = spawner.Spawn(layout, null);
            created.Add(rooms[0].gameObject);

            Assert.AreEqual(0, spawner.ProceduralResults.Count);
            Assert.IsNull(rooms[0].transform.Find("Procedural geometry"));
        }

        [Test]
        public void ProceduralRoom_IsBuiltInPlace_ForEveryRotation([Values(0, 1, 2, 3)] int turns)
        {
            Vector2Int position = new Vector2Int(5, -3);
            (Layout layout, List<RoomBlueprint> prefabs) = LayoutOf(Shell("Procedural", true), turns, position);
            var spawner = new LevelSpawner(prefabs, CellSize, 8f);
            IReadOnlyList<RoomBlueprint> rooms = spawner.Spawn(layout, null);
            created.Add(rooms[0].gameObject);

            Assert.AreEqual(1, spawner.ProceduralResults.Count);
            Assert.IsTrue(spawner.ProceduralResults[0].result.Succeeded);
            Transform geometry = rooms[0].transform.Find("Procedural geometry");
            Assert.IsNotNull(geometry);

            Physics.SyncTransforms();
            PlacedRoom placed = layout.Rooms[0];
            Bounds bounds = geometry.GetComponent<MeshRenderer>().bounds;
            Vector3 expectedMin = new Vector3(placed.Min.x * CellSize, 0f, placed.Min.y * CellSize);
            Assert.AreEqual(expectedMin.x, bounds.min.x, 1e-3f);
            Assert.AreEqual(expectedMin.z, bounds.min.z, 1e-3f);
            Assert.AreEqual(expectedMin.x + placed.Size.x * CellSize, bounds.max.x, 1e-3f);
            Assert.AreEqual(expectedMin.z + placed.Size.y * CellSize, bounds.max.z, 1e-3f);

            // At each door: the doorway is open, and the wall beside it is solid.
            foreach (PlacedDoor door in placed.Doors)
            {
                Vector3 outward = new Vector3(door.Direction.x, 0, door.Direction.y);
                Vector3 along = Vector3.Cross(Vector3.up, outward);
                Vector3 centre = new Vector3(door.Point.x * CellSize, 1.5f, door.Point.y * CellSize) - outward * 0.15f;
                Vector3 half = new Vector3(0.1f, 0.5f, 0.1f);

                Assert.IsFalse(Physics.CheckBox(centre, half), $"doorway of {door} is blocked");
                Assert.IsFalse(Physics.CheckBox(centre + along * 1.5f, half), $"doorway of {door} is narrower than its width");
                Assert.IsTrue(Physics.CheckBox(centre + along * 3f, half), $"wall beside {door} is missing");
                Assert.IsTrue(Physics.CheckBox(centre + Vector3.up * 2f, half), $"lintel over {door} is missing");
            }
        }

        [Test]
        public void ProceduralAndAuthoredRooms_ShareOneDungeon()
        {
            RoomBlueprint procedural = Shell("Procedural", true);
            RoomBlueprint authored = Shell("Authored", false);
            RoomDefinition a = RoomDefinitionBaker.Bake(procedural, 0, CellSize);
            RoomDefinition b = RoomDefinitionBaker.Bake(authored, 1, CellSize);
            var rooms = new[] { new PlacedRoom(0, a, Vector2Int.zero, 0), new PlacedRoom(1, b, new Vector2Int(6, 0), 0) };
            var layout = new Layout(99, 0, rooms, new PlacedDoor[0]);

            var spawner = new LevelSpawner(new List<RoomBlueprint> { procedural, authored }, CellSize, 8f);
            IReadOnlyList<RoomBlueprint> spawned = spawner.Spawn(layout, null);
            created.AddRange(spawned.Select(room => (Object)room.gameObject));

            Assert.AreEqual(1, spawner.ProceduralResults.Count);
            Assert.AreSame(rooms[0], spawner.ProceduralResults[0].room);
            Assert.IsNotNull(spawned[0].transform.Find("Procedural geometry"));
            Assert.IsNull(spawned[1].transform.Find("Procedural geometry"));
        }

        [Test]
        public void SameLayout_BuildsTheSamePlans_OnEveryMachine()
        {
            RoomBlueprint prefab = Shell("Procedural", true);
            int Checksum()
            {
                (Layout layout, List<RoomBlueprint> prefabs) = LayoutOf(prefab, 1, new Vector2Int(2, 2));
                var spawner = new LevelSpawner(prefabs, CellSize, 8f);
                IReadOnlyList<RoomBlueprint> rooms = spawner.Spawn(layout, null);
                created.Add(rooms[0].gameObject);
                return RoomPlanChecksum.Of(spawner.ProceduralResults[0].result.Room);
            }

            Assert.AreEqual(Checksum(), Checksum());
        }

        [Test]
        public void RoomsetValidation_ReportsAProceduralRoomThatCannotBeBuilt()
        {
            RoomBlueprint prefab = Shell("Bad", true);
            prefab.GetComponentsInChildren<ConnectionBlueprint>()[0].ConnectionLength = 4f; // a corridor between the rooms: not supported
            Roomset roomset = ScriptableObject.CreateInstance<Roomset>();
            created.Add(roomset);
            roomset.cellSize = CellSize;
            roomset.floorHeight = 8f;
            roomset.RoomCollection = new List<RoomBlueprint> { prefab };

            List<RoomValidationError> errors = ProceduralRoomChecks.Check(prefab, roomset);
            Assert.IsTrue(errors.Any(error => error.Rule == ProceduralRoomChecks.Rule), string.Join("\n", errors));
        }
    }
}
