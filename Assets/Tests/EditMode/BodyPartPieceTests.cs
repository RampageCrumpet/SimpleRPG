using NUnit.Framework;
using SimpleRPG.Appearance;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// A part of several pieces (plate over a mail undercoat, a shell with a rigid buckle) shows every piece, hides them together, and removes
    /// them together, while a one-piece part is built exactly as before.
    /// </summary>
    public class BodyPartPieceTests
    {
        private const string PartsFolder = "Assets/Art/BodyParts/HumanMale";
        private const string RigPath = "Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab";

        private GameObject rig;
        private Transform partsRoot;
        private AppearanceView view;
        private BodyPartDefinition torso;
        private BodyPartDefinition eyebrows;
        private BodyPartDefinition combined;

        [SetUp]
        public void SetUp()
        {
            rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            partsRoot = new GameObject("Parts").transform;
            partsRoot.SetParent(rig.transform, false);
            view = new AppearanceView(new RigBoneIndex(rig.transform.Find("Human_Male_Rig")), partsRoot);

            BodyPartDefinition[] all = AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { PartsFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            torso = all.Single(p => p.Slot == BodyPartSlot.Torso);
            eyebrows = all.Single(p => p.Slot == BodyPartSlot.Eyebrows);

            // A torso (skinned) with a second skinned piece and a rigid piece on the head bone: every kind of piece in one part.
            combined = ScriptableObject.CreateInstance<BodyPartDefinition>();
            combined.name = "Combined";
            BodyPartPiece skin = torso.Pieces[0];
            combined.Configure(BodyPartSlot.Torso, skin.Mesh, skin.Materials, skin.BoneNames, skin.RootBoneName, skin.LocalBounds,
                "", Vector3.zero, Quaternion.identity, Vector3.one);
            combined.SetAdditionalPieces(new[] { skin, eyebrows.Pieces[0] });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(combined);
        }

        [Test]
        public void ASinglePiecePart_HasOnePiece_AndIsBuiltAsOneObject()
        {
            Assert.AreEqual(1, torso.Pieces.Count);

            view.Show(torso);

            Assert.AreEqual(1, partsRoot.childCount);
            Assert.IsNotNull(partsRoot.GetChild(0).GetComponent<SkinnedMeshRenderer>(), "a one-piece part is its own renderer object");
        }

        [Test]
        public void EveryPieceOfAPart_IsBuilt()
        {
            view.Show(combined);

            BodyPartInstance instance = view.ShownIn(BodyPartSlot.Torso).GetComponent<BodyPartInstance>();
            Assert.AreEqual(3, instance.Renderers.Count);
            Assert.AreEqual(2, instance.Renderers.OfType<SkinnedMeshRenderer>().Count());
            MeshRenderer rigid = instance.Renderers.OfType<MeshRenderer>().Single();
            Assert.AreEqual("Head", rigid.transform.parent.name, "a rigid piece is parented to its own bone");
        }

        [Test]
        public void ThePiecesOfAPart_ShareOneSlot()
        {
            view.Show(combined);
            view.Show(combined);

            Assert.AreEqual(1, partsRoot.childCount, "showing the part again replaces it");
            Assert.AreEqual(1, rig.GetComponentsInChildren<MeshRenderer>().Length, "the rigid piece is not duplicated");
        }

        [Test]
        public void ClearingAPart_RemovesEveryPiece_IncludingRigidOnesOnBones()
        {
            view.Show(combined);
            view.Clear(BodyPartSlot.Torso);

            Assert.AreEqual(0, rig.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
            Assert.AreEqual(0, rig.GetComponentsInChildren<MeshRenderer>().Length);
        }

        [Test]
        public void APartIsBuildable_OnlyIfEveryPieceIs()
        {
            Assert.IsNull(combined.Problem());

            combined.SetAdditionalPieces(new[] { new BodyPartPiece(null, null, null, "", default, "", Vector3.zero, Quaternion.identity, Vector3.one) });

            Assert.IsNotNull(combined.Problem());
        }
    }
}
