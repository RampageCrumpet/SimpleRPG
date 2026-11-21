using System.Linq;
using NUnit.Framework;
using SimpleRPG.Appearance;
using UnityEditor;
using UnityEngine;
namespace SimpleRPG.Tests.EditMode
{
    public class BreechesBoneMotionTests
    {
        private const string PartPath = "Assets/Art/BodyParts/Armor/Breeches_BreechesCloth.asset";
        [Test]
        public void OneDrawSurface_Uses22LocalBones_AndHidesWaistWithoutAnotherRenderer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab");
            var rig = Object.Instantiate(prefab);
            try
            {
                var parts = new GameObject("Parts").transform; parts.SetParent(rig.transform, false);
                var view = new AppearanceView(new RigBoneIndex(rig.transform.Find("Human_Male_Rig")), parts);
                var definition = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(PartPath);
                Assert.IsNull(definition.Problem()); Assert.AreEqual(1, definition.Pieces.Count);
                Assert.AreEqual(52, definition.BoneNames.Length);
                view.Show(definition);
                var renderers = parts.GetComponentsInChildren<SkinnedMeshRenderer>(); Assert.AreEqual(1, renderers.Length);
                var renderer = renderers.Single(); var motion = renderer.GetComponent<CordBoneMotion>();
                Assert.AreEqual(1, renderer.sharedMaterials.Length); Assert.AreEqual(1, renderer.sharedMesh.subMeshCount);
                Assert.IsTrue(renderer.sharedMaterial.HasProperty("_WrapColor")); Assert.AreEqual(1, renderer.sharedMaterial.GetFloat("_FourColorMask"));
                Assert.AreEqual(0, parts.GetComponentsInChildren<Cloth>().Length);
                Assert.AreEqual(22, motion.SimulatedBoneCount); Assert.AreEqual(74, renderer.bones.Length);
                Assert.AreEqual(10, motion.Profile.Chains.Length);
                Assert.AreEqual(2, motion.Profile.Chains.Count(c => c.points.Length == 4));
                Assert.AreEqual(8, motion.Profile.Chains.Count(c => c.points.Length == 3));
                Assert.IsTrue(motion.SimulationBones.All(b => b.IsChildOf(parts)));
                int full = renderer.sharedMesh.triangles.Length;
                var rest = new Mesh(); renderer.BakeMesh(rest);
                var allBones = renderer.bones;
                renderer.sharedMesh = motion.Profile.Source; renderer.bones = allBones.Take(52).ToArray();
                var original = new Mesh(); renderer.BakeMesh(original);
                Assert.Less(rest.vertices.Zip(original.vertices, Vector3.Distance).Max(), .00001f, "Accessory bind poses changed the approved rest shape.");
                Object.DestroyImmediate(original);
                renderer.sharedMesh = motion.Profile.VisibleMesh; renderer.bones = allBones;
                view.Show(AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/LinenTunic_FINAL_LinenTunic.asset"));
                Assert.AreSame(motion.Profile.HiddenWaistMesh, renderer.sharedMesh);
                Assert.AreEqual(1, renderer.sharedMesh.subMeshCount); Assert.Less(renderer.sharedMesh.triangles.Length, full);
                view.Clear(BodyPartSlot.Torso); Assert.AreSame(motion.Profile.VisibleMesh, renderer.sharedMesh);
                var after = new Mesh(); renderer.BakeMesh(after);
                Assert.Less(rest.vertices.Zip(after.vertices, Vector3.Distance).Max(), .00001f);
                Object.DestroyImmediate(rest); Object.DestroyImmediate(after);
                view.Clear(BodyPartSlot.Legs); Assert.AreEqual(0, parts.childCount);
                Assert.AreEqual(0, rig.GetComponentsInChildren<CapsuleCollider>().Length);
                view.Show(definition); Assert.AreEqual(22, parts.GetComponentsInChildren<CordBoneMotion>().Single().SimulatedBoneCount);
                view.ClearAll(); Assert.AreEqual(0, parts.childCount);
            }
            finally { Object.DestroyImmediate(rig); }
        }
    }
}
