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
            RoomBlueprint first = Room(Vector3.zero, double_sided: true, out MeshRenderer firstWall, new Vector3(12, 2, 4));
            RoomBlueprint second = Room(new Vector3(0.01f, 0, 0), double_sided: true, out MeshRenderer secondWall, new Vector3(12, 2, 4));

            int hidden = CoincidentGeometry.HideDuplicates(new[] { first, second });

            Assert.That(hidden, Is.EqualTo(1));
            Assert.IsTrue(firstWall.enabled, "The lower-numbered room keeps drawing the piece.");
            Assert.IsFalse(secondWall.enabled);
        }

        [Test]
        public void DifferentPieces_AreBothDrawn()
        {
            RoomBlueprint first = Room(Vector3.zero, true, out MeshRenderer firstWall, new Vector3(12, 2, 4));
            RoomBlueprint second = Room(Vector3.zero, true, out MeshRenderer secondWall, new Vector3(20, 2, 4));

            Assert.That(CoincidentGeometry.HideDuplicates(new[] { first, second }), Is.Zero);
            Assert.IsTrue(firstWall.enabled && secondWall.enabled);
        }

        [Test]
        public void SingleSidedPieces_AreBothDrawn()
        {
            // Facing opposite ways they never show through each other, and hiding one would leave a hole seen from the other room.
            RoomBlueprint first = Room(Vector3.zero, false, out MeshRenderer firstWall, new Vector3(12, 2, 4));
            RoomBlueprint second = Room(Vector3.zero, false, out MeshRenderer secondWall, new Vector3(12, 2, 4));

            Assert.That(CoincidentGeometry.HideDuplicates(new[] { first, second }), Is.Zero);
            Assert.IsTrue(firstWall.enabled && secondWall.enabled);
        }

        [Test]
        public void TwoPiecesInTheSameRoom_AreLeftAlone()
        {
            RoomBlueprint room = Room(Vector3.zero, true, out MeshRenderer wall, new Vector3(12, 2, 4));
            GameObject copy = Object.Instantiate(wall.gameObject, room.transform);
            created.Add(copy);

            Assert.That(CoincidentGeometry.HideDuplicates(new[] { room }), Is.Zero);
        }

        private RoomBlueprint Room(Vector3 rootPosition, bool double_sided, out MeshRenderer wall, Vector3 wallPosition)
        {
            var root = new GameObject("Room");
            created.Add(root);
            root.transform.position = rootPosition;
            RoomBlueprint room = root.AddComponent<RoomBlueprint>();

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
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
