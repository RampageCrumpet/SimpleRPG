using System.IO;
using System.Linq;
using SimpleRPG.Appearance;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// Review scene for trying Unity Cloth on the mail hauberk: the human wearing the hauberk and leather leggings, running the RunForward clip in place.
    /// An experiment: nothing here is part of the game's equipment path.
    /// </summary>
    public static class HauberkClothReview
    {
        private const string ScenePath = "Assets/Scenes/Reviews/HauberkClothReview.unity";
        private const string ControllerPath = "Assets/Scenes/Reviews/HauberkClothReview.controller";
        private const string RunClipFbx = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunForward.fbx";

        // Cloth tuning. Heights are in the hauberk mesh's own units (metres, vertical axis = Z in the exported mesh).
        public static bool UseUnityCloth = false;     // the hauberk now carries its own bone chains; Unity Cloth is the discarded experiment
        public static float WaistHeight = 1.06f;      // at and above this the mail follows the skin exactly
        public static float HemHeight = 0.65f;
        public static float HemLeash = 0.06f;         // how far the hem may stray from the skinned pose
        public static float ThighRadius = 0.075f;
        public static float Bending = 0.8f;
        public static float Damping = 0.6f;

        private const string IntegratedScenePath = "Assets/Scenes/Reviews/HauberkIntegratedClothReview.unity";

        [MenuItem("SimpleRPG/Items/Open Hauberk Cloth Review")]
        public static void Open() => Build(ScenePath, false);

        /// <summary>The same review scene with the production native Cloth (the hauberk's ClothProfile) attached, so Play shows what the game shows.</summary>
        [MenuItem("SimpleRPG/Items/Open Hauberk Integrated Cloth Review")]
        public static void OpenIntegrated() => Build(IntegratedScenePath, true);

        private static void Build(string scenePath, bool integratedCloth)
        {
            Directory.CreateDirectory("Assets/Scenes/Reviews");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var stage = new GameObject("Hauberk cloth review");
            var character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab"));
            character.name = "Character";
            character.transform.SetParent(stage.transform, false);
            var partsRoot = new GameObject("Parts").transform;
            partsRoot.SetParent(character.transform, false);
            var rigRoot = character.transform.Find("Human_Male_Rig");
            var index = new RigBoneIndex(rigRoot);
            var view = new AppearanceView(index, partsRoot);
            foreach (string guid in AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { "Assets/Art/BodyParts/HumanMale" }))
            {
                var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (part.Slot != BodyPartSlot.Legs && part.Slot != BodyPartSlot.Torso) view.Show(part);
            }

            view.Show(AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/ReinforcedLeatherLeggings_ReinforcedLeatherLeggings.asset"));
            view.Show(AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/MailHauberk_GAME_MailHauberk.asset"));

            var hauberk = view.ShownIn(BodyPartSlot.Torso);
            var renderer = hauberk.GetComponentInChildren<SkinnedMeshRenderer>();
            if (UseUnityCloth) Configure(renderer, index);
            if (integratedCloth)
            {
                // The binder builds Cloth only while playing, and this scene is built in edit mode, so attach the production setup here; it is saved with the scene.
                var profile = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>("Assets/Art/BodyParts/Armor/MailHauberk_GAME_MailHauberk.asset").Pieces[0].Cloth;
                if (profile == null) Debug.LogError("The hauberk part has no ClothProfile. Run SimpleRPG > Items > Bake Hauberk Cloth Profile.");
                else renderer.gameObject.AddComponent<ClothGarment>().Initialize(profile, index, hauberk.GetComponent<BodyPartInstance>());
            }

            // The human run, in place.
            var clip = AssetDatabase.LoadAllAssetsAtPath(RunClipFbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Length == 0 ? machine.AddState("Run") : machine.states[0].state;
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            if (!character.TryGetComponent(out Animator animator)) animator = character.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/3d Models/Human/Human_Male.fbx").OfType<Avatar>().FirstOrDefault();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Review floor";
            floor.transform.localScale = Vector3.one * .3f;
            var cameraObject = new GameObject("Review camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(1.6f, 1.0f, 1.6f);
            cameraObject.transform.LookAt(new Vector3(0, .85f, 0));
            camera.orthographic = true;
            camera.orthographicSize = .75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .14f, .17f);
            var lightObject = new GameObject("Review key light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            lightObject.transform.rotation = Quaternion.Euler(35, 145, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.5f, .55f, .65f);
            RenderSettings.ambientEquatorColor = new Color(.3f, .32f, .35f);
            RenderSettings.ambientGroundColor = new Color(.16f, .15f, .14f);
            RenderSettings.fog = false;
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = stage;
        }

        /// <summary>Adds a Cloth to the hauberk's renderer: free below the waist, pinned to the skin above it, blocked by capsules on the thighs and a sphere on the pelvis.</summary>
        public static Cloth Configure(SkinnedMeshRenderer renderer, RigBoneIndex rig)
        {
            var mesh = renderer.sharedMesh;
            if (!renderer.TryGetComponent(out Cloth cloth)) cloth = renderer.gameObject.AddComponent<Cloth>();
            Vector3[] vertices = mesh.vertices;
            var coefficients = new ClothSkinningCoefficient[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float height = vertices[i].z;
                float t = Mathf.Clamp01((WaistHeight - height) / (WaistHeight - HemHeight));
                t = t * t * (3 - 2 * t);
                coefficients[i].maxDistance = t < 0.001f || Mathf.Abs(vertices[i].x) > 0.28f ? 0f : Mathf.Max(0.02f, t * HemLeash);
                coefficients[i].collisionSphereDistance = 0f;
            }

            cloth.coefficients = coefficients;
            cloth.useGravity = true;
            cloth.stretchingStiffness = 1f;
            cloth.bendingStiffness = Bending;
            cloth.damping = Damping;
            cloth.friction = 0.4f;
            cloth.worldVelocityScale = 0.15f;
            cloth.worldAccelerationScale = 0.15f;
            cloth.useVirtualParticles = 1f;
            cloth.clothSolverFrequency = 120f;
            cloth.sleepThreshold = 0.1f;

            var capsules = new System.Collections.Generic.List<CapsuleCollider>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform thigh = rig.Require(side + "UpperLeg");
                Transform knee = rig.Require(side + "LowerLeg");
                Vector3 toKnee = thigh.InverseTransformPoint(knee.position);
                if (!thigh.TryGetComponent(out CapsuleCollider capsule)) capsule = thigh.gameObject.AddComponent<CapsuleCollider>();
                int axis = 0;
                for (int a = 1; a < 3; a++) if (Mathf.Abs(toKnee[a]) > Mathf.Abs(toKnee[axis])) axis = a;
                capsule.direction = axis;
                capsule.center = toKnee * 0.5f;
                capsule.radius = ThighRadius;
                capsule.height = toKnee.magnitude + ThighRadius * 1.2f;
                capsules.Add(capsule);
            }

            Transform hips = rig.Require("Hips");
            if (!hips.TryGetComponent(out SphereCollider sphere)) sphere = hips.gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.16f;
            cloth.capsuleColliders = capsules.ToArray();
            cloth.sphereColliders = new[] { new ClothSphereColliderPair(sphere) };
            return cloth;
        }
    }
}
