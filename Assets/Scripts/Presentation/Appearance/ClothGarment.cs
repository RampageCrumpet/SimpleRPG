using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Unity's built-in Cloth on a worn garment's skinned renderer, configured from a <see cref="ClothProfile"/>. Purely cosmetic and built only
    /// where something is drawn; the thigh colliders it collides with are made on the wearer's own bones and belong to the part's
    /// <see cref="BodyPartInstance"/>, which destroys them with the part. The Cloth's mesh must have no coincident back-to-back faces and no custom normals: Cloth keeps one particle and one normal per vertex position, and opposite-facing normals on one position jitter every frame.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SkinnedMeshRenderer)), DefaultExecutionOrder(100)]
    public sealed class ClothGarment : MonoBehaviour
    {
        // A character that moves this far in one frame was repositioned, not run; cloth must not take the jump as acceleration.
        private const float TeleportDistance = 0.75f;

        /// <summary>The layer of the cloth colliders. The physics layer collision matrix has it colliding with nothing, so these colliders never touch gameplay physics; Cloth uses the colliders it is given explicitly.</summary>
        public const string ColliderLayerName = PhysicsLayers.ClothCollisionName;

        private Cloth cloth;
        private Transform follow;
        private Vector3 previousPosition;

        public Cloth Cloth => cloth;

        /// <summary>True where presentation physics may be built: playing, and with a graphics device (a headless server has none).</summary>
        public static bool CanSimulate => Application.isPlaying && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        /// <summary>
        /// Adds the Cloth and its colliders. Returns false, leaving the garment as plain skinned mesh, if the profile does not fit the mesh; the
        /// reason is logged, never applied half-way. The colliders are handed to <paramref name="owner"/>.
        /// </summary>
        public bool Initialize(ClothProfile profile, RigBoneIndex rig, BodyPartInstance owner)
        {
            var skinned = GetComponent<SkinnedMeshRenderer>();
            string problem = profile.Problem(skinned.sharedMesh);
            if (problem != null)
            {
                Debug.LogError("Cloth not applied: " + problem, this);
                return false;
            }

            int layer = LayerMask.NameToLayer(ColliderLayerName);
            if (layer < 0)
            {
                Debug.LogError("Cloth not applied: the '" + ColliderLayerName + "' layer does not exist (Project Settings > Tags and Layers).", this);
                return false;
            }

            var pairs = new List<ClothSphereColliderPair>();
            var made = new List<GameObject>();
            foreach (ClothProfile.ThighCollider spec in profile.Colliders)
            {
                Transform thigh = rig.Require(spec.bone);
                var holder = new GameObject(name + " collider " + spec.bone) { layer = layer };
                holder.transform.SetParent(thigh, false);
                SphereCollider upper = MakeSphere(holder, spec.center0, spec.r0, thigh);
                SphereCollider lower = MakeSphere(holder, spec.center1, spec.r1, thigh);
                pairs.Add(new ClothSphereColliderPair(upper, lower));
                made.Add(holder);
            }

            // The caller's instance destroys these with the part, so a failure below must not leave them behind either.
            foreach (GameObject holder in made) owner.Own(holder);

            cloth = gameObject.AddComponent<Cloth>();
            cloth.enabled = false;
            cloth.stretchingStiffness = profile.StretchingStiffness;
            cloth.bendingStiffness = profile.BendingStiffness;
            cloth.damping = profile.Damping;
            cloth.friction = profile.Friction;
            cloth.worldVelocityScale = profile.WorldVelocityScale;
            cloth.worldAccelerationScale = profile.WorldAccelerationScale;
            cloth.clothSolverFrequency = profile.SolverFrequency;
            cloth.sleepThreshold = profile.SleepThreshold;
            cloth.useTethers = profile.UseTethers;
            cloth.useGravity = profile.UseGravity;
            if (cloth.coefficients.Length != profile.ParticleCount)
            {
                Debug.LogError("Cloth not applied: " + profile.name + " has " + profile.ParticleCount + " particles but Unity built " + cloth.coefficients.Length + " for '" + skinned.sharedMesh.name + "'. Rebake the profile.", this);
                Destroy(cloth);
                cloth = null;
                return false;
            }

            cloth.coefficients = profile.BuildCoefficients();
            cloth.sphereColliders = pairs.ToArray();

            // Cloth keeps stale vertices while its renderer is culled, so the pose it resumes from would be old.
            skinned.updateWhenOffscreen = true;
            follow = skinned.rootBone != null ? skinned.rootBone : transform;
            cloth.enabled = true;
            ResetMotion();
            return true;
        }

        /// <summary>Forgets the cloth's motion so the garment resumes from the current pose: on equip, enable and teleport.</summary>
        public void ResetMotion()
        {
            if (cloth == null) return;
            cloth.ClearTransformMotion();
            previousPosition = follow.position;
        }

        private static SphereCollider MakeSphere(GameObject holder, Vector3 center, float radius, Transform bone)
        {
            var sphere = holder.AddComponent<SphereCollider>();
            sphere.isTrigger = false; // Cloth ignores trigger colliders; the layer matrix keeps these out of gameplay physics instead
            sphere.center = center;
            sphere.radius = radius / Mathf.Max(bone.lossyScale.x, 1e-4f);
            return sphere;
        }

        private void OnEnable() => ResetMotion();

        private void LateUpdate()
        {
            if (cloth == null) return;
            if ((follow.position - previousPosition).sqrMagnitude > TeleportDistance * TeleportDistance) cloth.ClearTransformMotion();
            previousPosition = follow.position;
        }
    }
}
