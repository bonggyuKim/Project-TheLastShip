using System.IO;
using System.Linq;
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
                scenes = EnabledBuildScenes(),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException($"LAST SHIFT network player build failed: {report.summary.result}");
            Debug.Log($"[LAST_SHIFT_NETWORK_PLAYER_BUILD] output={output} size={report.summary.totalSize} result=PASS");
        }

        /// <summary>
        /// Netcode 는 씬 인덱스 기반 해시로 server/client 씬을 대조한다. Player 에 network scene 하나만
        /// 넣으면 build settings 에 세 씬이 등록된 에디터를 host 로 쓸 때 인덱스가 어긋나
        /// "Scene Hash ... does not exist in the HashToBuildIndex table" 로 client 동기화가 실패한다.
        /// 그래서 에디터와 동일한 enabled 씬 목록을 그대로 굽고, network scene 이 빠지지 않았는지 확인한다.
        /// </summary>
        private static string[] EnabledBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(entry => entry.enabled)
                .Select(entry => entry.path)
                .ToList();
            if (!scenes.Contains(LastShiftNetworkSceneBuilder.ScenePath))
                throw new System.InvalidOperationException(
                    $"LAST SHIFT network scene missing from enabled build settings: {LastShiftNetworkSceneBuilder.ScenePath}");
            return scenes.ToArray();
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
