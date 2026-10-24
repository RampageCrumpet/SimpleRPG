using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SimpleRPG.TestAutomation
{
    /// <summary>Runs suites through the live editor and persists results across domain reloads.</summary>
    [InitializeOnLoad]
    public static class MvpTestRunner
    {
        private static TestRunnerApi api;
        static MvpTestRunner()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results());
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = SessionState.GetString("SimpleRPG.TestResultPath", "");
                if (string.IsNullOrEmpty(path)) return;
                TestRunnerApi.SaveResultToFile(result, path);
                File.WriteAllText(path + ".summary", $"Passed={result.PassCount} Failed={result.FailCount} Skipped={result.SkipCount} Result={result.TestStatus}");
            }
        }

        public static string Run(bool playMode, string filter = null)
        {
            string path = Path.GetFullPath("Library/Mvp" + (playMode ? "PlayMode" : "EditMode") + "Results.xml");
            SessionState.SetString("SimpleRPG.TestResultPath", path);
            var tests = new Filter { testMode = playMode ? TestMode.PlayMode : TestMode.EditMode };
            if (!string.IsNullOrEmpty(filter) && filter.StartsWith("assembly:")) tests.assemblyNames = filter.Substring("assembly:".Length).Split(';');
            else if (!string.IsNullOrEmpty(filter)) tests.testNames = filter.Split(';');
            api.Execute(new ExecutionSettings(tests));
            return path;
        }
    }
}
