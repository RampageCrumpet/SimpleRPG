using System.Linq;
using LevelGeneration;
using NUnit.Framework;
using SimpleRPG.Keys;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Where keys lie: on a clear floor spot inside the room, never in a wall or under the floor. Uses the real ModularDungeon rooms.
    /// </summary>
    public class KeyPlacementTests
    {
        private const string RoomsFolder = "Assets/Prefabs/World/TileSets/ModularDungeon/Rooms";

        private GameObject instance;

        private static string[] RoomPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { RoomsFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();

        [TearDown]
        public void TearDown()
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }

        private RoomBlueprint Instantiate(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(40f, 7f, -25f);
            instance.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Physics.SyncTransforms();
            return instance.GetComponent<RoomBlueprint>();
        }

        [Test]
        public void RoomPrefabs_AreFound()
        {
            Assert.IsNotEmpty(RoomPaths(), "Expected the ModularDungeon room prefabs.");
        }

        [Test]
        public void KeyPosition_IsClearOfColliders_OnTheFloor_AndInsideTheRoom([ValueSource(nameof(RoomPaths))] string path)
        {
            RoomBlueprint room = Instantiate(path);
            Assert.IsNotNull(room, path);

            Vector3 key = DungeonLocks.KeyPosition(room);

            Assert.IsFalse(Physics.CheckSphere(key, 0.3f, ~0, QueryTriggerInteraction.Ignore), $"{path}: the key at {key} is inside a collider.");

            Bounds bounds = room.GetComponentsInChildren<Renderer>().Select(x => x.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Assert.IsTrue(bounds.Contains(key), $"{path}: the key at {key} is outside the room bounds {bounds}.");

            Assert.IsTrue(Physics.Raycast(key, Vector3.down, out RaycastHit floor, 1.5f, ~0, QueryTriggerInteraction.Ignore),
                $"{path}: there is no floor within 1.5m below the key at {key}.");
            Assert.IsTrue(floor.collider.transform.IsChildOf(room.transform), $"{path}: the surface below the key is not part of the room.");
            Assert.GreaterOrEqual(floor.normal.y, 0.7f, $"{path}: the key is not above a floor.");
        }

        [Test]
        public void KeyPosition_IsTheSameEveryTime([ValueSource(nameof(RoomPaths))] string path)
        {
            RoomBlueprint room = Instantiate(path);
            Assert.AreEqual(DungeonLocks.KeyPosition(room), DungeonLocks.KeyPosition(room));
        }

        [Test]
        public void KeyPosition_AvoidsASolidBlockInTheMiddle()
        {
            instance = new GameObject("Room", typeof(RoomBlueprint));
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(instance.transform, false);
            floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.transform.SetParent(instance.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            pillar.transform.localScale = new Vector3(1.5f, 3f, 1.5f);
            Physics.SyncTransforms();

            Vector3 key = DungeonLocks.KeyPosition(instance.GetComponent<RoomBlueprint>());

            Assert.IsFalse(Physics.CheckSphere(key, 0.3f), "The key must not be inside the pillar.");
            Assert.AreEqual(0.1f + 0.5f, key.y, 0.05f, "The key should sit just above the floor.");
        }
    }
}
