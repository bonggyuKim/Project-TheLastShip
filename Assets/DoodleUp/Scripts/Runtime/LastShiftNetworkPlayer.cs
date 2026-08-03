using Unity.Netcode;
using UnityEngine;

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

        /// <summary>씬 빌더가 배치한 플레이어 카메라의 로컬 오프셋. 조준 원점 기본값 계산에만 쓴다.</summary>
        private static readonly Vector3 CameraLocalOffset = new(0f, 1.55f, 0f);

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
                if (body != null) bodyRenderer = body.GetComponent<Renderer>();
            }
            heldItemReference.OnValueChanged += OnHeldItemReferenceChanged;
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

        private void LateUpdate()
        {
            if (!IsOwner) return;
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
            if (bodyRenderer == null) return;
            bodyRenderer.enabled = !isLocalPlayer;
            bodyRenderer.material.color = PlayerColor;
        }

        private static Color ColorForClient(ulong clientId)
        {
            return PlayerColors[(int)(clientId % (ulong)PlayerColors.Length)];
        }
    }
}
