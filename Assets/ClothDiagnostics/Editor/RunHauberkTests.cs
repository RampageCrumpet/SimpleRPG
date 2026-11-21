using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ClothDiagnostics
{
    /// <summary>Runs the hauberk equipment play mode tests from the open editor and writes the outcome to Results/integrated_tests.txt.</summary>
    public static class RunHauberkTests
    {
        sealed class Report : ICallbacks
        {
            readonly StringBuilder sb = new StringBuilder();
            readonly string path;
            public Report(string path) { this.path = path; }
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.Test.IsSuite) return;
                sb.AppendLine($"{r.TestStatus} {r.Test.Name} ({r.Duration:F1}s)");
                if (!string.IsNullOrEmpty(r.Message)) sb.AppendLine("    " + r.Message.Replace("\n", "\n    "));
                if (r.TestStatus == TestStatus.Failed && !string.IsNullOrEmpty(r.StackTrace)) sb.AppendLine("    " + r.StackTrace.Replace("\n", "\n    "));
                if (!string.IsNullOrEmpty(r.Output)) sb.AppendLine("    output: " + r.Output.Replace("\n", "\n    "));
            }
            public void RunFinished(ITestResultAdaptor r)
            {
                sb.Insert(0, $"RUN {r.TestStatus} pass {r.PassCount} fail {r.FailCount} skip {r.SkipCount}\n");
                System.IO.File.WriteAllText(path, sb.ToString());
            }
        }

        [MenuItem("SimpleRPG/Tests/Run Hauberk Cloth Tests")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Report("Assets/ClothDiagnostics/Results/integrated_tests.txt"));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, groupNames = new[] { "HauberkClothEquipmentTests" } }));
        }

        [MenuItem("SimpleRPG/Tests/Run Query Mask Test To File")]
        public static void RunQueryMask()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Report("Assets/ClothDiagnostics/Results/query_mask_test.txt"));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "SimpleRPG.Abilities.Tests.PlayMode.HauberkClothEquipmentTests.GameplayQueryMasks_ExcludeTheClothColliders", "SimpleRPG.Abilities.Tests.PlayMode.HauberkClothEquipmentTests.Equipping_BuildsConfiguredClothOnTheServerAndOnTheClient" } }));
        }

        [MenuItem("SimpleRPG/Tests/Run All Edit Mode Tests To File")]
        public static void RunEditMode()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Report("Assets/ClothDiagnostics/Results/editmode_tests.txt"));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        [MenuItem("SimpleRPG/Tests/Run Appearance And Equipment Play Mode Tests To File")]
        public static void RunPlayModeEquipment()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Report("Assets/ClothDiagnostics/Results/playmode_equipment_tests.txt"));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, groupNames = new[] { "AppearanceNetworkTests", "ArmorEquipmentTests", "EquipmentNetworkTests", "BreechesCordMotionTests", "HauberkClothEquipmentTests" } }));
        }
    }
}
