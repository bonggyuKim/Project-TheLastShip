using System.IO;
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
    public static class LastShiftNetworkSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        public const string PlayerPrefabPath = "Assets/DoodleUp/Prefabs/LastShiftNetworkPlayer.prefab";

        [MenuItem("Last Shift/SP-02A/Rebuild Network Sandbox")]
        public static void RebuildSandbox()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAndSaveSandbox();
        }

        public static void RebuildSandboxForAutomation()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
                throw new System.InvalidOperationException("Refusing to replace a dirty active scene during automated SP-02A network rebuild.");
            BuildAndSaveSandbox();
        }

        private static void BuildAndSaveSandbox()
        {
            Directory.CreateDirectory("Assets/DoodleUp/Prefabs");
            var playerPrefab = CreatePlayerPrefab();
            var scene = EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            var items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();
            var soloPlayer = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftPlayerController>(true)).Single();
            Object.DestroyImmediate(soloPlayer.gameObject);
            sandbox.Configure(System.Array.Empty<LastShiftPlayerController>(), items);
            sandbox.gameObject.AddComponent<NetworkObject>();
            sandbox.gameObject.AddComponent<LastShiftNetworkSandbox>().Configure(sandbox);
            foreach (var item in items)
            {
                var networkObject = item.gameObject.AddComponent<NetworkObject>();
                networkObject.DontDestroyWithOwner = true;
                item.gameObject.AddComponent<LastShiftOwnerNetworkTransform>();
                item.gameObject.AddComponent<LastShiftNetworkGrabbable>();
            }

            var sessionObject = new GameObject("LAST_SHIFT_SP02A_NETWORK_Session");
            var manager = sessionObject.AddComponent<NetworkManager>();
            var transport = sessionObject.AddComponent<UnityTransport>();
            var session = sessionObject.AddComponent<LastShiftNetworkSession>();
            sessionObject.AddComponent<LastShiftNetworkLifecycleProbe>();
            session.Configure(manager, transport, playerPrefab, sandbox);
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.EnableSceneManagement = true;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_NETWORK_BUILD] scene={ScenePath} players=1-4 authority=host items={items.Length} result=PASS");
        }

        private static LastShiftNetworkPlayer CreatePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
                AssetDatabase.DeleteAsset(PlayerPrefabPath);

            var player = new GameObject("LastShiftNetworkPlayer");
            var characterController = player.AddComponent<CharacterController>();
            characterController.radius = 0.28f;
            characterController.height = 1.7f;
            characterController.center = new Vector3(0f, 0.85f, 0f);
            player.AddComponent<NetworkObject>();
            player.AddComponent<LastShiftOwnerNetworkTransform>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Remote Body";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(0.52f, 0.80f, 0.52f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var bodyRenderer = body.GetComponent<MeshRenderer>();

            var cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            var socket = new GameObject("HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            socket.localPosition = new Vector3(0.45f, -0.30f, 1.1f);

            var controller = player.AddComponent<LastShiftPlayerController>();
            controller.Configure(camera, socket);
            var networkPlayer = player.AddComponent<LastShiftNetworkPlayer>();
            networkPlayer.Configure(controller, camera, bodyRenderer);
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return prefab.GetComponent<LastShiftNetworkPlayer>();
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(scene => scene.path == ScenePath);
            if (existing != null) existing.enabled = true;
            else scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
