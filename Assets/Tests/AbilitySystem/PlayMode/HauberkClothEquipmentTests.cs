using NUnit.Framework;
using SimpleRPG.Appearance;
using SimpleRPG.InventorySystem;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// The mail hauberk's native Unity Cloth through the real equipment path: Equipment -> EquipmentAppearance -> CharacterAppearance on a host,
    /// replicated to a genuinely separate client. Checks the lifecycle (equip, move, stop, teleport, unequip, re-equip, two wearers), that the
    /// colliders belong to their wearer, and that a profile that does not fit its mesh is refused rather than applied.
    /// </summary>
    public class HauberkClothEquipmentTests : RemoteClientTestBase
    {
        private const string HauberkItemPath = "Assets/Items/Armor/MailHauberk.asset";
        private const string BreechesItemPath = "Assets/Items/Armor/Breeches.asset";
        private const string ProfilePath = "Assets/Art/BodyParts/Armor/MailHauberk_ClothProfile.asset";
        private const string ShotFolder = "Assets/ClothDiagnostics/Results/integrated/";

        private ArmorData hauberk;
        private ArmorData breeches;
        private ClothProfile profile;
        private static bool measurementsReset;

        [UnitySetUp]
        public IEnumerator UseRealDefinitions()
        {
            // The real registry, so the hauberk's look is found the way the game finds it.
            DefinitionRegistry.Instance = Resources.Load<DefinitionRegistry>("DefinitionRegistry");
            hauberk = AssetDatabase.LoadAssetAtPath<ArmorData>(HauberkItemPath);
            breeches = AssetDatabase.LoadAssetAtPath<ArmorData>(BreechesItemPath);
            profile = AssetDatabase.LoadAssetAtPath<ClothProfile>(ProfilePath);
            Assert.IsNotNull(hauberk);
            Assert.IsNotNull(profile);
            Directory.CreateDirectory(ShotFolder);
            if (!measurementsReset) { File.WriteAllText(ShotFolder + "measurements.txt", ""); measurementsReset = true; }
            yield return null;
        }

        private static void Record(string line)
        {
            Debug.Log(line);
            File.AppendAllText(ShotFolder + "measurements.txt", line + System.Environment.NewLine);
        }

        private static ClothGarment[] Garments(Character body) => body.GetComponentsInChildren<ClothGarment>(true);

        private static Equipment EquipmentOf(Character body) => body.GetComponent<Equipment>();

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static int Count(Cloth cloth) => cloth.coefficients.Length;

        // The particle that swings furthest (largest Max Distance), and one fixed to the torso.
        private static (int hem, int torso) Probes(ClothProfile p)
        {
            ClothSkinningCoefficient[] c = p.BuildCoefficients();
            int hem = 0, torso = 0;
            for (int i = 0; i < c.Length; i++) if (c[i].maxDistance > c[hem].maxDistance) hem = i;
            for (int i = 0; i < c.Length; i++) if (c[i].maxDistance == 0f) { torso = i; break; }
            return (hem, torso);
        }

        // How far the pinned particles (Max Distance 0) are from where plain skinning puts them: the worst one on the torso (spine, chest, neck) and
        // the worst on a limb (arms, hands), with the limb bone that was worst. Measured at rest, because Cloth reads the pose one step behind a moving animation.
        private static (float torso, float limb, string limbBone, float torsoP95, float torsoMean, string worst) PinnedDeviation(ClothGarment garment, ClothProfile p)
        {
            var smr = garment.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = p.Mesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bind = mesh.bindposes;
            Transform[] bones = smr.bones;
            ClothSkinningCoefficient[] coefficients = p.BuildCoefficients();
            Vector3[] cloth = garment.Cloth.vertices;
            // Cloth welds vertices that share a position and keeps the skinning of one of them, so each particle is compared with the closest of its copies.
            var particleOf = new System.Collections.Generic.Dictionary<(int, int, int), int>();
            var best = new System.Collections.Generic.List<(float distance, string top)>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                var key = (Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
                if (!particleOf.TryGetValue(key, out int particle))
                {
                    particle = best.Count;
                    particleOf.Add(key, particle);
                    best.Add((float.MaxValue, ""));
                }

                if (coefficients[particle].maxDistance != 0f) continue;
                BoneWeight w = weights[i];
                Vector3 skinned = Vector3.zero;
                foreach ((int index, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                    if (weight > 0f) skinned += (bones[index].localToWorldMatrix * bind[index]).MultiplyPoint3x4(v) * weight;
                float d = Vector3.Distance(skinned, smr.rootBone.TransformPoint(cloth[particle]));
                if (d < best[particle].distance) best[particle] = (d, bones[w.weight0 >= w.weight1 ? w.boneIndex0 : w.boneIndex1].name);
            }

            float torso = 0f, limb = 0f;
            string limbBone = "", worst = "";
            var torsoDistances = new System.Collections.Generic.List<float>();
            var heights = new System.Collections.Generic.Dictionary<int, float>();
            foreach (var pair in particleOf) heights[pair.Value] = pair.Key.Item3 / 10000f;
            for (int particle = 0; particle < best.Count; particle++)
            {
                if (coefficients[particle].maxDistance != 0f) continue;
                (float d, string top) = best[particle];
                bool isLimb = top.Contains("Arm") || top.Contains("Hand") || top.Contains("Shoulder") || top.Contains("Thumb") || top.Contains("Index") || top.Contains("Middle") || top.Contains("Ring") || top.Contains("Little");
                if (isLimb) { if (d > limb) { limb = d; limbBone = top; } }
                else
                {
                    torsoDistances.Add(d);
                    if (d > torso) { torso = d; worst = top + " at z " + heights[particle].ToString("F2"); }
                }
            }

            torsoDistances.Sort();
            float p95 = torsoDistances.Count > 0 ? torsoDistances[(int)(torsoDistances.Count * 0.95f)] : 0f;
            float mean = torsoDistances.Count > 0 ? torsoDistances.Average() : 0f;
            return (torso, limb, limbBone, p95, mean, worst);
        }

        // Where plain skinning puts a particle (world space), using the first mesh vertex that Cloth welded into it. The Max Distance leash limits how far
        // the particle strays from this point; it does not limit how far the skin itself moves (a hem tied to a thigh swings with the leg).
        private static Vector3 SkinnedParticle(ClothGarment garment, ClothProfile p, int particle)
        {
            var smr = garment.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = p.Mesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bind = mesh.bindposes;
            Transform[] bones = smr.bones;
            var seen = new System.Collections.Generic.HashSet<(int, int, int)>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                var key = (Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
                if (!seen.Add(key) || seen.Count - 1 != particle) continue;
                BoneWeight w = weights[i];
                Vector3 skinned = Vector3.zero;
                foreach ((int index, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                    if (weight > 0f) skinned += (bones[index].localToWorldMatrix * bind[index]).MultiplyPoint3x4(v) * weight;
                return skinned;
            }

            return Vector3.zero;
        }

        // Holds the pose still (animators paused, the server's upper-body articulation off) so Cloth's one-step latency cannot show as a gap.
        private static IEnumerator HoldStill(Character body)
        {
            foreach (Animator animator in body.GetComponentsInChildren<Animator>()) animator.speed = 0f;
            foreach (MonoBehaviour behaviour in body.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("Articulation")) behaviour.enabled = false;
            yield return Frames(15);
        }

        // Where a particle is relative to the hips, so the character's own movement does not count as cloth movement.
        private static Vector3 InHips(ClothGarment garment, int particle) => garment.Cloth.vertices[particle];

        // Feeds the character's real animator controller the speed the game would (AnimatableCharacter derives it from a controller this test does not have).
        private static void Locomote(Character body, float speed)
        {
            var driver = body.GetComponent<SimpleRPG.Animations.AnimatableCharacter>();
            if (driver != null) driver.enabled = false;
            foreach (Animator animator in body.GetComponentsInChildren<Animator>())
            {
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    if (parameter.name == "ForwardMovementSpeed" || parameter.name == "Speed") animator.SetFloat(parameter.name, speed);
                    else if (parameter.name == "Walk" && parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool(parameter.name, speed > 0.05f);
                }
            }
        }

        private IEnumerator Run(Character body, float seconds, float speed, float turnDegreesPerSecond = 0f)
        {
            Locomote(body, speed);
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                body.transform.position += body.transform.forward * (speed * Time.deltaTime);
                body.transform.Rotate(0f, turnDegreesPerSecond * Time.deltaTime, 0f);
                yield return null;
            }
        }

        private IEnumerator Equip(Character body, ArmorData item, EquipmentSlot slot, uint id)
        {
            Assert.IsTrue(EquipmentOf(body).Equip(slot, item, id), "the equipment accepts the " + item.name);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator Equipping_BuildsConfiguredClothOnTheServerAndOnTheClient()
        {
            Character onServer = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character onClient = null;
            yield return ClientCopyOf(onServer, copy => onClient = copy);
            Assert.AreEqual(0, Garments(onServer).Length, "nothing before the hauberk is worn");

            yield return Equip(onServer, hauberk, EquipmentSlot.chest, 500);
            yield return Until(() => Garments(onClient).Length == 1, 5f, "the client to build the hauberk");

            foreach (Character body in new[] { onServer, onClient })
            {
                ClothGarment[] garments = Garments(body);
                Assert.AreEqual(1, garments.Length, body.name);
                Cloth cloth = garments[0].Cloth;
                Assert.IsTrue(cloth.enabled);
                Assert.AreEqual(profile.ParticleCount, Count(cloth), "particle count matches the baked profile");
                Assert.AreEqual(profile.BuildCoefficients().Count(c => c.maxDistance > 0f), cloth.coefficients.Count(c => c.maxDistance > 0f), "the baked per-particle Max Distance is applied");
                Assert.AreEqual(profile.BuildCoefficients().Max(c => c.maxDistance), cloth.coefficients.Max(c => c.maxDistance), 1e-5f, "the hem leash is the baked one");
                Assert.AreEqual(profile.Damping, cloth.damping, 1e-5f);
                Assert.AreEqual(profile.WorldAccelerationScale, cloth.worldAccelerationScale, 1e-5f);
                Assert.AreEqual(profile.Colliders.Length, cloth.sphereColliders.Length, "one tapered sphere pair per thigh, registered explicitly with the Cloth");
                foreach (ClothSphereColliderPair pair in cloth.sphereColliders)
                {
                    foreach (SphereCollider sphere in new[] { pair.first, pair.second })
                    {
                        Assert.IsTrue(sphere.transform.IsChildOf(body.transform), "the colliders are on the wearer's own rig, not the prefab or another character");
                        Assert.AreEqual(LayerMask.NameToLayer("ClothCollision"), sphere.gameObject.layer, "on the dedicated ClothCollision layer");
                        Assert.GreaterOrEqual(sphere.gameObject.layer, 0, "the ClothCollision layer exists");
                        Assert.IsFalse(sphere.isTrigger, "Cloth ignores trigger colliders, so a trigger collider would be inert");
                        Assert.IsTrue(sphere.enabled);
                    }
                }

                Assert.IsTrue(garments[0].GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen);
                int layer = LayerMask.NameToLayer("ClothCollision");
                for (int other = 0; other < 32; other++)
                    Assert.IsTrue(Physics.GetIgnoreLayerCollision(layer, other), "ClothCollision must collide with no layer in the physics matrix (layer " + other + ")");
                foreach (int ordinary in new[] { 0, 9 })
                    Assert.IsFalse(Physics.GetIgnoreLayerCollision(ordinary, 0), "unrelated layer interactions are unchanged");
            }

            Assert.AreNotSame(Garments(onServer)[0].Cloth.sphereColliders[0].first, Garments(onClient)[0].Cloth.sphereColliders[0].first);
        }

        [UnityTest]
        public IEnumerator TheGarmentMovesWithTheWearer_TorsoStaysOnTheAnimation_AndSettlesAfterStopping()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            ClothGarment garment = Garments(body).Single();
            (int hem, _) = Probes(profile);
            yield return Run(body, 1.5f, 0f);
            Vector3 hemRest = InHips(garment, hem);
            yield return HoldStill(body);
            var pinnedAtRest = PinnedDeviation(garment, profile);
            foreach (Animator animator in body.GetComponentsInChildren<Animator>()) animator.speed = 1f;
            foreach (MonoBehaviour behaviour in body.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("Articulation")) behaviour.enabled = true;

            float hemMax = 0f, turnHem = 0f, hemFromSkin = 0f;
            var rootBone = garment.GetComponent<SkinnedMeshRenderer>().rootBone;
            float end = Time.time + 2f;
            while (Time.time < end)
            {
                float t = 1f - (end - Time.time) / 2f;
                Locomote(body, Mathf.Min(t / 0.5f, 1f) * 3.2f);
                body.transform.position += body.transform.forward * (Mathf.Min(t / 0.5f, 1f) * 3.2f * Time.deltaTime);
                yield return null;
                hemMax = Mathf.Max(hemMax, (InHips(garment, hem) - hemRest).magnitude);
                hemFromSkin = Mathf.Max(hemFromSkin, Vector3.Distance(rootBone.TransformPoint(InHips(garment, hem)), SkinnedParticle(garment, profile, hem)));
            }

            // Turning while moving.
            end = Time.time + 1.5f;
            while (Time.time < end)
            {
                Locomote(body, 3.2f);
                body.transform.position += body.transform.forward * (3.2f * Time.deltaTime);
                body.transform.Rotate(0f, 120f * Time.deltaTime, 0f);
                yield return null;
                turnHem = Mathf.Max(turnHem, (InHips(garment, hem) - hemRest).magnitude);
                hemFromSkin = Mathf.Max(hemFromSkin, Vector3.Distance(rootBone.TransformPoint(InHips(garment, hem)), SkinnedParticle(garment, profile, hem)));
            }

            Record($"Hauberk cloth hem probe: particle {hem} (Max Distance {profile.BuildCoefficients()[hem].maxDistance * 100f:F0} cm); moved {hemMax * 100f:F1} cm from its rest place in hips space (skinning included), strayed {hemFromSkin * 100f:F1} cm from where skinning puts it.");
            yield return Run(body, 3.5f, 0f);
            float settled = (InHips(garment, hem) - hemRest).magnitude;
            yield return HoldStill(body);
            var pinnedAfter = PinnedDeviation(garment, profile);
            Record($"Hauberk cloth: hem swing while running up to {hemMax * 100f:F1} cm, while turning up to {turnHem * 100f:F1} cm, pinned torso particles: mean {pinnedAtRest.torsoMean * 1000f:F1} mm, 95th percentile {pinnedAtRest.torsoP95 * 1000f:F1} mm, worst {pinnedAtRest.torso * 1000f:F1} mm ({pinnedAtRest.worst}) from the skin at rest and worst {pinnedAfter.torso * 1000f:F1} mm after the run (sleeves {pinnedAtRest.limb * 1000f:F1} / {pinnedAfter.limb * 1000f:F1} mm, worst bone {pinnedAfter.limbBone}), hem back within {settled * 100f:F1} cm of rest after stopping.");
            Assert.Greater(hemMax, 0.02f, "the hem lags the wearer's acceleration");
            Assert.Greater(turnHem, 0.02f, "the hem responds to turning");
            // Cloth reads the pose one physics step behind the animation, so at the run speed the hem can trail by that step's travel on top of its leash.
            float leash = profile.BuildCoefficients().Max(c => c.maxDistance);
            Assert.Less(hemFromSkin, leash + 3.2f * Time.fixedDeltaTime + 0.02f, "the hem never strays from where skinning puts it by more than its leash plus one step of the run (hemMax also counts the skin swinging with the thigh)");
            // Pinned (Max Distance 0) particles follow the skin to within a centimetre or so; a free hem moves ten times that. The same frame with
            // Cloth off is visually identical (see the *_skinonly pictures).
            Assert.Less(pinnedAtRest.torsoMean, 0.003f, "the torso sits on the skinned pose");
            Assert.Less(pinnedAtRest.torsoP95, 0.012f, "(95th percentile)");
            // The hip collider pushes the few pinned particles that lie inside it out of it (up to about 4 cm, with the colliders off the deviation is exactly zero), which keeps the mail from clipping the breeches.
            Assert.Less(pinnedAtRest.torso, 0.05f, "no pinned torso particle is far from it");
            Assert.Less(pinnedAfter.torsoP95, 0.012f, "and it is back on it after the wearer has run and stopped");
            Assert.Less(settled, 0.03f, "the garment settles back after the wearer stops");
        }

        // Where the hem's excursion comes from: the same run with the thigh colliders registered and with none. Records only; the tests above assert.
        private IEnumerator MeasureHemRun(bool withColliders, System.Action<float, float> result)
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            ClothGarment garment = Garments(body).Single();
            if (!withColliders) garment.Cloth.sphereColliders = new ClothSphereColliderPair[0];
            (int hem, _) = Probes(profile);
            var rootBone = garment.GetComponent<SkinnedMeshRenderer>().rootBone;
            yield return Run(body, 1.5f, 0f);
            Vector3 hemRest = InHips(garment, hem);
            yield return HoldStill(body);
            foreach (Animator animator in body.GetComponentsInChildren<Animator>()) animator.speed = 1f;
            foreach (MonoBehaviour behaviour in body.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("Articulation")) behaviour.enabled = true;
            float moved = 0f, fromSkin = 0f, end = Time.time + 2f;
            while (Time.time < end)
            {
                float t = 1f - (end - Time.time) / 2f;
                Locomote(body, Mathf.Min(t / 0.5f, 1f) * 3.2f);
                body.transform.position += body.transform.forward * (Mathf.Min(t / 0.5f, 1f) * 3.2f * Time.deltaTime);
                yield return null;
                moved = Mathf.Max(moved, (InHips(garment, hem) - hemRest).magnitude);
                fromSkin = Mathf.Max(fromSkin, Vector3.Distance(rootBone.TransformPoint(InHips(garment, hem)), SkinnedParticle(garment, profile, hem)));
            }

            result(moved, fromSkin);
        }

        // Skinning positions for every particle, read once (SkinnedParticle re-reads the mesh each call, which is too slow to run for all of them every frame).
        private sealed class SkinReference
        {
            private readonly Vector3[] vertices; private readonly BoneWeight[] weights; private readonly Matrix4x4[] bind; private readonly int[] vertexOfParticle;
            public SkinReference(ClothProfile p)
            {
                vertices = p.Mesh.vertices; weights = p.Mesh.boneWeights; bind = p.Mesh.bindposes;
                var seen = new System.Collections.Generic.HashSet<(int, int, int)>(); var first = new System.Collections.Generic.List<int>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = vertices[i];
                    if (seen.Add((Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f)))) first.Add(i);
                }
                vertexOfParticle = first.ToArray();
            }

            public Vector3 MeshPosition(int particle) => vertices[vertexOfParticle[particle]];

            public Vector3 At(Transform[] bones, int particle)
            {
                int i = vertexOfParticle[particle]; BoneWeight w = weights[i]; Vector3 skinned = Vector3.zero;
                foreach ((int index, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                    if (weight > 0f) skinned += (bones[index].localToWorldMatrix * bind[index]).MultiplyPoint3x4(vertices[i]) * weight;
                return skinned;
            }
        }

        [UnityTest]
        public IEnumerator Diagnostic_EveryFreeParticle_DistanceFromSkinning() => MeasureAllFree("baseline", null, true);

        [UnityTest]
        public IEnumerator Diagnostic_EveryFreeParticle_NoWorldTranslation() => MeasureAllFree("the body is NOT translated (animation only)", null, false);

        [UnityTest]
        public IEnumerator Diagnostic_EveryFreeParticle_NoWorldMotionScales() => MeasureAllFree("worldVelocityScale and worldAccelerationScale are 0", cloth => { cloth.worldVelocityScale = 0f; cloth.worldAccelerationScale = 0f; }, true);

        [UnityTest]
        public IEnumerator Diagnostic_EveryFreeParticle_NoGravityNoColliders() => MeasureAllFree("gravity off and no colliders", cloth => { cloth.useGravity = false; cloth.sphereColliders = new ClothSphereColliderPair[0]; }, true);

        [UnityTest]
        public IEnumerator Diagnostic_EveryFreeParticle_StandingStill() => MeasureAllFree("standing still, no locomotion", null, false, false);

        [UnityTest]
        public IEnumerator Diagnostic_EveryFreeParticle_RunningWithoutBending() => MeasureAllFree("running, bendingStiffness 0", cloth => cloth.bendingStiffness = 0f, true);

        [UnityTest]
        public IEnumerator Diagnostic_EveryFreeParticle_RunningWithoutBendingOrStretching() => MeasureAllFree("running, bending 0 and stretching 0", cloth => { cloth.bendingStiffness = 0f; cloth.stretchingStiffness = 0f; }, true);

        private IEnumerator MeasureAllFree(string label, System.Action<Cloth> configure, bool translate, bool locomote = true)
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            ClothGarment garment = Garments(body).Single();
            configure?.Invoke(garment.Cloth);
            var smr = garment.GetComponent<SkinnedMeshRenderer>(); var rootBone = smr.rootBone; var skin = new SkinReference(profile);
            ClothSkinningCoefficient[] c = profile.BuildCoefficients();
            Vector3 runtimeScale = smr.transform.lossyScale;
            var free = new System.Collections.Generic.List<int>();
            for (int i = 0; i < c.Length; i++) if (c[i].maxDistance > 0f) free.Add(i);
            var worst = new float[c.Length];
            yield return Run(body, 1.5f, 0f);
            yield return HoldStill(body);
            foreach (Animator animator in body.GetComponentsInChildren<Animator>()) animator.speed = 1f;
            foreach (MonoBehaviour behaviour in body.GetComponents<MonoBehaviour>())
                if (behaviour != null && behaviour.GetType().Name.Contains("Articulation")) behaviour.enabled = true;
            float end = Time.time + 2f;
            while (Time.time < end)
            {
                float t = 1f - (end - Time.time) / 2f;
                if (locomote) Locomote(body, Mathf.Min(t / 0.5f, 1f) * 3.2f);
                if (translate) body.transform.position += body.transform.forward * (Mathf.Min(t / 0.5f, 1f) * 3.2f * Time.deltaTime);
                yield return null;
                Vector3[] vertices = garment.Cloth.vertices;
                foreach (int p in free) worst[p] = Mathf.Max(worst[p], Vector3.Distance(rootBone.TransformPoint(vertices[p]), skin.At(smr.bones, p)));
            }

            // Where the free particles sit relative to where skinning puts them, at the end: the mean offset (world space) and how many are below / above their skin position.
            Vector3[] finalVertices = garment.Cloth.vertices; Vector3 meanOffset = Vector3.zero; int above = 0;
            foreach (int p in free)
            {
                Vector3 offset = rootBone.TransformPoint(finalVertices[p]) - skin.At(smr.bones, p); meanOffset += offset;
                if (offset.y > 0.05f) above++;
            }

            meanOffset /= free.Count;
            Record($"DIAGNOSTIC final offsets [{label}]: mean offset of a free particle from its skin position ({meanOffset.x * 100f:F1}, {meanOffset.y * 100f:F1}, {meanOffset.z * 100f:F1}) cm world; {above} of {free.Count} are more than 5 cm ABOVE it.");

            var sorted = free.OrderByDescending(p => worst[p]).ToList();
            int over = free.Count(p => worst[p] > c[p].maxDistance + 0.05f);
            string top = string.Join("; ", sorted.Take(6).Select(p => $"#{p} mesh({skin.MeshPosition(p).x:F2},{skin.MeshPosition(p).y:F2},{skin.MeshPosition(p).z:F2}) maxDist {c[p].maxDistance * 100f:F0} cm, strayed {worst[p] * 100f:F0} cm"));
            Record($"DIAGNOSTIC every free particle [{label}]: {free.Count} free, {over} stray more than 5 cm beyond their Max Distance; median stray {worst[sorted[sorted.Count / 2]] * 100f:F1} cm; SMR lossyScale {runtimeScale.x:F3}, cloth reports {garment.Cloth.coefficients.Length} coefficients. Worst: {top}");
        }

        [UnityTest]
        public IEnumerator Diagnostic_HemExcursion_WithColliders()
        {
            float moved = 0f, fromSkin = 0f;
            yield return MeasureHemRun(true, (m, s) => { moved = m; fromSkin = s; });
            Record($"DIAGNOSTIC hem excursion WITH the thigh colliders: moved {moved * 100f:F1} cm in hips space, {fromSkin * 100f:F1} cm from skinning.");
        }

        [UnityTest]
        public IEnumerator Diagnostic_HemExcursion_WithoutColliders()
        {
            float moved = 0f, fromSkin = 0f;
            yield return MeasureHemRun(false, (m, s) => { moved = m; fromSkin = s; });
            Record($"DIAGNOSTIC hem excursion WITHOUT any collider: moved {moved * 100f:F1} cm in hips space, {fromSkin * 100f:F1} cm from skinning.");
        }

        // Depth of the deepest free particle inside any registered collider surface (positive = inside).
        private static float DeepestInsideCollider(ClothGarment garment)
        {
            var cloth = garment.Cloth; var coefficients = cloth.coefficients; var vertices = cloth.vertices; var root = garment.GetComponent<SkinnedMeshRenderer>().rootBone; float deepest = 0f;
            for (int j = 0; j < coefficients.Length; j++)
            {
                if (coefficients[j].maxDistance <= 0f) continue;
                Vector3 p = root.TransformPoint(vertices[j]);
                foreach (ClothSphereColliderPair pair in cloth.sphereColliders)
                {
                    Vector3 a = pair.first.transform.TransformPoint(pair.first.center), b = pair.second.transform.TransformPoint(pair.second.center); var ab = b - a;
                    float ra = pair.first.radius * pair.first.transform.lossyScale.x, rb = pair.second.radius * pair.second.transform.lossyScale.x;
                    float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                    deepest = Mathf.Max(deepest, -(Vector3.Distance(p, a + ab * u) - Mathf.Lerp(ra, rb, u)));
                }
            }

            return deepest;
        }

        // Re-registers the colliders (so Cloth re-reads them), clears its motion and lets it settle in a frozen pose; returns the deepest free particle inside a collider.
        private IEnumerator SettleInFrozenPose(ClothGarment garment, bool asTriggers, System.Action<float> result)
        {
            foreach (ClothSphereColliderPair pair in garment.Cloth.sphereColliders) { pair.first.isTrigger = asTriggers; pair.second.isTrigger = asTriggers; }
            garment.Cloth.sphereColliders = garment.Cloth.sphereColliders.ToArray();
            garment.Cloth.ClearTransformMotion();
            yield return Frames(90);
            result(DeepestInsideCollider(garment));
        }

        [UnityTest]
        public IEnumerator TheClothActuallyCollides_WithItsColliders_UnderTheFinalLayerMatrix()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            ClothGarment garment = Garments(body).Single();
            // A running pose frozen mid-stride, so Cloth.vertices is not a physics step behind the bones. Working colliders leave no particle inside them;
            // the same colliders as triggers (the original defect, which Cloth ignores) do not hold the cloth out.
            yield return Run(body, 1.3f, 3.2f);
            yield return HoldStill(body);
            float working = 0f, triggers = 0f, moved = 0f;
            Vector3[] withColliders = null;
            yield return SettleInFrozenPose(garment, false, d => { working = d; withColliders = garment.Cloth.vertices.ToArray(); });
            yield return SettleInFrozenPose(garment, true, d =>
            {
                triggers = d;
                Vector3[] without = garment.Cloth.vertices;
                for (int i = 0; i < without.Length; i++) moved = Mathf.Max(moved, Vector3.Distance(withColliders[i], without[i]));
            });
            Record($"Cloth collision in a frozen running pose: deepest free particle inside a collider {working * 1000f:F1} mm with working colliders, {triggers * 1000f:F1} mm with trigger colliders; the colliders move a particle by up to {moved * 1000f:F1} mm.");

            // The thigh colliders are fitted tightly to the breeches, so the skirt itself barely reaches them in this pose and the depth above can show nothing either way.
            // What proves they act is that the same frozen pose settles differently with them working and with them as triggers (which Cloth ignores).
            Assert.Less(working, 0.008f, "collision works: no particle is left inside the thigh colliders");
            Assert.Greater(moved, 0.01f, "the colliders move the cloth: the same pose settles differently with working colliders and with trigger colliders");
        }

        [UnityTest]
        public IEnumerator GameplayQueryMasks_ExcludeTheClothColliders()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            ClothGarment garment = Garments(body).Single();
            int layer = LayerMask.NameToLayer("ClothCollision");
            Assert.AreEqual(layer, SimpleRPG.PhysicsLayers.ClothCollision);
            Assert.AreEqual(0, SimpleRPG.PhysicsLayers.AllExceptCloth & (1 << layer), "the 'everything' mask leaves out the cloth layer");
            Assert.AreEqual(0, SimpleRPG.PhysicsLayers.DefaultRaycastExceptCloth & (1 << layer), "so does the default raycast mask");
            Assert.AreNotEqual(0, Physics.DefaultRaycastLayers & (1 << layer), "Unity's own default mask would hit it, which is why the game's unfiltered queries use PhysicsLayers");
            Assert.AreEqual(Physics.DefaultRaycastLayers & ~(1 << layer), SimpleRPG.PhysicsLayers.DefaultRaycastExceptCloth, "otherwise the default mask is unchanged");
            // A real ray straight down onto a thigh collider: hits it with the default mask, misses it with the game's mask.
            var sphere = garment.Cloth.sphereColliders[0].first; Vector3 top = sphere.transform.TransformPoint(sphere.center) + Vector3.up * 1.5f; sphere.transform.root.GetComponentsInChildren<Collider>(true);
            yield return null;
            bool defaultHits = Physics.Raycast(top, Vector3.down, out RaycastHit h1, 3f) && h1.collider.gameObject.layer == layer;
            bool gameHits = Physics.Raycast(top, Vector3.down, out RaycastHit h2, 3f, SimpleRPG.PhysicsLayers.DefaultRaycastExceptCloth) && h2.collider.gameObject.layer == layer;
            Record($"Physics queries: a default ray onto a thigh collider hits the cloth layer: {defaultHits}; with PhysicsLayers.DefaultRaycastExceptCloth: {gameHits}.");
            Assert.IsFalse(gameHits, "the game's query mask never hits a cloth collider");
        }

        [UnityTest]
        public IEnumerator Teleporting_DoesNotThrowTheClothAbout()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            ClothGarment garment = Garments(body).Single();
            (int hem, _) = Probes(profile);
            yield return Run(body, 1.0f, 0f);
            Vector3 rest = InHips(garment, hem);

            body.transform.position += new Vector3(40f, 0f, 15f);
            float worst = 0f;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                worst = Mathf.Max(worst, (InHips(garment, hem) - rest).magnitude);
            }

            Record($"Hauberk cloth after a 43 m teleport: hem at most {worst * 100f:F1} cm from rest over the next 20 frames.");
            Assert.Less(worst, 0.04f, "the motion history is cleared, so the jump is not taken as acceleration");
        }

        [UnityTest]
        public IEnumerator Unequipping_RemovesTheClothAndItsColliders_WithoutTouchingOtherEquipment()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character onClient = null;
            yield return ClientCopyOf(body, copy => onClient = copy);
            yield return Equip(body, breeches, EquipmentSlot.legs, 400);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            yield return Until(() => Garments(onClient).Length == 1, 5f, "the client to build the hauberk");

            Transform thigh = body.GetComponentsInChildren<Transform>(true).First(t => t.name == "LeftUpperLeg");
            int thighChildren = thigh.childCount;
            int cordsBefore = body.GetComponentsInChildren<CordBoneMotion>(true).Length;
            int renderersBefore = body.GetComponentsInChildren<Renderer>(true).Length;
            Assert.AreEqual(1, cordsBefore, "the breeches' cord motion is present");
            Assert.AreEqual(2, thigh.GetComponentsInChildren<SphereCollider>(true).Count(), "the hauberk's sphere pair is under the thigh");

            EquipmentOf(body).Unequip(EquipmentSlot.chest);
            yield return Frames(3);
            yield return Until(() => Garments(onClient).Length == 0, 5f, "the client to remove the hauberk");

            Assert.AreEqual(0, Garments(body).Length, "the Cloth is gone");
            Assert.AreEqual(thighChildren - 1, thigh.childCount, "its collider holder on the thigh is gone");
            Assert.AreEqual(0, thigh.GetComponentsInChildren<SphereCollider>(true).Count(), "no cloth colliders are left on the wearer");
            Assert.AreEqual(cordsBefore, body.GetComponentsInChildren<CordBoneMotion>(true).Length, "the breeches still run their own motion");
            Assert.IsFalse(body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh == profile.Mesh), "the hauberk's mesh is gone (the bare torso part took its slot back)");
            Assert.AreEqual(renderersBefore, body.GetComponentsInChildren<Renderer>(true).Length, "nothing else was added or removed");
            Assert.IsTrue(body.GetComponentsInChildren<Renderer>(true).Any(r => r.GetComponentInParent<CordBoneMotion>() != null), "the breeches still render");
        }

        [UnityTest]
        public IEnumerator ReEquipping_BuildsAFreshClothWithoutDuplicates()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                EquipmentOf(body).Unequip(EquipmentSlot.chest);
                yield return Frames(3);
                Assert.AreEqual(0, Garments(body).Length);
                yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
                Assert.AreEqual(1, Garments(body).Length);
                Assert.AreEqual(profile.Colliders.Length * 2, body.GetComponentsInChildren<SphereCollider>(true).Count(s => s.name.Contains("collider")), "two thighs, one sphere pair each, no leftovers from earlier equips");
                Assert.AreEqual(profile.ParticleCount, Count(Garments(body)[0].Cloth));
            }
        }

        [UnityTest]
        public IEnumerator TwoWearers_UseTheirOwnCollidersAndSimulations()
        {
            Character a = SpawnOnServer(PlayerPrefab, new Vector3(0f, 0f, 0f), ClientId);
            Character b = SpawnOnServer(PlayerPrefab, new Vector3(5f, 0f, 0f));
            yield return Equip(a, hauberk, EquipmentSlot.chest, 500);
            yield return Equip(b, hauberk, EquipmentSlot.chest, 501);
            ClothGarment ga = Garments(a).Single(), gb = Garments(b).Single();
            Assert.AreNotSame(ga.Cloth, gb.Cloth);
            foreach (var (garment, owner, other) in new[] { (ga, a, b), (gb, b, a) })
            {
                foreach (ClothSphereColliderPair pair in garment.Cloth.sphereColliders)
                {
                    Assert.IsTrue(pair.first.transform.IsChildOf(owner.transform));
                    Assert.IsFalse(pair.first.transform.IsChildOf(other.transform));
                    Assert.IsTrue(pair.second.transform.IsChildOf(owner.transform));
                }
            }

            (int hem, _) = Probes(profile);
            yield return Run(a, 1.0f, 0f);
            yield return Run(b, 0.5f, 0f);
            Vector3 restA = InHips(ga, hem), restB = InHips(gb, hem);
            float swingA = 0f, swingB = 0f;
            float end = Time.time + 2f;
            Locomote(a, 3.2f);
            while (Time.time < end)
            {
                a.transform.position += a.transform.forward * (3.2f * Time.deltaTime);
                yield return null;
                swingA = Mathf.Max(swingA, (InHips(ga, hem) - restA).magnitude);
                swingB = Mathf.Max(swingB, (InHips(gb, hem) - restB).magnitude);
            }

            Record($"Two wearers: running wearer's hem swung {swingA * 100f:F1} cm, the standing wearer's {swingB * 100f:F1} cm.");
            Assert.Greater(swingA, 0.02f);
            Assert.Less(swingB, 0.02f, "the standing wearer's cloth does not feel the other's motion");

            // Taking it off one wearer leaves the other's cloth and colliders alone.
            EquipmentOf(a).Unequip(EquipmentSlot.chest);
            yield return Frames(3);
            Assert.AreEqual(0, Garments(a).Length);
            Assert.AreEqual(1, Garments(b).Length);
            Assert.IsTrue(gb.Cloth.sphereColliders.All(p => p.first != null && p.second != null), "the other wearer's colliders survive");
        }

        [UnityTest]
        public IEnumerator ADedicatedServerOrAStillPreview_BuildsNoCloth_AndStillShowsTheGarment()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero);
            var appearance = body.GetComponent<CharacterAppearance>();
            var view = new AppearanceView(new RigBoneIndex(appearance.RigRoot), new GameObject("PreviewParts").transform) { SimulateCloth = false };
            BodyPartDefinition part = ArmorParts.For(hauberk).Single();
            view.Show(part);
            GameObject shown = view.ShownIn(part.Slot);
            Assert.IsNotNull(shown);
            Assert.AreEqual(0, shown.GetComponentsInChildren<ClothGarment>(true).Length, "no Cloth when simulation is off");
            Assert.AreEqual(0, shown.GetComponentsInChildren<Cloth>(true).Length);
            Assert.IsNotNull(shown.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh, "the garment still renders");
            view.ClearAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AProfileThatDoesNotFitItsMesh_IsRefusedAndLoggedNotApplied()
        {
            ClothProfile wrong = ScriptableObject.CreateInstance<ClothProfile>();
            wrong.name = "WrongProfile";
            wrong.SetBake(profile.Mesh, "1611-deadbeef", profile.BuildCoefficients().Select(c => c.maxDistance).ToArray(), new float[profile.ParticleCount], profile.Colliders);
            Assert.IsNotNull(wrong.Problem(profile.Mesh), "a stale fingerprint is a problem");
            Assert.IsNull(profile.Problem(profile.Mesh), "the committed profile fits its mesh");
            Assert.IsNotNull(profile.Problem(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/3d Models/Items/Gambeson/Gambeson.fbx")), "another mesh is refused");

            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero);
            var go = new GameObject("Garment", typeof(SkinnedMeshRenderer), typeof(ClothGarment));
            go.GetComponent<SkinnedMeshRenderer>().sharedMesh = profile.Mesh;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Cloth not applied"));
            bool applied = go.GetComponent<ClothGarment>().Initialize(wrong, new RigBoneIndex(body.GetComponent<CharacterAppearance>().RigRoot), body.GetComponentInChildren<BodyPartInstance>());
            Assert.IsFalse(applied);
            Assert.AreEqual(0, go.GetComponents<Cloth>().Length, "no Cloth was added");
            UnityEngine.Object.Destroy(go);
            UnityEngine.Object.Destroy(wrong);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheEquippedGarment_AddsNoPerFrameAllocationOfItsOwn_AndPicturesAreCaptured()
        {
            Character body = SpawnOnServer(PlayerPrefab, Vector3.zero, ClientId);
            Character copy = null;
            yield return ClientCopyOf(body, c => copy = c);
            yield return Equip(body, hauberk, EquipmentSlot.chest, 500);
            ClothGarment garment = Garments(body).Single();
            yield return Until(() => Garments(copy).Length == 1, 5f, "the client to build the hauberk");
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = false; // the client's copy sits on the same spot; keep it out of the pictures
            var lightObject = new GameObject("ShotLight");
            lightObject.AddComponent<Light>().type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            yield return Frames(30);

            long Measure(int frames)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < frames; i++) garment.SendMessage("LateUpdate");
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }

            long perFrame = Measure(200) / 200;
            Record($"ClothGarment.LateUpdate allocates {perFrame} bytes per call.");
            Assert.AreEqual(0, perFrame, "the per-frame teleport check allocates nothing");

            // Pictures from a camera standing where the gameplay camera does (about 2.4 m back), at rest, mid-run, and just after stopping.
            var cameraObject = new GameObject("ShotCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.4f, 0.45f);
            yield return Run(body, 1.0f, 0f);
            yield return Shot(camera, body, "rest");
            float end = Time.time + 1.6f;
            Locomote(body, 3.2f);
            while (Time.time < end) { body.transform.position += body.transform.forward * (3.2f * Time.deltaTime); yield return null; }
            yield return Shot(camera, body, "running");
            yield return Run(body, 0.25f, 0f);
            yield return Shot(camera, body, "stopping");

            // The same moment with Cloth off (plain skinning), to tell a Cloth artifact from the art.
            garment.Cloth.enabled = false;
            yield return Frames(3);
            yield return Shot(camera, body, "stopping_skinonly");
            garment.Cloth.enabled = true;
            UnityEngine.Object.Destroy(cameraObject);
            UnityEngine.Object.Destroy(lightObject);
        }

        private IEnumerator Shot(Camera camera, Character body, string label)
        {
            foreach (var (view, offset, fov) in new[] { ("gameplay", new Vector3(0.4f, 1.6f, -2.4f), 50f), ("close", new Vector3(0.35f, 0.95f, -1.1f), 32f), ("upper", new Vector3(0.5f, 1.5f, -1.2f), 40f) })
            {
                camera.fieldOfView = fov;
                camera.transform.position = body.transform.TransformPoint(offset);
                camera.transform.LookAt(body.transform.position + Vector3.up * (view == "close" ? 0.85f : view == "upper" ? 1.3f : 1.0f));
                var rt = RenderTexture.GetTemporary(900, 600, 24);
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(900, 600, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 900, 600), 0, 0);
                tex.Apply();
                File.WriteAllBytes(ShotFolder + "hauberk_" + label + "_" + view + ".png", tex.EncodeToPNG());
                camera.targetTexture = null;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(tex);
            }

            yield return null;
        }
    }
}
