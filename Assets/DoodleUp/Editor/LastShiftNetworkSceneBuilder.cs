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

        [MenuItem("Last Shift/SP-02A/Realign Scene Items")]
        public static void RealignSceneItems()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RealignSceneItemsForAutomation();
        }

        /// <summary>
        /// 씬에 놓인 부품 넷을 좌표 정본으로 되맞춘다. <b>방 배치가 움직인 커밋과 씬을 다시 굽는
        /// 커밋이 갈렸을 때 실제로 깨지는 자리가 여기다.</b>
        ///
        /// 중앙 광장 개편(<c>docs/central-plaza-hub-layout-v1.md</c> §9.3)이 방 넷을 방사형으로
        /// 옮겼는데 씬은 일자 스파인 좌표 그대로 남았고, 그래서 <c>PatchPlate</c> 가 산소실
        /// 발자국 밖(<c>x = 15.5</c>, 방은 <c>x ∈ [6, 14]</c>)에 떠 <see cref="LastShiftSandboxController.BreachZone"/>
        /// 이 조종석으로 풀렸다. 파공이 조종석에 났다고 읽히면 CT-05 산소 3단 구조 검증이
        /// 통째로 전제부터 무너진다 — 승무원 사망 경로를 재는 PlayMode 다섯 건이 모두
        /// "승무원이 파공 구역에 없다" 로 죽은 것이 그 증상이었다.
        ///
        /// <b><see cref="RebuildSandboxForAutomation"/> 로 대신할 수 없다.</b> 저쪽은 선체·구획·
        /// 드레싱까지 다시 굽고 드레싱 규칙 위반이 남아 있으면 던지므로, 아트 데이터가 정리될
        /// 때까지 시뮬레이션 정합성이 인질로 잡힌다. 부품 좌표는 그것과 독립적으로 맞출 수 있다.
        /// </summary>
        public static void RealignSceneItemsForAutomation()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
                throw new System.InvalidOperationException("Refusing to reopen the SP-02A scene while the active scene has unsaved changes.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var items = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true))
                .ToArray();
            if (items.Length == 0)
                throw new System.InvalidOperationException($"{ScenePath} has no LastShiftGrabbable items to realign.");

            var moved = LastShiftSceneBuilder.RealignSceneItems(items);
            if (moved > 0)
            {
                foreach (var item in items) EditorUtility.SetDirty(item);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            Debug.Log($"[LAST_SHIFT_ITEM_REALIGN] scene={ScenePath} items={items.Length} moved={moved} result=PASS");
        }

        /// <summary>
        /// 이 프로젝트의 유일한 레벨을 짓는다.
        ///
        /// <b>예전에는 SP01 을 열어 변형해 저장하는 파생물이었다.</b> 그래서 같은 배가 두 벌
        /// 존재했고, SP01 만 다시 굽고 여기를 안 구우면 4인 씬이 조용히 옛 선체로 남았다 —
        /// 그레이박스 구획 11개가 SP01 에만 들어가고 여기는 0개였던 것이 실제로 그렇게 났다.
        /// 두 씬 어느 쪽 테스트도 그것을 못 잡았다.
        ///
        /// 이제 선체·아이템은 프리팹이고 씬은 하나다. 솔로 플레이는 씬을 따로 두는 대신
        /// host 1인으로 돈다 — <see cref="LastShiftNetworkSession"/> 이 에디터 Play 에서
        /// host 를 자동 기동하므로 별도 진입점이 필요 없고, 무엇보다 <b>솔로에서만 도는
        /// 코드 경로가 사라진다.</b> 4인 co-op 에서 정본은 언제나 host 권위 경로다.
        /// </summary>
        private static void BuildAndSaveSandbox()
        {
            Directory.CreateDirectory("Assets/DoodleUp/Prefabs");
            Directory.CreateDirectory("Assets/Scenes");
            var playerPrefab = CreatePlayerPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LAST_SHIFT_SP02A_NETWORK";
            LastShiftSceneBuilder.CreateLighting();
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(LastShiftSceneBuilder.RebuildShipPrefab());
            LastShiftSceneBuilder.RebuildItemPrefabs();
            var items = LastShiftSceneBuilder.CreateItems();
            LastShiftSceneBuilder.CreateMeteorStimulus();

            var runtime = new GameObject("LAST_SHIFT_RUNTIME");
            runtime.AddComponent<LastShiftImpactFeedback>();
            var sandbox = runtime.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(System.Array.Empty<LastShiftPlayerController>(), items);
            sandbox.gameObject.AddComponent<NetworkObject>();
            sandbox.gameObject.AddComponent<LastShiftNetworkSandbox>().Configure(sandbox);

            // 자유 배치 복제. 샌드박스와 같은 NetworkObject 에 붙는다 — 판 안 상태와 판 밖
            // 배치는 같은 세션의 두 장부이고, 오브젝트를 따로 두면 둘의 스폰 순서가 갈린다.
            sandbox.gameObject.AddComponent<LastShiftNetworkPlacement>();

            // 기항 배치 화면. 팔레트는 아직 자산이 없어 비워 두고, 그러면 조립기가 그레이박스로
            // 세운다(LastShiftModuleAssembler). 아트 모듈 프리팹이 들어오면 여기 물린다.
            runtime.AddComponent<LastShiftPlacementUi>().Configure(ship.transform, null);

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
            StampSceneNetworkObjectHash();
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
            characterController.radius = LastShiftShipPhysics.CrewRadius;
            characterController.height = LastShiftShipPhysics.StandingHeight;
            characterController.center = new Vector3(0f, LastShiftShipPhysics.StandingHeight * 0.5f, 0f);
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
            cameraObject.transform.localPosition = new Vector3(0f, LastShiftShipPhysics.EyeHeight, 0f);
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

        /// <summary>
        /// 씬에 직접 세운 <see cref="NetworkObject"/>(sandbox)의 <c>GlobalObjectIdHash</c> 를 확정한다.
        ///
        /// 그 값은 NGO 의 <c>OnValidate</c> 가 <c>GlobalObjectId</c> 로부터 찍는데, 스크립트가
        /// <c>AddComponent</c> 로 붙인 직후에는 아직 안 돈다 — 그대로 저장하면 <b>0 이 파일에 남는다.</b>
        /// 0 이면 NGO 가 이 오브젝트를 구분하지 못해 spawn 이 죽는데, 씬은 멀쩡히 저장되고 빌드
        /// 로그도 PASS 라 조용히 지나간다. 프리팹 쪽에서 이미 두 번 겪은 함정이다
        /// (<c>CreatePlayerPrefab</c>, <c>RebuildItemPrefabs</c>).
        ///
        /// 씬 오브젝트는 프리팹처럼 재임포트로 못 밀어 넣으므로 <b>한 번 다시 열어</b> OnValidate 를
        /// 돌린 뒤 그 값을 저장한다. 아래 검사가 침묵을 막는다.
        /// </summary>
        private static void StampSceneNetworkObjectHash()
        {
            var reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var identity = reopened.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LastShiftNetworkSandbox>(true))
                .Single()
                .GetComponent<NetworkObject>();
            EditorUtility.SetDirty(identity);
            EditorSceneManager.MarkSceneDirty(reopened);
            EditorSceneManager.SaveScene(reopened, ScenePath);
            if (identity.PrefabIdHash == 0u)
                throw new System.InvalidOperationException(
                    $"{ScenePath} sandbox saved with GlobalObjectIdHash 0 — NGO cannot spawn it.");
            Debug.Log($"[LAST_SHIFT_SCENE_IDENTITY] sandboxHash={identity.PrefabIdHash} result=PASS");
        }
    }
}
