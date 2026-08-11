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

        /// <summary>스폰 좌표에서 갑판을 찾을 때 위로 띄우는 높이.</summary>
        private const float DeckProbeRise = 0.5f;

        /// <summary>그 높이에서 아래로 훑는 거리. 갑판이 스폰 y 바로 밑에 있으므로 짧게 둔다.</summary>
        private const float DeckProbeDrop = 2f;

        /// <summary>선외 보행면 밑으로 이만큼 더 내려가야 "떨어졌다" 로 본다.</summary>
        private const float CrewFallMargin = 2.5f;

        /// <summary>낙하 점검 주기. 물건 회수(0.25초)와 같은 대역이면 충분하다.</summary>
        private const float CrewFallCheckSeconds = 0.25f;

        private readonly LastShiftNetworkSlotAllocator slotAllocator = new();
        private LastShiftRoomBeacon beacon;
        private bool connectionOverridden;
        private float nextCrewFallCheckTime;

        public NetworkManager NetworkManager => networkManager;
        public LastShiftSandboxController Sandbox => sandbox;
        public string Address => address;
        public ushort Port => port;

        /// <summary>호스트로 방을 연 뒤 발급된 코드. 방을 열지 않았으면 빈 문자열.</summary>
        public string RoomCode { get; private set; } = string.Empty;

        /// <summary>코드로 방을 찾을 수 있는 상태인지. 디스커버리 포트를 못 잡으면 false 다.</summary>
        public bool RoomDiscoverable => beacon != null;

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

            // 로비는 씬 빌더가 붙이지만, 이미 저장된 씬에는 아직 없다. 씬을 다시 굽는 것은
            // 배 프리팹과 NetworkObject 해시까지 새로 찍는 큰 작업이라 그 하나 때문에 돌리지
            // 않는다. 런타임에 없으면 여기서 채워, 어느 쪽 씬이든 같은 화면으로 시작한다.
            if (Application.isPlaying && GetComponent<LastShiftRoomLobby>() == null)
                gameObject.AddComponent<LastShiftRoomLobby>();
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
        ///
        /// 이름은 그대로지만 켜졌을 때의 뜻이 하나 바뀌었다. 방 코드 로비가 생긴 뒤로 인자 없는
        /// 경로는 <b>바로 host 를 띄우는 대신 로비를 연다</b> — 사람이 "방 열기"와 "코드로 입장"을
        /// 고르는 화면이다. 로비가 없는 씬(로비 컴포넌트를 못 붙인 경우)에서만 예전처럼 자동으로
        /// host 가 뜬다. 끄면(false) 로비도 자동 host 도 없다 — 테스트가 쓰는 그 의미 그대로다.
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
                var lobby = GetComponent<LastShiftRoomLobby>();
                if (lobby != null)
                {
                    lobby.Open();
                    return;
                }

                // 로비가 없는 경로. host 가 켜지지 않으면 player 가 spawn 되지 않아 카메라도 없고
                // preset 도 적용되지 않아, 잡을 물건 하나 없는 검은 화면으로 보인다.
                if (StartHost())
                    Debug.Log($"[LAST_SHIFT_NETWORK_READY] mode=host-auto address={address} port={port} localClient={networkManager.LocalClientId}");
                else
                    Debug.LogError($"[LAST_SHIFT_NETWORK_FAILED] mode=host-auto address={address} port={port}");
            }
        }

        /// <summary>
        /// 방을 연다. host 를 띄우고 그 코드를 묻는 LAN 질의에 답할 비컨을 함께 세운다.
        ///
        /// 비컨을 못 세워도 방 자체는 성공으로 본다. 코드 검색이 막히는 것은 같은 PC 에 이미
        /// 다른 방이 열려 디스커버리 포트가 잡혀 있을 때가 대부분인데, 그 경우에도 IP 를 아는
        /// 상대는 여전히 들어올 수 있다. 대신 <see cref="RoomDiscoverable"/> 로 그 사실을 알린다.
        /// </summary>
        public bool OpenRoom(string roomCode)
        {
            if (!StartHost()) return false;
            RoomCode = LastShiftRoomCode.Normalize(roomCode);
            CloseBeacon();
            try
            {
                beacon = new LastShiftRoomBeacon(RoomCode, port);
                Debug.Log($"[LAST_SHIFT_ROOM] phase=opened code={RoomCode} port={port} discovery={LastShiftRoomProtocol.DiscoveryPort}");
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[LAST_SHIFT_ROOM] phase=opened code={RoomCode} discovery=unavailable detail={error.Message}");
            }
            return true;
        }

        /// <summary>
        /// 찾아낸 호스트로 붙는다. 주소를 여기서 못 박으므로 커맨드라인 인자가 이것을 덮지 않는다
        /// — 로비로 들어간 방이 낡은 <c>-lastShiftAddress</c> 때문에 엉뚱한 곳으로 가면 안 된다.
        /// </summary>
        public bool JoinRoom(string hostAddress, ushort hostPort)
        {
            SetConnection(hostAddress, hostPort);
            return StartClient();
        }

        public void SetConnection(string hostAddress, ushort hostPort)
        {
            if (!string.IsNullOrWhiteSpace(hostAddress)) address = hostAddress;
            if (hostPort != 0) port = hostPort;
            connectionOverridden = true;
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
            CloseBeacon();
            RoomCode = string.Empty;
        }

        /// <summary>
        /// 비컨은 UDP 포트를 잡은 배경 스레드다. Play 를 멈추거나 씬을 갈아 끼울 때 여기서
        /// 걷어내지 않으면 포트가 물린 채 남아 다음 방이 코드 검색 없이 뜬다.
        /// </summary>
        private void OnDestroy()
        {
            CloseBeacon();
        }

        private void CloseBeacon()
        {
            beacon?.Dispose();
            beacon = null;
        }

        public void PlaceAndRegisterPlayer(LastShiftNetworkPlayer player)
        {
            if (player == null || !slotAllocator.TryGet(player.OwnerClientId, out var slot)) return;
            var controller = player.GetComponent<LastShiftPlayerController>();
            var pose = player.GetComponent<LastShiftMapSpawnPose>();
            var position = pose != null ? pose.SpawnForSlot(slot) : SpawnForSlot(slot);
            var rotation = pose != null ? pose.RotationFor(position) : RotationForSlot(slot);
            // <b>CharacterController 를 켠 채 transform 만 옮기지 않는다.</b> PhysX 컨트롤러는
            // 옮기기 전 자세를 자기 안에 들고 있어서, 물리 동기화 전에 그 프레임의 첫 Move 가
            // 돌면 승무원이 프리팹 원점 기준으로 쓸린다. 그러면 스폰 좌표는 갑판 위인데 실제
            // 캡슐은 다른 자리에서 시작하고, 그 아래가 비어 있으면 그대로 떨어진다.
            // ResetPlayer 가 이미 끄고-옮기고-켜는 순서를 지키므로 스폰도 같은 문으로 들어간다.
            if (controller != null) controller.ResetPlayer(position, rotation);
            else player.transform.SetPositionAndRotation(position, rotation);
            UnityEngine.Physics.SyncTransforms();
            WarnWhenSpawnHasNoDeck(slot, position);
            sandbox?.RegisterPlayer(controller);
            if (HasArgument("-lastShiftLifecycleProbe"))
                Debug.Log($"[LAST_SHIFT_SLOT] client={player.OwnerClientId} slot={slot} phase=assigned active={slotAllocator.Count}");
        }

        /// <summary>
        /// 스폰 자리 밑에 실제로 밟을 것이 있는지 본다. 좌표는 <see cref="LastShiftShipDimensions.SpawnPoint"/>
        /// 하나에서 나오지만 그 좌표를 덮는 갑판은 씬(배 프리팹) 몫이라, 배가 다시 구워지면서
        /// 조종석 방 갑판이 갈리면 좌표만 맞고 밑은 비어 있는 상태가 조용히 성립한다.
        /// 그때 화면에 보이는 것은 "방을 열자마자 승무원이 떨어진다" 뿐이라 원인을 못 가린다.
        /// </summary>
        private static void WarnWhenSpawnHasNoDeck(int slot, Vector3 position)
        {
            var origin = position + Vector3.up * DeckProbeRise;
            if (UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit, DeckProbeRise + DeckProbeDrop))
            {
                if (HasArgument("-lastShiftLifecycleProbe"))
                    Debug.Log($"[LAST_SHIFT_SPAWN_DECK] slot={slot} deck={hit.collider.name} y={hit.point.y:F2} result=OK");
                return;
            }

            Debug.LogError(
                $"[LAST_SHIFT_SPAWN_DECK] slot={slot} spawn={position} result=NO_DECK " +
                $"detail=스폰 좌표 아래 {DeckProbeRise + DeckProbeDrop:F1}m 안에 콜라이더가 없다");
        }

        public void UnregisterPlayer(LastShiftNetworkPlayer player)
        {
            if (player == null) return;
            sandbox?.UnregisterPlayer(player.GetComponent<LastShiftPlayerController>());
        }

        /// <summary>
        /// 배 밖으로 떨어진 승무원을 자기 슬롯으로 되돌린다 — 물건 쪽의
        /// <see cref="LastShiftNetworkSandbox.RecoverItemsOutsideSafetyBounds"/> 와 같은 자리의
        /// 승무원 판이다. <b>낙하는 스스로 끝나지 않는다</b>: 저중력이라 천천히 떨어질 뿐
        /// 바닥이 없으면 영원히 내려가고, 그 사이 조작·산소·판정이 전부 무의미해진다.
        ///
        /// 기준면은 <b>정당하게 설 수 있는 가장 깊은 자리</b>다. 그 밑으로
        /// <see cref="CrewFallMargin"/> 이상 내려간 좌표는 밟을 것이 있는 자리가 아니다.
        /// 여유를 두는 것은 발판 모서리에서 한 뼘 미끄러지는 정상 궤적을 회수로 읽지 않기
        /// 위해서다.
        ///
        /// <b>예전에는 선외 보행면을 기준으로 삼았다.</b> 그때는 보행면이 배 밑면이라 "배 안의
        /// 가장 깊은 자리도 그 위" 가 성립했는데, EVA 가 상향으로 뒤집히면서(2026-08-11)
        /// 보행면이 <c>+6.2</c> 로 올라가 그 전제가 반대가 됐다. 그대로 두면 판정면이
        /// <c>3.7</c> 이 되어 <b>갑판에 서 있는 승무원이 전부 월드 밖으로 잡힌다</b>.
        /// PlayMode 의 슬롯 복귀 검사가 "복구를 기다리다 시간 초과" 로 그것을 잡았다.
        /// </summary>
        public static float CrewFallFloorY =>
            Mathf.Min(LastShiftHullShell.RimBaseY, LastShiftBypassDuct.AirlockFloorY) - CrewFallMargin;

        /// <summary>
        /// 떨어진 승무원을 슬롯 자리로 되돌린다. 서버에서만 돈다.
        /// </summary>
        /// <returns>되돌린 승무원 수.</returns>
        public int RecoverCrewBelowWorld()
        {
            if (networkManager == null || !networkManager.IsServer) return 0;
            var floor = CrewFallFloorY;
            var recovered = 0;
            foreach (var client in networkManager.ConnectedClients)
            {
                if (client.Value.PlayerObject == null) continue;
                var fell = client.Value.PlayerObject.transform.position;
                if (fell.y >= floor) continue;
                if (!slotAllocator.TryGet(client.Key, out var slot)) continue;
                var player = client.Value.PlayerObject.GetComponent<LastShiftNetworkPlayer>();
                if (player == null) continue;

                var pose = player.GetComponent<LastShiftMapSpawnPose>();
                var position = pose != null ? pose.SpawnForSlot(slot) : SpawnForSlot(slot);
                var rotation = pose != null ? pose.RotationFor(position) : RotationForSlot(slot);
                player.ResetServerAimCache(position, rotation);
                player.ResetToSlotRpc(position, rotation);
                // 경고로 남긴다. 정상 플레이에서는 한 번도 안 나와야 하는 줄이고, 나온다면
                // 어디서 떨어졌는지가 다음 재현의 유일한 단서다.
                Debug.LogWarning(
                    $"[LAST_SHIFT_CREW_RECOVER] client={client.Key} slot={slot} " +
                    $"fell=({fell.x:F2},{fell.y:F2},{fell.z:F2}) floor={floor:F2} action=return-to-slot");
                recovered++;
            }
            return recovered;
        }

        private void Update()
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (Time.unscaledTime < nextCrewFallCheckTime) return;
            nextCrewFallCheckTime = Time.unscaledTime + CrewFallCheckSeconds;
            RecoverCrewBelowWorld();
        }

        public void ResetRegisteredPlayerPositions()
        {
            if (networkManager == null || !networkManager.IsServer) return;
            foreach (var client in networkManager.ConnectedClients)
            {
                if (client.Value.PlayerObject == null || !slotAllocator.TryGet(client.Key, out var slot)) continue;
                var player = client.Value.PlayerObject.GetComponent<LastShiftNetworkPlayer>();
                if (player == null) continue;
                var pose = player.GetComponent<LastShiftMapSpawnPose>();
                var slotPosition = pose != null ? pose.SpawnForSlot(slot) : SpawnForSlot(slot);
                var slotRotation = pose != null ? pose.RotationFor(slotPosition) : RotationForSlot(slot);
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
        /// 스폰 시선. 조종석에서 중앙광장(<c>ModularKitAssembly</c> 원점)을 바라본다.
        /// 외피 모듈 교체 후에도 첫 프레임에 레벨 내부가 보이도록, 방 너머의 냉각 아이템이
        /// 아니라 실제 조립 기준 원점을 프레이밍한다.
        /// </summary>
        public static Quaternion RotationForSlot(int slot)
        {
            var position = SpawnForSlot(slot);
            var target = new Vector3(0f, position.y, 0f);
            return Quaternion.LookRotation((target - position).normalized, Vector3.up);
        }

        private void ConfigureTransport()
        {
            if (transport == null) return;
            if (connectionOverridden)
            {
                transport.SetConnectionData(address, port, "0.0.0.0");
                return;
            }
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
