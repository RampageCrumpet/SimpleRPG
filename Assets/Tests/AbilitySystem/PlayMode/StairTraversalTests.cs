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
            var body = new GameObject("Stair navigation actor");
            try
            {
                var surface = room.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = LayerMask.GetMask("World", "Walkable");
                surface.BuildNavMesh();
                Assert.IsTrue(NavMesh.SamplePosition(room.transform.position + new Vector3(2, .1f, 6), out var start, 1, NavMesh.AllAreas));
                Assert.IsTrue(NavMesh.SamplePosition(room.transform.position + new Vector3(22, 8.1f, 6), out var finish, 1, NavMesh.AllAreas));
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
                body.transform.position = room.transform.position + new Vector3(2.8f, .8f, 6);
                Physics.SyncTransforms();
                yield return null;
                for (int i = 0; i < 450; i++) capsule.Move(new Vector3(3, -9.81f, 0) / 60);
                Assert.Greater(body.transform.position.x - room.transform.position.x, 21, "The actual player capsule must clear every stair flight.");
                Assert.That(body.transform.position.y, Is.InRange(8.5f, 9f));
                for (int i = 0; i < 450; i++) capsule.Move(new Vector3(-3, -9.81f, 0) / 60);
                Assert.Less(body.transform.position.x - room.transform.position.x, 3.6f);
                Assert.That(body.transform.position.y, Is.InRange(.5f, 1f));
            }
            finally { Object.Destroy(body); Object.Destroy(room); }
        }
    }
}
