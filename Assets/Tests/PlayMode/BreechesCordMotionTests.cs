using System.Collections;
using System.Linq;
using NUnit.Framework;
using SimpleRPG.Appearance;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace SimpleRPG.Tests.PlayMode
{
    public class BreechesCordMotionTests
    {
#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AnimatedAnchors_WithUnevenFrames_SettleWithoutChatter()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab"));
            try
            {
                var parts = new GameObject("Parts").transform; parts.SetParent(instance.transform, false);
                var index = new RigBoneIndex(instance.transform.Find("Human_Male_Rig"));
                var item = BodyPartBinder.Create(AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/Breeches_BreechesCloth.asset"), index, parts);
                var motion = item.GetComponent<CordBoneMotion>(); motion.enabled = false;
                var anchors = motion.Profile.Chains.SelectMany(c => Enumerable.Repeat(index.Require(c.attachment), c.points.Length - 1)).ToArray();
                var previous = motion.SimulationBones.Select((b,i) => Quaternion.Inverse(anchors[i].rotation) * b.rotation).ToArray();
                var leg = index.Require("LeftLowerLeg"); var rest = leg.localRotation;
                float time = 0, peakSpeed = 0, settledSpeed = 0;
                float[] intervals = { 1f/120, 1f/30, 1f/90, 1f/20, 1f/60, 1f/144, 1f/40, .18f };
                int frame = 0;
                while (time < 12)
                {
                    float dt = intervals[frame++ % intervals.Length]; time += dt;
                    float movingTime = Mathf.Min(time, 8);
                    instance.transform.position = new Vector3(.06f * Mathf.Sin(movingTime*4), .008f*Mathf.Sin(movingTime*8), 0);
                    leg.localRotation = rest * Quaternion.Euler(12 * Mathf.Sin(movingTime*5), 0, 0);
                    motion.Simulate(dt);
                    for (int i = 0; i < previous.Length; i++)
                    {
                        var rotation = Quaternion.Inverse(anchors[i].rotation) * motion.SimulationBones[i].rotation;
                        var delta = Quaternion.Inverse(previous[i]) * rotation;
                        float angle = 2 * Mathf.Atan2(new Vector3(delta.x,delta.y,delta.z).magnitude, Mathf.Abs(delta.w)) * Mathf.Rad2Deg;
                        float speed = angle / dt;
                        if (time > 1 && time < 7) peakSpeed = Mathf.Max(peakSpeed, speed);
                        if (time > 11) settledSpeed = Mathf.Max(settledSpeed, speed);
                        previous[i] = rotation;
                    }
                }
                Debug.Log($"Cord temporal stability: peak={peakSpeed:F1}deg/s, settled={settledSpeed:F2}deg/s.");
                Assert.Less(peakSpeed, 180, "Uneven frame times produce jerky bone rotations.");
                Assert.Less(settledSpeed, 3, "Cords keep chattering after the character stops.");
                yield return null;
            }
            finally { Object.Destroy(instance); }
        }

        [UnityTest]
        public IEnumerator BoneChains_SwayAt30And60Fps_KeepLengthsAndRoots_AndResetTeleport()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab");
            var instance = Object.Instantiate(asset);
            try
            {
                var parts = new GameObject("Parts").transform; parts.SetParent(instance.transform, false);
                var definition = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/Breeches_BreechesCloth.asset");
                var index = new RigBoneIndex(instance.transform.Find("Human_Male_Rig"));
                var item = BodyPartBinder.Create(definition, index, parts);
                var motion = item.GetComponent<CordBoneMotion>(); motion.enabled = false;
                var renderer = item.GetComponent<SkinnedMeshRenderer>();
                Assert.AreEqual(0, item.GetComponentsInChildren<Cloth>().Length); Assert.AreEqual(1, renderer.sharedMesh.subMeshCount);
                float maxSway = 0;
                foreach (int rate in new[] { 30, 60 })
                {
                    instance.transform.position = Vector3.zero; motion.ResetMotion();
                    for (int frame = 0; frame < rate * 3; frame++)
                    {
                        float t = (float)frame / rate; instance.transform.position = new Vector3(.12f * Mathf.Sin(t*8), .02f*Mathf.Sin(t*16),0);
                        motion.Simulate(1f / rate); int offset = 0;
                        foreach (var chain in motion.Profile.Chains)
                        {
                            var anchor = index.Require(chain.attachment); int n = chain.points.Length - 1;
                            Assert.Less(Vector3.Distance(motion.SimulationBones[offset].position,anchor.TransformPoint(chain.points[0])), .00001f);
                            for (int j = 1; j < n; j++)
                            {
                                var a = motion.SimulationBones[offset+j-1]; var b = motion.SimulationBones[offset+j];
                                Assert.AreEqual(Vector3.Distance(chain.points[j-1],chain.points[j]),Vector3.Distance(a.position,b.position), .00001f);
                                maxSway = Mathf.Max(maxSway, Vector3.Distance(b.position, anchor.TransformPoint(chain.points[j])));
                                Assert.IsTrue(float.IsFinite(b.position.x) && float.IsFinite(b.position.y) && float.IsFinite(b.position.z));
                            }
                            offset += n;
                        }
                    }
                }
                Assert.Greater(maxSway, .001f); Assert.Less(maxSway, .1f);
                instance.transform.position += Vector3.right * 3; motion.Simulate(1f/60);
                int i = 0;
                foreach (var chain in motion.Profile.Chains)
                    for (int j = 0; j < chain.points.Length - 1; j++)
                        Assert.Less(Vector3.Distance(motion.SimulationBones[i++].position,index.Require(chain.attachment).TransformPoint(chain.points[j])), .00001f);
                Debug.Log($"Breeches bone motion: max sway {maxSway:F4}m;30/60fps, segment lengths, roots and teleport passed.");
                yield return null;
            }
            finally { Object.Destroy(instance); }
        }
#endif
    }
}
