using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DoodleUp.Editor
{
    public static class LastShiftNetworkBuild
    {
        public static void BuildWindowsPlayer()
        {
            LastShiftNetworkSceneVerifier.RequireCleanActiveScene("build the Windows network player");
            LastShiftNetworkSceneVerifier.VerifySavedScene();
            Debug.Log($"[LAST_SHIFT_NETWORK_PREBUILD_VERIFY] scene={LastShiftNetworkSceneBuilder.ScenePath} result=PASS");
            var requestedOutput = CommandLineValue("-buildOutput") ?? "Builds/LastShiftNetwork/LastShiftNetwork.exe";
            var output = Path.GetFullPath(requestedOutput);
            var outputDirectory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { LastShiftNetworkSceneBuilder.ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException($"LAST SHIFT network player build failed: {report.summary.result}");
            Debug.Log($"[LAST_SHIFT_NETWORK_PLAYER_BUILD] output={output} size={report.summary.totalSize} result=PASS");
        }

        private static string CommandLineValue(string name)
        {
            var arguments = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (arguments[index] == name) return arguments[index + 1];
            return null;
        }
    }
}
