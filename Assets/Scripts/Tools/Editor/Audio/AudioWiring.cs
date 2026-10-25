using SimpleRPG.Audio;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AudioTools
{
    /// <summary>
    /// Connects the audio system to the game's content: components on the player and skeleton prefabs, the skeleton's animation events, item
    /// profiles and the scene's ambience controller. Idempotent, so it is safe to run again after changing the catalog.
    /// </summary>
    public static class AudioWiring
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string SkeletonPrefab = "Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab";
        private const string Enemies = "Assets/Prefabs/Characters/Enemies/";
        private const string SkeletonSounds = "Assets/Art/Audio/SFX/Skeleton/";
        private const string CombatSounds = "Assets/Art/Audio/SFX/Combat/";

        private static T Catalog<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AudioCatalog.Root + "/" + path);
            if (asset == null) Debug.LogError("[Audio] Missing catalog asset " + path);
            return asset;
        }

        [MenuItem("SimpleRPG/Audio/Wire Gameplay Prefabs And Scene")]
        public static void WireMenu()
        {
            Wire();
            Debug.Log("[Audio] Gameplay wiring finished.");
        }

        public static void Wire()
        {
            AudioCatalog.Build();

            AudioTag steel = Catalog<AudioTag>("Tags/Material_Steel.asset");
            AudioTag bone = Catalog<AudioTag>("Tags/Material_Bone.asset");
            AudioTag flesh = Catalog<AudioTag>("Tags/Material_Flesh.asset");
            AudioTag barefoot = Catalog<AudioTag>("Tags/Footwear_Barefoot.asset");
            AudioTag boots = Catalog<AudioTag>("Tags/Footwear_Boots.asset");

            AudioEventDefinition overhead = AudioCatalog.Event("Swing_Skeleton_Overhead", AudioCategory.Combat, 0.3f, 0.05f, CombatSounds + "SFX_Skeleton_Swing_Overhead_01.wav");
            AudioEventDefinition slash = AudioCatalog.Event("Swing_Skeleton_Slash", AudioCategory.Combat, 0.3f, 0.05f, CombatSounds + "SFX_Skeleton_Swing_Slash_01.wav");
            AudioEventDefinition death = AudioCatalog.Event("Death_Skeleton", AudioCategory.Characters, 0.2f, 0.05f, SkeletonSounds + "SFX_Skeleton_Die_01.wav", 70);

            WireSkeletonAnimations(overhead, slash);
            WireSkeletonPrefab(bone, barefoot, death);
            WirePlayerPrefab(flesh, boots);
            WireItems(steel);
            WireScene();
            AssetDatabase.SaveAssets();
        }

        // The swings already play from animation events; they now name an audio event instead of a bare clip, so they get mixer routing,
        // variation and limits. The walk gets foot-down events at the moments the footstep recordings were timed to.
        private static void WireSkeletonAnimations(AudioEventBase overhead, AudioEventBase slash)
        {
            ReplaceClipEvent(Enemies + "Skeleton_Overhead.anim", overhead);
            ReplaceClipEvent(Enemies + "Skeleton_Slash.anim", slash);

            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(Enemies + "Skeleton_Walk.anim");
            if (walk != null)
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(walk);
                foreach (AnimationEvent e in events)
                {
                    if (e.functionName == "OnFootDown") return;
                }

                AnimationEvent[] withSteps = new AnimationEvent[events.Length + 2];
                events.CopyTo(withSteps, 0);
                withSteps[events.Length] = new AnimationEvent { functionName = "OnFootDown", intParameter = 0, time = 0.06f };
                withSteps[events.Length + 1] = new AnimationEvent { functionName = "OnFootDown", intParameter = 1, time = 0.68f };
                AnimationUtility.SetAnimationEvents(walk, withSteps);
                EditorUtility.SetDirty(walk);
            }
        }

        private static void ReplaceClipEvent(string clipPath, AudioEventBase sound)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null || sound == null) return;

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            bool changed = false;
            foreach (AnimationEvent e in events)
            {
                if (e.functionName == "PlayClip")
                {
                    e.functionName = "PlayEvent";
                    e.objectReferenceParameter = sound;
                    changed = true;
                }
            }

            if (changed)
            {
                AnimationUtility.SetAnimationEvents(clip, events);
                EditorUtility.SetDirty(clip);
            }
        }

        private static void WireSkeletonPrefab(AudioTag bone, AudioTag barefoot, AudioEventBase death)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SkeletonPrefab);
            try
            {
                SerializedObject identity = new SerializedObject(Ensure<AudioIdentity>(root));
                Set(identity, "bodyMaterial", bone);
                Set(identity, "death", death);
                identity.ApplyModifiedPropertiesWithoutUndo();

                Ensure<CombatAudio>(root);

                MovementAudioState state = Ensure<MovementAudioState>(root);
                state.DefaultFootwear = barefoot;
                EditorUtility.SetDirty(state);

                // Steps come from the walk clip's foot-down events.
                Ensure<FootstepEmitter>(root);

                PrefabUtility.SaveAsPrefabAsset(root, SkeletonPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WirePlayerPrefab(AudioTag flesh, AudioTag boots)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                SerializedObject identity = new SerializedObject(Ensure<AudioIdentity>(root));
                Set(identity, "bodyMaterial", flesh);
                identity.ApplyModifiedPropertiesWithoutUndo();

                Ensure<CombatAudio>(root);

                MovementAudioState state = Ensure<MovementAudioState>(root);
                state.DefaultFootwear = boots;
                EditorUtility.SetDirty(state);
                Ensure<EquipmentMovementAudio>(root);

                // The player's clips carry no foot events, so steps come from the foot bones. The root sits 0.43 m above the soles on this rig
                // (measured from the planted foot in the run and strafe clips).
                FootstepEmitter emitter = Ensure<FootstepEmitter>(root);
                SerializedObject steps = new SerializedObject(emitter);
                steps.FindProperty("detectFromBones").boolValue = true;
                steps.FindProperty("rootToSoleOffset").floatValue = 0.43f;
                steps.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireItems(AudioTag steel)
        {
            ItemAudioProfile gems = Catalog<ItemAudioProfile>("Profiles/ItemAudio_Gems.asset");
            foreach (string guid in AssetDatabase.FindAssets("Brillaint t:Item", new[] { "Assets/Items/LootItems" }))
            {
                Object item = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
                SerializedObject so = new SerializedObject(item);
                Set(so, "audioProfile", gems);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }

            Object shortsword = AssetDatabase.LoadAssetAtPath<Object>("Assets/Items/Weapon Data/SkeletonShortsword.asset");
            if (shortsword != null)
            {
                SerializedObject so = new SerializedObject(shortsword);
                Set(so, "audioMaterial", steel);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(shortsword);
            }
        }

        private static void WireScene()
        {
            AmbienceController existing = Object.FindFirstObjectByType<AmbienceController>();
            if (existing != null) return;

            GameObject host = new GameObject("Ambience");
            AmbienceController controller = host.AddComponent<AmbienceController>();
            controller.DefaultProfile = Catalog<AmbienceProfile>("Profiles/Ambience_Crypt.asset");
            EditorUtility.SetDirty(controller);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(host.scene);
        }

        private static T Ensure<T>(GameObject root) where T : Component
        {
            return root.TryGetComponent(out T existing) ? existing : root.AddComponent<T>();
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) Debug.LogError("[Audio] No field " + field + " on " + so.targetObject);
            else property.objectReferenceValue = value;
        }
    }
}
