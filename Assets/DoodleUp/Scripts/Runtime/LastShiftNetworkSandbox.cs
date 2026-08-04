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
        private readonly System.Collections.Generic.Dictionary<LastShiftNetworkGrabbable, float> outOfBoundsSince = new();

        /// <summary>
        /// 경계 밖 체류를 이탈로 판정하기까지의 유예. 0.25초 tick 이므로 실제로는 6~7 tick 이다.
        /// 저중력에서 물건이 튕겨 경계를 스치고 돌아오는 시간(관측상 1초 이내)보다 길고,
        /// 정말 소실된 물건을 회수하기까지 사용자가 기다릴 만한 시간보다는 짧다.
        /// </summary>
        public const float OutOfBoundsGraceSeconds = 1.5f;

        // 선체 내부는 x≈±6.15, z≈±2.45, y=0..3.2(천장 내면) 범위다. CT-02 에서 천장과
        // 벽을 CeilingInnerHeight 까지 닫았으므로 위로 새는 경로는 사라졌고, 남는 이탈
        // 경로는 콜라이더 틈뿐이다. 그래도 bounds 는 선체보다 넉넉히 두어, 틈으로 튄
        // 물건이 되돌아올 여유를 주고 도보 회수가 불가능한 범위만 막는다.
        public static readonly Bounds ItemSafetyBounds = new(
            new Vector3(0f, 2.5f, 0f),
            new Vector3(16f, 11f, 12f));

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
            RecoverItemsOutsideSafetyBounds();
            PublishSnapshot();
        }

        /// <summary>
        /// loose/held/ownership 전환 중 어느 상태라도 world safety bounds 밖이면 서버에서 복구한다.
        /// secured item 은 nominal 에 있어야 하므로 예외 없이 같은 경계를 적용한다.
        ///
        /// 저중력(CT-02)에서는 즉시 복구하면 안 된다. 물건이 천천히 떠서 흐르다 경계를 살짝
        /// 넘고 되돌아오는 궤적은 정상 연출이고, 그걸 회수하면 "떠다니는 느낌" 자체가 사라진다.
        /// 그래서 경계 밖 <see cref="OutOfBoundsGraceSeconds"/> 연속 체류만 이탈로 판정한다.
        /// 되돌아온 물건은 타이머가 초기화되므로 복구되지 않는다.
        /// </summary>
        public int RecoverItemsOutsideSafetyBounds()
        {
            if (!IsServer) return 0;
            var recovered = 0;
            foreach (var item in FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None))
            {
                if (item == null || !item.IsSpawned) continue;
                item.EnforcePendingRecoveryPose();
                if (ItemSafetyBounds.Contains(item.transform.position))
                {
                    outOfBoundsSince.Remove(item);
                    continue;
                }

                if (!outOfBoundsSince.TryGetValue(item, out var since))
                {
                    outOfBoundsSince[item] = Time.unscaledTime;
                    continue;
                }
                if (Time.unscaledTime - since < OutOfBoundsGraceSeconds) continue;

                var reason = item.transform.position.y < ItemSafetyBounds.min.y
                    ? "below-world"
                    : item.transform.position.y > ItemSafetyBounds.max.y
                        ? "above-world"
                        : "outside-hull-range";
                if (item.RecoverFromServer(reason)) recovered++;
                outOfBoundsSince.Remove(item);
            }
            return recovered;
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
            foreach (var item in FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None))
                item.SyncResetPoseFromServer();
            FindFirstObjectByType<LastShiftNetworkSession>()?.ResetRegisteredPlayerPositions();
            PublishSnapshot();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestPresetResetRpc(LastShiftPreset preset, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender)) return;
            var generationBefore = sandbox != null ? sandbox.ResetGeneration : -1;
            ResetPresetFromServer(preset);
            Debug.Log($"[LAST_SHIFT_PRESET_REQUEST] client={sender} requested={preset} applied={sandbox?.CurrentPreset} generation={generationBefore}->{sandbox?.ResetGeneration} result={(sandbox != null && sandbox.CurrentPreset == preset && sandbox.ResetGeneration > generationBefore ? "PASS" : "FAIL")}");
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
