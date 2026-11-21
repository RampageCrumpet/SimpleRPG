using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>Damped accessory-bone springs follow the character animation without changing segment lengths.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SkinnedMeshRenderer)), DefaultExecutionOrder(100)]
    public sealed class CordBoneMotion : MonoBehaviour
    {
        [SerializeField] private CordMotionProfile profile;
        private SkinnedMeshRenderer meshRenderer;
        private State[] states;
        [SerializeField, HideInInspector] private GameObject bonesRoot;
        private bool torsoArmorVisible;
        private Transform[] blockerBones = Array.Empty<Transform>();
        private Vector3 previousPosition;
        private bool ready;
        private sealed class State
        {
            public CordMotionProfile.Chain spec;
            public Transform anchor;
            public Transform[] bones;
            public Vector3[] current, offsets, velocities;
            public Vector3 filteredAnchor;
            public Quaternion filteredRotation;
            public float[] lengths;
            public Vector3 lastAnchor;
        }
        public CordMotionProfile Profile => profile;
        public int SimulatedBoneCount { get; private set; }
        public IReadOnlyList<Transform> SimulationBones { get; private set; }

        private void Awake()
        {
            if (profile != null && !ready)
                Initialize(profile, new RigBoneIndex(GetComponent<SkinnedMeshRenderer>().rootBone));
        }

        public void Initialize(CordMotionProfile settings, RigBoneIndex rig)
        {
            Release(); profile = settings; meshRenderer = GetComponent<SkinnedMeshRenderer>();
            if (meshRenderer.sharedMesh != profile.Source && meshRenderer.sharedMesh != profile.VisibleMesh && meshRenderer.sharedMesh != profile.HiddenWaistMesh)
                throw new InvalidOperationException("Cord motion profile belongs to another mesh.");
            int bodyCount = profile.Source.bindposes.Length;
            var all = new List<Transform>();
            for (int i = 0; i < bodyCount; i++) all.Add(meshRenderer.bones[i]);
            bonesRoot = new GameObject("Breeches secondary bones"); bonesRoot.transform.SetParent(transform, false);
            bonesRoot.hideFlags = gameObject.hideFlags;
            var blockers = profile.Blockers;
            blockerBones = new Transform[blockers.Length];
            for (int b = 0; b < blockers.Length; b++) blockerBones[b] = rig.Require(blockers[b].bone);
            states = new State[profile.Chains.Length];
            var simulated = new List<Transform>();
            for (int c = 0; c < states.Length; c++)
            {
                var spec = profile.Chains[c]; int n = spec.points.Length;
                var s = new State { spec = spec, anchor = rig.Require(spec.attachment), bones = new Transform[n - 1], current = new Vector3[n], offsets = new Vector3[n - 1], velocities = new Vector3[n - 1], lengths = new float[n - 1] };
                for (int j = 0; j < n - 1; j++)
                {
                    var bone = new GameObject(spec.name + "_" + j).transform;
                    bone.SetParent(j == 0 ? bonesRoot.transform : s.bones[j - 1], false);
                    bone.gameObject.hideFlags = gameObject.hideFlags;
                    s.bones[j] = bone; all.Add(bone); simulated.Add(bone);
                    s.lengths[j] = Vector3.Distance(spec.points[j], spec.points[j + 1]);
                }
                states[c] = s;
            }
            meshRenderer.sharedMesh = torsoArmorVisible ? profile.HiddenWaistMesh : profile.VisibleMesh;
            meshRenderer.bones = all.ToArray(); meshRenderer.updateWhenOffscreen = true;
            var bounds = profile.Source.bounds; bounds.Expand(.4f); meshRenderer.localBounds = bounds;
            SimulationBones = simulated; SimulatedBoneCount = simulated.Count; ready = true; ResetMotion();
        }

        public void ResetMotion()
        {
            if (!ready) return;
            foreach (var s in states)
            {
                Reset(s);
            }
            previousPosition = transform.position;
        }

        private static void Apply(State s)
        {
            for (int j = 0; j < s.bones.Length; j++)
            {
                Vector3 rest = s.anchor.TransformVector(s.spec.points[j + 1] - s.spec.points[j]);
                var rotation = Quaternion.FromToRotation(rest, s.current[j + 1] - s.current[j]) * s.anchor.rotation;
                s.bones[j].SetPositionAndRotation(s.current[j], rotation);
            }
        }

        private static void Reset(State s)
        {
            for (int j = 0; j < s.current.Length; j++) s.current[j] = s.anchor.TransformPoint(s.spec.points[j]);
            Array.Clear(s.offsets, 0, s.offsets.Length); Array.Clear(s.velocities, 0, s.velocities.Length);
            s.filteredAnchor = s.lastAnchor = s.current[0]; s.filteredRotation = s.anchor.rotation;
            Apply(s);
        }

        /// <summary>Critically damped analytic springs remain stable with uneven rendering intervals.</summary>
        private static void Spring(ref Vector3 value, ref Vector3 velocity, Vector3 target, float dt)
        {
            const float frequency = 14;
            Vector3 error = value - target;
            Vector3 change = (velocity + error * frequency) * dt;
            float decay = Mathf.Exp(-frequency * dt);
            value = target + (error + change) * decay;
            velocity = (velocity - change * frequency) * decay;
        }

        /// <summary>Moves the tip out of every blocker capsule, then returns the unit direction from the segment's start so the segment keeps its length.</summary>
        private Vector3 PushOut(Vector3 tip, Vector3 start)
        {
            var blockers = profile.Blockers;
            for (int b = 0; b < blockers.Length; b++)
            {
                Transform bone = blockerBones[b];
                Vector3 a = bone.TransformPoint(blockers[b].start), c = bone.TransformPoint(blockers[b].end);
                Vector3 axis = c - a;
                float t = Mathf.Clamp01(Vector3.Dot(tip - a, axis) / Mathf.Max(axis.sqrMagnitude, 1e-8f));
                Vector3 closest = a + axis * t;
                float radius = blockers[b].radius * bone.lossyScale.x;
                Vector3 away = tip - closest;
                float distance = away.magnitude;
                if (distance < radius) tip = closest + (distance > 1e-5f ? away / distance : Vector3.up) * radius;
            }

            Vector3 d = tip - start;
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.down;
        }

        /// <summary>Public deterministic step also used by motion and teleport tests.</summary>
        public void Simulate(float deltaTime)
        {
            if (!ready || !float.IsFinite(deltaTime) || deltaTime <= 0) return;
            if ((transform.position - previousPosition).sqrMagnitude > .75f * .75f) { ResetMotion(); return; }
            previousPosition = transform.position;
            float dt = Mathf.Min(deltaTime, .1f);
            foreach (var s in states)
            {
                Vector3 root = s.anchor.TransformPoint(s.spec.points[0]);
                if ((root - s.lastAnchor).sqrMagnitude > .75f * .75f) { ResetMotion(); return; }
                s.lastAnchor = root;
                if (s.spec.waist && torsoArmorVisible) { Reset(s); continue; }

                // Lag is measured once per rendered pose. It never comes from projected particle
                // corrections, which used to feed collision/length errors back as velocity.
                float follow = 1 - Mathf.Exp(-12 * dt);
                s.filteredAnchor = Vector3.Lerp(s.filteredAnchor, root, follow);
                s.filteredRotation = Quaternion.Slerp(s.filteredRotation, s.anchor.rotation, follow);
                Vector3 lag = s.anchor.InverseTransformVector(s.filteredAnchor - root);
                float length = 0;
                foreach (float segment in s.lengths) length += segment;
                Quaternion rotationLag = Quaternion.Inverse(s.anchor.rotation) * s.filteredRotation;
                Vector3 gravity = s.anchor.InverseTransformDirection(Vector3.down) * (.02f * profile.GravityScale);
                s.current[0] = root;
                for (int j = 0; j < s.bones.Length; j++)
                {
                    Vector3 restLocal = (s.spec.points[j + 1] - s.spec.points[j]).normalized;
                    Vector3 force = lag * (.35f / Mathf.Max(length, .04f)) + (rotationLag * restLocal - restLocal) * .4f + gravity;
                    Vector3 target = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(force, restLocal), .18f);
                    Spring(ref s.offsets[j], ref s.velocities[j], target, dt);
                    Vector3 direction = s.anchor.TransformDirection((restLocal + s.offsets[j]).normalized);

                    // Retain the outward garment plane, but apply it only to the rendered pose;
                    // its correction is never integrated as fresh momentum on the next frame.
                    Vector3 normal = s.anchor.TransformDirection(s.spec.normals[j + 1]).normalized;
                    Vector3 restPoint = s.anchor.TransformPoint(s.spec.points[j + 1]);
                    float minimum = Mathf.Clamp((Vector3.Dot(restPoint - s.current[j], normal) - .001f) / s.lengths[j], -1, .9999f);
                    if (Vector3.Dot(direction, normal) < minimum)
                    {
                        Vector3 tangent = Vector3.ProjectOnPlane(direction, normal).normalized;
                        if (tangent.sqrMagnitude < .01f) tangent = Vector3.ProjectOnPlane(s.anchor.TransformDirection(restLocal), normal).normalized;
                        direction = normal * minimum + tangent * Mathf.Sqrt(1 - minimum * minimum);
                    }
                    s.current[j + 1] = s.current[j] + direction * s.lengths[j];
                    if (blockerBones.Length > 0) s.current[j + 1] = s.current[j] + PushOut(s.current[j + 1], s.current[j]) * s.lengths[j];
                }
                Apply(s);
            }
        }

        private void LateUpdate() => Simulate(Time.deltaTime);
        private void OnEnable() { if (ready) ResetMotion(); }
        public void SetTorsoArmorVisible(bool visible)
        {
            torsoArmorVisible = visible;
            if (ready) { meshRenderer.sharedMesh = visible ? profile.HiddenWaistMesh : profile.VisibleMesh; ResetMotion(); }
        }
        public void SetBoneHideFlags(HideFlags flags)
        { if (bonesRoot != null) foreach (var t in bonesRoot.GetComponentsInChildren<Transform>()) t.gameObject.hideFlags = flags; }
        public void Release()
        {
            ready = false;
            if (bonesRoot != null) { if (Application.isPlaying) Destroy(bonesRoot); else DestroyImmediate(bonesRoot); }
            bonesRoot = null; states = null; SimulatedBoneCount = 0;
        }
        private void OnDestroy() => Release();
#if UNITY_EDITOR
        public void Configure(CordMotionProfile settings) => profile = settings;
#endif
    }
}
