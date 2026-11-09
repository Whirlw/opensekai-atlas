using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace OpenSekai.EditorTools
{
    /// <summary>
    /// Runs the project's test suites from a menu item, the CLI (-executeMethod) or the editor eval
    /// hook, and writes the NUnit report next to the project. CI uses this because `unity test`
    /// refuses to run while an editor already has the project open.
    /// </summary>
    public static class TestRunnerBridge
    {
        public const string EditModeReportPath = "TestResults/editmode.xml";
        public const string PlayModeReportPath = "TestResults/playmode.xml";

        private static TestRunnerApi _api;
        private static Callbacks _callbacks;

        [MenuItem("OpenSekai/Dev/Run EditMode Tests", priority = 900)]
        private static void RunEditModeFromMenu() => Run(TestMode.EditMode, EditModeReportPath);

        [MenuItem("OpenSekai/Dev/Run PlayMode Tests", priority = 901)]
        private static void RunPlayModeFromMenu() => Run(TestMode.PlayMode, PlayModeReportPath);

        public static void RunEditMode() => Run(TestMode.EditMode, EditModeReportPath);

        public static void RunPlayMode() => Run(TestMode.PlayMode, PlayModeReportPath);

        public static bool Run(string mode, string reportPath)
        {
            TestMode testMode;
            if (!Enum.TryParse(mode, true, out testMode))
            {
                Debug.LogError($"[Tests] Unknown mode '{mode}'. Use EditMode or PlayMode.");
                return false;
            }

            return Run(testMode, reportPath);
        }

        private static bool Run(TestMode testMode, string reportPath)
        {
            var directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // A stale report would be mistaken for a fresh one by whatever polls for it.
            if (File.Exists(reportPath))
            {
                File.Delete(reportPath);
            }

            if (_api == null)
            {
                _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            }

            if (_callbacks != null)
            {
                _api.UnregisterCallbacks(_callbacks);
            }

            _callbacks = new Callbacks(reportPath);
            _api.RegisterCallbacks(_callbacks);
            _api.Execute(new ExecutionSettings(new Filter { testMode = testMode }));
            Debug.Log($"[Tests] {testMode} run started, report will be written to {reportPath}");
            return true;
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string _reportPath;

            public Callbacks(string reportPath)
            {
                _reportPath = reportPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, _reportPath);
                Debug.Log($"[Tests] Finished: passed={result.PassCount} failed={result.FailCount} " +
                          $"skipped={result.SkipCount} inconclusive={result.InconclusiveCount} " +
                          $"duration={result.Duration:0.00}s report={_reportPath}");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[Tests] FAILED {result.FullName}: {result.Message}");
                }
            }
        }
    }
}
