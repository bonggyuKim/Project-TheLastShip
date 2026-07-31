using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DoodleUp.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DoodleUp.Runtime
{
    public sealed class Du02ProvenanceLogger : MonoBehaviour
    {
        private void Start()
        {
            var devices = InputSystem.devices.Count == 0
                ? "none"
                : string.Join(";", InputSystem.devices.Select(device => $"{device.deviceId}:{device.layout}:{device.description.product}"));

            var scene = SceneManager.GetActiveScene();
            var sceneHash = ComputeSha256(scene.path);
            var executableHash = ComputeSha256(Application.dataPath.Replace("_Data", ".exe"));
            var runtimeAssemblyHash = ComputeSha256(typeof(Du02ProvenanceLogger).Assembly.Location);
            var provenanceHash = runtimeAssemblyHash.Trim('0').Length > 0 ? runtimeAssemblyHash : executableHash;
            var buildId = $"DU02-{Application.unityVersion}-{provenanceHash.Substring(0, 12)}";
            var mainCamera = Camera.main != null;
            var playerLayer = LayerMask.NameToLayer("Player");
            var courseLayer = LayerMask.NameToLayer("Course");
            var goalLayer = LayerMask.NameToLayer("Goal");
            var runtimeConfigurationValid = mainCamera && playerLayer == 8 && courseLayer == 9 && goalLayer == 10;
            Debug.Log($"[DU02_PROVENANCE] buildId={buildId} unity={Application.unityVersion} inputSystem={InputSystem.version} devices={devices} fixedDeltaTime={Du02LogFormat.Float(Time.fixedDeltaTime)} scene={scene.name} sceneSha256={sceneHash} executableSha256={executableHash} runtimeAssemblySha256={runtimeAssemblyHash} course={Du02Profile.CourseId} profile={Du02Profile.ProfileId} mainCameraTag={mainCamera} playerLayer={playerLayer} courseLayer={courseLayer} goalLayer={goalLayer} runtimeConfigurationValid={runtimeConfigurationValid}");
            if (!runtimeConfigurationValid)
                Debug.LogError($"[DU02_PROVENANCE_INVALID] mainCameraTag={mainCamera} playerLayer={playerLayer} courseLayer={courseLayer} goalLayer={goalLayer}");

            foreach (Du02TaskId taskId in System.Enum.GetValues(typeof(Du02TaskId)))
            {
                var lane = Du02CourseDefinition.Get(taskId);
                Debug.Log($"[DU02_COURSE] task={taskId} start={Du02LogFormat.Vector(lane.StartCenter)} goal={Du02LogFormat.Vector(lane.GoalCenter)} edgeGap={Du02LogFormat.Float(lane.EdgeGap)} contactBand={Du02LogFormat.Float(lane.ContactBandWidth)} spawn={Du02LogFormat.Vector(lane.SpawnPosition)}");
            }
        }

        private static string ComputeSha256(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return new string('0', 64);
            using var sha = SHA256.Create();
            var bytes = File.ReadAllBytes(fullPath);
            var hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var value in hash) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }
    }
}
