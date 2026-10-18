using System.Collections.Generic;
using LevelGeneration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Generates dungeons from the real room sets and checks that no connection opens onto a wall.
    /// </summary>
    public class DungeonGeneratorTests
    {
        private static readonly string[] RoomsetPaths =
        {
            "Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset",
            
            
        };

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject room in spawned)
            {
                if (room != null)
                {
                    Object.DestroyImmediate(room);
                }
            }
            spawned.Clear();
        }

        [Test]
        public void GeneratedDungeons_NeverFaceAConnectionIntoAWall([ValueSource(nameof(RoomsetPaths))] string roomsetPath, [NUnit.Framework.Range(0, 29)] int seed)
        {
            Roomset roomset = AssetDatabase.LoadAssetAtPath<Roomset>(roomsetPath);
            Assert.IsNotNull(roomset, $"Could not load {roomsetPath}.");

            var generator = new DungeonGenerator(seed, roomset.RoomCollection, roomset.cellSize);
            generator.RoomInstantiated = spawned.Add;
            generator.GenerateLevel(8, null);

            var occupant = new Dictionary<Vector2Int, Room>();
            foreach (Room room in generator.PlacedRooms)
            {
                for (int x = 0; x < room.Size.x; x++)
                {
                    for (int y = 0; y < room.Size.y; y++)
                    {
                        occupant[new Vector2Int(x, y) + room.RoomOrigin + room.Position] = room;
                    }
                }
            }

            foreach (Room room in generator.PlacedRooms)
            {
                foreach (Connection connection in room.Connections)
                {
                    int length = Mathf.RoundToInt(connection.ConnectionLength / roomset.cellSize);
                    Vector2Int target = connection.GlobalTilePosition + connection.Forward * length;

                    if (!occupant.TryGetValue(connection.FacingCell(length), out Room neighbour))
                    {
                        continue;
                    }

                    bool mutual = neighbour.Connections.Exists(other =>
                        other.GlobalTilePosition == target &&
                        other.CanConnect(connection) &&
                        other.GlobalTilePosition + other.Forward * length == connection.GlobalTilePosition);

                    Assert.IsTrue(mutual, $"Seed {seed}: a connection at {connection.GlobalTilePosition} facing {connection.Forward} opens onto a wall of another room.");
                }
            }
        }
    }
}
