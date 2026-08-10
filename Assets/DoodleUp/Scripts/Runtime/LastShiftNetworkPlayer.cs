using Unity.Netcode;
using UnityEngine;
using System.Linq;

namespace DoodleUp.Runtime
{
    [RequireComponent(typeof(NetworkObject), typeof(LastShiftPlayerController))]
    public sealed class LastShiftNetworkPlayer : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> heldItemReference = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Vector3> holdPosition = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Quaternion> holdRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// owner 카메라의 실제 조준 원점과 방향. 이전에는 서버가 원격 플레이어의 조준을
        /// holdPosition/holdRotation 에서 역산했는데, HoldSocket 이 카메라 중심에서
        /// (0.45, -0.30, 1.1) 만큼 어긋나 있어 위·아래를 본 유효 grab 이 origin 오차로 거부됐다.
        /// owner 가 조준 자체를 직접 올려 서버 raycast 와 클라이언트 판정이 같은 값을 쓰게 한다.
        /// </summary>
        private readonly NetworkVariable<Vector3> aimOrigin = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Vector3> aimDirection = new(
            Vector3.forward,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>
        /// 개인 예비 산소(N1). 소모 계산은 서버의 <see cref="LastShiftSandboxController"/> 만 하고
        /// 클라이언트는 값만 받는다. 자기 막대뿐 아니라 동료의 사망도 알아야 하므로 전원 공개다.
        /// </summary>
        private readonly NetworkVariable<float> suitOxygen = new(
            LastShiftRecoveryTuning.SuitOxygenInitial,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> crewDead = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> crewDraining = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private GUIStyle suitGaugeStyle;
        private LastShiftSandboxController hudSandbox;

        [SerializeField] private LastShiftPlayerController playerController;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Renderer bodyRenderer;

        private static readonly Color[] PlayerColors =
        {
            new(0.20f, 0.65f, 1f),
            new(1f, 0.38f, 0.22f),
            new(0.35f, 0.85f, 0.35f),
            new(0.85f, 0.45f, 1f)
        };

        private LastShiftNetworkGrabbable heldItem;
        private LastShiftOwnerNetworkTransform ownerNetworkTransform;
        private bool appliedGhostPresentation;

        /// <summary>씬 빌더가 배치한 플레이어 카메라의 로컬 오프셋. 조준 원점 기본값 계산에만 쓴다.</summary>
        private static readonly Vector3 CameraLocalOffset = new(0f, LastShiftShipPhysics.EyeHeight, 0f);

        public LastShiftNetworkGrabbable HeldItem => heldItem;
        public Renderer BodyRenderer => bodyRenderer;
        public bool IsBodyVisible => bodyRenderer != null && bodyRenderer.enabled;
        public Color PlayerColor => ColorForClient(OwnerClientId);
        public Transform HoldSocket => playerController != null ? playerController.HoldSocket : null;
        public Vector3 HoldPosition => IsOwner && HoldSocket != null ? HoldSocket.position : holdPosition.Value;
        public Quaternion HoldRotation => IsOwner && HoldSocket != null ? HoldSocket.rotation : holdRotation.Value;
        public Vector3 AuthoritativeAimOrigin => IsOwner && playerController != null
            ? playerController.AimOrigin
            : aimOrigin.Value;
        public Vector3 AuthoritativeAimDirection
        {
            get
            {
                if (IsOwner && playerController != null) return playerController.AimDirection;
                var replicated = aimDirection.Value;
                return replicated.sqrMagnitude > 0.0001f ? replicated.normalized : HoldRotation * Vector3.forward;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (playerController == null) playerController = GetComponent<LastShiftPlayerController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
            if (bodyRenderer == null)
            {
                var body = transform.Find("Remote Body");
                if (body != null) bodyRenderer = body.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer.name.Contains("Combined"))
                    ?? body.GetComponentInChildren<Renderer>(true);
            }
            heldItemReference.OnValueChanged += OnHeldItemReferenceChanged;
            // 서버는 sandbox 가 Ensure 하지만, 클라이언트는 sandbox 가 꺼져 있어 아무도 붙이지 않는다.
            // 복제값을 받을 그릇이 먼저 있어야 하므로 여기서 직접 붙인다.
            if (playerController != null) LastShiftCrewOxygen.Ensure(playerController);
            ApplyLocalPresentation(IsOwner);
            if (!IsServer)
            {
                ResolveHeldItem(heldItemReference.Value);
                return;
            }

            var session = FindFirstObjectByType<LastShiftNetworkSession>();
            if (session != null) session.PlaceAndRegisterPlayer(this);
        }

        public override void OnNetworkDespawn()
        {
            heldItemReference.OnValueChanged -= OnHeldItemReferenceChanged;
            if (IsServer)
            {
                if (heldItem != null) heldItem.ReleaseForDisconnectOrReset();
                var session = FindFirstObjectByType<LastShiftNetworkSession>();
                if (session != null) session.UnregisterPlayer(this);
            }
            heldItem = null;
        }

        public void Configure(LastShiftPlayerController controller, Camera targetCamera, Renderer visualRenderer = null)
        {
            playerController = controller;
            playerCamera = targetCamera;
            bodyRenderer = visualRenderer;
        }

        /// <summary>
        /// 개인 예비 산소를 서버에서 내리고 클라이언트에서 받는다(N1/N8 의 네트워크 경로).
        /// 사망은 조작 차단까지 동반하므로 owner 여부와 무관하게 모든 클라이언트가 적용해야 한다 —
        /// 그래서 <see cref="LateUpdate"/> 의 owner 가드보다 앞에 둔다.
        /// </summary>
        private void SyncCrewOxygen()
        {
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (crew == null) return;
            if (IsServer)
            {
                if (!Mathf.Approximately(suitOxygen.Value, crew.SuitOxygen)) suitOxygen.Value = crew.SuitOxygen;
                if (crewDead.Value != crew.IsDead) crewDead.Value = crew.IsDead;
                if (crewDraining.Value != crew.IsDraining) crewDraining.Value = crew.IsDraining;
                // 소지품 반환은 서버만 할 수 있다. 솔로 경로는 SetGhost 가 자기 손에 든 것을
                // 놓지만, 네트워크에서 소지품의 정본은 이쪽 heldItem 이다 — 안 놓으면 부품이
                // 시신에 잠긴 채 복제되어 남은 1인이 그 부품으로는 아무것도 못 고친다.
                if (crew.IsDead && heldItem != null) heldItem.DropFromServer(this, Vector3.zero);
            }
            else
            {
                crew.ApplyReplicated(suitOxygen.Value, crewDead.Value, crewDraining.Value);
            }

            ApplyGhostPresentation(crew.IsDead);
        }

        /// <summary>
        /// 유령의 반투명 실루엣(기획 §4.4 N11 구현물 4). 사망 여부는 <see cref="crewDead"/> 로
        /// 이미 전원에게 복제되므로 새 NetworkVariable 을 만들지 않는다 — 표현은 복제된
        /// 상태에서 유도하는 것이지 따로 동기화할 값이 아니다.
        ///
        /// <see cref="Renderer.enabled"/> 는 소유권 게이트가 정한 값을 그대로 둔다. 소유자는
        /// 1인칭이라 자기 몸을 안 보고, 남들에게는 이미 보인다 — 유령이 바꾸는 것은 "보이는가"
        /// 가 아니라 "어떻게 보이는가" 다.
        /// </summary>
        private void ApplyGhostPresentation(bool isGhost)
        {
            if (bodyRenderer == null || appliedGhostPresentation == isGhost) return;
            appliedGhostPresentation = isGhost;
            LastShiftGhostVisuals.Apply(bodyRenderer.material, isGhost, PlayerColor);
        }

        /// <summary>
        /// 클라이언트에서는 sandbox 가 <c>enabled = IsServer</c> 로 꺼져 있어 OnGUI 가 돌지 않는다.
        /// 그래서 owner 가 자기 예비 산소 막대와 구역 압력 줄을 직접 그린다. 서버(호스트)에서는
        /// sandbox 가 둘 다 이미 그리므로 여기서는 그리지 않는다 — 그렸다면 호스트만 겹친다.
        ///
        /// 구역 압력(N10)을 클라이언트에도 두는 이유는 격리가 여기서 판단되기 때문이다. 문을
        /// 닫아 놓고 그 결과를 볼 수 없으면 클라이언트에게 격리는 "눌렀지만 뭐가 달라졌는지
        /// 모르는" 조작이 된다. 값 자체는 스냅샷으로 sandbox 안에 이미 최신으로 들어와 있다.
        /// </summary>
        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || IsServer) return;
            if (LastShiftRoomLobby.IsBlockingGameplay) return;
            var crew = GetComponent<LastShiftCrewOxygen>();
            LastShiftCrewOxygen.DrawGauge(crew, playerController != null ? playerController.PlayerSlot.ToString() : "CREW", 0, ref suitGaugeStyle);

            // OnGUI 는 이벤트마다(레이아웃·리페인트) 돌므로 씬 전수 조회를 매번 하면 안 된다.
            // 한 번 찾으면 sandbox 는 씬 수명 동안 바뀌지 않는다.
            if (hudSandbox == null) hudSandbox = FindFirstObjectByType<LastShiftSandboxController>();
            var sandbox = hudSandbox;
            if (sandbox == null) return;
            GUI.Box(new Rect(16f, 16f, LastShiftSandboxController.ZonePressureRowWidth + 24f, 62f), GUIContent.none);
            sandbox.DrawZonePressureCells(28f, 28f);
        }

        private void LateUpdate()
        {
            if (IsSpawned) SyncCrewOxygen();
            // Shutdown 과 scene teardown 사이 프레임에는 object 가 아직 살아 있지만 RPC 전송기는
            // 이미 멈춰 있다. 이 구간에서 매 프레임 RPC 를 호출하면 오류 로그가 폭주하고
            // Editor pipeline 까지 응답 불능이 된다.
            if (!IsSpawned || !IsOwner || NetworkManager == null || !NetworkManager.IsListening) return;
            // 서버 프롬프트·거부 사유가 최신 조준을 반영하도록 pose 와 함께 조준도 계속 올린다.
            // grab 요청 시점의 정확한 값은 RequestGrabRpc 가 별도로 함께 전달한다.
            if (!IsServer) ReportAimRpc(AimOriginForOwner, AimDirectionForOwner);
            if (HoldSocket == null) return;
            RequestHoldPoseRpc(HoldSocket.position, HoldSocket.rotation);
        }

        public void RequestGrab(LastShiftNetworkGrabbable item)
        {
            if (!IsOwner || item == null || HeldItem != null) return;
            // 아직 spawn 되지 않았거나 이미 despawn 된 아이템으로 참조를 만들면
            // NetworkObjectReference 생성 자체가 예외를 던진다. 프리셋 리셋 직후처럼
            // 재spawn 사이 프레임에 E 가 눌리면 실제로 발생한다.
            if (item.NetworkObject == null || !item.NetworkObject.IsSpawned)
            {
                Debug.Log($"[LAST_SHIFT_INTERACTION] client={OwnerClientId} action=grab result=FAIL reason=item-not-spawned");
                return;
            }
            // grab 요청과 같은 프레임의 조준을 함께 보낸다. LateUpdate 의 주기적 pose 갱신만 믿으면
            // 유실이나 한 프레임 지연으로 서버가 stale 조준을 검증해 유효한 grab 을 거부한다.
            RequestGrabRpc(item.NetworkObject, AimOriginForOwner, AimDirectionForOwner);
        }

        private Vector3 AimOriginForOwner => playerController != null ? playerController.AimOrigin : transform.position;
        private Vector3 AimDirectionForOwner => playerController != null ? playerController.AimDirection : transform.forward;

        public void RequestDrop(Vector3 velocity)
        {
            if (!IsOwner || HeldItem == null) return;
            RequestDropRpc(HeldItem.NetworkObject, velocity);
        }

        public void RequestSecureHeldItem()
        {
            if (!IsOwner || HeldItem == null) return;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (networkSandbox != null) networkSandbox.RequestSecureHeldItemRpc();
        }

        /// <summary>
        /// 문 개폐 요청. 어느 문인지는 클라이언트가 지정하지 않는다 — 서버가 요청자 위치로만
        /// 고른다. 클라이언트가 경계 번호를 보내면 배 반대편에서 남의 구역을 격리할 수 있다.
        /// </summary>
        public void RequestDoorToggle()
        {
            if (!IsOwner) return;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (networkSandbox != null) networkSandbox.RequestDoorToggleRpc();
        }

        public void RequestRepair(LastShiftRepairMode mode)
        {
            if (!IsOwner) return;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (networkSandbox != null) networkSandbox.RequestRepairRpc(mode);
        }

        /// <summary>
        /// 냉각실 밸브 유지 상태 전환(<c>C-3</c>, §4.3). 호출자가 <b>상태가 바뀔 때만</b> 부르는
        /// 것이 계약이다 — 누르고 있는 <c>14</c>초 내내 부르면 초당 수십 개의 RPC 가 된다.
        /// </summary>
        public void RequestCoolingValveHold(bool held)
        {
            if (!IsOwner) return;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (networkSandbox != null) networkSandbox.RequestCoolingValveHoldRpc(held);
        }

        /// <summary>
        /// 운석 1회 적용 요청(M). <b>서버 RPC 는 있었는데 부르는 곳이 없었다</b> —
        /// M 은 <see cref="LastShiftSandboxController.Update"/> 에만 배선돼 있고, 그 블록은
        /// 네트워크 샌드박스가 스폰되면 통째로 꺼진다. 씬이 하나가 되면서 에디터에서도
        /// host 가 자동으로 뜨므로, 결과적으로 M 이 아무 데서도 안 먹었다.
        ///
        /// 프리셋·리셋과 같은 계열이라 유령도 쓸 수 있게 둔다 — 조작 동사가 아니라 검증
        /// 도구이고, 막으면 둘 다 죽은 뒤 아무도 사건을 다시 일으킬 수 없다.
        /// </summary>
        public void RequestMeteorImpact()
        {
            if (!IsOwner) return;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (networkSandbox != null) networkSandbox.RequestMeteorImpactRpc();
        }

        public void RequestPresetReset(LastShiftPreset preset)
        {
            if (!IsOwner) return;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (networkSandbox != null) networkSandbox.RequestPresetResetRpc(preset);
        }

        public void RequestCurrentPresetReset()
        {
            if (!IsOwner) return;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (networkSandbox != null) networkSandbox.RequestPresetResetRpc(networkSandbox.Snapshot.Preset);
        }

        [Rpc(SendTo.Server, RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]
        private void RequestHoldPoseRpc(Vector3 position, Quaternion rotation, RpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId) return;
            var maxReach = LastShiftPlayerController.GrabDistance + 0.75f;
            if ((position - transform.position).sqrMagnitude > maxReach * maxReach) return;
            holdPosition.Value = position;
            holdRotation.Value = rotation;
        }

        /// <summary>
        /// 리셋은 반드시 소유 클라이언트에서 실행한다. player 는 owner-authoritative
        /// <see cref="LastShiftOwnerNetworkTransform"/> 이므로 서버가 원격 플레이어의 transform 을
        /// 직접 쓰면 소유자가 계속 송신하는 이전 위치가 그 값을 덮어써 리셋이 되돌아온다.
        /// 또한 순간이동을 Teleport 로 알리지 않으면 NetworkTransform 이 보간해 다른 화면에서
        /// 플레이어가 리셋 지점까지 미끄러져 이동한다.
        /// </summary>
        [Rpc(SendTo.Owner, RequireOwnership = false)]
        public void ResetToSlotRpc(Vector3 position, Quaternion rotation)
        {
            playerController.ResetPlayer(position, rotation);
            if (ownerNetworkTransform == null) ownerNetworkTransform = GetComponent<LastShiftOwnerNetworkTransform>();
            // Teleport 는 authority 가 아니면 예외를 던진다. owner-authoritative 라 여기서는 authority 지만
            // spawn 직후처럼 아직 커밋 권한이 없는 순간에는 건너뛰고 CharacterController 이동만 남긴다.
            if (ownerNetworkTransform != null && ownerNetworkTransform.CanCommitToTransform)
                ownerNetworkTransform.Teleport(position, rotation, transform.localScale);
            if (IsServer && IsOwner)
            {
                holdPosition.Value = HoldSocket.position;
                holdRotation.Value = HoldSocket.rotation;
                aimOrigin.Value = AimOriginForOwner;
                aimDirection.Value = AimDirectionForOwner.normalized;
            }
            Debug.Log($"[LAST_SHIFT_RESET_POSITION] client={OwnerClientId} owner=True position={position} result=applied");
        }

        /// <summary>
        /// 원격 소유 플레이어의 서버 측 조준 캐시를 리셋 자세로 맞춘다. owner 의 다음 보고가
        /// 도착하기 전에 남은 이전 조준으로 grab 이 판정되지 않게 한다.
        /// </summary>
        public void ResetServerAimCache(Vector3 position, Quaternion rotation)
        {
            if (!IsServer || IsOwner) return;
            aimOrigin.Value = position + rotation * CameraLocalOffset;
            aimDirection.Value = (rotation * Vector3.forward).normalized;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestGrabRpc(
            NetworkObjectReference itemReference,
            Vector3 ownerAimOrigin,
            Vector3 ownerAimDirection,
            RpcParams rpcParams = default)
        {
            if (!itemReference.TryGet(out var itemObject)) return;
            ApplyOwnerAim(rpcParams.Receive.SenderClientId, ownerAimOrigin, ownerAimDirection);
            TryGrabFromServer(rpcParams.Receive.SenderClientId, itemObject.GetComponent<LastShiftNetworkGrabbable>());
        }

        /// <summary>
        /// owner 가 보고한 조준을 서버 권위 값으로 채택한다. 원점이 플레이어 캡슐에서 물리적으로
        /// 불가능한 거리면 무시해 조준 위조로 사거리를 늘리지 못하게 한다.
        /// </summary>
        private void ApplyOwnerAim(ulong senderClientId, Vector3 ownerAimOrigin, Vector3 ownerAimDirection)
        {
            if (!IsServer || senderClientId != OwnerClientId) return;
            if (ownerAimDirection.sqrMagnitude < 0.0001f) return;
            var maxOriginOffset = LastShiftPlayerController.GrabDistance;
            if ((ownerAimOrigin - transform.position).sqrMagnitude > maxOriginOffset * maxOriginOffset) return;
            aimOrigin.Value = ownerAimOrigin;
            aimDirection.Value = ownerAimDirection.normalized;
        }

        [Rpc(SendTo.Server, RequireOwnership = true, Delivery = RpcDelivery.Unreliable)]
        private void ReportAimRpc(Vector3 ownerAimOrigin, Vector3 ownerAimDirection, RpcParams rpcParams = default)
        {
            ApplyOwnerAim(rpcParams.Receive.SenderClientId, ownerAimOrigin, ownerAimDirection);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestDropRpc(NetworkObjectReference itemReference, Vector3 velocity, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsServer || sender != OwnerClientId || heldItem == null || !itemReference.TryGet(out var itemObject)) return;
            var item = itemObject.GetComponent<LastShiftNetworkGrabbable>();
            if (item == null || item != heldItem) return;
            item.DropFromServer(this, velocity);
        }

        public bool TryGrabFromServer(ulong senderClientId, LastShiftNetworkGrabbable item)
        {
            var reason = ValidateGrab(senderClientId, item);
            if (reason != null)
            {
                Debug.Log($"[LAST_SHIFT_INTERACTION] client={senderClientId} action=grab role={item?.Grabbable?.Role.ToString() ?? "none"} result=FAIL reason={reason}");
                ReportRejectionToOwner(reason);
                return false;
            }

            var grabbed = item.TryBeginHold(this);
            Debug.Log($"[LAST_SHIFT_INTERACTION] client={senderClientId} action=grab role={item.Grabbable.Role} result={(grabbed ? "PASS" : "FAIL")} reason={(grabbed ? "accepted" : "claim-race")}");
            if (!grabbed) ReportRejectionToOwner("claim-race");
            return grabbed;
        }

        private void ReportRejectionToOwner(string reason)
        {
            if (!IsServer || string.IsNullOrEmpty(reason)) return;
            if (IsOwner)
            {
                playerController?.ReportServerRejection(reason);
                return;
            }
            NotifyGrabRejectedRpc(reason);
        }

        [Rpc(SendTo.Owner, RequireOwnership = false)]
        private void NotifyGrabRejectedRpc(string reason)
        {
            playerController?.ReportServerRejection(reason);
        }

        public void SetHeldItemFromServer(LastShiftNetworkGrabbable item)
        {
            if (!IsServer) return;
            heldItem = item;
            heldItemReference.Value = item != null ? item.NetworkObject : default;
        }

        public void SetHeldItemFromReplication(LastShiftNetworkGrabbable item)
        {
            if (!IsServer) heldItem = item;
        }

        public void ClearHeldItemFromReplication(LastShiftNetworkGrabbable expectedItem)
        {
            if (!IsServer && heldItem == expectedItem) heldItem = null;
        }

        private void OnHeldItemReferenceChanged(NetworkObjectReference previous, NetworkObjectReference current)
        {
            if (!IsServer) ResolveHeldItem(current);
        }

        private void ResolveHeldItem(NetworkObjectReference reference)
        {
            heldItem = reference.TryGet(out var itemObject)
                ? itemObject.GetComponent<LastShiftNetworkGrabbable>()
                : null;
        }

        private string ValidateGrab(ulong senderClientId, LastShiftNetworkGrabbable item)
        {
            if (!IsServer) return "not-server";
            if (senderClientId != OwnerClientId) return "sender-not-owner";
            // 유령은 물건을 만질 수 없다(기획 §4.4). 클라이언트도 요청 자체를 막지만, 잡기의
            // 권위는 이 함수 하나이므로 조건도 여기 있어야 한다.
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead) return "crew-dead";
            if (heldItem != null) return "already-holding-item";
            if (item == null || item.Grabbable == null) return "invalid-item";
            if (item.IsClaimed) return "item-already-claimed";
            if (item.IsSecured || item.Grabbable.Secured) return "item-secured";
            if (!LastShiftPlayerController.TryResolveGrabTarget(
                    AuthoritativeAimOrigin,
                    AuthoritativeAimDirection,
                    out var aimedItem,
                    out _))
                return "no-target-in-range";
            return aimedItem == item ? null : "aim-target-mismatch";
        }

        private void ApplyLocalPresentation(bool isLocalPlayer)
        {
            if (playerController != null) playerController.enabled = isLocalPlayer;
            if (playerCamera != null) playerCamera.enabled = isLocalPlayer;
            // 귀도 카메라와 같은 소유권 게이트를 탄다. 승무원 넷의 리스너가 다 살아 있으면
            // Unity 는 그중 하나만 쓰고 어느 것을 쓸지는 스폰 순서가 정한다 — 3D 감쇠가
            // 남의 자리 기준으로 계산되는데 경고 한 줄 말고는 표가 안 난다.
            LastShiftZoneAudio.EnsureListener(playerCamera, isLocalPlayer);
            if (bodyRenderer == null) return;
            foreach (var renderer in bodyRenderer.transform.root.Find("Remote Body")
                         .GetComponentsInChildren<Renderer>(true))
                renderer.enabled = !isLocalPlayer;
            bodyRenderer.material.color = PlayerColor;
        }

        private static Color ColorForClient(ulong clientId)
        {
            return PlayerColors[(int)(clientId % (ulong)PlayerColors.Length)];
        }
    }
}
