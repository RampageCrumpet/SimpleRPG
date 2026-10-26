using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Appearance;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The sliced human male: every part is buildable and registered, parts bind to the shared rig, and swapping a slot replaces the piece.
    /// </summary>
    public class AppearanceTests
    {
        private const string PartsFolder = "Assets/Art/BodyParts/HumanMale";
        private const string RigPath = "Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab";
        private const string PlayerPath = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";

        private GameObject rig;
        private Transform partsRoot;
        private AppearanceView view;

        [SetUp]
        public void SetUp()
        {
            rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            partsRoot = new GameObject("Parts").transform;
            partsRoot.SetParent(rig.transform, false);
            view = new AppearanceView(new RigBoneIndex(rig.transform.Find("Human_Male_Rig")), partsRoot);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rig);

        private static BodyPartDefinition[] AllParts() => AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { PartsFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        private static BodyPartDefinition Part(BodyPartSlot slot) => AllParts().Single(p => p.Slot == slot);

        [Test]
        public void EveryPartIsBuildable_AndHasAPermanentId()
        {
            BodyPartDefinition[] parts = AllParts();
            Assert.AreEqual(8, parts.Length);
            foreach (BodyPartDefinition part in parts)
            {
                Assert.IsNull(part.Problem(), part.name);
                Assert.IsTrue(part.HasId, part.name + " has no id");
            }
        }

        [Test]
        public void EveryPartIsInTheDefinitionRegistry_WithAShortId()
        {
            foreach (BodyPartDefinition part in AllParts())
            {
                Assert.IsTrue(new RegistryBodyPartCatalog().TryGetId(part, out uint id), part.name + " is not registered");
                Assert.IsTrue(new RegistryBodyPartCatalog().TryGetPart(id, out BodyPartDefinition back) && back == part, part.name);
            }
        }

        [Test]
        public void ThereIsOneDefaultPartPerSlot_OnThePlayer()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            BodyPartSlot[] slots = player.GetComponent<CharacterAppearance>().DefaultParts.Select(p => p.Slot).ToArray();

            Assert.AreEqual(slots.Length, slots.Distinct().Count(), "two default parts share a slot");
            Assert.AreEqual(8, slots.Length);
        }

        [Test]
        public void ASkinnedPart_BindsEveryBoneToTheRig()
        {
            view.Show(Part(BodyPartSlot.Torso));

            SkinnedMeshRenderer renderer = partsRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(renderer);
            Assert.AreEqual(renderer.sharedMesh.bindposes.Length, renderer.bones.Length);
            Assert.IsTrue(renderer.bones.All(b => b != null && b.IsChildOf(rig.transform.Find("Human_Male_Rig"))), "a bone is not in the character's rig");
            Assert.AreEqual("Hips", renderer.rootBone.name);
        }

        [Test]
        public void ARigidPart_IsParentedToItsBone()
        {
            view.Show(Part(BodyPartSlot.Eyebrows));

            MeshRenderer renderer = rig.GetComponentsInChildren<MeshRenderer>().Single();
            Assert.AreEqual("Head", renderer.transform.parent.name);
        }

        [Test]
        public void ShowingAPart_ReplacesThePartInItsSlot()
        {
            BodyPartDefinition chest = Part(BodyPartSlot.Torso);
            view.Show(chest);
            view.Show(chest);

            Assert.AreEqual(1, partsRoot.childCount);
        }

        [Test]
        public void ClearingASlot_RemovesItsPart()
        {
            view.Show(Part(BodyPartSlot.Head));
            view.Clear(BodyPartSlot.Head);

            Assert.IsFalse(view.IsShowing(BodyPartSlot.Head));
            Assert.AreEqual(0, rig.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
        }

        [Test]
        public void ThePlayersRig_IsTheHumanMaleAvatar_AndDrivesTheCombatBones()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Animator animator = player.GetComponent<Animator>();

            Assert.IsTrue(animator.avatar.isValid && animator.avatar.isHuman);
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode, "hurtboxes must follow the animation even when nothing is rendered");
            Assert.IsNotNull(player.GetComponentsInChildren<Weapon>(true).SingleOrDefault(), "the sword is missing");
        }
    }
}
