using NUnit.Framework;
using SimpleRPG.Appearance;
using SimpleRPG.AppearanceTools;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// One skeleton, one rest pose: the body rig and every piece of armor skinned to it agree (see <see cref="RigConformance"/>), and the rig has
    /// the sockets rigid pieces attach to.
    /// </summary>
    public class RigConformanceTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        // A one-bone rig at (0, 1, 0) and a mesh bound with that bone there.
        private Mesh BuildOneBoneRig(out string[] names)
        {
            root = new GameObject("Rig");
            var bone = new GameObject("Arm").transform;
            bone.SetParent(root.transform, false);
            bone.localPosition = new Vector3(0f, 1f, 0f);
            names = new[] { "Arm" };
            return new Mesh { bindposes = new[] { bone.worldToLocalMatrix * root.transform.localToWorldMatrix } };
        }

        [Test]
        public void ABoneWhereTheMeshWasBound_Conforms()
        {
            Mesh mesh = BuildOneBoneRig(out string[] names);

            Assert.IsEmpty(RigConformance.Compare(root.transform, mesh, names));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ABoneInAnotherPose_IsReportedWithHowFarItIs()
        {
            Mesh mesh = BuildOneBoneRig(out string[] names);
            root.transform.Find("Arm").localPosition = new Vector3(0.4f, 1f, 0f);

            var deviations = RigConformance.Compare(root.transform, mesh, names);

            Assert.AreEqual(1, deviations.Count);
            Assert.AreEqual("Arm", deviations[0].Bone);
            Assert.AreEqual(0.4f, deviations[0].Metres, 1e-4f);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ABoneTheRigLacks_IsReported()
        {
            Mesh mesh = BuildOneBoneRig(out _);

            var deviations = RigConformance.Compare(root.transform, mesh, new[] { "Tail" });

            Assert.AreEqual("Tail", deviations.Single().Bone);
            Object.DestroyImmediate(mesh);
        }

        // The proof the body is on the canonical A-pose rig (Docs/RigMigrationReport.md). FBX meshes keep their node's axis conversion, so the
        // armor is compared in the body's own mesh space, read from the body's chest part, which fits the rig it came from.
        [Test]
        public void EveryArmorPart_IsBoundToTheRestPoseOfTheBodyRig()
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HumanMaleSetup.RigPrefabPath));
            var chest = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(HumanMaleSetup.PartsFolder + "/Human_Male_Chest.asset");
            BodyPartPiece reference = chest.Pieces.First(p => p.IsSkinned);
            Matrix4x4 meshToRig = RigConformance.MeshToRig(root.transform, reference.Mesh, reference.BoneNames);
            Assert.IsEmpty(RigConformance.Compare(root.transform, reference.Mesh, reference.BoneNames, RigConformance.DefaultTolerance, meshToRig), "the body's own chest does not fit the body rig");
            var failures = new System.Collections.Generic.List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { ArmorSetup.PartsFolder }))
            {
                var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (BodyPartPiece piece in part.Pieces.Where(p => p.IsSkinned))
                {
                    var deviations = RigConformance.Compare(root.transform, piece.Mesh, piece.BoneNames, RigConformance.DefaultTolerance, meshToRig);
                    if (deviations.Count > 0) failures.Add(part.name + ": " + deviations.Count + " bones off, worst " + deviations[0]);
                }
            }

            Assert.IsEmpty(failures, "armor is bound to a different rest pose than the body rig:\n" + string.Join("\n", failures));
        }

        [Test]
        public void TheBodyRig_HasTheHeadgearSocket_UnderTheHead()
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HumanMaleSetup.RigPrefabPath));

            Transform socket = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == RigSockets.Headgear);

            Assert.IsNotNull(socket, "the body model needs an empty named " + RigSockets.Headgear + " under the Head bone (Docs/CharacterAppearance.md)");
            Assert.AreEqual("Head", socket.parent.name);
        }

        [Test]
        public void EveryRigidPart_AttachesToABoneOrSocketTheRigHas()
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HumanMaleSetup.RigPrefabPath));
            var index = new RigBoneIndex(root.transform);
            foreach (string folder in new[] { ArmorSetup.PartsFolder, HumanMaleSetup.PartsFolder })
            {
                foreach (string guid in AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { folder }))
                {
                    var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (BodyPartPiece piece in part.Pieces.Where(p => !p.IsSkinned))
                    {
                        Assert.IsTrue(index.TryGet(piece.AttachBoneName, out _), part.name + " attaches to '" + piece.AttachBoneName + "', which the body rig does not have");
                    }
                }
            }
        }
    }
}
