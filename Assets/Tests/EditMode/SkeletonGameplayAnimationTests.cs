using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    public class SkeletonGameplayAnimationTests
    {
        private GameObject skeleton;
        private Animator animator;

        [SetUp]
        public void SetUp()
        {
            skeleton = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab"));
            animator = skeleton.GetComponent<Animator>();
            animator.fireEvents = false; // Pose checks run without a network session or combat initialization.
            animator.Rebind();
            animator.Update(0);
            Step(3.5f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(skeleton);

        private void Step(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds * 120); i++) animator.Update(1f / 120);
        }

        [Test]
        public void Overhead_UsesSixtyFpsAndSynchronizedDamageWindow()
        {
            const string folder = "Assets/Prefabs/Characters/Enemies/";
            var main = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Skeleton_Overhead.anim");
            var offHand = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Skeleton_Overhead_OffHand.anim");
            foreach (var clip in new[] { main, offHand })
            {
                Assert.That(clip.length, Is.EqualTo(1.3f).Within(.0001f));
                Assert.AreEqual(60, clip.frameRate);
            }
            var allEvents = AnimationUtility.GetAnimationEvents(main);
            Assert.AreEqual(1, allEvents.Count(e => e.functionName == "PlayEvent"), "the swing sound is an audio event");
            var events = allEvents.Where(e => e.functionName != "PlayEvent").ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual("ActivateWeapon", events[0].functionName);
            Assert.That(events[0].time, Is.EqualTo(.55f).Within(.0001f));
            Assert.AreEqual("DeactivateWeaponH", events[1].functionName);
            Assert.That(events[1].time, Is.EqualTo(.75f).Within(.0001f));
            Assert.IsEmpty(AnimationUtility.GetAnimationEvents(offHand), "The off-hand layer must not duplicate damage events.");
        }

        [Test]
        public void Overhead_PreservesTheApprovedElbowFootAndThumb()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Overhead.anim");
            animator.enabled = false;
            Transform Bone(string name) => skeleton.GetComponentsInChildren<Transform>().First(t => t.name == name);
            var shoulder = Bone("RightUpperArm");
            var otherShoulder = Bone("LeftUpperArm");
            var elbow = Bone("RightLowerArm");
            var foot = Bone("LeftFoot");
            var hand = Bone("LeftHand");
            var thumb = Bone("LeftThumbDistal");
            clip.SampleAnimation(skeleton, 0);
            Vector3 footStart = foot.position, thumbStart = hand.InverseTransformPoint(thumb.position);
            for (int sample = 0; sample <= 156; sample++)
            {
                clip.SampleAnimation(skeleton, sample / 120f);
                Vector3 right = (shoulder.position - otherShoulder.position).normalized;
                Assert.Greater(Vector3.Dot(elbow.position - shoulder.position, right), .08f, "Elbow must stay outside through windup and recovery.");
                Assert.Less(Vector3.Distance(footStart, foot.position), .001f, "Left foot must remain planted.");
                Assert.Less(Vector3.Distance(thumbStart, hand.InverseTransformPoint(thumb.position)), .0001f, "Thumb must follow the hand without independent drift.");
            }
        }

        [Test]
        public void Slash_UsesApprovedTimingAndDamageWindow()
        {
            const string folder = "Assets/Prefabs/Characters/Enemies/";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Skeleton_Slash.anim");
            var offHand = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Skeleton_Slash_OffHand.anim");
            foreach (var c in new[] { clip, offHand })
            {
                Assert.That(c.length, Is.EqualTo(1.3f).Within(.0001f));
                Assert.AreEqual(60, c.frameRate);
            }
            var allEvents = AnimationUtility.GetAnimationEvents(clip);
            Assert.AreEqual(1, allEvents.Count(e => e.functionName == "PlayEvent"), "the swing sound is an audio event");
            var events = allEvents.Where(e => e.functionName != "PlayEvent").ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual("ActivateWeapon", events[0].functionName);
            Assert.That(events[0].time, Is.EqualTo(.524389f).Within(.0001f));
            Assert.AreEqual("DeactivateWeaponH", events[1].functionName);
            Assert.That(events[1].time, Is.EqualTo(.772265f).Within(.0001f));
            Assert.IsEmpty(AnimationUtility.GetAnimationEvents(offHand));
        }

        [Test]
        public void Slash_PreservesFrameThirteenGuardAndStableThumbAndToes()
        {
            animator.enabled = false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Slash.anim");
            Transform Bone(string name) => skeleton.GetComponentsInChildren<Transform>().First(t => t.name == name);
            var foot = Bone("LeftFoot");
            var hand = Bone("LeftHand");
            var thumb = Bone("LeftThumbDistal");
            var toes = skeleton.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Left") && t.name.Contains("Toe")).ToArray();
            clip.SampleAnimation(skeleton, 0);
            var toeStart = toes.Select(t => foot.InverseTransformPoint(t.position)).ToArray();
            var thumbStart = hand.InverseTransformPoint(thumb.position);
            for (int sample = 0; sample <= 156; sample++)
            {
                clip.SampleAnimation(skeleton, sample / 120f);
                for (int i = 0; i < toes.Length; i++)
                    Assert.Less(Vector3.Distance(toeStart[i], foot.InverseTransformPoint(toes[i].position)), .0001f, toes[i].name);
                Assert.Less(Vector3.Distance(thumbStart, hand.InverseTransformPoint(thumb.position)), .0001f, "Thumb must follow the hand.");
            }
            clip.SampleAnimation(skeleton, 12f / 60f);
            Vector3 up = (Bone("Neck").position - Bone("Hips").position).normalized;
            Assert.Less(Vector3.Dot(Bone("RightLowerArm").position - Bone("RightUpperArm").position, up), -.15f,
                "Frame 13 must keep the elbow below the shoulder instead of flaring sideways.");
        }

        [Test]
        public void ReadyArm_MatchesTheApprovedOverheadEntryPose()
        {
            animator.enabled = false;
            var ready = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Ready.anim");
            var overhead = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Overhead.anim");
            var arm = skeleton.GetComponentsInChildren<Transform>().Where(t => new[] { "RightUpperArm", "RightLowerArm", "RightHand" }.Contains(t.name)).ToArray();
            ready.SampleAnimation(skeleton, 0);
            var expected = arm.Select(t => t.localRotation).ToArray();
            overhead.SampleAnimation(skeleton, 0);
            for (int i = 0; i < arm.Length; i++) Assert.Less(Quaternion.Angle(expected[i], arm[i].localRotation), .1f, arm[i].name);
        }

        [Test]
        public void Walk_UsesAuthoredTimingAtOneXAndMatchingNavigationSpeed()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Walk.anim");
            Assert.That(clip.length, Is.EqualTo(1.2f).Within(.0001f));
            Assert.AreEqual(60, clip.frameRate);
            Assert.IsTrue(clip.isLooping);
            Assert.That(skeleton.GetComponent<UnityEngine.AI.NavMeshAgent>().speed, Is.EqualTo(.8f / clip.length).Within(.0001f));
            Assert.IsFalse(animator.applyRootMotion, "Navigation supplies translation exactly once.");
            var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
            var walk = controller.layers[0].stateMachine.states.First(s => s.state.name == "Walk").state;
            Assert.AreEqual(1, walk.speed, "Cadence must be authored, not accelerated at runtime.");
            Assert.IsFalse(walk.speedParameterActive);
        }

        [Test]
        public void Walk_PreservesSourceGroundContactsAndSwingKneeRecovery()
        {
            animator.enabled = false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Walk.anim");
            var bones = skeleton.GetComponentsInChildren<Transform>();
            var renderer = skeleton.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            try
            {
                for (int side = 0; side < 2; side++)
                {
                    string prefix = side == 0 ? "Left" : "Right";
                    var hip = bones.First(b => b.name == prefix + "UpperLeg");
                    var knee = bones.First(b => b.name == prefix + "LowerLeg");
                    var foot = bones.First(b => b.name == prefix + "Foot");
                    Vector3 anchor = Vector3.zero;
                    bool anchored = false;
                    float maximumSwingBend = 0;
                    for (int sample = 0; sample <= 288; sample++)
                    {
                        float frame = sample / 4f, time = frame / 60f;
                        clip.SampleAnimation(skeleton, time);
                        float phase = Mathf.Repeat(frame - (side == 0 ? 0 : 28f / 54 * 72), 72);
                        float stance = (side == 0 ? 34f : 32f) / 54 * 72;
                        float bend = 180 - Vector3.Angle(hip.position - knee.position, foot.position - knee.position);
                        Assert.Greater(bend, 15, "Knee must not lock or hyperextend.");
                        if (phase > stance) maximumSwingBend = Mathf.Max(maximumSwingBend, bend);
                        if (phase > 5 && phase < stance - 14)
                        {
                            Vector3 planted = foot.position + Vector3.forward * (.8f / 1.2f * time);
                            if (!anchored) { anchor = planted; anchored = true; }
                            Assert.Less(Vector3.Distance(anchor, planted), .001f, "Flat stance must remain planted at the actual travel speed.");
                        }
                        if (side == 0)
                        {
                            renderer.BakeMesh(mesh);
                            Assert.Greater(mesh.vertices.Min(v => renderer.transform.TransformPoint(v).y), -.001f,
                                "Skinned feet must not penetrate the floor between displayed frames.");
                        }
                    }
                    Assert.Greater(maximumSwingBend, 49, "Recovery must fold the knee instead of swinging a nearly straight leg.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Walk_WeaponLayerRetainsAuthoredSwordArmWhileMoving()
        {
            animator.SetBool("Walk", true);
            Step(.8f);
            int layer = animator.GetLayerIndex("Weapon_Arm");
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsName("Walk"));
            var hand = skeleton.GetComponentsInChildren<Transform>().First(t => t.name == "RightHand");
            Vector3 expected = hand.position;
            Quaternion rotation = hand.rotation;
            float phase = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Walk.anim");
            clip.SampleAnimation(skeleton, phase * clip.length);
            Assert.Less(Vector3.Distance(expected, hand.position), .002f);
            Assert.Less(Quaternion.Angle(rotation, hand.rotation), .25f);
        }

        [Test]
        public void Walk_WithWeaponLayers_StepsBothFeetAndStaysInPlace()
        {
            animator.SetBool("Walk", true);
            Step(.6f);
            foreach (string name in new[] { "LeftFoot", "RightFoot" })
            {
                Transform foot = skeleton.GetComponentsInChildren<Transform>().First(t => t.name == name);
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                for (int i = 0; i < 216; i++)
                {
                    animator.Update(1f / 120);
                    min = Mathf.Min(min, foot.position.z);
                    max = Mathf.Max(max, foot.position.z);
                    Assert.That(foot.position.y, Is.InRange(.04f, .22f), name + " must allow heel recovery while staying near the ground.");
                }
                Assert.Greater(max - min, .3f, name + " must step even with the weapon layers active.");
            }
            Assert.Less(skeleton.transform.position.magnitude, .001f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Death_AfterLocomotionOrAttack_LeavesEntireMeshOnGround(bool attack)
        {
            animator.SetBool("Walk", true);
            Step(2);
            if (attack) { animator.SetTrigger("Overhead"); Step(.65f); }
            animator.SetTrigger("Die");
            Step(3);
            var renderer = skeleton.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh);
                var vertices = mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
                Assert.GreaterOrEqual(vertices.Min(v => v.y), -.015f, "No bones should sink below the floor.");
                Assert.Less(vertices.Max(v => v.y), .25f, "Hands, head and all bone pieces must reach the pile.");
            }
            finally { Object.DestroyImmediate(mesh); }
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Die"));
        }

        [Test]
        public void Death_StartsFromIdleWithoutOpeningIntoTPose()
        {
            var death = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Characters/Enemies/Skeleton_Die.anim");
            Assert.AreEqual(60, death.frameRate);
            Assert.That(death.length, Is.EqualTo(1.6f).Within(.0001f));
            var hands = skeleton.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "LeftHand" || t.name == "RightHand").ToArray();
            var initial = hands.Select(t => t.position).ToArray();
            var previous = (Vector3[])initial.Clone();
            animator.SetTrigger("Die");
            for (int sample = 0; sample < 24; sample++)
            {
                animator.Update(1f / 120);
                for (int hand = 0; hand < hands.Length; hand++)
                {
                    Assert.Less(Vector3.Distance(initial[hand], hands[hand].position), .16f,
                        "Idle-to-death must retain the lowered arms until the fragments release.");
                    Assert.Less(Vector3.Distance(previous[hand], hands[hand].position), .03f,
                        "Weapon layers must fade out without a sudden hand-position jump.");
                    previous[hand] = hands[hand].position;
                }
            }
            animator.enabled = false;
            death.SampleAnimation(skeleton, 0);
            for (int hand = 0; hand < hands.Length; hand++)
                Assert.Less(Vector3.Distance(initial[hand], hands[hand].position),
                    hands[hand].name == "RightHand" ? .035f : .16f,
                    "The opening must retain the weapon guard and allow the unarmed idle arm's breathing phase.");
        }

        [Test]
        public void Death_FinalPileMatchesRiseOpening()
        {
            animator.enabled = false;
            const string folder = "Assets/Prefabs/Characters/Enemies/";
            var death = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Skeleton_Die.anim");
            var rise = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Skeleton_Rise.anim");
            var bones = skeleton.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            death.SampleAnimation(skeleton, death.length);
            var positions = bones.Select(b => b.position).ToArray();
            rise.SampleAnimation(skeleton, 0);
            for (int bone = 0; bone < bones.Length; bone++)
                Assert.Less(Vector3.Distance(positions[bone], bones[bone].position), .003f,
                    bones[bone].name + " must not jump when reassembly begins.");
        }
    }
}
