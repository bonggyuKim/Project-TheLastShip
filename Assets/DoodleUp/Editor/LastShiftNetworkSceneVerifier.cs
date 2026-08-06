using System;
using System.Linq;
using DoodleUp.Runtime;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class LastShiftNetworkSceneVerifier
    {
        [MenuItem("Last Shift/SP-02A/Verify Network Sandbox")]
        public static void VerifySavedScene()
        {
            RequireCleanActiveScene("verify saved network scene");
            var scene = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath, OpenSceneMode.Single);
            VerifyScene(scene);
        }

        public static void RequireCleanActiveScene(string operation)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded && activeScene.isDirty)
                throw new InvalidOperationException($"LAST SHIFT SP-02A refused to {operation}: active scene '{activeScene.name}' has unsaved changes.");
        }

        public static void VerifySavedSandboxLifecycle()
        {
            var setup = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(setup.IsValid() && setup.isLoaded, "setup scene must be loaded");
            LastShiftNetworkSceneBuilder.RebuildSandboxForAutomation();
            VerifyScene(SceneManager.GetActiveScene());
            var reopened = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath, OpenSceneMode.Single);
            VerifyScene(reopened);
            Debug.Log($"[LAST_SHIFT_NETWORK_LIFECYCLE] scene={LastShiftNetworkSceneBuilder.ScenePath} rebuild=PASS reopen=PASS result=PASS");
        }

        /// <summary>
        /// 씬이 하나가 된 뒤로 이 검사가 선체 지오메트리까지 책임진다.
        /// <see cref="LastShiftSceneVerifier"/> 의 시선 차단(A3)·통행 여유·카메라 사거리 검사는
        /// 네트워크와 무관하지만 검사할 씬이 여기밖에 없다 — 안 부르면 배가 잘못 지어져도
        /// 아무도 안 본다. 예전에는 SP01 검증기가 따로 돌았고, 그래서 두 씬이 어긋난 것을
        /// 어느 쪽도 못 잡았다.
        /// </summary>
        public static void VerifyScene(Scene scene)
        {
            Require(scene.IsValid() && scene.isLoaded, "scene must be loaded");
            Require(scene.path == LastShiftNetworkSceneBuilder.ScenePath, "scene path mismatch");
            LastShiftSceneVerifier.VerifyScene(scene);
            Require(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == LastShiftNetworkSceneBuilder.ScenePath), "network scene must be enabled in build settings");
            var roots = scene.GetRootGameObjects();
            Require(roots.SelectMany(root => root.GetComponentsInChildren<LastShiftPlayerController>(true)).Any() == false, "scene must not contain split-screen players");

            var manager = roots.SelectMany(root => root.GetComponentsInChildren<NetworkManager>(true)).Single();
            Require(manager.GetComponent<UnityTransport>() != null, "UnityTransport missing");
            Require(manager.GetComponent<LastShiftNetworkSession>() != null, "network session missing");
            Require(manager.NetworkConfig.ConnectionApproval, "connection approval must be enabled");
            Require(manager.NetworkConfig.PlayerPrefab != null, "network player prefab missing");
            VerifyPlayerPrefab(manager.NetworkConfig.PlayerPrefab);

            var sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            Require(sandbox.GetComponent<NetworkObject>() != null, "sandbox NetworkObject missing");
            Require(sandbox.GetComponent<LastShiftNetworkSandbox>() != null, "host authority seam missing");
            var items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();
            Require(items.Length == 4, "network item count must be 4");
            Require(items.All(item => item.GetComponent<NetworkObject>() != null), "item NetworkObject missing");
            Require(items.All(item => item.GetComponent<NetworkObject>().DontDestroyWithOwner), "canonical items must survive holder disconnect");
            Require(items.All(item => item.GetComponent<LastShiftOwnerNetworkTransform>() != null), "item owner-authoritative transform missing");
            Require(items.All(item => item.GetComponent<LastShiftNetworkGrabbable>() != null), "item ownership guard missing");

            // N0b-3. 네트워크 씬은 SP-01 씬을 열어 개조하므로 문도 함께 넘어와야 한다.
            // 여기서 확인하지 않으면 솔로에만 문이 있고 네트워크에는 없는 상태가 조용히 지나간다.
            var doors = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftZoneDoor>(true)).ToArray();
            Require(doors.Length == LastShiftZoneAtlas.BoundaryCount, "network scene zone door count must equal boundary count");
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                Require(doors.Count(door => door.Boundary == boundary) == 1, $"network boundary {boundary} must have exactly one door");
            Debug.Log($"[LAST_SHIFT_NETWORK_VERIFY] scene={scene.path} stack=NGO+UTP connectionApproval=enabled playerPrefab=owner-gated-camera items=4 doors={doors.Length} itemDisconnectSurvival=enabled hostAuthoritySeam=present result=PASS");
        }

        private static void VerifyPlayerPrefab(GameObject playerPrefab)
        {
            const float tolerance = 0.0001f;
            Require(Approximately(playerPrefab.transform.localPosition, Vector3.zero, tolerance), "network player root position mismatch");
            Require(Approximately(playerPrefab.transform.localRotation, Quaternion.identity, tolerance), "network player root rotation mismatch");
            Require(Approximately(playerPrefab.transform.localScale, Vector3.one, tolerance), "network player root scale mismatch");
            Require(playerPrefab.GetComponent<NetworkObject>() != null, "network player NetworkObject missing");
            var networkPlayer = playerPrefab.GetComponent<LastShiftNetworkPlayer>();
            Require(networkPlayer != null, "network player behavior missing");
            Require(playerPrefab.GetComponent<LastShiftOwnerNetworkTransform>() != null, "network player owner-authoritative transform missing");

            var body = playerPrefab.transform.Find("Remote Body");
            Require(body != null, "network player remote body missing");
            Require(Approximately(body.localPosition, new Vector3(0f, 0.85f, 0f), tolerance), "network player remote body position mismatch");
            Require(Approximately(body.localRotation, Quaternion.identity, tolerance), "network player remote body rotation mismatch");
            Require(Approximately(body.localScale, new Vector3(0.52f, 0.80f, 0.52f), tolerance), "network player remote body scale mismatch");
            var bodyRenderer = body.GetComponent<MeshRenderer>();
            Require(bodyRenderer != null, "network player remote body renderer missing");
            Require(bodyRenderer.enabled, "network player remote body renderer must default enabled");
            Require(body.GetComponent<MeshFilter>()?.sharedMesh != null, "network player remote body mesh missing");
            Require(networkPlayer.BodyRenderer == bodyRenderer, "network player remote body renderer reference mismatch");

            var characterController = playerPrefab.GetComponent<CharacterController>();
            Require(characterController != null, "network player CharacterController missing");
            Require(Mathf.Abs(characterController.radius - 0.28f) <= tolerance, "network player CharacterController radius mismatch");
            Require(Mathf.Abs(characterController.height - 1.7f) <= tolerance, "network player CharacterController height mismatch");
            Require(Approximately(characterController.center, new Vector3(0f, 0.85f, 0f), tolerance), "network player CharacterController center mismatch");

            var cameras = playerPrefab.GetComponentsInChildren<Camera>(true);
            Require(cameras.Length == 1, "network player must have one owner-gated camera");
            var camera = cameras[0];
            Require(Approximately(camera.transform.localPosition, new Vector3(0f, 1.55f, 0f), tolerance), "network player camera position mismatch");
            Require(Approximately(camera.transform.localRotation, Quaternion.identity, tolerance), "network player camera rotation mismatch");
            Require(Mathf.Abs(camera.fieldOfView - 72f) <= tolerance, "network player camera FOV mismatch");
            Require(Mathf.Abs(camera.nearClipPlane - 0.05f) <= tolerance, "network player camera near clip mismatch");
            Require(Mathf.Abs(camera.farClipPlane - 80f) <= tolerance, "network player camera far clip mismatch");
            Require(Approximately(camera.rect, new Rect(0f, 0f, 1f, 1f), tolerance), "network player camera rect mismatch");

            var holdSocket = camera.transform.Find("HoldSocket");
            Require(holdSocket != null, "network player HoldSocket missing");
            Require(Approximately(holdSocket.localPosition, new Vector3(0.45f, -0.30f, 1.1f), tolerance), "network player HoldSocket position mismatch");
            Require(Approximately(holdSocket.localRotation, Quaternion.identity, tolerance), "network player HoldSocket rotation mismatch");
        }

        private static bool Approximately(Vector3 actual, Vector3 expected, float tolerance)
        {
            return (actual - expected).sqrMagnitude <= tolerance * tolerance;
        }

        private static bool Approximately(Quaternion actual, Quaternion expected, float tolerance)
        {
            return 1f - Mathf.Abs(Quaternion.Dot(actual, expected)) <= tolerance;
        }

        private static bool Approximately(Rect actual, Rect expected, float tolerance)
        {
            return Mathf.Abs(actual.x - expected.x) <= tolerance &&
                   Mathf.Abs(actual.y - expected.y) <= tolerance &&
                   Mathf.Abs(actual.width - expected.width) <= tolerance &&
                   Mathf.Abs(actual.height - expected.height) <= tolerance;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"LAST SHIFT SP-02A network verification failed: {message}.");
        }
    }
}
