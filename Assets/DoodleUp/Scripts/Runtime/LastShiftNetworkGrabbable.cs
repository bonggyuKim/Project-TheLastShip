using Unity.Netcode;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [RequireComponent(typeof(NetworkObject), typeof(LastShiftGrabbable))]
    public sealed class LastShiftNetworkGrabbable : NetworkBehaviour
    {
        public const ulong NoHolder = ulong.MaxValue;

        private readonly NetworkVariable<ulong> holderClientId = new(
            NoHolder,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> secured = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> securedByCrew = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [SerializeField] private LastShiftGrabbable grabbable;
        private LastShiftNetworkPlayer holder;
        private float nextHolderResolveTime;
        private float enforceRecoveryPoseUntil;
        private LastShiftOwnerNetworkTransform ownerNetworkTransform;

        public ulong HolderClientId => holderClientId.Value;
        public bool IsClaimed => holderClientId.Value != NoHolder;
        public bool IsSecured => secured.Value;
        public bool IsSecuredByCrew => secured.Value && securedByCrew.Value;
        public bool HasResolvedHolder => holder != null;
        /// <summary>
        /// spawn 전에도 유효해야 한다. 이전에는 OnNetworkSpawn 에서만 채워서, 아직 spawn 되지 않은
        /// 아이템을 조준하면 프롬프트 생성(BuildInteractionPrompt)이 null 을 참조해 OnGUI 에서
        /// 매 프레임 NullReferenceException 을 던졌다. RequireComponent 로 컴포넌트는 항상 있으므로
        /// 여기서 지연 해석한다.
        /// </summary>
        public LastShiftGrabbable Grabbable => grabbable != null
            ? grabbable
            : grabbable = GetComponent<LastShiftGrabbable>();

        public override void OnNetworkSpawn()
        {
            if (grabbable == null) grabbable = GetComponent<LastShiftGrabbable>();
            holderClientId.OnValueChanged += OnHolderChanged;
            secured.OnValueChanged += OnSecuredChanged;
            securedByCrew.OnValueChanged += OnSecuredByCrewChanged;
            if (IsServer)
            {
                secured.Value = grabbable.Secured;
                securedByCrew.Value = grabbable.SecuredByCrew;
            }
            else
            {
                OnSecuredChanged(grabbable.Secured, secured.Value);
                if (holderClientId.Value != NoHolder) grabbable.BeginReplicatedHold();
                TryResolveHolder(holderClientId.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            holderClientId.OnValueChanged -= OnHolderChanged;
            secured.OnValueChanged -= OnSecuredChanged;
            securedByCrew.OnValueChanged -= OnSecuredByCrewChanged;
            ClearResolvedHolder();
        }

        private void LateUpdate()
        {
            if (!IsSpawned || holderClientId.Value == NoHolder) return;
            if (holder == null)
            {
                if (Time.unscaledTime < nextHolderResolveTime) return;
                nextHolderResolveTime = Time.unscaledTime + 0.25f;
                TryResolveHolder(holderClientId.Value);
                return;
            }

            transform.SetPositionAndRotation(holder.HoldPosition, holder.HoldRotation);
        }

        public bool TryBeginHold(LastShiftNetworkPlayer player)
        {
            if (!IsServer || IsClaimed || player == null || !player.IsSpawned || player.HeldItem != null) return false;
            holder = player;
            holderClientId.Value = player.OwnerClientId;
            secured.Value = false;
            securedByCrew.Value = false;
            grabbable.BeginNetworkHold(player.HoldSocket);
            NetworkObject.ChangeOwnership(player.OwnerClientId);
            player.SetHeldItemFromServer(this);
            return true;
        }

        public bool DropFromServer(LastShiftNetworkPlayer player, Vector3 velocity)
        {
            if (!IsServer || player == null || holderClientId.Value != player.OwnerClientId || player.HeldItem != this) return false;
            ReleaseFromServer(velocity);
            return true;
        }

        public bool SecureFromServer(LastShiftNetworkPlayer player)
        {
            if (!IsServer || player == null || holderClientId.Value != player.OwnerClientId || player.HeldItem != this) return false;
            if (Vector3.Distance(transform.position, grabbable.NominalPosition) > LastShiftSandboxController.SecureDistance) return false;
            player.SetHeldItemFromServer(null);
            holder = null;
            holderClientId.Value = NoHolder;
            NetworkObject.RemoveOwnership();
            grabbable.EndNetworkHold(Vector3.zero);
            grabbable.SetSecured(true, true);
            secured.Value = true;
            securedByCrew.Value = true;
            return true;
        }

        public void ReleaseForDisconnectOrReset()
        {
            if (!IsServer) return;
            ReleaseFromServer(Vector3.zero);
        }

        /// <summary>
        /// 서버 권위 월드 경계 복구. held 상태라도 player reference → holder id → ownership →
        /// grabbable physics 순으로 정리한 뒤 nominal 위치로 돌린다. 중간 상태가 client 에
        /// 복제되어도 stale holder가 다시 item을 잡은 것으로 해석되지 않게 한다.
        /// </summary>
        public bool RecoverFromServer(string reason)
        {
            if (!IsServer || !IsSpawned || string.IsNullOrWhiteSpace(reason)) return false;

            var previousPosition = transform.position;
            var previousHolder = holderClientId.Value;
            var wasSecured = secured.Value || grabbable.Secured;
            var wasSecuredByCrew = securedByCrew.Value || grabbable.SecuredByCrew;
            if (holder != null) holder.SetHeldItemFromServer(null);
            holder = null;
            holderClientId.Value = NoHolder;
            NetworkObject.RemoveOwnership();
            grabbable.RecoverToNominal(wasSecured, wasSecuredByCrew);
            secured.Value = wasSecured;
            securedByCrew.Value = wasSecuredByCrew;

            enforceRecoveryPoseUntil = Time.unscaledTime + 0.75f;
            CommitRecoveryPose();

            NotifyRecoveryRpc(grabbable.Role, previousPosition, grabbable.NominalPosition, previousHolder, reason);
            return true;
        }

        /// <summary>
        /// preset reset 에서도 network pose 를 명시적으로 리셋한다. 단순 ResetItem 은 로컬
        /// Transform/Rigidbody 만 바꾸므로 owner-authoritative NetworkTransform 의 stale pose 가
        /// 뒤늦게 nominal 을 덮어쓸 수 있다.
        /// </summary>
        public void SyncResetPoseFromServer()
        {
            if (!IsServer || !IsSpawned) return;
            enforceRecoveryPoseUntil = Time.unscaledTime + 0.75f;
            CommitRecoveryPose();
        }

        /// <summary>
        /// LastShiftNetworkSandbox 의 0.25초 tick 에서만 호출해 network spam 없이 stale owner
        /// pose 창을 덮는다. 복구 로그는 최초 RecoverFromServer 에서 한 번만 남는다.
        /// </summary>
        public void EnforcePendingRecoveryPose()
        {
            if (!IsServer || !IsSpawned || holderClientId.Value != NoHolder ||
                Time.unscaledTime >= enforceRecoveryPoseUntil)
                return;
            grabbable.RecoverToNominal(secured.Value, securedByCrew.Value);
            CommitRecoveryPose();
        }

        private void CommitRecoveryPose()
        {
            if (ownerNetworkTransform == null) ownerNetworkTransform = GetComponent<LastShiftOwnerNetworkTransform>();
            if (ownerNetworkTransform == null) return;
            ownerNetworkTransform.SetState(
                grabbable.NominalPosition,
                transform.rotation,
                transform.localScale,
                teleportDisabled: false);
        }

        [Rpc(SendTo.Everyone, RequireOwnership = false)]
        private void NotifyRecoveryRpc(
            LastShiftItemRole role,
            Vector3 previousPosition,
            Vector3 recoveryPosition,
            ulong previousHolder,
            string reason)
        {
            // 상태 NetworkVariable 과 RPC 는 서로 다른 전송 스트림이라 이 메시지가 먼저 도착할 수 있다.
            // 복제 전 현재값(IsClaimed/IsSecured)을 출력하면 성공 이벤트에 stale claimed=true 가 보여
            // 오진을 만든다. 서버가 원자적으로 확정한 결과를 명시한다.
            Debug.Log($"[LAST_SHIFT_ITEM_RECOVERY] observer={NetworkManager.LocalClientId} role={role} reason={reason} previous={previousPosition:F2} recovery={recoveryPosition:F2} previousHolder={(previousHolder == NoHolder ? "none" : previousHolder.ToString())} resultHolder=none resultOwner=server resultVelocity=zero result=PASS");
        }

        public void SyncSecuredFromServer()
        {
            if (!IsServer) return;
            secured.Value = grabbable.Secured;
            securedByCrew.Value = grabbable.SecuredByCrew;
        }

        private void ReleaseFromServer(Vector3 velocity)
        {
            if (holder != null) holder.SetHeldItemFromServer(null);
            holder = null;
            holderClientId.Value = NoHolder;
            NetworkObject.RemoveOwnership();
            grabbable.EndNetworkHold(velocity);
            secured.Value = false;
            securedByCrew.Value = false;
        }

        private void OnHolderChanged(ulong previous, ulong current)
        {
            ClearResolvedHolder();
            if (current == NoHolder)
            {
                if (!IsServer && grabbable.IsHeld) grabbable.EndNetworkHold(Vector3.zero);
                return;
            }

            if (!IsServer) grabbable.BeginReplicatedHold();
            TryResolveHolder(current);
        }

        public bool TryResolveHolder(ulong clientId)
        {
            if (clientId == NoHolder || NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return false;
            var resolvedHolder = client.PlayerObject != null ? client.PlayerObject.GetComponent<LastShiftNetworkPlayer>() : null;
            if (resolvedHolder == null) return false;
            if (holder != resolvedHolder) ClearResolvedHolder();
            holder = resolvedHolder;
            if (!IsServer)
            {
                grabbable.BeginReplicatedHold();
                holder.SetHeldItemFromReplication(this);
            }
            return true;
        }

        private void ClearResolvedHolder()
        {
            var previousHolder = holder;
            holder = null;
            if (!IsServer && previousHolder != null) previousHolder.ClearHeldItemFromReplication(this);
        }

        private void OnSecuredChanged(bool previous, bool current)
        {
            if (!IsServer) grabbable.ApplyReplicatedSecured(current, securedByCrew.Value);
        }

        private void OnSecuredByCrewChanged(bool previous, bool current)
        {
            if (!IsServer) grabbable.ApplyReplicatedSecured(secured.Value, current);
        }
    }
}
