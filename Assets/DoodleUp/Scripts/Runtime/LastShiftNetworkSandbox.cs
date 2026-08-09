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

        /// <summary>호스트의 저장 서비스. 클라이언트에는 이것이 없어도 된다 — 조항 S-2.</summary>
        [SerializeField] private LastShiftSaveService saveService;
        private float nextSnapshotTime;
        private readonly System.Collections.Generic.Dictionary<LastShiftNetworkGrabbable, float> outOfBoundsSince = new();

        /// <summary>
        /// 경계 밖 체류를 이탈로 판정하기까지의 유예. 0.25초 tick 이므로 실제로는 6~7 tick 이다.
        /// 저중력에서 물건이 튕겨 경계를 스치고 돌아오는 시간(관측상 1초 이내)보다 길고,
        /// 정말 소실된 물건을 회수하기까지 사용자가 기다릴 만한 시간보다는 짧다.
        /// </summary>
        public const float OutOfBoundsGraceSeconds = 1.5f;

        // 선체 치수는 LastShiftShipDimensions 가 정본이다. CT-02 에서 천장과 벽을
        // CeilingInnerHeight 까지 닫았으므로 위로 새는 경로는 사라졌고, 남는 이탈 경로는
        // 콜라이더 틈뿐이다. 그래도 bounds 는 선체보다 넉넉히 두어, 틈으로 튄 물건이
        // 되돌아올 여유를 주고 도보 회수가 불가능한 범위만 막는다.
        //
        // 선체가 커지면 이 값도 따라와야 한다 — 고정 16m 를 남겨 두면 36m 배에서는
        // 산소실 물건이 전부 "경계 밖" 으로 판정돼 매 tick 제자리로 튕겨 돌아온다.
        public static readonly Bounds ItemSafetyBounds = LastShiftShipDimensions.ItemSafetyBounds;

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

        /// <summary>
        /// 서버 상태를 스냅샷으로 접어 올린다. <b>필드 채우기는 여기 없다</b> —
        /// <see cref="LastShiftSandboxController.CaptureRuntimeSnapshot"/> 하나가 정본이고,
        /// 세이브 파일 층도 같은 함수를 부른다. 두 벌로 두면 필드를 늘릴 때 한쪽만 늘어난다.
        ///
        /// 여기 남는 것은 네트워크에만 있는 부수 효과다.
        /// </summary>
        public void PublishSnapshot()
        {
            if (!IsServer || sandbox == null) return;
            foreach (var item in sandbox.Items)
            {
                if (item == null) continue;
                // 고정된 항목만 동기화하면 secured -> loose 로 바뀐 항목이 stale secured=true 를 유지한다.
                // 그 상태에서 클라이언트는 "고정됨" 을 표시하고 서버 검증은 item-secured 로 거부한다.
                var networkItem = item.GetComponent<LastShiftNetworkGrabbable>();
                if (networkItem != null) networkItem.SyncSecuredFromServer();
            }

            snapshot.Value = sandbox.CaptureRuntimeSnapshot();
        }

        /// <summary>
        /// 클라이언트가 누른 저장을 호스트로 넘긴다(<c>docs/tech/save-backbone-feasibility-v1.md</c>
        /// §7.1). <b>클라이언트는 세이브를 갖지 않는다</b>(조항 S-2) — 파일도 캡처도 호스트 것이고,
        /// 여기서 넘어가는 것은 "지금 저장해 달라" 는 요청 하나뿐이다.
        ///
        /// <b>클라이언트를 세우지 않는다.</b> 아이템·승무원 포즈가 소유자 권위라 호스트가 보는 값은
        /// <c>RTT</c> + 보간만큼 지난 것이지만, 시뮬이 이미 그 지연된 값으로 판정하므로(§7.3)
        /// 저장이 시뮬보다 더 최신인 값을 뜰 이유가 없다. 되받아 오면 요구 (가)(체감 정지 <c>0</c>)를
        /// 스스로 깨고 얻는 것은 판정에 안 쓰이는 <c>30</c>cm 다.
        ///
        /// 캡처 시점은 이 프레임의 <c>LateUpdate</c> 다 — <see cref="LastShiftSaveService.RequestSave"/>
        /// 가 플래그만 세우고, 재진입 가드도 그쪽 하나다(§1.4-마-1).
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestSaveRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender)) return;

            if (saveService == null) saveService = GetComponent<LastShiftSaveService>();
            if (saveService == null)
            {
                Debug.LogError($"[LAST_SHIFT_SAVE] result=FAIL reason=no-service client={sender}");
                return;
            }

            saveService.RequestSave();
            Debug.Log($"[LAST_SHIFT_SAVE_REQUEST] client={sender} writing={saveService.IsWriting} result=ACCEPT");
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
            if (IsGhostCrew(player, sender, "secure")) return;
            if (player.HeldItem.SecureFromServer(player))
            {
                sandbox.RefreshResultAfterImpact();
                PublishSnapshot();
            }
        }

        /// <summary>
        /// 문 개폐의 네트워크 경로(N0b). 대상 문은 서버가 요청자 위치로 고르고, 살아 있는
        /// 승무원인지도 서버가 <see cref="LastShiftZoneDoor.TryOperate"/> 안에서 확인한다.
        /// 클라이언트 쪽 판정은 프롬프트용이고 권위는 이쪽 하나다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestDoorToggleRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender) || sandbox == null) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(sender, out var client)) return;
            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<LastShiftNetworkPlayer>() : null;
            if (player == null || player.OwnerClientId != sender) return;

            var crew = player.GetComponent<LastShiftPlayerController>();
            var door = LastShiftZoneDoor.FindOperable(player.transform.position);
            if (door != null)
            {
                if (!door.TryOperate(crew)) return;
            }
            else
            {
                // 같은 Q 가 승강구 해치도 조작한다(§23.6 — 새 조작 동사를 안 늘린다). 문과 해치는
                // 사거리가 겹치지 않으므로 순서를 정할 필요가 없고, 여기서는 문을 먼저 볼 뿐이다.
                var hatch = LastShiftDeckHatch.FindOperable(player.transform.position);
                if (hatch == null || !hatch.TryOperate(crew)) return;
            }
            PublishSnapshot();
        }

        /// <summary>
        /// R1 수리 동사의 네트워크 경로. 어느 계통을 되돌릴지는 서버가 요청자의 소지품과 위치로만
        /// 판정한다. 클라이언트가 계통을 직접 지정하면 들고 있지 않은 부품으로 복구할 수 있다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestRepairRpc(LastShiftRepairMode mode, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender) || sandbox == null) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(sender, out var client)) return;
            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<LastShiftNetworkPlayer>() : null;
            if (player == null || player.OwnerClientId != sender) return;
            if (IsGhostCrew(player, sender, "repair")) return;

            var held = player.HeldItem != null ? player.HeldItem.Grabbable : null;
            if (!sandbox.TryBeginRepair(mode, held, player.transform.position)) return;
            PublishSnapshot();
        }

        /// <summary>
        /// 냉각실 밸브 유지의 네트워크 경로(<c>C-3</c>, §4.3). 사거리·생사 판정은 전부
        /// <see cref="LastShiftSandboxController.SetCoolingValveHeld"/> 안에 있다.
        ///
        /// <b>스냅샷을 안 올린다.</b> 밸브는 상태를 남기지 않는 동사라 클라이언트가 복원할 것이
        /// 없고, 결과는 이미 동기화되는 <c>EngineHeat</c> 로 매 tick 관측된다. 여기서
        /// <c>PublishSnapshot</c> 을 부르면 누르고 뗄 때마다 전체 상태가 한 번 더 날아간다.
        ///
        /// <b>놓기는 유령도 통과시킨다.</b> 잡은 채로 죽는 경우가 있고, 그때 놓기 요청까지 막으면
        /// 서버 매 tick 정리(<c>crew-dead</c>)만이 유일한 해제 경로가 된다 — 그건 동작하지만,
        /// 같은 사실을 두 곳이 다르게 판정하게 두는 자리다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestCoolingValveHoldRpc(bool held, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender) || sandbox == null) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(sender, out var client)) return;
            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<LastShiftNetworkPlayer>() : null;
            if (player == null || player.OwnerClientId != sender) return;
            if (held && IsGhostCrew(player, sender, "cooling-valve")) return;

            var crew = player.GetComponent<LastShiftPlayerController>();
            if (crew != null) sandbox.SetCoolingValveHeld(crew, held);
        }

        /// <summary>
        /// 요청자가 유령인가(기획 §4.4 — 수리 동사 3종 불가). 문은
        /// <see cref="LastShiftZoneDoor.TryOperate"/> 가, 잡기는
        /// <see cref="LastShiftNetworkPlayer.TryGrabFromServer"/> 가 각자 같은 판정을 하고,
        /// 여기는 그 둘을 지나지 않는 나머지 두 동사(수리·고정)의 자리다.
        /// </summary>
        private static bool IsGhostCrew(LastShiftNetworkPlayer player, ulong sender, string action)
        {
            var crew = player.GetComponent<LastShiftCrewOxygen>();
            if (crew == null || !crew.IsDead) return false;
            Debug.Log($"[LAST_SHIFT_INTERACTION] client={sender} action={action} result=REJECT reason=crew-dead");
            return true;
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
