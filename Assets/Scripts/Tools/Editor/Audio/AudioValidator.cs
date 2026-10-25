using System.Collections.Generic;
using SimpleRPG.Audio;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AudioTools
{
    /// <summary>
    /// Finds invalid audio configuration across the whole project: events with no clips, loops in one-shots, tables with empty rows, routing
    /// with no mixer group for a category. Run it from SimpleRPG > Audio > Validate Audio Definitions; the build gate can call
    /// <see cref="ValidateAll"/> too.
    /// </summary>
    public static class AudioValidator
    {
        /// <summary>
        /// Checks every audio event, table and the routing asset. Errors make the project's audio unusable; warnings are silent or odd content.
        /// </summary>
        public static AudioValidationReport ValidateAll()
        {
            AudioValidationReport report = new AudioValidationReport();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioEventBase"))
            {
                AudioEventBase asset = AssetDatabase.LoadAssetAtPath<AudioEventBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    asset.Validate(report);
                }
            }

            ValidateRouting(report);
            return report;
        }

        public static void ValidateRouting(AudioValidationReport report)
        {
            AudioRouting routing = AudioRouting.Instance;
            if (routing == null)
            {
                report.Warnings.Add("There is no AudioRouting asset in a Resources folder, so sounds play unrouted. Use SimpleRPG > Audio > Create Mixer And Routing.");
                return;
            }

            if (routing.Mixer == null)
            {
                report.Warnings.Add("AudioRouting has no mixer.");
            }

            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
            {
                if (routing.GroupFor(category) == null)
                {
                    report.Warnings.Add("AudioRouting has no mixer group for " + category + ".");
                }
            }
        }

        [MenuItem("SimpleRPG/Audio/Validate Audio Definitions")]
        public static void ValidateMenu()
        {
            AudioValidationReport report = ValidateAll();
            foreach (string error in report.Errors) Debug.LogError("[Audio] " + error);
            foreach (string warning in report.Warnings) Debug.LogWarning("[Audio] " + warning);
            Debug.Log("[Audio] Validation finished: " + report.Errors.Count + " errors, " + report.Warnings.Count + " warnings.");
        }

        /// <summary>
        /// The assets that name this clip, so replacing a clip can be checked against where it is used.
        /// </summary>
        public static List<AudioEventBase> EventsUsing(AudioClip clip)
        {
            List<AudioEventBase> users = new List<AudioEventBase>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioEventDefinition"))
            {
                AudioEventDefinition asset = AssetDatabase.LoadAssetAtPath<AudioEventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (AudioLayer layer in asset.Layers)
                {
                    if (layer == null || layer.clips == null) continue;
                    foreach (WeightedClip entry in layer.clips)
                    {
                        if (entry.clip == clip)
                        {
                            users.Add(asset);
                        }
                    }
                }
            }

            return users;
        }
    }
}
