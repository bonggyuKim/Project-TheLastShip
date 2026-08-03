using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public sealed class LastShiftNetworkSession : MonoBehaviour
    {
        public const ushort DefaultPort = 7979;
        public const int MaxPlayers = 4;

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
                if (transport != null) networkManager.NetworkConfig.NetworkTransport = transport;
                networkManager.ConnectionApprovalCallback = ApproveConnection;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
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
