using SimpleRPG.Audio;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AudioTools
{
    /// <summary>
    /// Inspector for audio events and tables: the normal property editor plus the problems found in the definition and a preview that
    /// needs no scene. Tables get a small context form to hear what a given surface, footwear or material combination resolves to.
    /// </summary>
    [CustomEditor(typeof(AudioEventBase), true)]
    public sealed class AudioEventEditor : UnityEditor.Editor
    {
        private AudioContext previewContext = new AudioContext { Intensity = 0.5f };
        private string lastResolved;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            AudioEventBase target = (AudioEventBase)base.target;
            EditorGUILayout.Space();

            AudioValidationReport report = new AudioValidationReport();
            target.Validate(report);
            foreach (string error in report.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            foreach (string warning in report.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            if (report.IsClean)
            {
                EditorGUILayout.HelpBox("No problems found.", MessageType.None);
            }

            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (target is AudioEventTable)
            {
                DrawContextForm();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                {
                    AudioEventDefinition resolved = AudioPreview.Play(target, previewContext);
                    lastResolved = resolved != null ? "Plays: " + resolved.name : "Silent in this context.";
                }

                if (GUILayout.Button("Stop"))
                {
                    AudioPreview.Stop();
                }
            }

            if (!string.IsNullOrEmpty(lastResolved))
            {
                EditorGUILayout.LabelField(lastResolved);
            }

            EditorGUILayout.HelpBox("Previews play 2D and bypass the mixer. Check mixer routing and 3D attenuation in play mode.", MessageType.None);
        }

        private void DrawContextForm()
        {
            previewContext.Surface = (AudioTag)EditorGUILayout.ObjectField("Surface", previewContext.Surface, typeof(AudioTag), false);
            previewContext.Footwear = (AudioTag)EditorGUILayout.ObjectField("Footwear", previewContext.Footwear, typeof(AudioTag), false);
            previewContext.Source = (AudioTag)EditorGUILayout.ObjectField("Source material", previewContext.Source, typeof(AudioTag), false);
            previewContext.Target = (AudioTag)EditorGUILayout.ObjectField("Target material", previewContext.Target, typeof(AudioTag), false);
            previewContext.Armor = (AudioTag)EditorGUILayout.ObjectField("Armor", previewContext.Armor, typeof(AudioTag), false);
            previewContext.Subject = (AudioTag)EditorGUILayout.ObjectField("Subject", previewContext.Subject, typeof(AudioTag), false);
            previewContext.Region = (AudioRegion)EditorGUILayout.EnumFlagsField("Region", previewContext.Region);
            previewContext.Intensity = EditorGUILayout.Slider("Intensity", previewContext.Intensity, 0f, 1f);
        }
    }
}
