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

        public LastShiftNetworkGrabbable HeldItem => heldItem;
        public Renderer BodyRenderer => bodyRenderer;
        public bool IsBodyVisible => bodyRenderer != null && bodyRenderer.enabled;
        public Color PlayerColor => ColorForClient(OwnerClientId);
        public Transform HoldSocket => playerController != null ? playerController.HoldSocket : null;
        public Vector3 HoldPosition => IsOwner && HoldSocket != null ? HoldSocket.position : holdPosition.Value;
        public Quaternion HoldRotation => IsOwner && HoldSocket != null ? HoldSocket.rotation : holdRotation.Value;
        public Vector3 AuthoritativeAimOrigin => IsOwner && playerController != null
            ? playerController.AimOrigin
            : HoldPosition - HoldRotation * (HoldSocket != null ? HoldSocket.localPosition : Vector3.zero);
        public Vector3 AuthoritativeAimDirection => IsOwner && playerController != null
            ? playerController.AimDirection
            : HoldRotation * Vector3.forward;

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
            if (!IsOwner || HoldSocket == null) return;
            RequestHoldPoseRpc(HoldSocket.position, HoldSocket.rotation);
        }

        public void RequestGrab(LastShiftNetworkGrabbable item)
        {
            if (!IsOwner || item == null || HeldItem != null) return;
            RequestGrabRpc(item.NetworkObject);
        }

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

        [Rpc(SendTo.Owner, RequireOwnership = false)]
        public void ResetToSlotRpc(Vector3 position, Quaternion rotation)
        {
            playerController.ResetPlayer(position, rotation);
            if (IsServer)
            {
                transform.SetPositionAndRotation(position, rotation);
                holdPosition.Value = HoldSocket.position;
                holdRotation.Value = HoldSocket.rotation;
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestGrabRpc(NetworkObjectReference itemReference, RpcParams rpcParams = default)
        {
            if (!itemReference.TryGet(out var itemObject)) return;
            TryGrabFromServer(rpcParams.Receive.SenderClientId, itemObject.GetComponent<LastShiftNetworkGrabbable>());
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
