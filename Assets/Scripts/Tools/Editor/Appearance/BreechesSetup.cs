using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimpleRPG.Appearance;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>Imports the canonical breeches and preserves cord simulation through equipment binding.</summary>
    public static class BreechesSetup
    {
        public const string Folder = "Assets/Art/3d Models/Items/Breeches";
        public const string PrefabPath = Folder + "/Breeches.prefab";

        [MenuItem("SimpleRPG/Items/Open Breeches Motion Review")]
        public static void CreateReviewScene()
        {
            string scenePath = "Assets/Scenes/Reviews/BreechesMotionReview.unity";
            Directory.CreateDirectory("Assets/Scenes/Reviews");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var stage = new GameObject("Breeches bone motion review");
            var character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab"));
            character.name = "Character"; character.transform.SetParent(stage.transform, false);
            var partsRoot = new GameObject("Parts").transform; partsRoot.SetParent(character.transform, false);
            var index = new RigBoneIndex(character.transform.Find("Human_Male_Rig"));
            var view = new AppearanceView(index, partsRoot);
            foreach (string guid in AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { "Assets/Art/BodyParts/HumanMale" }))
            {
                var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (part.Slot != BodyPartSlot.Legs) view.Show(part);
            }
            view.Show(AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/Breeches_BreechesCloth.asset"));
            string clipPath = Folder + "/BreechesMotionReviewMotion.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.ClearCurves(); clip.frameRate = 30;
            const float duration = 1.4f;
            void Curve(string path, string property, Func<float, float> value)
            {
                var keys = Enumerable.Range(0, 43).Select(i => { float t = i * duration / 42; return new Keyframe(t, value(t)); }).ToArray();
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(keys));
            }
            Curve("Character", "m_LocalPosition.x", t => .12f * Mathf.Sin(t * 2 * Mathf.PI / duration));
            Curve("Character", "m_LocalPosition.y", t => .012f * (1 - Mathf.Cos(t * 4 * Mathf.PI / duration)));
            foreach (string side in new[] { "Left", "Right" })
                foreach (string segment in new[] { "UpperLeg", "LowerLeg" })
                {
                    Transform bone = index.Require(side + segment); Quaternion rest = bone.localRotation;
                    string path = AnimationUtility.CalculateTransformPath(bone, stage.transform);
                    float phase = side == "Left" ? 0 : Mathf.PI;
                    Quaternion Rotation(float t)
                    {
                        float wave = Mathf.Sin(t * 2 * Mathf.PI / duration + phase);
                        float angle = segment == "UpperLeg" ? wave * 16 : (wave + 1) * 9;
                        return rest * Quaternion.AngleAxis(angle, Vector3.right);
                    }
                    Curve(path, "m_LocalRotation.x", t => Rotation(t).x);
                    Curve(path, "m_LocalRotation.y", t => Rotation(t).y);
                    Curve(path, "m_LocalRotation.z", t => Rotation(t).z);
                    Curve(path, "m_LocalRotation.w", t => Rotation(t).w);
                }
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings); clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
            string controllerPath = Folder + "/BreechesMotionReview.controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (controller == null) controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.states.Length == 0 ? stateMachine.AddState("Walking motion") : stateMachine.states[0].state;
            state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var animator = stage.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Review floor"; floor.transform.localScale = Vector3.one * .3f;
            var cameraObject = new GameObject("Breeches review camera"); var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera"; cameraObject.transform.position = new Vector3(.85f, .8f, 2.2f); cameraObject.transform.LookAt(new Vector3(0, .73f, 0));
            camera.orthographic = true; camera.orthographicSize = .83f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .17f);
            var lightObject = new GameObject("Review key light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; lightObject.transform.rotation = Quaternion.Euler(35, 145, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.5f, .55f, .65f); RenderSettings.ambientEquatorColor = new Color(.3f, .32f, .35f); RenderSettings.ambientGroundColor = new Color(.16f, .15f, .14f);
            RenderSettings.fog = false;
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = stage;
            SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0, .8f, 0), new Vector3(.8f, 1.7f, .6f)), false);
        }

        [Serializable] private class ChainInput { public CordMotionProfile.Chain[] chains; }

        [MenuItem("SimpleRPG/Items/Build Breeches Bone Motion")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/Breeches.fbx");
            importer.globalScale = 1; importer.isReadable = true; importer.importAnimation = false;
            importer.importCameras = false; importer.importLights = false; importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.optimizeMeshPolygons = false; importer.optimizeMeshVertices = false; importer.SaveAndReimport();
            foreach (string file in Directory.GetFiles(Folder + "/Textures", "BreechesAtlas_*.png"))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                ti.textureType = file.Contains("_Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = file.Contains("_BaseColor"); ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false; ti.mipmapEnabled = true; ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            }
            var material = Linen("BreechesMasked", new Color32(92, 68, 48, 255));
            material.SetFloat("_FourColorMask", 1); material.SetColor("_WrapColor", new Color32(91, 42, 28, 255));
            material.SetColor("_CordColor", Color.white); EditorUtility.SetDirty(material);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Breeches.fbx"));
            try
            {
                instance.name = "Breeches";
                var renderer = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                renderer.sharedMaterials = new[] { material };
                if (renderer.sharedMesh.subMeshCount != 1) throw new InvalidOperationException("Breeches must export one submesh.");
                var profile = BuildMotionProfile(renderer, new RigBoneIndex(instance.transform));
                renderer.gameObject.AddComponent<CordBoneMotion>().Configure(profile);
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                MakeIcon(instance);
                var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/Breeches_BreechesCloth.asset");
                if (part != null)
                {
                    part.Configure(BodyPartSlot.Legs, renderer.sharedMesh, renderer.sharedMaterials, renderer.bones.Select(b => b.name).ToArray(),
                        renderer.rootBone.name, renderer.localBounds, "", Vector3.zero, Quaternion.identity, Vector3.one, profile);
                    part.SetAdditionalPieces(Array.Empty<BodyPartPiece>()); EditorUtility.SetDirty(part);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            AssetDatabase.SaveAssets(); Debug.Log("Breeches: one renderer, one material, 10 bone chains / 22 simulated bones.");
        }

        private static CordMotionProfile BuildMotionProfile(SkinnedMeshRenderer renderer, RigBoneIndex rig)
        {
            string path = Folder + "/BreechesMotion.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CordMotionProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<CordMotionProfile>(); AssetDatabase.CreateAsset(profile, path); }
            var raw = JsonUtility.FromJson<ChainInput>(File.ReadAllText("I:/Art/3d Models/Breeches/Authoring/SecondaryChains.json"));
            Vector3 FromBlender(Vector3 p) => new Vector3(p.x, p.z, -p.y);
            var binds = renderer.sharedMesh.bindposes.ToList();
            var offsets = new List<int>();
            foreach (var chain in raw.chains)
            {
                Transform anchor = rig.Require(chain.attachment); offsets.Add(binds.Count);
                for (int j = 0; j < chain.points.Length; j++)
                {
                    Vector3 world = FromBlender(chain.points[j]);
                    if (j < chain.points.Length - 1)
                        binds.Add(Matrix4x4.TRS(world, anchor.rotation, Vector3.one).inverse * renderer.localToWorldMatrix);
                    chain.points[j] = anchor.InverseTransformPoint(world);
                    chain.normals[j] = anchor.InverseTransformDirection(FromBlender(chain.normals[j])).normalized;
                }
            }
            var weights = renderer.sharedMesh.boneWeights; var colors = renderer.sharedMesh.colors;
            for (int i = 0; i < weights.Length; i++)
            {
                int chain = Mathf.RoundToInt(colors[i].g * 255) - 1;
                if (chain < 0 || colors[i].a > .5f) continue;
                int count = raw.chains[chain].points.Length - 1;
                float t = colors[i].b * count; int lower = Mathf.Min(Mathf.FloorToInt(t), count - 1);
                int upper = Mathf.Min(lower + 1, count - 1); float blend = upper == lower ? 0 : t - lower;
                weights[i] = new BoneWeight { boneIndex0 = offsets[chain] + lower, boneIndex1 = offsets[chain] + upper, weight0 = 1 - blend, weight1 = blend };
            }
            var generated = UnityEngine.Object.Instantiate(renderer.sharedMesh); generated.name = "Breeches_SimulatedSkin";
            generated.bindposes = binds.ToArray(); generated.boneWeights = weights;
            Mesh visible = profile.VisibleMesh;
            if (visible == null) { visible = new Mesh(); AssetDatabase.AddObjectToAsset(visible, profile); }
            EditorUtility.CopySerialized(generated, visible);
            var triangles = generated.triangles;
            generated.triangles = Enumerable.Range(0, triangles.Length / 3).Where(t => colors[triangles[t*3]].r < .5f).SelectMany(t => new[] { triangles[t*3], triangles[t*3+1], triangles[t*3+2] }).ToArray();
            generated.name = "Breeches_WithoutWaistString";
            Mesh hidden = profile.HiddenWaistMesh;
            if (hidden == null) { hidden = new Mesh(); AssetDatabase.AddObjectToAsset(hidden, profile); }
            EditorUtility.CopySerialized(generated, hidden); UnityEngine.Object.DestroyImmediate(generated);
            profile.Configure(renderer.sharedMesh, visible, hidden, raw.chains);
            EditorUtility.SetDirty(visible); EditorUtility.SetDirty(hidden); EditorUtility.SetDirty(profile); return profile;
        }

        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + ".png");
        private static Material Material(string name, string shader)
        {
            string path = Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(mat, path); }
            return mat;
        }
        private static Material Linen(string name, Color tint)
        {
            var mat = Material(name, "SimpleRPG/Equipment/Masked Cloth Lit");
            mat.SetTexture("_BaseMap", Texture("BreechesAtlas_BaseColor"));
            mat.SetTexture("_BumpMap", Texture("BreechesAtlas_Normal"));
            mat.SetTexture("_MetallicGlossMap", Texture("BreechesAtlas_MetallicSmoothness"));
            mat.SetTexture("_OcclusionMap", Texture("BreechesAtlas_Occlusion"));
            mat.SetTexture("_RecolorMaskMap", Texture("BreechesAtlas_RecolorMask"));
            mat.SetColor("_BaseColor", Color.white); mat.SetColor("_ClothColor", tint); mat.SetColor("_ThreadColor", new Color32(217, 201, 166, 255));
            mat.SetFloat("_Smoothness", 1); mat.SetFloat("_Metallic", 0); mat.SetFloat("_OcclusionStrength", 0.6f);
            mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(mat); return mat;
        }

        private static void MakeIcon(GameObject source)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            GameObject copy = null, cameraObject = null, lightObject = null;
            var rt = new RenderTexture(512, 512, 24); var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                copy = UnityEngine.Object.Instantiate(source); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, scene);
                foreach (var simulation in copy.GetComponentsInChildren<CordBoneMotion>()) simulation.enabled = false;
                foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                cameraObject = new GameObject("BreechesIconCamera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 31; camera.orthographic = true; camera.orthographicSize = 0.5f; camera.targetTexture = rt;
                camera.transform.position = new Vector3(0.08f, 0.57f, 2); camera.transform.LookAt(new Vector3(0, 0.54f, 0));
                lightObject = new GameObject("BreechesIconLight"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(35, 145, 0);
                camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                File.WriteAllBytes(Folder + "/Breeches_Icon.png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.ImportAsset(Folder + "/Breeches_Icon.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/Breeches_Icon.png");
            importer.textureType = TextureImporterType.Sprite; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        }
    }
}
