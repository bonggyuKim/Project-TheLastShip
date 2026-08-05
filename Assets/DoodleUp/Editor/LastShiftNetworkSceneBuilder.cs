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
            // 예전에는 여기서 기존 프리팹을 DeleteAsset 했다. 그러면 매 재빌드마다 파일 안의
            // local fileID 가 전부 새로 찍히고(에셋 GUID 는 유지된다), 루트 fileID 로 프리팹을
            // 가리키는 DefaultNetworkPrefabs.asset 이 그걸 따라가 <b>내용이 같은데 79줄이 바뀐
            // 커밋</b>이 재빌드마다 생긴다. 팀원 둘이 각자 빌드하면 그때마다 충돌한다.
            // SaveAsPrefabAsset 은 같은 경로에 덮어쓸 때 대응되는 오브젝트의 fileID 를 유지하므로
            // 지우지 않는 편이 결과가 같고 diff 가 조용하다. 지우는 것으로 되돌리지 말 것.
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
            // ForceUpdate 가 붙어 있는 이유는 NetworkObject.GlobalObjectIdHash 다. 그 값은 NGO 의
            // OnValidate 가 임포트 시점에 GlobalObjectId 로부터 찍는데, 같은 경로에 덮어쓰기만
            // 하면 재검증이 안 걸려 <b>0 인 채로 파일에 남는다</b>. 0 이면 NetworkManager 가 이
            // 프리팹을 등록하지 못해 클라이언트 스폰이 죽는데, 씬은 멀쩡히 저장되고 빌드 로그도
            // PASS 라 조용히 지나간다. 아래 검사가 그 침묵을 막는다.
            AssetDatabase.ImportAsset(PlayerPrefabPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var identity = prefab.GetComponent<NetworkObject>();
            // OnValidate 는 메모리의 값만 고친다. 여기서 밀어 넣지 않으면 파일에는 0 이 남고,
            // 다음 사람이 프리팹을 열어 보기 전까지 아무도 모른다.
            EditorUtility.SetDirty(identity);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            if (identity.PrefabIdHash == 0u)
                throw new System.InvalidOperationException(
                    $"{PlayerPrefabPath} saved with GlobalObjectIdHash 0 — NGO cannot register it. " +
                    "Prefab overwrite skipped NetworkObject.OnValidate.");
            return prefab.GetComponent<LastShiftNetworkPlayer>();
        }

        /// <summary>
        /// network scene 을 enabled 목록의 첫 항목으로 유지한다. Player 는 첫 씬으로 부팅되므로
        /// 다른 씬이 앞에 있으면 client 가 network scene 이 아닌 곳에서 시작한다. 동시에
        /// Netcode 는 씬 인덱스 기반 해시를 대조하므로 host(에디터)와 Player 가 같은 순서의
        /// 같은 목록을 써야 client 동기화가 성립한다.
        /// </summary>
        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => scene.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
