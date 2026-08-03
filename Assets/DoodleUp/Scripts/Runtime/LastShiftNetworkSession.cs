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
#if UNITY_EDITOR
            else if (StartHost())
            {
                // 에디터 Play 는 -lastShiftNetworkMode 인자를 받을 수 없다. host 가 켜지지 않으면
                // player 가 spawn 되지 않고 preset 도 적용되지 않아 잡을 물건이 하나도 없는 상태로 보인다.
                Debug.Log($"[LAST_SHIFT_NETWORK_READY] mode=host-editor address={address} port={port} localClient={networkManager.LocalClientId}");
            }
#endif
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

        public static Quaternion RotationForSlot(int slot)
        {
            var position = SpawnForSlot(slot);
            var target = new Vector3(0f, position.y, -1.3f);
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
