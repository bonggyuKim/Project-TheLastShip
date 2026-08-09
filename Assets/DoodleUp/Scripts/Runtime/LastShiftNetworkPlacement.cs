using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 자유 배치 사슬의 <b>네트워크 정본</b> — 모듈 표 · 여력 원장 · 항해 진행 · 커서 주인이
    /// 전부 여기를 지나 모든 클라이언트에 같은 값으로 선다.
    ///
    /// <b>권위는 서버 하나다.</b> 클라이언트가 보내는 것은 요청뿐이고
    /// (<c>카탈로그 번호 · 회전 · 최소 모서리 · 부모</c>), 판정·여력·표 갱신은 전부 서버에서
    /// <see cref="LastShiftPlacementCommands"/> 가 한다 — 화면이 혼자 돌 때 부르는 함수와
    /// <b>같은 함수</b>다. 두 벌로 두면 여력을 무는 순서가 한쪽에서만 지켜지고, 그 어긋남은
    /// 잔액이 서서히 갈리는 형태로만 드러난다.
    ///
    /// <b>씬은 안 실어 나른다.</b> 건너가는 것은 표(<see cref="LastShiftPlacementRecord"/> 목록)
    /// 뿐이고, 방과 문틀은 각자가 그 표에서 다시 세운다
    /// (<see cref="LastShiftModuleAssembler.Rebuild"/> → <see cref="LastShiftBakedDoorways"/>).
    /// 조립이 표의 순수 함수라 그래도 되고, 그래서 <b>벽뚫기 상태에 별도 동기화 필드가 없다</b> —
    /// 문틀은 상태가 아니라 표에서 나오는 값이다. 별도로 실으면 표와 구멍이 갈리는 자리가 생기고,
    /// 그건 한쪽에서만 못 지나가는 벽으로 나타난다.
    ///
    /// <b><see cref="LastShiftNetworkSandbox"/> 와 형제다.</b> 그쪽이 판 안(시뮬레이션 상태)을
    /// 나르고 이쪽이 판 밖(기항 배치)을 나른다. 갈라 둔 이유는 주기다 — 시뮬 스냅샷은 0.25초마다
    /// 통째로 날아가지만 배치는 기항에서 몇 번뿐이라, 한 칸에 묶으면 안 바뀌는 표가 매 tick
    /// 함께 실린다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LastShiftNetworkPlacement : NetworkBehaviour
    {
        /// <summary>
        /// 서버가 확정한 모듈 표. <see cref="NetworkList{T}"/> 라 늦게 들어온 클라이언트도
        /// 접속하는 순간 지금까지 놓인 것을 전부 받는다 — 항해 도중 합류가 실제 경로다.
        /// </summary>
        private readonly NetworkList<LastShiftPlacementRecord> modules = new();

        private readonly NetworkVariable<LastShiftPlacementLedger> ledger = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<LastShiftPlacementRecord> captured = new();

        /// <summary>
        /// 지금 살아 있는 배치 세션. 화면(<see cref="LastShiftPlacementUi"/>)이 "네트워크가
        /// 붙었는가" 를 묻는 자리이고, 비어 있으면 화면은 예전과 한 글자도 다르지 않게
        /// 혼자 돈다 — EditMode 배치 테스트가 세션 없이 도는 근거가 그것이다.
        /// </summary>
        public static LastShiftNetworkPlacement Active { get; private set; }

        /// <summary>표를 마지막으로 실은 시점의 <see cref="LastShiftCompartments.Revision"/>.</summary>
        private int publishedRevision = -1;

        /// <summary>클라이언트가 받은 표를 아직 안 세웠는가. 한 프레임에 한 번만 세운다.</summary>
        private bool applyPending;

        public LastShiftPlacementLedger Ledger => ledger.Value;

        /// <summary>지금 복제된 모듈 수. 테스트와 진단이 읽는다.</summary>
        public int ReplicatedModuleCount => modules.Count;

        public LastShiftPlacementRecord ReplicatedModuleAt(int index) => modules[index];

        /// <summary>
        /// 씬에 이 컴포넌트가 없으면 런타임에 붙인다. <b>임시 다리이고, 그렇게 적어 두는 것이
        /// 요지다</b> — <see cref="LastShiftPlacementUi"/> 의 같은 훅과 같은 성격이고
        /// 같은 이유다: 정본 자리는 <c>LastShiftNetworkSceneBuilder</c> 이고 거기 붙여 뒀지만,
        /// 씬을 다시 세우면 배 프리팹이 통째로 재직렬화돼 <c>fileID</c> 가 전부 갈린다.
        /// 컴포넌트 하나를 넣으려고 물 값이 아니다.
        ///
        /// <b>스폰 전에 붙어야 한다.</b> <see cref="NetworkObject"/> 는 자식
        /// <see cref="NetworkBehaviour"/> 목록을 스폰 시점에 굳히므로, 그 뒤에 붙으면 이
        /// 컴포넌트의 변수도 RPC 도 배선되지 않는다. <c>AfterSceneLoad</c> 는 씬의
        /// <c>Awake</c> 뒤 <c>Start</c> 앞이고 세션은 <see cref="LastShiftNetworkSession.Start"/>
        /// 에서 뜨므로, 이 훅은 언제나 스폰보다 앞선다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenMissing()
        {
            if (FindAnyObjectByType<LastShiftNetworkPlacement>() != null) return;

            var sandbox = FindAnyObjectByType<LastShiftNetworkSandbox>();
            if (sandbox == null) return;

            sandbox.gameObject.AddComponent<LastShiftNetworkPlacement>();
        }

        public override void OnNetworkSpawn()
        {
            Active = this;
            modules.OnListChanged += OnModulesChanged;
            ledger.OnValueChanged += OnLedgerChanged;

            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback -= ReleaseCursorOfDepartedClient;
                NetworkManager.OnClientDisconnectCallback += ReleaseCursorOfDepartedClient;
                PublishWhenChanged(true);
                return;
            }

            // 늦게 들어온 클라이언트는 스폰 시점에 이미 표를 다 받은 상태다. 변경 콜백은
            // 그 뒤의 변화만 오므로, 여기서 한 번 세우지 않으면 합류 직후 화면에만 배가 빈다.
            applyPending = true;
        }

        public override void OnNetworkDespawn()
        {
            modules.OnListChanged -= OnModulesChanged;
            ledger.OnValueChanged -= OnLedgerChanged;
            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= ReleaseCursorOfDepartedClient;
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (IsServer)
            {
                PublishWhenChanged(false);
                return;
            }

            if (!applyPending) return;
            applyPending = false;
            ApplyReplicatedState();
        }

        // ── 서버: 요청 수신 ─────────────────────────────────────────────────

        /// <summary>
        /// 커서를 잡는다. <b>먼저 잡은 쪽이 갖는다</b> — 뺏기 규칙을 안 두는 것이 권위 클래스의
        /// 결정이고(<see cref="LastShiftPlacementAuthority.TryClaim"/>), 여기는 그 결정을
        /// 네트워크로 옮기기만 한다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestClaimCursorRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender)) return;

            var claimed = LastShiftPlacementAuthority.TryClaim((int)sender);
            Debug.Log($"[LAST_SHIFT_PLACEMENT_CURSOR] client={sender} action=claim " +
                      $"result={(claimed ? "PASS" : "REJECT")} holder={LastShiftPlacementAuthority.HolderId}");
            PublishWhenChanged(true);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestReleaseCursorRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender)) return;

            LastShiftPlacementAuthority.Release((int)sender);
            PublishWhenChanged(true);
        }

        /// <summary>
        /// 배치 확정 요청. <b>치수가 안 실린다</b> — 서버가 카탈로그에서 읽는다
        /// (<see cref="LastShiftPlacementCommands.CursorFor"/>). 치수를 실으면 목록에 없는
        /// 크기의 방을 요청할 수 있고, 판정기는 겹침·사슬만 보므로 그 방은 통과한다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestPlaceModuleRpc(
            int catalogIndex, int quarterTurns, float anchorX, float anchorZ,
            int parentIndex, bool autoParent, RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender)) return;

            var outcome = ServerPlace(
                sender, catalogIndex, quarterTurns, anchorX, anchorZ, parentIndex, autoParent);
            ReportOutcome(sender, outcome);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestRemoveLastModuleRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (!IsConnectedSender(sender)) return;

            ReportOutcome(sender, ServerRemoveLast(sender));
        }

        /// <summary>
        /// 배치 확정의 서버 몸통. <b>호스트도 이 문으로 들어온다</b> — 호스트만 다른 경로를
        /// 타면 커서 소유 검사가 호스트에서만 빠지고, 그 차이는 2인 이상에서만 드러난다.
        /// </summary>
        public LastShiftPlacementOutcome ServerPlace(
            ulong clientId, int catalogIndex, int quarterTurns, float anchorX, float anchorZ,
            int parentIndex, bool autoParent)
        {
            if (!IsServer || !LastShiftPlacementAuthority.IsHeldBy((int)clientId))
                return LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.NotCursorHolder);

            var cursor = LastShiftPlacementCommands.CursorFor(
                catalogIndex, quarterTurns, anchorX, anchorZ, parentIndex, autoParent);
            LastShiftPlacementCommands.TryPlace(cursor, out var outcome);

            Debug.Log($"[LAST_SHIFT_PLACEMENT_REQUEST] client={clientId} action=place " +
                      $"catalog={catalogIndex} turns={quarterTurns} anchor=({anchorX:0.#},{anchorZ:0.#}) " +
                      $"result={outcome.Result} index={outcome.Index} balance={LastShiftMaintenance.Balance}");

            if (outcome.Accepted) PublishWhenChanged(true);
            return outcome;
        }

        /// <summary>철거의 서버 몸통. 커서를 잡은 사람만 뺀다 — 확정과 같은 문이다.</summary>
        public LastShiftPlacementOutcome ServerRemoveLast(ulong clientId)
        {
            if (!IsServer || !LastShiftPlacementAuthority.IsHeldBy((int)clientId))
                return LastShiftPlacementOutcome.Rejected(LastShiftPlacementCommandResult.NotCursorHolder);

            LastShiftPlacementCommands.TryRemoveLast(out var outcome);

            Debug.Log($"[LAST_SHIFT_PLACEMENT_REQUEST] client={clientId} action=remove " +
                      $"result={outcome.Result} refund={outcome.Refunded} balance={LastShiftMaintenance.Balance}");

            if (outcome.Accepted) PublishWhenChanged(true);
            return outcome;
        }

        /// <summary>
        /// 커서를 든 채로 나간 자리를 푼다. <b>호스트 강제 해제</b>이고, 이것이 없으면 아무도
        /// 배치를 못 하는 기항에 갇힌다(<see cref="LastShiftPlacementAuthority.Revoke"/>).
        /// </summary>
        public void ReleaseCursorOfDepartedClient(ulong clientId)
        {
            if (!IsServer || !LastShiftPlacementAuthority.IsHeldBy((int)clientId)) return;

            LastShiftPlacementAuthority.Revoke();
            Debug.Log($"[LAST_SHIFT_PLACEMENT_CURSOR] client={clientId} action=revoke reason=disconnected");
            PublishWhenChanged(true);
        }

        // ── 서버: 싣기 ──────────────────────────────────────────────────────

        /// <summary>
        /// 바뀐 것만 싣는다. 표는 <see cref="LastShiftCompartments.Revision"/> 이 오를 때,
        /// 숫자는 값이 달라졌을 때다.
        ///
        /// <b>매 프레임 물어도 싸다</b> — 표는 정수 비교 하나, 숫자는 구조체 비교 하나다.
        /// 대신 배치·항해·기항 어느 경로로 상태가 바뀌든 여기 한 곳에서 잡히므로, 바뀌는
        /// 자리마다 "실어라" 를 심을 필요가 없다. 그 심는 일을 한 곳에서 빠뜨리면 그 경로의
        /// 변화만 클라이언트에 영영 안 간다.
        /// </summary>
        public void PublishWhenChanged(bool force)
        {
            if (!IsServer) return;

            if (force || publishedRevision != LastShiftCompartments.Revision)
            {
                publishedRevision = LastShiftCompartments.Revision;
                LastShiftPlacementReplication.Capture(captured);
                if (!SameAsReplicated(captured))
                {
                    modules.Clear();
                    for (var index = 0; index < captured.Count; index++) modules.Add(captured[index]);
                }
            }

            var current = LastShiftPlacementReplication.CaptureLedger();
            if (!current.Equals(ledger.Value)) ledger.Value = current;
        }

        /// <summary>
        /// 이미 같은 표가 실려 있는가. <b>같으면 안 건드린다</b> — <see cref="NetworkList{T}"/> 는
        /// 지우고 다시 넣은 것을 지운 만큼 · 넣은 만큼 전부 델타로 보내므로, 값이 안 바뀐
        /// 재발행 한 번이 클라이언트에서 씬을 통째로 다시 세운다.
        /// </summary>
        private bool SameAsReplicated(List<LastShiftPlacementRecord> records)
        {
            if (records.Count != modules.Count) return false;
            for (var index = 0; index < records.Count; index++)
                if (!records[index].Equals(modules[index]))
                    return false;
            return true;
        }

        // ── 클라이언트: 받기 ────────────────────────────────────────────────

        private void OnModulesChanged(NetworkListEvent<LastShiftPlacementRecord> change)
        {
            // 표 한 벌을 다시 싣는 것은 지움 + 넣음 여러 건으로 도착한다. 건마다 씬을 세우면
            // 방 전체를 지웠다 세우는 값을 그 횟수만큼 문다 — 프레임 끝에 한 번만 세운다.
            if (!IsServer) applyPending = true;
        }

        private void OnLedgerChanged(LastShiftPlacementLedger previous, LastShiftPlacementLedger current)
        {
            if (IsServer) return;

            // 숫자는 표와 달리 씬을 안 건드리므로 곧바로 앉힌다. 화면이 같은 프레임에 잔액을
            // 읽어도 서버와 같은 값을 본다.
            LastShiftPlacementReplication.ApplyLedger(current);
        }

        /// <summary>
        /// 받은 표로 배를 다시 세운다. <b>클라이언트 전용이다</b> — 서버에서 부르면 자기가 방금
        /// 확정한 표를 자기 복제본으로 덮어쓰게 되고, 그 왕복 중 하나라도 어긋나면 확정이
        /// 조용히 취소된 것처럼 보인다.
        /// </summary>
        private void ApplyReplicatedState()
        {
            if (IsServer) return;

            var records = new List<LastShiftPlacementRecord>(modules.Count);
            for (var index = 0; index < modules.Count; index++) records.Add(modules[index]);

            var complete = LastShiftPlacementReplication.Apply(records);
            LastShiftPlacementReplication.ApplyLedger(ledger.Value);

            var ui = FindAnyObjectByType<LastShiftPlacementUi>();
            var report = ui != null ? ui.RebuildShip() : default;

            Debug.Log($"[LAST_SHIFT_PLACEMENT_SYNC] client={NetworkManager.LocalClientId} " +
                      $"modules={LastShiftCompartments.ModuleCount}/{records.Count} " +
                      $"balance={LastShiftMaintenance.Balance} port={LastShiftMaintenance.PortIndex} " +
                      $"doors={report.Cut}/{report.Doorways} result={(complete ? "PASS" : "FAIL")}");
        }

        // ── 결과 회신 ───────────────────────────────────────────────────────

        private void ReportOutcome(ulong clientId, in LastShiftPlacementOutcome outcome)
        {
            PlacementResultRpc(
                (byte)outcome.Result, (int)outcome.Rejection, (int)outcome.Faults,
                outcome.Index, outcome.Refunded,
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// 요청한 사람에게만 결과를 돌려준다. <b>문장이 아니라 사유 플래그를 보낸다</b> —
        /// 받는 쪽이 <see cref="LastShiftPlacementCommands.Reason"/> 로 같은 문구를 만든다.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void PlacementResultRpc(
            byte result, int rejection, int faults, int index, int refunded, RpcParams rpcParams = default)
        {
            var outcome = new LastShiftPlacementOutcome(
                (LastShiftPlacementCommandResult)result, index, 0, refunded,
                (LastShiftPlacementRejection)rejection, (LastShiftPlacementFault)faults);

            var ui = FindAnyObjectByType<LastShiftPlacementUi>();
            if (ui != null) ui.ReportNetworkOutcome(outcome);
        }

        private bool IsConnectedSender(ulong sender) =>
            IsServer && NetworkManager != null && NetworkManager.ConnectedClients.ContainsKey(sender);
    }
}
