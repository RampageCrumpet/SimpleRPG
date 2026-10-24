using HitDetection;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The shipped bodies: which bone carries which region, that every region is a trigger on the HurtBox layer, that each body's table is the
    /// one the design calls for, and that no network message lets a client name the region it hit.
    /// </summary>
    public class HitRegionPrefabTests
    {
        private const string PlayerPath = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string SkeletonPath = "Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab";
        private const string GremlinPath = "Assets/Prefabs/Characters/Enemies/Gremlin.prefab";

        private static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            return prefab;
        }

        [TestCase(SkeletonPath, "Head", BodyLocation.Head)]
        [TestCase(SkeletonPath, "Hips", BodyLocation.Torso)]
        [TestCase(SkeletonPath, "Spine", BodyLocation.Torso)]
        [TestCase(SkeletonPath, "Chest", BodyLocation.Torso)]
        [TestCase(SkeletonPath, "UpperChest", BodyLocation.Torso)]
        [TestCase(SkeletonPath, "LeftUpperArm", BodyLocation.Arms)]
        [TestCase(SkeletonPath, "LeftLowerArm", BodyLocation.Arms)]
        [TestCase(SkeletonPath, "RightUpperArm", BodyLocation.Arms)]
        [TestCase(SkeletonPath, "RightLowerArm", BodyLocation.Arms)]
        [TestCase(SkeletonPath, "LeftUpperLeg", BodyLocation.Legs)]
        [TestCase(SkeletonPath, "LeftLowerLeg", BodyLocation.Legs)]
        [TestCase(SkeletonPath, "LeftFoot", BodyLocation.Legs)]
        [TestCase(SkeletonPath, "RightUpperLeg", BodyLocation.Legs)]
        [TestCase(SkeletonPath, "RightLowerLeg", BodyLocation.Legs)]
        [TestCase(SkeletonPath, "RightFoot", BodyLocation.Legs)]
        [TestCase(PlayerPath, "head.x", BodyLocation.Head)]
        [TestCase(PlayerPath, "spine_03.x", BodyLocation.Torso)]
        [TestCase(PlayerPath, "arm_twist.l", BodyLocation.Arms)]
        [TestCase(PlayerPath, "thigh_twist.r", BodyLocation.Legs)]
        public void ARegionIsOnTheBoneThatDefinesIt(string prefab, string bone, BodyLocation expected)
        {
            HurtBox region = Load(prefab).GetComponentsInChildren<HurtBox>(true).SingleOrDefault(h => h.name == bone);

            Assert.IsNotNull(region, $"no region on '{bone}'");
            Assert.AreEqual(expected, region.BodyLocation);
        }

        [TestCase(SkeletonPath)]
        [TestCase(PlayerPath)]
        [TestCase(GremlinPath)]
        public void EveryBodyCoversTheHeadTorsoArmsAndLegs_WithTriggersOnTheHurtBoxLayer(string path)
        {
            GameObject prefab = Load(path);
            HurtBox[] regions = prefab.GetComponentsInChildren<HurtBox>(true);

            foreach (BodyLocation expected in new[] { BodyLocation.Head, BodyLocation.Torso, BodyLocation.Arms, BodyLocation.Legs })
            {
                Assert.IsTrue(regions.Any(r => r.BodyLocation == expected), $"{path} has no {expected} region");
            }

            foreach (HurtBox region in regions)
            {
                Assert.AreEqual(LayerMask.NameToLayer("HurtBox"), region.gameObject.layer, region.name);
                Assert.IsTrue(region.GetComponents<Collider>().Length > 0 && region.GetComponents<Collider>().All(c => c.isTrigger), region.name + " is a trigger");
                Assert.IsNotNull(region.GetComponentInParent<Character>(true), region.name + " has an owner");
            }
        }

        [Test]
        public void TheSkeletonsSkull_IsOnTheAnimatedHeadBone_AndNothingIsLeftOnTheRoot()
        {
            GameObject skeleton = Load(SkeletonPath);
            HurtBox skull = skeleton.GetComponentsInChildren<HurtBox>(true).Single(h => h.BodyLocation == BodyLocation.Head);

            Assert.AreEqual("Head", skull.name);
            Assert.IsNotNull(skull.GetComponentInParent<Animator>(true), "the skull is below the animated skeleton");
            Assert.IsTrue(skull.transform.IsChildOf(skeleton.transform.Find("Skeleton_Rig")), "it rides the rig, not the character's root");
            Assert.IsNull(skeleton.transform.Find("TorsoHurtbox"), "the single static torso box has been replaced");
        }

        [TestCase(SkeletonPath)]
        [TestCase(PlayerPath)]
        public void TheDesignedMultipliers_AreOnEachBodysTable(string path)
        {
            HitAnatomy anatomy = Load(path).GetComponent<HitAnatomy>();

            Assert.IsNotNull(anatomy, path);
            Assert.IsNotNull(anatomy.Profile, path);
            Assert.AreEqual(1.75f, anatomy.DamageMultiplier((int)BodyLocation.Head));
            Assert.AreEqual(1f, anatomy.DamageMultiplier((int)BodyLocation.Torso));
            Assert.AreEqual(0.75f, anatomy.DamageMultiplier((int)BodyLocation.Arms));
            Assert.AreEqual(0.75f, anatomy.DamageMultiplier((int)BodyLocation.Legs));
        }

        [Test]
        public void TheCharacterAnswersTheResolverFromItsAnatomy()
        {
            Character player = Load(PlayerPath).GetComponent<Character>();

            Assert.AreEqual(1.75f, ((SimpleRPG.Gameplay.IRegionDamageModifier)player).DamageMultiplier((int)BodyLocation.Head));
        }

        [Test]
        public void NoNetworkMessage_LetsAClientNameTheRegionItHit()
        {
            System.Type[] regionTypes = { typeof(BodyLocation), typeof(HurtBox), typeof(HitAnatomy), typeof(HitCandidate) };
            MethodInfo[] messages = typeof(Character).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name.Contains("Rpc")))
                .ToArray();

            Assert.Greater(messages.Length, 0, "test set-up: the project does have network messages");
            foreach (MethodInfo message in messages)
            {
                foreach (ParameterInfo parameter in message.GetParameters())
                {
                    Assert.IsFalse(regionTypes.Contains(parameter.ParameterType), $"{message.DeclaringType.Name}.{message.Name} takes a {parameter.ParameterType.Name}");
                }
            }
        }
    }
}
