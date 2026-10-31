using System.Collections.Generic;
using NUnit.Framework;
using SimpleRPG.LevelGeneration;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Rooms that share an edge each draw the same flat wall; one of the two has to stop drawing it.
    /// </summary>
    public class CoincidentGeometryTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        [Test]
        public void SamePieceInTwoRooms_IsDrawnOnce()
        {
            RoomBlueprint first = Room(Vector3.zero, double_sided: true, out MeshRenderer firstWall, new Vector3(12, 2, 4), PrimitiveType.Cube);
            RoomBlueprint second = Room(new Vector3(0.01f, 0, 0), double_sided: true, out MeshRenderer secondWall, new Vector3(12, 2, 4), PrimitiveType.Cube);

            int hidden = CoincidentGeometry.HideDuplicates(new[] { first, second });

            Assert.That(hidden, Is.EqualTo(1));
            Assert.IsTrue(firstWall.enabled, "The lower-numbered room keeps drawing the piece.");
            Assert.IsFalse(secondWall.enabled);
        }

        [Test]
        public void DifferentPieces_AreBothDrawn()
        {
            RoomBlueprint first = Room(Vector3.zero, true, out MeshRenderer firstWall, new Vector3(12, 2, 4), PrimitiveType.Cube);
            RoomBlueprint second = Room(Vector3.zero, true, out MeshRenderer secondWall, new Vector3(20, 2, 4), PrimitiveType.Cube);

            Assert.That(CoincidentGeometry.HideDuplicates(new[] { first, second }), Is.Zero);
            Assert.IsTrue(firstWall.enabled && secondWall.enabled);
        }

        [Test]
        public void SingleSidedPieces_AreBothDrawn()
        {
            // Facing opposite ways they never show through each other, and hiding one would leave a hole seen from the other room.
            RoomBlueprint first = Room(Vector3.zero, false, out MeshRenderer firstWall, new Vector3(12, 2, 4), PrimitiveType.Cube);
            RoomBlueprint second = Room(Vector3.zero, false, out MeshRenderer secondWall, new Vector3(12, 2, 4), PrimitiveType.Cube);

            Assert.That(CoincidentGeometry.HideDuplicates(new[] { first, second }), Is.Zero);
            Assert.IsTrue(firstWall.enabled && secondWall.enabled);
        }

        [Test]
        public void TwoPiecesInTheSameRoom_AreLeftAlone()
        {
            RoomBlueprint room = Room(Vector3.zero, true, out MeshRenderer wall, new Vector3(12, 2, 4), PrimitiveType.Cube);
            GameObject copy = Object.Instantiate(wall.gameObject, room.transform);
            created.Add(copy);

            Assert.That(CoincidentGeometry.HideDuplicates(new[] { room }), Is.Zero);
        }

        [Test]
        public void PlanesThatOverlapInPart_AreMovedIntoTheirOwnRooms()
        {
            // Two 12 x 12 rooms, one north of the other, each with a 4-wide wall plane on the shared line; the planes overlap by 2 along x.
            RoomBlueprint south = PlaneRoom(new Vector3(0, 0, 0), new Vector3(6, 2, 12), out MeshRenderer southWall);
            RoomBlueprint north = PlaneRoom(new Vector3(0, 0, 12), new Vector3(8, 2, 12), out MeshRenderer northWall);

            int moved = CoincidentGeometry.SeparateOverlappingPlanes(new[] { south, north });

            Assert.That(moved, Is.EqualTo(2));
            Assert.That(southWall.transform.position.z, Is.LessThan(12f), "The south room's wall moves south, into its room.");
            Assert.That(northWall.transform.position.z, Is.GreaterThan(12f), "The north room's wall moves north, into its room.");
        }

        [Test]
        public void PlanesThatDoNotOverlap_StayPut()
        {
            RoomBlueprint south = PlaneRoom(new Vector3(0, 0, 0), new Vector3(6, 2, 12), out MeshRenderer southWall);
            RoomBlueprint north = PlaneRoom(new Vector3(0, 0, 12), new Vector3(12, 2, 12), out MeshRenderer northWall);

            Assert.That(CoincidentGeometry.SeparateOverlappingPlanes(new[] { south, north }), Is.Zero);
            Assert.That(southWall.transform.position.z, Is.EqualTo(12f));
        }

        /// <summary>
        /// A room with a floor slab and one 4 x 4 wall plane standing at the given position.
        /// </summary>
        private RoomBlueprint PlaneRoom(Vector3 rootPosition, Vector3 planePosition, out MeshRenderer plane)
        {
            RoomBlueprint room = Room(rootPosition, true, out plane, planePosition, PrimitiveType.Quad);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(room.transform, false);
            floor.transform.position = rootPosition + new Vector3(6, 0, 6);
            floor.transform.localScale = new Vector3(12, 0.2f, 12);
            floor.GetComponent<MeshRenderer>().sharedMaterial = plane.sharedMaterial;
            return room;
        }

        private RoomBlueprint Room(Vector3 rootPosition, bool double_sided, out MeshRenderer wall, Vector3 wallPosition, PrimitiveType shape)
        {
            var root = new GameObject("Room");
            created.Add(root);
            root.transform.position = rootPosition;
            RoomBlueprint room = root.AddComponent<RoomBlueprint>();

            GameObject quad = GameObject.CreatePrimitive(shape);
            quad.transform.SetParent(root.transform, false);
            quad.transform.position = wallPosition;
            quad.transform.localScale = new Vector3(4, 4, 1);
            wall = quad.GetComponent<MeshRenderer>();

            // Sprites/Default has no cull setting and so counts as double sided; a Lit material that culls back faces is single sided.
            Material material = new Material(Shader.Find(double_sided ? "Sprites/Default" : "Universal Render Pipeline/Lit"));
            if (!double_sided && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 2f);
            }
            created.Add(material);
            wall.sharedMaterial = material;
            return room;
        }
    }
}
