using Unity.Netcode;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LastShiftNetworkSandbox : NetworkBehaviour
    {
        private readonly NetworkVariable<LastShiftNetworkSnapshot> snapshot = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [SerializeField] private LastShiftSandboxController sandbox;
        private float nextSnapshotTime;

        public LastShiftNetworkSnapshot Snapshot => snapshot.Value;

        public override void OnNetworkSpawn()
        {
            if (sandbox == null) sandbox = GetComponent<LastShiftSandboxController>();
            snapshot.OnValueChanged += OnSnapshotChanged;
            if (sandbox != null) sandbox.enabled = IsServer;
            if (IsServer)
            {
                if (sandbox.ResetGeneration == 0) sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
                PublishSnapshot();
            }
            else if (sandbox != null) sandbox.ApplyNetworkSnapshot(snapshot.Value);
        }

        public override void OnNetworkDespawn()
        {
            snapshot.OnValueChanged -= OnSnapshotChanged;
        }

        private void Update()
        {
            if (!IsServer || Time.unscaledTime < nextSnapshotTime) return;
            nextSnapshotTime = Time.unscaledTime + 0.25f;
            PublishSnapshot();
        }

        public void Configure(LastShiftSandboxController controller)
        {
            sandbox = controller;
        }

        public void PrepareForPresetReset()
        {
            if (!IsServer) return;
            foreach (var item in FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None))
                item.ReleaseForDisconnectOrReset();
        }

        public void PublishSnapshot()
        {
            if (!IsServer || sandbox == null) return;
            byte securedMask = 0;
            foreach (var item in sandbox.Items)
            {
                if (item == null) continue;
                if (item.Secured) securedMask |= (byte)(1 << (int)item.Role);
                // 고정된 항목만 동기화하면 secured -> loose 로 바뀐 항목이 stale secured=true 를 유지한다.
                // 그 상태에서 클라이언트는 "고정됨" 을 표시하고 서버 검증은 item-secured 로 거부한다.
                var networkItem = item.GetComponent<LastShiftNetworkGrabbable>();
                if (networkItem != null) networkItem.SyncSecuredFromServer();
            }

            snapshot.Value = new LastShiftNetworkSnapshot
            {
                Preset = sandbox.CurrentPreset,
                ShipState = sandbox.CurrentState,
                FirstProblem = sandbox.FirstResult.Problem,
                CurrentProblem = sandbox.LastResult.Problem,
                CoolingScore = sandbox.LastResult.CoolingScore,
                BatteryScore = sandbox.LastResult.BatteryScore,
                LeakScore = sandbox.LastResult.LeakScore,
                DockingSecondsRemaining = sandbox.DockingSecondsRemaining,
                ResetGeneration = sandbox.ResetGeneration,
                ImpactApplicationCount = sandbox.ImpactApplicationCount,
                SecuredItemMask = securedMask,
                HasAppliedImpact = sandbox.HasAppliedImpact
            };
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestMeteorImpactRpc(RpcParams rpcParams = default)
        {
            if (!IsConnectedSender(rpcParams.Receive.SenderClientId) || sandbox == null) return;
            sandbox.ApplyMeteorImpact();
            PublishSnapshot();
        }

        public void ResetPresetFromServer(LastShiftPreset preset)
        {
            if (!IsServer || sandbox == null) return;
            sandbox.ResetPreset(preset);
            FindFirstObjectByType<LastShiftNetworkSession>()?.ResetRegisteredPlayerPositions();
            PublishSnapshot();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestPresetResetRpc(LastShiftPreset preset, RpcParams rpcParams = default)
        {
            if (!IsConnectedSender(rpcParams.Receive.SenderClientId)) return;
            ResetPresetFromServer(preset);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestSecureHeldItemRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender) || !NetworkManager.ConnectedClients.TryGetValue(sender, out var client)) return;
            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<LastShiftNetworkPlayer>() : null;
            if (player == null || player.OwnerClientId != sender || player.HeldItem == null) return;
            if (player.HeldItem.SecureFromServer(player))
            {
                sandbox.RefreshResultAfterImpact();
                PublishSnapshot();
            }
        }

        private void OnSnapshotChanged(LastShiftNetworkSnapshot previous, LastShiftNetworkSnapshot current)
        {
            if (!IsServer && sandbox != null) sandbox.ApplyNetworkSnapshot(current);
        }

        private bool IsConnectedSender(ulong sender)
        {
            return IsServer && NetworkManager != null && NetworkManager.ConnectedClients.ContainsKey(sender);
        }
    }
}
