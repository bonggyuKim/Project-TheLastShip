using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Runtime
{
    public sealed class LastShiftNetworkSession : MonoBehaviour
    {
        public const ushort DefaultPort = 7979;
        public const int MaxPlayers = 4;
        public const string NetworkSceneName = "LAST_SHIFT_SP02A_NETWORK";

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private LastShiftNetworkPlayer playerPrefab;

        /// <summary>
        /// 승무원 프리팹. 레벨이 하나가 된 뒤로 씬에는 플레이어가 없고 접속 시 여기서 스폰되므로,
        /// 승무원이 필요한 쪽(테스트 등)이 경로를 따로 적지 않고 이것을 본다 — 경로를 각자 적으면
        /// 빌더가 프리팹을 옮겼을 때 그쪽만 조용히 뒤처진다.
        /// </summary>
        public LastShiftNetworkPlayer PlayerPrefab => playerPrefab;
        [SerializeField] private LastShiftSandboxController sandbox;
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = DefaultPort;

        private readonly LastShiftNetworkSlotAllocator slotAllocator = new();

        public NetworkManager NetworkManager => networkManager;
        public LastShiftSandboxController Sandbox => sandbox;
        public string Address => address;
        public ushort Port => port;

        public void Configure(
            NetworkManager manager,
            UnityTransport networkTransport,
            LastShiftNetworkPlayer networkPlayerPrefab,
            LastShiftSandboxController sandboxController)
        {
            networkManager = manager;
            transport = networkTransport;
            // EditMode scene rebuild를 반복하면 AddComponent<NetworkManager>() 직후 한 프레임 동안
            // NetworkConfig가 아직 null일 수 있다. builder가 즉시 Configure를 호출해도 안전하게 한다.
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            playerPrefab = networkPlayerPrefab;
            sandbox = sandboxController;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab.gameObject;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (transport == null) transport = GetComponent<UnityTransport>();
            if (sandbox == null) sandbox = FindFirstObjectByType<LastShiftSandboxController>();
            if (networkManager != null)
            {
                if (Application.isPlaying) DiscardStaleNetworkManagerSingleton();
                // 씬 빌더가 편집 모드에서 컴포넌트를 붙일 때 NetworkManager 내부 설정은 아직
                // 초기화 중이다. 그 시점에 NetworkConfig 를 만지면 Configure() 의 후속 접근이
                // NullReferenceException 으로 실패한다. 런타임에만 직렬화 참조를 config 로 복원한다.
                if (Application.isPlaying)
                {
                    if (transport != null) networkManager.NetworkConfig.NetworkTransport = transport;
                    if (playerPrefab != null) networkManager.NetworkConfig.PlayerPrefab = playerPrefab.gameObject;
                }
                networkManager.ConnectionApprovalCallback = ApproveConnection;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
                if (Application.isPlaying) RestrictSceneSynchronizationToNetworkScene();
            }
        }

        /// <summary>
        /// 앞선 세션의 NetworkManager 가 DDOL 에 남아 있으면 새 씬 인스턴스가 Singleton 을
        /// 잡지 못한다. NetworkManager.OnEnable 은 <c>Singleton == null</c> 일 때만
        /// SetSingleton() 을 부르고(NetworkManager.cs), 스스로는 DontDestroyOnLoad 로 살아남기
        /// 때문이다. 그 결과 새 씬의 아이템 NetworkObject 는 NetworkManagerOwner 가 비어
        /// NetworkObject.NetworkManager => NetworkManager.Singleton 으로 죽은 옛 매니저를
        /// 가리키고, ServerSpawnSceneObjectsOnStartSweep 의
        /// <c>networkObject.NetworkManager != NetworkManager</c> 검사에서 전부 걸러진다.
        /// 즉 host 는 떠도 씬 안의 물건이 하나도 spawn 되지 않는다(spawned=0).
        ///
        /// 그래서 네트워크 씬이 다시 열릴 때 남은 매니저를 걷어내고 이 씬의 매니저를
        /// Singleton 으로 세운다. 테스트 전용 우회가 아니라, 씬을 재로드하는 모든 경로
        /// (로비 복귀, 재시작)가 같은 함정을 밟기 때문에 런타임에서 막는다.
        /// </summary>
        private void DiscardStaleNetworkManagerSingleton()
        {
            var singleton = NetworkManager.Singleton;
            if (singleton == networkManager) return;
            if (singleton != null)
            {
                if (singleton.IsListening) singleton.Shutdown();
                Debug.Log($"[LAST_SHIFT_NETWORK_SINGLETON] stale={singleton.gameObject.scene.name}/{singleton.name} action=destroyed replacement={name}");
                Destroy(singleton.gameObject);
            }

            // Destroy 는 프레임 끝에 반영되므로 OnDestroy 가 Singleton 을 null 로 되돌리는
            // 시점을 기다릴 수 없다. 이 씬의 매니저를 즉시 Singleton 으로 지정한다.
            networkManager.SetSingleton();
        }

        /// <summary>
        /// build settings 에 등록된 다른 씬이 additive 로 함께 로드되지 않게 막는다.
        /// Netcode 씬 관리는 등록된 씬 전부를 동기화 대상으로 보므로, network scene 외의 씬이
        /// 함께 열리면 LastShiftSandboxController 가 씬마다 하나씩 생겨 두 개가 공존한다.
        /// 그러면 server 가 실제로 갱신하는 sandbox 와 화면·조회가 잡는 sandbox 가 달라져
        /// 프리셋 전환이 반영되지 않은 것처럼 보인다.
        /// </summary>
        private void RestrictSceneSynchronizationToNetworkScene()
        {
            // NetworkManager.SceneManager 는 세션이 시작된 뒤에 만들어진다. 씬 빌더가 컴포넌트를
            // 붙이는 편집 시점에 건드리면 초기화 전 상태를 만져 NullReferenceException 이 난다.
            networkManager.OnServerStarted -= ApplySceneVerification;
            networkManager.OnServerStarted += ApplySceneVerification;
            networkManager.OnClientStarted -= ApplySceneVerification;
            networkManager.OnClientStarted += ApplySceneVerification;
        }

        private void ApplySceneVerification()
        {
            var sceneManager = networkManager != null ? networkManager.SceneManager : null;
            if (sceneManager == null) return;
            // 서버에서만 필터를 건다. client 는 서버가 지시한 씬을 그대로 받아야 하고,
            // 여기서 client 측 검증을 덮으면 서버 씬 동기화가 통째로 막힌다.
            if (!networkManager.IsServer) return;
            sceneManager.VerifySceneBeforeLoading = IsNetworkScene;
        }

        private static bool IsNetworkScene(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            // 동기화 대상은 network scene 으로 한정하되, 이미 열려 있는 씬을 다시 확인하는 호출도
            // 통과시켜야 한다. 여기서 network scene 을 거부하면 client 가 서버 씬을 못 받는다.
            var allowed = sceneName == NetworkSceneName;
            if (!allowed)
                Debug.Log($"[LAST_SHIFT_SCENE_FILTER] scene={sceneName} mode={loadSceneMode} result=skipped");
            return allowed;
        }

        /// <summary>
        /// 모드 인자가 없을 때의 자동 host 를 끈다.
        ///
        /// 레벨이 하나가 되면서 <b>모든 PlayMode 테스트가 이 씬을 연다.</b> 그러면 테스트마다
        /// host 가 자동으로 떠서 같은 UDP 포트를 잡으려 하고, 앞 테스트의 host 가 아직 안 내려간
        /// 사이에 다음 테스트가 뜨면 "address is already in use" 로 SetUp 부터 죽는다. 산소·임무
        /// 시계처럼 네트워크와 무관한 검사까지 그 경쟁에 얹히면 실패가 무엇 때문인지 안 갈린다.
        ///
        /// 그래서 <b>씬을 로드하기 전에</b> 이 값을 <c>false</c> 로 두면 자동 host 를 건너뛴다.
        /// 명시적 <c>-lastShiftNetworkMode</c> 인자 경로와 <see cref="StartHost"/> 직접 호출은
        /// 영향받지 않는다 — 끄는 것은 "인자가 없어서 알아서 뜨는" 편의 경로 하나뿐이다.
        ///
        /// 한때 이 분기가 <c>#if UNITY_EDITOR</c> 안에 있었다. 그래서 인자 없이 띄운 standalone
        /// 빌드는 host 가 뜨지 않아 player 가 spawn 되지 않았고, 카메라는 player 프리팹에만 있으므로
        /// <b>화면에 HUD 만 남고 3D 가 통째로 안 보였다.</b> 에디터에서는 재현되지 않는 종류의 실패라
        /// 가드를 걷어내고 에디터와 빌드가 같은 경로를 타게 한다.
        /// </summary>
        public static bool AutoStartHost = true;

        private void Start()
        {
            var mode = ReadArgument("-lastShiftNetworkMode");
            if (string.Equals(mode, "host", StringComparison.OrdinalIgnoreCase))
            {
                if (StartHost())
                    Debug.Log($"[LAST_SHIFT_NETWORK_READY] mode=host address={address} port={port} localClient={networkManager.LocalClientId}");
            }
            else if (string.Equals(mode, "client", StringComparison.OrdinalIgnoreCase))
            {
                networkManager.OnClientConnectedCallback += OnClientConnected;
                if (!StartClient()) Debug.LogError($"[LAST_SHIFT_NETWORK_FAILED] mode=client address={address} port={port}");
            }
            else if (AutoStartHost)
            {
                // 인자가 없는 경로. host 가 켜지지 않으면 player 가 spawn 되지 않아 카메라도 없고
                // preset 도 적용되지 않아, 잡을 물건 하나 없는 검은 화면으로 보인다.
                if (StartHost())
                    Debug.Log($"[LAST_SHIFT_NETWORK_READY] mode=host-auto address={address} port={port} localClient={networkManager.LocalClientId}");
                else
                    Debug.LogError($"[LAST_SHIFT_NETWORK_FAILED] mode=host-auto address={address} port={port}");
            }
        }

        /// <summary>
        /// 이미 host 로 떠 있으면 성공으로 본다. 에디터 Play 자동 host 와 명시적 StartHost 호출이
        /// 겹쳐도 두 번째 호출이 false 를 돌려주며 실패로 읽히지 않게 한다.
        /// </summary>
        public bool StartHost()
        {
            if (networkManager == null) return false;
            if (networkManager.IsHost) return true;
            ConfigureTransport();
            return networkManager.StartHost();
        }

        public bool StartClient()
        {
            ConfigureTransport();
            return networkManager != null && networkManager.StartClient();
        }

        /// <summary>
        /// 테스트가 포트를 갈라 쓰게 한다. 같은 포트를 연속으로 재사용하면 앞 테스트의 UDP 소켓이
        /// 아직 풀리지 않아 "address is already in use" 로 bind 가 실패한다.
        /// </summary>
        public void OverridePort(ushort value)
        {
            port = value;
        }

        public void StopSession()
        {
            if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
            slotAllocator.Clear();
        }

        public void PlaceAndRegisterPlayer(LastShiftNetworkPlayer player)
        {
            if (player == null || !slotAllocator.TryGet(player.OwnerClientId, out var slot)) return;
            player.transform.SetPositionAndRotation(SpawnForSlot(slot), RotationForSlot(slot));
            sandbox?.RegisterPlayer(player.GetComponent<LastShiftPlayerController>());
            if (HasArgument("-lastShiftLifecycleProbe"))
                Debug.Log($"[LAST_SHIFT_SLOT] client={player.OwnerClientId} slot={slot} phase=assigned active={slotAllocator.Count}");
        }

        public void UnregisterPlayer(LastShiftNetworkPlayer player)
        {
            if (player == null) return;
            sandbox?.UnregisterPlayer(player.GetComponent<LastShiftPlayerController>());
        }

        public void ResetRegisteredPlayerPositions()
        {
            if (networkManager == null || !networkManager.IsServer) return;
            foreach (var client in networkManager.ConnectedClients)
            {
                if (client.Value.PlayerObject == null || !slotAllocator.TryGet(client.Key, out var slot)) continue;
                var player = client.Value.PlayerObject.GetComponent<LastShiftNetworkPlayer>();
                if (player == null) continue;
                var slotPosition = SpawnForSlot(slot);
                var slotRotation = RotationForSlot(slot);
                // 실제 이동은 소유 클라이언트가 수행한다. 서버는 조준 캐시만 리셋 자세로 맞춰
                // owner 보고가 도착하기 전 stale 조준으로 grab 이 판정되는 것을 막는다.
                player.ResetServerAimCache(slotPosition, slotRotation);
                player.ResetToSlotRpc(slotPosition, slotRotation);
            }
        }

        public static Vector3 SpawnForSlot(int slot)
        {
            if (slot < 0 || slot >= MaxPlayers) throw new ArgumentOutOfRangeException(nameof(slot));
            return LastShiftSandboxController.PlayerSpawn + new Vector3(0f, 0f, (slot - 1.5f) * 0.85f);
        }

        /// <summary>
        /// 스폰 시선. 조종석에서 배 안쪽(엔진실의 냉각통 자리)을 바라본다. 36m 선체에서는
        /// 그 지점이 인지 거리 밖이라 물건 자체는 보이지 않지만, 시작 시선이 끝벽이 아니라
        /// 배 진행 방향을 향해야 어디로 가야 하는지가 첫 프레임에 읽힌다.
        /// </summary>
        public static Quaternion RotationForSlot(int slot)
        {
            var position = SpawnForSlot(slot);
            var target = new Vector3(
                LastShiftShipDimensions.CoolingNominal.x,
                position.y,
                LastShiftShipDimensions.CoolingNominal.z);
            return Quaternion.LookRotation((target - position).normalized, Vector3.up);
        }

        private void ConfigureTransport()
        {
            if (transport == null) return;
            var commandLineAddress = ReadArgument("-lastShiftAddress");
            if (!string.IsNullOrWhiteSpace(commandLineAddress)) address = commandLineAddress;
            var commandLinePort = ReadArgument("-lastShiftPort");
            if (ushort.TryParse(commandLinePort, out var parsedPort)) port = parsedPort;
            transport.SetConnectionData(address, port, "0.0.0.0");
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager != null && clientId == networkManager.LocalClientId)
                Debug.Log($"[LAST_SHIFT_NETWORK_READY] mode=client address={address} port={port} localClient={clientId}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var released = slotAllocator.Release(clientId);
            if (released && HasArgument("-lastShiftLifecycleProbe"))
                Debug.Log($"[LAST_SHIFT_SLOT] client={clientId} phase=released active={slotAllocator.Count}");
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = slotAllocator.TryReserve(request.ClientNetworkId, out var slot);
            response.CreatePlayerObject = response.Approved;
            response.Position = response.Approved ? SpawnForSlot(slot) : Vector3.zero;
            response.Rotation = response.Approved ? RotationForSlot(slot) : Quaternion.identity;
            response.Pending = false;
            response.Reason = response.Approved ? string.Empty : "LAST SHIFT session is full (maximum 4 players).";
        }

        private static bool HasArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return null;
        }
    }
}
