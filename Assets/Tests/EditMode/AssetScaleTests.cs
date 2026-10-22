using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    public class AssetScaleTests
    {
        [Test]
        public void Skeleton_PreservesSourceHeightWithoutTransformScaling()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/3d Models/Skeleton/Skeleton_Model.fbx");
            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var vertices = renderer.sharedMesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            Assert.AreEqual(1.78004f, vertices.Max(v => v.y) - vertices.Min(v => v.y), .001f);
            foreach (var transform in model.GetComponentsInChildren<Transform>())
                Assert.Less((transform.localScale - Vector3.one).sqrMagnitude, .00001f, transform.name);
            var gameplay = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab");
            Assert.AreEqual(Vector3.one, gameplay.transform.localScale);
        }

        [Test]
        public void Dungeon_UsesOriginalPackageDimensionsAndUnitStructuralScales()
        {
            const string folder = "Assets/Art/3d Models/ModularDungeonCollection/Meshes/";
            var stairs = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "SM_Stairs_01_Sml.fbx");
            var size = stairs.GetComponentInChildren<Renderer>().bounds.size;
            Assert.AreEqual(4.00574f, size.x, .002f);
            Assert.AreEqual(2.04863f, size.y, .002f);
            Assert.AreEqual(Vector3.one, stairs.transform.localScale);
            Assert.AreEqual(1, ((ModelImporter)AssetImporter.GetAtPath(folder + "SM_Stairs_01_Sml.fbx")).globalScale);
            var roomset = AssetDatabase.LoadAssetAtPath<Roomset>("Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset");
            Assert.AreEqual(2, roomset.cellSize);
            Assert.AreEqual(8, roomset.floorHeight);
            foreach (var room in roomset.RoomCollection)
            {
                Assert.AreEqual(Vector3.one, room.transform.localScale, room.name);
                foreach (var filter in room.GetComponentsInChildren<MeshFilter>())
                    if (filter.name.StartsWith("Stairs_Flight") || filter.name.StartsWith("Floor "))
                        Assert.Less((filter.transform.lossyScale - Vector3.one).sqrMagnitude, .00000001f, filter.name);
            }
        }
    }
}
