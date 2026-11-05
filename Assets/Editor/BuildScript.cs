using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CQRa.EditorTools
{
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void BuildWindows()
        {
            try
            {
                var outDir = Path.GetFullPath("Builds/Windows");
                Directory.CreateDirectory(outDir);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(outDir, "CQRaRevival.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;

                Debug.Log($"[Build] {summary.result} | {summary.totalSize / (1024 * 1024)} MB | errors={summary.totalErrors} | {summary.totalTime}");

                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[Build] failed: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
