using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    public class StairTraversalTests
    {
        [UnityTest]
        public IEnumerator NavigationAgent_CanTraverseEveryStairFlight()
        {
            var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/TileSets/ModularDungeon/Rooms/Room_Stairwell.prefab"), new Vector3(2000, 0, 2000), Quaternion.identity);
            AddNeighborFloors(room);
            var body = new GameObject("Stair navigation actor");
            try
            {
                var surface = room.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = LayerMask.GetMask("World", "Walkable");
                surface.BuildNavMesh();
                Assert.IsTrue(NavMesh.SamplePosition(room.transform.position + new Vector3(-1, .1f, 2), out var start, 1, NavMesh.AllAreas));
                Assert.IsTrue(NavMesh.SamplePosition(room.transform.position + new Vector3(18, 8.1f, 2), out var finish, 1, NavMesh.AllAreas));
                body.transform.position = start.position;
                var agent = body.AddComponent<NavMeshAgent>();
                agent.radius = .3f;
                agent.height = 1.8f;
                agent.speed = 5;
                agent.acceleration = 30;
                agent.SetDestination(finish.position);
                float deadline = Time.time + 12;
                while (Time.time < deadline && (agent.pathPending || Vector3.Distance(body.transform.position, finish.position) > .3f)) yield return null;
                Assert.Less(Vector3.Distance(body.transform.position, finish.position), .3f, "AI must climb all four flights, not merely find a path near the stairs.");
            }
            finally { Object.Destroy(body); Object.Destroy(room); }
        }

        [UnityTest]
        public IEnumerator PlayerCapsule_CanClimbAndDescendShippedStairwell()
        {
            var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/TileSets/ModularDungeon/Rooms/Room_Stairwell.prefab"), new Vector3(2000, 0, 2000), Quaternion.identity);
            AddNeighborFloors(room);
            var body = new GameObject("Stair traversal capsule");
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player/TestCharacter.prefab").GetComponent<CharacterController>();
                var capsule = body.AddComponent<CharacterController>();
                capsule.height = source.height;
                capsule.radius = source.radius;
                capsule.center = source.center;
                capsule.stepOffset = source.stepOffset;
                capsule.slopeLimit = source.slopeLimit;
                capsule.skinWidth = source.skinWidth;
                body.transform.position = room.transform.position + new Vector3(-1.2f, .8f, 2);
                Physics.SyncTransforms();
                yield return null;
                for (int i = 0; i < 400; i++) capsule.Move(new Vector3(3, -9.81f, 0) / 60);
                Assert.Greater(body.transform.position.x - room.transform.position.x, 17, "The actual player capsule must clear every stair flight.");
                Assert.That(body.transform.position.y, Is.InRange(8.5f, 9f));
                for (int i = 0; i < 400; i++) capsule.Move(new Vector3(-3, -9.81f, 0) / 60);
                Assert.Less(body.transform.position.x - room.transform.position.x, -.4f);
                Assert.That(body.transform.position.y, Is.InRange(.5f, 1f));
            }
            finally { Object.Destroy(body); Object.Destroy(room); }
        }

        private static void AddNeighborFloors(GameObject room)
        {
            foreach (var position in new[] { new Vector3(-2, -.1f, 2), new Vector3(22, 7.9f, 2) })
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Neighboring room floor";
                floor.layer = LayerMask.NameToLayer("Walkable");
                floor.transform.SetParent(room.transform, false);
                floor.transform.localPosition = position;
                floor.transform.localScale = new Vector3(4, .2f, 4);
            }
        }
    }
}
