using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DoodleUp.Editor
{
    public static class Du02PlayerBuild
    {
        private const string ScenePath = "Assets/Scenes/DU02_SoloCourse.unity";
        private const string BuildDirectory = "Builds/DU02_RuntimeProbe";
        private const string ExecutablePath = BuildDirectory + "/DoodleUp-DU02-Probe.exe";

        public static void BuildWindowsProbe()
        {
            Directory.CreateDirectory(BuildDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"DU-02 probe build failed: {report.summary.result}");

            Debug.Log($"[DU02_BUILD] executable={Path.GetFullPath(ExecutablePath)} sizeBytes={report.summary.totalSize} result=PASS");
        }
    }
}
