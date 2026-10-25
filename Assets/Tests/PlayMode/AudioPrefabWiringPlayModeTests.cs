#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleRPG.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// The shipped footstep wiring against the real clips and rigs: the skeleton's walk clip delivers its foot-down events to the footstep
    /// emitter at the approved times, and the player's foot bones in the real run clip produce one step per footfall with the prefab's own
    /// calibration, none while standing. Checks that steps happen and when; the sound itself is the recording's business.
    /// </summary>
    public sealed class AudioPrefabWiringPlayModeTests
    {
        private const string PlayerPath = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string Enemies = "Assets/Prefabs/Characters/Enemies/";

        private readonly List<Object> created = new List<Object>();
        private PlayableGraph graph;
        private bool? previousHeadless;

        [SetUp]
        public void SetUp()
        {
            if (AudioService.Current != null) Object.DestroyImmediate(AudioService.Current.gameObject);
            previousHeadless = AudioService.HeadlessOverride;
            AudioService.HeadlessOverride = false;
            AudioRouting.ResetCache();
            Track(new GameObject("Listener")).AddComponent<AudioListener>();
        }

        [TearDown]
        public void TearDown()
        {
            if (graph.IsValid()) graph.Destroy();
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            if (AudioService.Current != null) Object.DestroyImmediate(AudioService.Current.gameObject);
            AudioService.HeadlessOverride = previousHeadless;
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private void Play(Animator animator, AnimationClip clip)
        {
            graph = PlayableGraph.Create("AudioWiringTest");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "out", animator);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            graph.Play();
        }

        [UnityTest]
        public IEnumerator SkeletonWalkClipDeliversLeftThenRightFootDownAtTheApprovedTimes()
        {
            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(Enemies + "Skeleton_Walk.anim");
            Assert.IsNotNull(walk);
            GameObject skeleton = Track(new GameObject("Skeleton"));
            Animator animator = skeleton.AddComponent<Animator>();
            FootstepEmitter emitter = skeleton.AddComponent<FootstepEmitter>();
            List<(Foot foot, float time)> steps = new List<(Foot, float)>();
            float start = Time.time;
            emitter.Stepped += foot => steps.Add((foot, Time.time - start));

            Play(animator, walk);
            yield return new WaitForSeconds(walk.length);
            graph.Stop();

            Assert.GreaterOrEqual(steps.Count, 2, "one stride is two footfalls");
            Assert.AreEqual(Foot.Left, steps[0].foot);
            Assert.AreEqual(Foot.Right, steps[1].foot);

            // Absolute times include the first frame's start-up hitch, so check the gap: the approved footfalls are at 0.06 s and 0.68 s.
            Assert.AreEqual(0.62f, steps[1].time - steps[0].time, 0.12f);
        }

        [UnityTest]
        public IEnumerator PlayerFootBonesGiveOneStepPerFootfallWhileRunningAndNoneWhileStanding()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Assert.IsNotNull(prefab);
            Animator prefabAnimator = prefab.GetComponent<Animator>();
            AnimationClip run = null;
            foreach (AnimationClip clip in prefabAnimator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == "RunForward") run = clip;
            }

            Assert.IsNotNull(run);

            // Only the model and the footstep settings: the rest of the player needs the network.
            GameObject owner = Track(new GameObject("PlayerModel"));
            foreach (Transform child in prefab.transform)
            {
                if (child.GetComponentInChildren<Renderer>(true) != null || child.name.Contains("Armature") || child.childCount > 0)
                {
                    Object.Instantiate(child.gameObject, owner.transform, false);
                }
            }

            Animator animator = owner.AddComponent<Animator>();
            animator.avatar = prefabAnimator.avatar;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            FootstepEmitter emitter = owner.AddComponent<FootstepEmitter>();
            EditorUtility.CopySerialized(prefab.GetComponent<FootstepEmitter>(), emitter);
            emitter.Footsteps = null;

            List<Foot> steps = new List<Foot>();
            emitter.Stepped += steps.Add;

            // Standing still in the run pose: the bones go up and down but the character does not move.
            Play(animator, run);
            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual(0, steps.Count, "a character that is not moving makes no steps");

            // Running at 4 m/s for 2 s of a 0.67 s stride: about six footfalls.
            float until = Time.time + 2f;
            while (Time.time < until)
            {
                owner.transform.position += Vector3.forward * (4f * Time.deltaTime);
                yield return null;
            }

            Assert.That(steps.Count, Is.InRange(4, 8), "steps counted: " + steps.Count);
            for (int i = 1; i < steps.Count; i++)
            {
                Assert.AreNotEqual(steps[i - 1], steps[i], "feet should alternate");
            }
        }
    }
}
#endif
