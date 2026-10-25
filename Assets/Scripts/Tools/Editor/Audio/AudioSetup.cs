using System;
using System.Reflection;
using SimpleRPG.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace SimpleRPG.AudioTools
{
    /// <summary>
    /// Creates the project's mixer and routing asset. The mixer has one group per <see cref="AudioCategory"/> under Master, each with its
    /// volume exposed as <c>Volume_&lt;Category&gt;</c>, and the routing asset in Resources ties categories to those groups. Safe to run again:
    /// it keeps what exists and only adds what is missing.
    /// </summary>
    public static class AudioSetup
    {
        public const string MixerPath = "Assets/Audio/Mixers/GameMixer.mixer";
        public const string RoutingPath = "Assets/Resources/AudioRouting.asset";

        [MenuItem("SimpleRPG/Audio/Create Mixer And Routing")]
        public static void CreateMenu()
        {
            AudioRouting routing = CreateMixerAndRouting();
            Debug.Log("[Audio] Mixer and routing ready: " + AssetDatabase.GetAssetPath(routing.Mixer) + ", " + RoutingPath, routing);
        }

        public static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        public static AudioRouting CreateMixerAndRouting()
        {
            EnsureFolder("Assets/Audio/Mixers");
            EnsureFolder("Assets/Resources");

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                mixer = BuildMixer();
            }

            AudioRouting routing = AssetDatabase.LoadAssetAtPath<AudioRouting>(RoutingPath);
            if (routing == null)
            {
                routing = ScriptableObject.CreateInstance<AudioRouting>();
                AssetDatabase.CreateAsset(routing, RoutingPath);
            }

            SerializedObject so = new SerializedObject(routing);
            so.FindProperty("mixer").objectReferenceValue = mixer;
            SerializedProperty routes = so.FindProperty("routes");
            Array categories = Enum.GetValues(typeof(AudioCategory));
            routes.arraySize = categories.Length;
            for (int i = 0; i < categories.Length; i++)
            {
                AudioCategory category = (AudioCategory)categories.GetValue(i);
                SerializedProperty route = routes.GetArrayElementAtIndex(i);
                route.FindPropertyRelative("category").enumValueIndex = i;
                AudioMixerGroup[] found = mixer.FindMatchingGroups("Master/" + category);
                route.FindPropertyRelative("group").objectReferenceValue = found.Length > 0 ? found[0] : null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(routing);
            AssetDatabase.SaveAssets();
            AudioRouting.ResetCache();
            return routing;
        }

        // The mixer asset can only be created through editor-internal types, so this uses reflection and says clearly what to do if it breaks.
        private static AudioMixer BuildMixer()
        {
            try
            {
                Assembly editor = typeof(Editor).Assembly;
                Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
                Type groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
                Type pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
                Type exposedType = editor.GetType("UnityEditor.Audio.ExposedAudioParameter", true);
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

                object controller = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
                object master = controllerType.GetProperty("masterGroup", all).GetValue(controller);

                foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
                {
                    object group = controllerType.GetMethod("CreateNewGroup", all).Invoke(controller, new object[] { category.ToString(), true });
                    controllerType.GetMethod("AddChildToParent", all).Invoke(controller, new[] { group, master });

                    object guid = groupType.GetMethod("GetGUIDForVolume", all).Invoke(group, null);
                    object path = Activator.CreateInstance(pathType, all, null, new[] { group, guid }, null);
                    controllerType.GetMethod("AddExposedParameter", all).Invoke(controller, new[] { path });

                    // New exposed parameters get a generic name; give this one its stable name.
                    PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", all);
                    Array exposed = (Array)exposedProperty.GetValue(controller);
                    FieldInfo guidField = exposedType.GetField("guid", all);
                    FieldInfo nameField = exposedType.GetField("name", all);
                    for (int i = 0; i < exposed.Length; i++)
                    {
                        object entry = exposed.GetValue(i);
                        if (guidField.GetValue(entry).Equals(guid))
                        {
                            nameField.SetValue(entry, AudioRouting.VolumeParameter(category));
                            exposed.SetValue(entry, i);
                        }
                    }

                    exposedProperty.SetValue(controller, exposed);
                }

                EditorUtility.SetDirty((UnityEngine.Object)controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception exception)
            {
                // Do not leave a half-built mixer behind: the next run would mistake it for a finished one.
                AssetDatabase.DeleteAsset(MixerPath);
                Debug.LogError("[Audio] Could not build the mixer through the editor's internal API (" + exception.Message + "). Create " + MixerPath +
                               " by hand: Master with one child group per AudioCategory, each group's volume exposed as Volume_<Category>.");
                throw;
            }

            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }
    }
}
