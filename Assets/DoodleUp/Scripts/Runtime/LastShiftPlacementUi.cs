using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 도면 화면의 탭. <b>배치 대상이 둘이라는 사실 자체다</b> —
    /// <c>docs/outboard-outpost-and-map-final-v1.md</c> §4.4 가 거점을 "카탈로그의 일부가 아니라
    /// 배치 시스템의 두 번째 대상" 으로 정한 결론이 이 enum 이다. 거점을 선체 카탈로그에 항목
    /// 하나로 넣었으면 통화가 둘인 목록이 생기고, 조항 <c>O-2</c> 는 그 목록에서 먼저 깨진다.
    /// </summary>
    public enum LastShiftPlacementTab
    {
        /// <summary>선체 — 정비 여력으로 배에 방을 붙인다.</summary>
        Hull = 0,

        /// <summary>거점 — 자재로 선외 골조를 세운다.</summary>
        Outpost = 1
    }

    /// <summary>
    /// 기항 <b>선체 도면</b> 화면. 배 전체를 위에서 내려다보며 배치한다 —
    /// <c>docs/core-four-rooms-and-hull-schematic-v1.md</c> §4 가 정본이다.
    ///
    /// <b>미니맵이 아니라 도면이다</b>(§0-4). 판 안 HUD 미니맵과 다른 물건이고, 둘을 같은
    /// 단어로 부르면 "판 안에서도 도면을 보는가" 가 조용히 열린다.
    ///
    /// <b><c>1</c>인칭 커서를 버린 이유는 조작 편의가 아니라 정보다</b>(§4.1). 이 시스템의 유일한
    /// 결정이 "어느 면에 붙일까" 인데 <c>1</c>인칭으로 서 있으면 배 반대편 벽이 비어 있는지를
    /// 볼 수 없다. 그래서 이 화면이 새로 보여주는 것 중 가장 중요한 것은 배 그림이 아니라
    /// <b>자유면 하이라이트</b>(<see cref="LastShiftFreeFaces"/>)다 — 그것만 빼면 개편이 반만 온다.
    ///
    /// <b>규칙을 하나도 안 갖는다.</b> 여기 있는 것은 입력을 커서 함수로 옮기는 일과, 커서가
    /// 이미 계산해 둔 값을 그리는 일뿐이다. 판정을 여기서 한 줄이라도 다시 하면 화면이 통과라고
    /// 적은 배치가 표에서 물리는 자리가 생긴다.
    ///
    /// <b>IMGUI 다.</b> §4.6 이 "구획표가 이미 AABB 목록이라 IMGUI 유지 가능" 으로 적었고,
    /// 실제 레이아웃·방 아이콘 <c>10</c>종은 <c>game-art</c> 몫이라(§7-9) 지금 캔버스 계층을
    /// 세우면 그 작업이 통째로 버려진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftPlacementUi : MonoBehaviour
    {
        /// <summary>씬에 선 배 프리팹 인스턴스의 이름. 조립기와 절단기가 받는 뿌리다.</summary>
        public const string DefaultShipRootName = "LastShiftShipGraybox";

        /// <summary>
        /// 세션이 없을 때 쓰는 주인 번호. 네트워크가 붙으면 <see cref="ClientId"/> 가
        /// <c>NetworkManager.LocalClientId</c> 를 돌려준다.
        /// </summary>
        public const int LocalClientId = 0;

        [Tooltip("선체 판과 구획 루트를 담은 칸. 비면 이름으로 찾는다.")]
        [SerializeField] private Transform shipRoot;

        [Tooltip("모듈 프리팹·머티리얼. 비면 조립기가 그레이박스로 세운다.")]
        [SerializeField] private LastShiftModulePalette palette;

        [Tooltip("이 화면을 여닫는 키. 기항에서만 눌린다는 전제라 판 안 조작과 안 겹친다.")]
        [SerializeField] private Key toggleKey = Key.B;

        [Tooltip("항해 루프가 없는 샌드박스에서 화면을 처음 열 때 가정하는 래치 수(0~4).")]
        [Range(0, LastShiftMaintenance.MaxLatches)]
        [SerializeField] private int sandboxLatches = LastShiftMaintenance.MaxLatches;

        [Tooltip("선체 탭과 거점 탭을 오가는 키.")]
        [SerializeField] private Key tabKey = Key.T;

        private readonly LastShiftPlacementCursor cursor = new();

        /// <summary>
        /// 거점 커서. <b>탭마다 커서를 따로 드는 것이 의도다</b> — 하나로 쓰면 탭을 옮길 때마다
        /// 고른 것과 자리와 회전이 통째로 날아가고, "두 자리를 나란히 대 본다"(§4.1-1)가
        /// 탭 경계에서 끊긴다. 화면이 닫혀도 살아 있으므로 다시 열면 대 보던 자리가 그대로다.
        /// </summary>
        private readonly LastShiftOutpostCursor outpostCursor = new();

        /// <summary>지금 표에서 잰 자유면. 표 개정 번호가 안 바뀌면 다시 안 잰다.</summary>
        private readonly List<LastShiftFreeFace> freeFaces = new();

        private int freeFacesRevision = -1;

        /// <summary>거점 표의 자유면. 선체와 같은 계산이고 표만 다르다.</summary>
        private readonly List<LastShiftFreeFace> outpostFreeFaces = new();

        private int outpostFreeFacesRevision = -1;

        private LastShiftPlacementTab tab = LastShiftPlacementTab.Hull;

        private bool open;

        /// <summary>커서를 서버에 청구해 두고 승낙을 기다리는 중인가.</summary>
        private bool awaitingCursor;

        /// <summary>
        /// 후보가 지금 자리에서 확정을 기다리는가. <b><c>2</c>단 클릭인 것이 의도다</b> —
        /// §4.4 표는 "확정 = 클릭" 이지만 도면에서 클릭 하나가 곧 지출이면 자리를 <b>대 보는</b>
        /// 동작이 사라진다. 이 화면의 목적이 "두 자리를 나란히 대 본다"(§4.1-1)이므로, 첫 클릭은
        /// 후보를 옮기고 같은 자리를 다시 누르면 확정한다. 확정 순간은 여전히 클릭 하나다.
        /// </summary>
        private bool armed;

        private GameObject preview;
        private Material previewMaterial;
        private string lastResult = string.Empty;

        /// <summary>도면 위에서 마우스가 얹힌 지은 모듈. 없으면 <c>-1</c> 이다.</summary>
        private int hoveredIndex = -1;

        private Texture2D fillTexture;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle centeredStyle;

        /// <summary>지금 배치 화면이 열려 있는가.</summary>
        public bool IsOpen => open;

        /// <summary>
        /// 배치 세션이 붙어 있는가. 아니면 이 화면은 예전과 한 글자도 다르지 않게 혼자 돈다 —
        /// EditMode 배치 테스트와 배치만 세운 검증 씬이 그 경로다.
        /// </summary>
        private static LastShiftNetworkPlacement Network
        {
            get
            {
                var active = LastShiftNetworkPlacement.Active;
                return active != null && active.IsSpawned ? active : null;
            }
        }

        /// <summary>
        /// 이 화면이 쓰는 주인 번호. 세션이 있으면 <c>NetworkManager.LocalClientId</c> 다 —
        /// 전부 <see cref="LocalClientId"/> 를 쓰면 커서 주인이 언제나 <c>0</c> 이라
        /// <b>둘째 클라이언트가 호스트의 커서를 자기 것으로 읽는다.</b>
        /// </summary>
        public static int ClientId
        {
            get
            {
                var network = Network;
                return network != null ? (int)network.NetworkManager.LocalClientId : LocalClientId;
            }
        }

        /// <summary>화면이 들고 있는 커서. 테스트와 기항 화면이 같은 물건을 봐야 한다.</summary>
        public LastShiftPlacementCursor Cursor => cursor;

        /// <summary>거점 탭이 들고 있는 커서.</summary>
        public LastShiftOutpostCursor OutpostCursor => outpostCursor;

        /// <summary>지금 어느 탭인가.</summary>
        public LastShiftPlacementTab Tab => tab;

        /// <summary>
        /// 탭을 고른다. <b>후보 확정 대기(<see cref="armed"/>)를 푸는 것이 요지다</b> — 안 풀면
        /// 선체에서 한 번 누른 상태로 거점에 건너가서 첫 클릭이 곧 지출이 된다.
        /// </summary>
        public void SelectTab(LastShiftPlacementTab next)
        {
            if (tab == next) return;

            tab = next;
            armed = false;
            lastResult = string.Empty;
            DestroyPreview();
        }

        /// <summary>씬 빌더와 테스트가 쓴다.</summary>
        public void Configure(Transform root, LastShiftModulePalette modulePalette)
        {
            shipRoot = root;
            palette = modulePalette;
        }

        /// <summary>
        /// 씬에 이 화면이 없으면 런타임에 붙인다. <b>임시 다리이고, 그렇게 적어 두는 것이 요지다.</b>
        ///
        /// 정본 자리는 <c>LastShiftNetworkSceneBuilder</c> 다 — 거기 붙여 뒀으므로 씬을 다시
        /// 세우면 이 훅은 아무 일도 안 한다. 지금 훅이 필요한 이유는 씬 재생성 값이다:
        /// 씬을 다시 세우면 <c>LastShiftSceneBuilder.RebuildShipPrefab</c> 이 배 프리팹을 통째로
        /// 다시 직렬화하고 <c>fileID</c> 가 전부 갈린다(프리팹 `36,000`줄). <b>배치 화면 하나를
        /// 넣으려고 물 값이 아니다</b> — 그 값은 아트 모듈 프리팹이 들어와 팔레트를 물릴 때
        /// 한 번에 치른다.
        ///
        /// 팔레트는 이 경로로 붙으면 비어 있고, 그러면 조립기가 그레이박스로 세운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenMissing()
        {
            if (FindAnyObjectByType<LastShiftPlacementUi>() != null) return;

            var sandbox = FindAnyObjectByType<LastShiftSandboxController>();
            if (sandbox == null) return;

            sandbox.gameObject.AddComponent<LastShiftPlacementUi>();
        }

        private void Awake()
        {
            if (shipRoot == null)
            {
                var found = GameObject.Find(DefaultShipRootName);
                if (found != null) shipRoot = found.transform;
            }
        }

        private void OnDisable() => Close();

        private void OnDestroy()
        {
            if (fillTexture != null) Destroy(fillTexture);
            fillTexture = null;
        }

        // ── 흐름 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 화면을 연다. <b>커서 소유권을 못 잡으면 안 열린다</b> — 옆 사람이 배치 중이면
        /// 화면이 뜨는 것 자체가 틀린 신호다(§12-9, <see cref="LastShiftPlacementAuthority"/>).
        ///
        /// <b>조항 U-1 이 여기서 반만 성립한다.</b> "커서는 한 명이 잡고 도면은 전원이 본다"
        /// (§4.5) 중 앞은 이미 서버 권위로 서 있고, 뒤(안 잡은 사람도 도면을 보는 것)는 이
        /// 화면이 소유권을 열림 조건으로 쓰는 한 안 된다. 여는 조건을 푸는 것은 배치 요청을
        /// 누가 보낼 수 있는가와 같은 물음이라 커서 층에서 닫아야 한다 — 여기서 열어 두면
        /// 그 사이에 누른 확정이 전부 조용히 버려진다.
        /// </summary>
        public bool Open()
        {
            if (open) return true;

            var network = Network;
            if (network != null)
            {
                // 커서는 서버가 나눠 준다. 호스트에서는 이 요청이 그 자리에서 서버 몸통을
                // 돌므로 아래 검사가 곧바로 참이 되고, 원격 클라이언트는 승낙이 복제로
                // 돌아오는 프레임에 열린다(<see cref="OpenWhenCursorGranted"/>).
                if (!LastShiftPlacementAuthority.IsHeldBy(ClientId)) network.RequestClaimCursorRpc();
                if (!LastShiftPlacementAuthority.IsHeldBy(ClientId))
                {
                    awaitingCursor = true;
                    lastResult = "배치 권한을 기다린다";
                    return false;
                }
            }
            else if (!LastShiftPlacementAuthority.TryClaim(ClientId))
            {
                lastResult = "다른 승무원이 배치 중이다";
                return false;
            }

            EnterPortWhenNoVoyageDrivesIt();

            awaitingCursor = false;
            armed = false;
            open = true;
            lastResult = string.Empty;
            return true;
        }

        /// <summary>
        /// 청구해 둔 커서가 넘어왔으면 그때 연다. <b>화면을 미리 열어 두지 않는 것이 의도다</b> —
        /// 열어 두고 나중에 거부되면 그 사이에 누른 확정이 전부 조용히 버려지고, 화면에는
        /// 아무 일도 안 일어난 것으로 보인다.
        /// </summary>
        private void OpenWhenCursorGranted()
        {
            if (!awaitingCursor || open) return;
            if (!LastShiftPlacementAuthority.IsHeldBy(ClientId)) return;

            awaitingCursor = false;
            Open();
        }

        /// <summary>
        /// 서버가 커서를 거둬 갔으면 화면을 닫는다. 접속이 끊긴 사람의 커서를 호스트가 푸는
        /// 경로(<see cref="LastShiftPlacementAuthority.Revoke"/>)가 그것이고, 재접속 뒤 예전
        /// 화면이 열린 채 남아 있으면 <b>서버가 남에게 넘긴 커서로 확정을 계속 누르게 된다.</b>
        /// </summary>
        private void CloseWhenCursorRevoked()
        {
            if (!open || Network == null) return;
            if (LastShiftPlacementAuthority.IsHeldBy(ClientId)) return;

            open = false;
            armed = false;
            DestroyPreview();
            lastResult = "배치 권한을 잃었다";
        }

        /// <summary>
        /// 항해 루프가 없는 씬(배치만 세운 검증 씬)에서 화면이 처음 열릴 때 원장을 첫 기항으로
        /// 밀어 준다. <b>임시 다리이고 그렇게 적어 두는 것이 요지다</b> —
        /// <see cref="InstallWhenMissing"/> 와 같은 성격이다.
        ///
        /// <b>정본 경로가 붙었으므로 이제 여기는 항해가 안 돌 때만 움직인다</b> —
        /// <see cref="LastShiftVoyage.SettleSegment"/> 가 구간 판정에서 래치 수를 들고
        /// <see cref="LastShiftMaintenance.ArriveAtPort"/> 를 부른다. 이 조건이 없으면 구간
        /// <c>1</c> 도중에 화면을 여는 것만으로 기항이 하나 생겨서, "이월이 첫 기항을 비게
        /// 만드는가"(§9-3)를 실제 항해에서 못 본다.
        ///
        /// <b>여기서 잔액을 직접 안 만진다</b> — 수입을 만드는 식은 원장에만 있어야 나중에 래치
        /// 환산이 바뀔 때 고칠 자리가 하나다.
        /// </summary>
        private void EnterPortWhenNoVoyageDrivesIt()
        {
            // 원장을 만지는 것은 서버뿐이다. 클라이언트가 여기서 기항을 하나 열면 그 잔액은
            // 다음 복제에 덮여 사라지고, 그 사이에 누른 확정만 서버에서 여력 부족으로 물린다.
            var network = Network;
            if (network != null && !network.IsServer) return;
            if (LastShiftVoyage.IsRunning || LastShiftMaintenance.IsAtPort) return;

            LastShiftMaintenance.ArriveAtPort(sandboxLatches);
        }

        public void Close()
        {
            awaitingCursor = false;
            armed = false;
            if (!open) return;

            open = false;
            var network = Network;
            if (network != null) network.RequestReleaseCursorRpc();
            else LastShiftPlacementAuthority.Release(ClientId);
            DestroyPreview();
        }

        /// <summary>
        /// 확정한다. 표에 들어가면 씬을 다시 세우고 문틀을 뚫는다.
        ///
        /// <b>씬 재조립은 확정 뒤에만 돈다.</b> 커서를 옮길 때마다 돌리면 방 전체를 지웠다
        /// 세우는 값을 매 프레임 물게 되고, 그 사이 <see cref="LastShiftBakedDoorways"/> 가
        /// 구멍을 메웠다 다시 뚫는다.
        /// </summary>
        public bool Confirm()
        {
            armed = false;
            if (tab == LastShiftPlacementTab.Outpost) return ConfirmOutpost();

            var network = Network;
            if (network == null)
            {
                LastShiftPlacementCommands.TryPlace(cursor, out var local);
                return ApplyPlaceOutcome(local);
            }

            // 서버(호스트 포함)는 자기 몸통을 그 자리에서 부른다 — 원격 클라이언트와 같은 문이라
            // 커서 소유 검사가 호스트에서만 빠지는 일이 없다.
            if (network.IsServer)
                return ApplyPlaceOutcome(network.ServerPlace(
                    (ulong)ClientId, cursor.CatalogIndex, cursor.QuarterTurns,
                    cursor.Anchor.x, cursor.Anchor.z, cursor.ParentIndex, cursor.AutoParent));

            network.RequestPlaceModuleRpc(
                cursor.CatalogIndex, cursor.QuarterTurns,
                cursor.Anchor.x, cursor.Anchor.z, cursor.ParentIndex, cursor.AutoParent);
            lastResult = "확정 요청을 보냈다";
            return false;
        }

        /// <summary>
        /// 마지막에 놓은 모듈을 뺀다. <b>잎부터 빼는 것은 표가 강제한다</b> —
        /// 자식이 달린 칸은 <see cref="LastShiftCompartments.TryRemove"/> 가 거부한다.
        /// </summary>
        /// <summary>
        /// 거점 확정. <b>서버 갈래가 없다</b> — 자재 원장에 복제 경로가 아직 없으므로
        /// (<see cref="LastShiftOutpostCommands"/> 주석) 여기서 세션을 물으면 "값은 각자 내고
        /// 골조는 공유하는" 반쪽 경로가 생긴다. 선외 파밍이 서버 권위로 옮겨오는 카드가
        /// 그 갈래를 함께 연다.
        /// </summary>
        private bool ConfirmOutpost()
        {
            LastShiftOutpostCommands.TryPlace(outpostCursor, out var outcome);

            if (!outcome.Accepted)
            {
                lastResult = outcome.Result == LastShiftPlacementCommandResult.Unaffordable
                    ? $"자재가 모자란다 — {outpostCursor.Kind.Name} {outpostCursor.Kind.MaterialCost} · " +
                      $"잔액 {LastShiftMaterials.Balance}"
                    : outcome.Message;
                return false;
            }

            var built = LastShiftOutpostAssembler.Rebuild(palette);
            lastResult = $"거점 확정 #{outcome.Index} · 자재 -{outcome.Cost} → 잔액 {LastShiftMaterials.Balance} · " +
                         $"사슬 깊이 {outpostCursor.ChainDepth} · 세운 것 {built}";
            return true;
        }

        private bool UndoLastOutpost()
        {
            LastShiftOutpostCommands.TryRemoveLast(out var outcome);

            if (!outcome.Accepted)
            {
                lastResult = outcome.Result == LastShiftPlacementCommandResult.NothingToRemove
                    ? "뜯을 골조 없음"
                    : outcome.Message;
                return false;
            }

            var built = LastShiftOutpostAssembler.Rebuild(palette);
            lastResult = $"골조 해제 · 자재 +{outcome.Refunded} → 잔액 {LastShiftMaterials.Balance} · " +
                         $"남은 {built}";
            return true;
        }

        public bool UndoLast()
        {
            if (tab == LastShiftPlacementTab.Outpost) return UndoLastOutpost();

            var network = Network;
            if (network == null)
            {
                LastShiftPlacementCommands.TryRemoveLast(out var local);
                return ApplyRemoveOutcome(local);
            }

            if (network.IsServer) return ApplyRemoveOutcome(network.ServerRemoveLast((ulong)ClientId));

            network.RequestRemoveLastModuleRpc();
            lastResult = "해제 요청을 보냈다";
            return false;
        }

        /// <summary>
        /// 확정 결과를 화면 문구로 옮기고, 통과했으면 배를 다시 세운다.
        /// <b>서버가 낸 결과와 혼자 도는 결과가 같은 문장을 만든다</b> — 문구를 두 벌로 두면
        /// 어느 쪽 경로로 들어왔는지가 화면에서 갈린다.
        /// </summary>
        private bool ApplyPlaceOutcome(in LastShiftPlacementOutcome outcome)
        {
            if (!outcome.Accepted)
            {
                lastResult = outcome.Result == LastShiftPlacementCommandResult.Unaffordable
                    ? $"여력이 모자란다 — {cursor.Kind.Name} {cursor.Kind.MaintenanceCost} · 잔액 {LastShiftMaintenance.Balance}"
                    : outcome.Message;
                return false;
            }

            var verdict = outcome.Verdict;
            var report = RebuildShip();
            lastResult = $"배치 확정 #{outcome.Index} · 여력 -{outcome.Cost} → 잔액 {LastShiftMaintenance.Balance} · " +
                         $"구역 {verdict.Zone} · 깊이 {verdict.DoorDepth} · " +
                         $"이탈 {verdict.EgressSeconds:0.0}s · 문 {report.Cut}/{report.Doorways}" +
                         (report.Missing > 0 ? $" · 벽 못 찾음 {report.Missing}" : string.Empty);
            return true;
        }

        private bool ApplyRemoveOutcome(in LastShiftPlacementOutcome outcome)
        {
            if (!outcome.Accepted)
            {
                lastResult = outcome.Message;
                return false;
            }

            var report = RebuildShip();
            lastResult = $"모듈 해제 · 여력 +{outcome.Refunded} → 잔액 {LastShiftMaintenance.Balance} · " +
                         $"남은 {LastShiftCompartments.ModuleCount} · 문 {report.Cut}/{report.Doorways}";
            return true;
        }

        /// <summary>
        /// 요청자에게 돌아온 서버 판정을 적는다. <b>여기서 표를 안 건드린다</b> — 모듈이 실제로
        /// 서는 것은 복제가 하는 일이고, 이 회신은 "왜 안 됐는가" 를 적기 위한 것뿐이다.
        /// </summary>
        public void ReportNetworkOutcome(in LastShiftPlacementOutcome outcome)
        {
            lastResult = outcome.Accepted
                ? outcome.Refunded > 0
                    ? $"모듈 해제 · 여력 +{outcome.Refunded}"
                    : $"배치 확정 #{outcome.Index}"
                : outcome.Message;
        }

        /// <summary>
        /// 표에서 배를 다시 세운다. <b>복제로 표를 받은 클라이언트도 이 문으로 세운다</b>
        /// (<see cref="LastShiftNetworkPlacement"/>) — 씬 뿌리와 팔레트를 아는 것이 이 화면
        /// 하나뿐이라, 세우는 문을 하나로 두지 않으면 그 참조를 또 한 곳이 갖게 된다.
        /// </summary>
        public LastShiftBakedDoorwayReport RebuildShip()
        {
            if (shipRoot == null) return default;

            LastShiftModuleAssembler.Rebuild(shipRoot, palette, out var doorways);
            return doorways;
        }

        // ── 입력 ────────────────────────────────────────────────────────────

        private void Update()
        {
            OpenWhenCursorGranted();
            CloseWhenCursorRevoked();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                if (open || awaitingCursor) Close();
                else Open();
            }

            if (!open) return;

            if (keyboard[tabKey].wasPressedThisFrame)
                SelectTab(tab == LastShiftPlacementTab.Hull
                    ? LastShiftPlacementTab.Outpost
                    : LastShiftPlacementTab.Hull);

            // §4.4 — Tab/Shift+Tab 또는 1~0 으로 고른다. 카탈로그가 10종이라 숫자 한 줄이 곧 목록이다.
            var count = CatalogCount;
            if (keyboard[Key.Tab].wasPressedThisFrame) SelectCatalog(CatalogIndex + (keyboard[Key.LeftShift].isPressed ? -1 : 1));
            for (var slot = 0; slot < count && slot < 10; slot++)
                if (keyboard[DigitKeyFor(slot)].wasPressedThisFrame)
                    SelectCatalog(slot);

            if (keyboard[Key.R].wasPressedThisFrame) Rotate(keyboard[Key.LeftShift].isPressed ? -1 : 1);

            var stepX = (keyboard[Key.RightArrow].wasPressedThisFrame ? 1 : 0) -
                        (keyboard[Key.LeftArrow].wasPressedThisFrame ? 1 : 0);
            var stepZ = (keyboard[Key.UpArrow].wasPressedThisFrame ? 1 : 0) -
                        (keyboard[Key.DownArrow].wasPressedThisFrame ? 1 : 0);
            if (stepX != 0 || stepZ != 0)
            {
                if (tab == LastShiftPlacementTab.Outpost) outpostCursor.Nudge(stepX, stepZ);
                else cursor.Nudge(stepX, stepZ);
                armed = false;
            }

            if (keyboard[Key.Enter].wasPressedThisFrame) Confirm();
            if (keyboard[Key.Delete].wasPressedThisFrame) UndoLast();
            if (keyboard[Key.Escape].wasPressedThisFrame) Close();

            UpdatePreview();
        }

        /// <summary>
        /// <c>1~0</c> 이 카탈로그 <c>0~9</c> 다. <c>0</c> 이 열째인 것은 키보드 배열 순서이고,
        /// 목록이 <c>10</c> 종을 넘으면 이 대응이 먼저 깨진다.
        /// </summary>
        private static Key DigitKeyFor(int slot) => slot == 9 ? Key.Digit0 : Key.Digit1 + slot;

        /// <summary>지금 탭의 목록 길이. 숫자 키 대응과 카탈로그 칸 높이가 이 값을 본다.</summary>
        private int CatalogCount => tab == LastShiftPlacementTab.Outpost
            ? LastShiftOutpostCatalog.Count
            : LastShiftModuleCatalog.Count;

        /// <summary>지금 탭에서 고른 항목.</summary>
        private int CatalogIndex => tab == LastShiftPlacementTab.Outpost
            ? outpostCursor.CatalogIndex
            : cursor.CatalogIndex;

        private void SelectCatalog(int index)
        {
            if (tab == LastShiftPlacementTab.Outpost) outpostCursor.Select(index);
            else cursor.Select(index);
            armed = false;
        }

        private void Rotate(int steps)
        {
            if (tab == LastShiftPlacementTab.Outpost) outpostCursor.Rotate(steps);
            else cursor.Rotate(steps);
            armed = false;
        }

        // ── 미리보기 ────────────────────────────────────────────────────────

        /// <summary>
        /// 후보 발자국을 반투명 상자로 세운다. 색이 판정 결과다 — 초록이 들어가는 자리,
        /// 붉은색이 물리는 자리다.
        ///
        /// <b>도면이 생겨도 이 상자를 안 버린다.</b> 도면은 판 밖 화면이고 이 상자는 씬에 선
        /// 물건이라, 화면을 닫고 실제로 걸어 들어갔을 때 방금 고른 자리가 어디였는지를 아는
        /// 유일한 표시다.
        ///
        /// <b>머티리얼을 새로 안 만든다.</b> <c>Shader.Find</c> 로 만든 셰이더는 빌드에서
        /// 스트립돼 분홍색이 된다(<see cref="LastShiftModulePalette"/>). 팔레트 벽 재질이
        /// 있으면 그것의 사본을 쓰고, 없으면 프리미티브 기본 재질 사본을 쓴다. 반투명으로
        /// 바꾸는 방법은 <see cref="LastShiftGhostVisuals"/> 가 이미 갖고 있는 것을 그대로 쓴다.
        /// </summary>
        private void UpdatePreview()
        {
            if (preview == null)
            {
                preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.name = "PlacementPreview";
                preview.hideFlags = HideFlags.DontSave;
                Destroy(preview.GetComponent<Collider>());

                var renderer = preview.GetComponent<MeshRenderer>();
                var source = palette != null && palette.WallMaterial != null
                    ? palette.WallMaterial
                    : renderer.sharedMaterial;
                previewMaterial = new Material(source) { name = "LS_PlacementPreview" };
                renderer.sharedMaterial = previewMaterial;

                // <b>거점 상자는 배에 안 매단다</b>(조항 O-5). 배가 움직이면 따라가고, 그 상자가
                // 가리키는 자리는 그 순간부터 표와 다른 곳이다.
                var host = tab == LastShiftPlacementTab.Outpost ? null : shipRoot;
                if (host != null) preview.transform.SetParent(host, false);
                else preview.transform.SetParent(null);
            }

            if (tab == LastShiftPlacementTab.Outpost)
            {
                var outpost = outpostCursor.Candidate;
                preview.transform.position = new Vector3(
                    outpost.CenterX,
                    LastShiftOutpost.DeckY + LastShiftOutpost.FrameHeight * 0.5f,
                    outpost.CenterZ);
                preview.transform.localScale = new Vector3(
                    outpost.LengthX, LastShiftOutpost.FrameHeight, outpost.WidthZ);

                LastShiftGhostVisuals.Apply(
                    previewMaterial, true,
                    outpostCursor.CanCommit ? new Color(0.45f, 1f, 0.6f) : new Color(1f, 0.4f, 0.3f));
                return;
            }

            var candidate = cursor.Candidate;
            preview.transform.localPosition = new Vector3(
                candidate.CenterX, LastShiftCompartments.InteriorHeight * 0.5f, candidate.CenterZ);
            preview.transform.localScale = new Vector3(
                candidate.LengthX, LastShiftCompartments.InteriorHeight, candidate.WidthZ);

            LastShiftGhostVisuals.Apply(
                previewMaterial, true,
                cursor.CanCommit ? new Color(0.45f, 1f, 0.6f) : new Color(1f, 0.4f, 0.3f));
        }

        private void DestroyPreview()
        {
            if (preview != null) Destroy(preview);
            if (previewMaterial != null) Destroy(previewMaterial);
            preview = null;
            previewMaterial = null;
        }

        // ── 화면 ────────────────────────────────────────────────────────────

        private static readonly Color PanelColor = new(0.05f, 0.07f, 0.1f, 0.94f);
        private static readonly Color GridColor = new(0.22f, 0.28f, 0.36f, 1f);
        private static readonly Color HullColor = new(0.14f, 0.18f, 0.24f, 1f);
        private static readonly Color FixedColor = new(0.30f, 0.36f, 0.44f, 1f);
        private static readonly Color ModuleColor = new(0.42f, 0.62f, 0.78f, 1f);
        private static readonly Color HoverColor = new(0.85f, 0.72f, 0.35f, 1f);
        private static readonly Color FreeFaceColor = new(0.35f, 1f, 0.72f, 0.95f);
        private static readonly Color RimColor = new(0.45f, 0.55f, 0.70f, 0.85f);
        private static readonly Color OkColor = new(0.45f, 1f, 0.75f, 1f);
        private static readonly Color BadColor = new(1f, 0.45f, 0.35f, 1f);

        /// <summary>거점 도면의 잔해 뿌리. 지은 것이 아니라는 것이 색으로 먼저 읽혀야 한다.</summary>
        private static readonly Color SalvageColor = new(0.44f, 0.38f, 0.28f, 1f);

        /// <summary>
        /// 거점 도면이 덮는 월드 반경. 잔해 뿌리를 가운데 두고 사슬 상한
        /// (<see cref="LastShiftOutpost.MaxChainDepth"/>)까지 이어 붙인 거점이 화면 안에 들어와야
        /// 한다 — 넘치면 사람이 자기가 뭘 지었는지 보려고 탭을 껐다 켜게 된다.
        /// </summary>
        private const float OutpostViewHalfSpanMeters = 16f;

        /// <summary>압력 구역 넷의 옅은 색. 모듈이 어느 구역에 편입되는지가 곧 진공 조건이다(조항 F-1).</summary>
        private static readonly Color[] ZoneColors =
        {
            new(0.20f, 0.30f, 0.45f, 1f),
            new(0.34f, 0.26f, 0.20f, 1f),
            new(0.20f, 0.34f, 0.34f, 1f),
            new(0.26f, 0.22f, 0.38f, 1f)
        };

        private void OnGUI()
        {
            EnsureStyles();

            if (!open)
            {
                GUI.Label(new Rect(16f, Screen.height - 28f, 420f, 22f),
                    $"선체 도면 — {toggleKey}", bodyStyle);
                return;
            }

            var panel = new Rect(16f, 16f, Screen.width - 32f, Screen.height - 32f);
            var header = new Rect(panel.x, panel.y, panel.width, 34f);
            var tabs = new Rect(panel.x, header.yMax + 2f, panel.width, 24f);
            var body = new Rect(panel.x, tabs.yMax + 4f, panel.width, panel.height - header.height - tabs.height - 6f);
            var catalog = new Rect(body.x, body.y, 200f, body.height);
            var readout = new Rect(body.xMax - 260f, body.y, 260f, body.height);
            var chart = new Rect(catalog.xMax + 6f, body.y, readout.x - catalog.xMax - 12f, body.height);

            Fill(panel, PanelColor);

            DrawHeader(header);
            DrawTabs(tabs);

            if (tab == LastShiftPlacementTab.Outpost)
            {
                RefreshOutpostFreeFaces();

                var outpostChart = OutpostSchematicFor(chart);
                HandleChartInput(chart, outpostChart);

                DrawOutpostCatalog(catalog);
                DrawOutpostChart(chart, outpostChart);
                DrawOutpostReadout(readout);
                return;
            }

            RefreshFreeFaces();

            var schematic = new LastShiftHullSchematic(chart);
            HandleChartInput(chart, schematic);

            DrawCatalog(catalog);
            DrawChart(chart, schematic);
            DrawReadout(readout);
        }

        /// <summary>
        /// 거점 도면의 투영. <b>배율은 선체와 같은 자다</b> — 다른 자로 그리면 탭을 옮길 때
        /// 골조 크기가 바뀌고, "같은 손동작"(§5.1)이 눈에서 먼저 깨진다. 다른 것은 화면
        /// 가운데에 오는 좌표뿐이다.
        /// </summary>
        private static LastShiftHullSchematic OutpostSchematicFor(Rect chart)
        {
            var anchor = LastShiftOutpost.Anchor;
            return new LastShiftHullSchematic(
                chart, OutpostViewHalfSpanMeters, OutpostViewHalfSpanMeters,
                new Vector2(anchor.CenterX, anchor.CenterZ));
        }

        private void EnsureStyles()
        {
            if (fillTexture == null)
            {
                fillTexture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                fillTexture.SetPixel(0, 0, Color.white);
                fillTexture.Apply();
            }

            headingStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };
            smallStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 10, normal = { textColor = new Color(0.82f, 0.88f, 0.96f) } };
            centeredStyle ??= new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter };
        }

        /// <summary>
        /// 표가 바뀐 뒤에만 자유면을 다시 잰다. <b>후자를 빼면 안 된다</b> — 옆 사람이 배치를
        /// 확정한 순간 내 도면의 굵은 선이 낡은 표를 근거로 남는다(커서 캐시와 같은 이유).
        /// </summary>
        private void RefreshFreeFaces()
        {
            if (freeFacesRevision == LastShiftCompartments.Revision) return;

            LastShiftFreeFaces.Collect(freeFaces);
            freeFacesRevision = LastShiftCompartments.Revision;
        }

        /// <summary>
        /// 거점 자유면. <b>같은 계산에 <c>includeHull: false</c> 하나만 다르다</b> — 켜 두면
        /// 원반 바깥 진공에서 광장 벽이 굵은 선으로 나온다.
        /// </summary>
        private void RefreshOutpostFreeFaces()
        {
            if (outpostFreeFacesRevision == LastShiftOutpost.Revision) return;

            LastShiftFreeFaces.Collect(
                LastShiftOutpost.Specs, outpostFreeFaces,
                LastShiftFreeFaces.ClearanceMeters, LastShiftFreeFaces.MinimumRunMeters, false);
            outpostFreeFacesRevision = LastShiftOutpost.Revision;
        }

        // ── 도면 조작 ───────────────────────────────────────────────────────

        /// <summary>
        /// §4.4 의 마우스 조작. <b>도면 밖 클릭은 안 먹는다</b> — 카탈로그 칸과 같은 프레임에
        /// 눌리면 방을 고르는 클릭이 곧 배치가 된다.
        /// </summary>
        private void HandleChartInput(Rect chart, in LastShiftHullSchematic schematic)
        {
            var current = Event.current;
            if (current == null) return;

            hoveredIndex = chart.Contains(current.mousePosition)
                ? tab == LastShiftPlacementTab.Outpost
                    ? OutpostPieceAt(schematic.ToWorld(current.mousePosition))
                    : ModuleAt(schematic.ToWorld(current.mousePosition))
                : -1;

            if (!chart.Contains(current.mousePosition)) return;

            switch (current.type)
            {
                case EventType.MouseDown when current.button == 0:
                case EventType.MouseDrag when current.button == 0:
                    MoveCandidateTo(schematic.ToWorld(current.mousePosition), current.type == EventType.MouseDown);
                    current.Use();
                    break;

                case EventType.MouseDown when current.button == 1:
                    armed = false;
                    lastResult = string.Empty;
                    current.Use();
                    break;

                case EventType.ScrollWheel:
                    Rotate(current.delta.y > 0f ? 1 : -1);
                    current.Use();
                    break;
            }
        }

        /// <summary>
        /// 후보를 그 자리로 옮긴다. <b>같은 칸을 다시 누르면 확정이다</b>(<see cref="armed"/>).
        /// 접면 자석은 커서의 <see cref="LastShiftPlacementCursor.AutoParent"/> 가 이미 한다 —
        /// 문이 닿은 벽의 주인이 곧 부모라, 부모를 따로 고르게 하면 벽에 붙여 놓고 엉뚱한
        /// 부모를 고른 배치가 판정을 통과한다(§4.4).
        /// </summary>
        private void MoveCandidateTo(Vector3 world, bool click)
        {
            var before = tab == LastShiftPlacementTab.Outpost ? outpostCursor.Anchor : cursor.Anchor;

            if (tab == LastShiftPlacementTab.Outpost) outpostCursor.MoveTo(world);
            else cursor.MoveTo(world);

            var after = tab == LastShiftPlacementTab.Outpost ? outpostCursor.Anchor : cursor.Anchor;

            if (!click) { armed = false; return; }

            var moved = !Mathf.Approximately(before.x, after.x) ||
                        !Mathf.Approximately(before.z, after.z);
            if (moved) { armed = true; return; }

            if (armed) Confirm();
            else armed = true;
        }

        /// <summary>도면 위 한 점이 어느 <b>세운 골조</b> 안인가. 잔해 뿌리는 안 센다 — 못 뜯는다.</summary>
        private static int OutpostPieceAt(Vector3 world)
        {
            var table = LastShiftOutpost.Specs;
            for (var index = table.Length - 1; index >= LastShiftOutpost.FixedCount; index--)
            {
                var spec = table[index];
                if (world.x >= spec.MinX && world.x <= spec.MaxX &&
                    world.z >= spec.MinZ && world.z <= spec.MaxZ) return index;
            }

            return -1;
        }

        /// <summary>도면 위 한 점이 어느 <b>지은 모듈</b> 안인가. 고정 구획은 안 센다 — 못 뜯는다.</summary>
        private static int ModuleAt(Vector3 world)
        {
            var table = LastShiftCompartments.Specs;
            for (var index = table.Length - 1; index >= LastShiftCompartments.FixedCount; index--)
            {
                var spec = table[index];
                if (world.x >= spec.MinX && world.x <= spec.MaxX &&
                    world.z >= spec.MinZ && world.z <= spec.MaxZ) return index;
            }

            return -1;
        }

        // ── 머리줄 ──────────────────────────────────────────────────────────

        private void DrawHeader(Rect header)
        {
            var holder = LastShiftPlacementAuthority.HolderId;
            var holderText = holder == LastShiftPlacementAuthority.NoHolder
                ? "커서 없음"
                : holder == ClientId ? "커서 나" : $"커서 승무원 {holder}";

            // <b>지금 탭이 쓰는 잔액만 적는다</b>(조항 O-2 · 튜토리얼 §2-1). 둘을 나란히 띄우면
            // "어느 게 뭘 사는 건지" 를 화면이 다시 설명해야 하고, 튜토리얼이 그 장면을 끝까지
            // 안 만들려고 순서를 짜 둔 것이 그대로 무너진다.
            var money = tab == LastShiftPlacementTab.Outpost
                ? $"자재 {LastShiftMaterials.Balance} (이번 기항 반입 {LastShiftMaterials.LastPortSalvaged})"
                : $"정비 여력 {LastShiftMaintenance.Balance} " +
                  $"(수입 {LastShiftMaintenance.LastPortIncome} + 이월 {LastShiftMaintenance.LastCarriedOver}" +
                  // 버림은 <b>0 이 아닐 때만</b> 적는다 — 상한(조항 B-2)에 닿는 것은 후반뿐이라
                  // 평시에 "버림 0" 이 늘 떠 있으면 머리줄만 길어지고 아무것도 안 알린다.
                  (LastShiftMaintenance.LastPortForfeited > 0
                      ? $" · 버림 {LastShiftMaintenance.LastPortForfeited}"
                      : string.Empty) + ")";

            var title = tab == LastShiftPlacementTab.Outpost ? "선외 거점" : "선체 도면";

            GUI.Label(new Rect(header.x + 8f, header.y + 4f, header.width - 140f, 26f),
                $"{title} — 기항 {LastShiftMaintenance.PortIndex} · {money} · {holderText}",
                headingStyle);

            if (GUI.Button(new Rect(header.xMax - 96f, header.y + 3f, 88f, 26f), "닫기  Esc")) Close();
        }

        /// <summary>
        /// 탭 둘. <b>거점이 카탈로그 항목이 아니라 탭인 것이 §4.4 의 결론이다</b> — 목록에
        /// 섞으면 통화가 둘인 목록이 생기고, 조항 <c>O-2</c> 는 화면에서 먼저 깨진다.
        ///
        /// <b>튜토리얼 잠금은 여기 없다.</b> 조항 <c>T-4</c>("튜토리얼 중 선체 탭은 비활성이고
        /// 화면에 안 뜬다")는 튜토리얼 상태기가 이 화면에 거는 훅이고, 그 상태기가 아직 없다 —
        /// 없는 상태기를 흉내 내는 분기를 지금 넣으면 그 카드가 그것부터 걷어내야 한다.
        /// </summary>
        private void DrawTabs(Rect strip)
        {
            const float width = 120f;

            for (var index = 0; index < 2; index++)
            {
                var value = (LastShiftPlacementTab)index;
                var rect = new Rect(strip.x + index * (width + 4f), strip.y, width, strip.height);
                var chosen = tab == value;

                Fill(rect, chosen
                    ? new Color(0.20f, 0.34f, 0.44f, 1f)
                    : new Color(0.11f, 0.14f, 0.19f, 1f));
                GUI.Label(rect,
                    value == LastShiftPlacementTab.Outpost ? "거점" : "선체",
                    centeredStyle);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) SelectTab(value);
            }

            GUI.Label(new Rect(strip.x + 2f * (width + 4f) + 8f, strip.y, strip.width, strip.height),
                $"{tabKey} 로 전환", smallStyle);
        }

        // ── 카탈로그 ────────────────────────────────────────────────────────

        private void DrawCatalog(Rect column)
        {
            GUI.Label(new Rect(column.x + 4f, column.y, column.width, 18f), "카탈로그", bodyStyle);

            var top = column.y + 20f;
            var rowHeight = Mathf.Min(34f, (column.height - 44f) / Mathf.Max(LastShiftModuleCatalog.Count, 1));

            for (var index = 0; index < LastShiftModuleCatalog.Count; index++)
            {
                var kind = LastShiftModuleCatalog.At(index);
                var row = new Rect(column.x, top + index * (rowHeight + 2f), column.width, rowHeight);
                var chosen = index == cursor.CatalogIndex;
                var affordable = LastShiftMaintenance.CanAfford(kind.MaintenanceCost);

                Fill(row, chosen
                    ? new Color(0.20f, 0.34f, 0.44f, 1f)
                    : new Color(0.11f, 0.14f, 0.19f, 1f));

                var previous = GUI.color;
                GUI.color = affordable ? Color.white : new Color(1f, 0.6f, 0.5f, 1f);
                GUI.Label(new Rect(row.x + 6f, row.y + 2f, row.width - 12f, 16f),
                    $"{DigitLabel(index)}  {kind.Name}", smallStyle);
                GUI.Label(new Rect(row.x + 6f, row.y + 16f, row.width - 12f, 16f),
                    $"{kind.LengthX:0.#}×{kind.WidthZ:0.#}m · 값 {kind.MaintenanceCost}", smallStyle);
                GUI.color = previous;

                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) SelectCatalog(index);
            }

            GUI.Label(new Rect(column.x + 4f, column.yMax - 20f, column.width, 18f),
                $"잔여 {LastShiftMaintenance.Balance}", bodyStyle);
        }

        private static string DigitLabel(int index) => index == 9 ? "0" : (index + 1).ToString();

        /// <summary>
        /// 거점 카탈로그. 선체 쪽과 <b>칸 모양이 같고 값의 이름만 다르다</b> — 같은 손으로 읽는
        /// 목록이어야 §5.1 의 순서(거점에서 조작을 배우고 선체에서 규칙을 배운다)가 성립한다.
        /// </summary>
        private void DrawOutpostCatalog(Rect column)
        {
            GUI.Label(new Rect(column.x + 4f, column.y, column.width, 18f), "카탈로그 (자재)", bodyStyle);

            var top = column.y + 20f;
            var rowHeight = Mathf.Min(34f, (column.height - 44f) / Mathf.Max(LastShiftOutpostCatalog.Count, 1));

            for (var index = 0; index < LastShiftOutpostCatalog.Count; index++)
            {
                var kind = LastShiftOutpostCatalog.At(index);
                var row = new Rect(column.x, top + index * (rowHeight + 2f), column.width, rowHeight);
                var chosen = index == outpostCursor.CatalogIndex;
                var affordable = LastShiftMaterials.CanAfford(kind.MaterialCost);

                Fill(row, chosen
                    ? new Color(0.20f, 0.34f, 0.44f, 1f)
                    : new Color(0.11f, 0.14f, 0.19f, 1f));

                var previous = GUI.color;
                GUI.color = affordable ? Color.white : new Color(1f, 0.6f, 0.5f, 1f);
                GUI.Label(new Rect(row.x + 6f, row.y + 2f, row.width - 12f, 16f),
                    $"{DigitLabel(index)}  {kind.Name}", smallStyle);
                GUI.Label(new Rect(row.x + 6f, row.y + 16f, row.width - 12f, 16f),
                    $"{kind.LengthX:0.#}×{kind.WidthZ:0.#}m · 자재 {kind.MaterialCost}", smallStyle);
                GUI.color = previous;

                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) SelectCatalog(index);
            }

            GUI.Label(new Rect(column.x + 4f, column.yMax - 20f, column.width, 18f),
                $"잔여 자재 {LastShiftMaterials.Balance}", bodyStyle);
        }

        // ── 도면 ────────────────────────────────────────────────────────────

        /// <summary>
        /// §4.3 의 표 순서 그대로 그린다. <b>자유면이 방보다 뒤에 오는 것은 겹침 때문이지
        /// 우선순위가 낮아서가 아니다</b> — 우선순위 <c>1</c> 이라 방 위에 얹혀야 보인다.
        /// </summary>
        private void DrawChart(Rect chart, in LastShiftHullSchematic schematic)
        {
            Fill(chart, new Color(0.07f, 0.09f, 0.12f, 1f));

            DrawRim(schematic);
            DrawZones(schematic);
            DrawCompartments(schematic);
            DrawFreeFaces(schematic);
            DrawCandidate(schematic);

            GUI.Label(new Rect(chart.x + 6f, chart.y + 4f, 200f, 16f), "선수 ←", smallStyle);
            GUI.Label(new Rect(chart.xMax - 60f, chart.y + 4f, 60f, 16f), "→ 선미", smallStyle);
            GUI.Label(new Rect(chart.x + 6f, chart.yMax - 18f, chart.width - 12f, 16f),
                "드래그 이동(1m 격자) · 같은 자리 다시 클릭 = 확정 · 휠/R 회전 · 우클릭 취소 · Del 마지막 해제",
                smallStyle);
        }

        /// <summary>
        /// 거점 도면. <b>구역 띠도 원반 테두리도 안 그린다</b> — 거점에는 압력 구역이 없고
        /// (§4.4 의 "<c>RG-1</c> 없음"), 원반은 여기서 <b>화면 밖</b>이다. 남는 것은 잔해 뿌리 ·
        /// 세운 골조 · 자유면 · 후보 넷이고, 그게 이 탭이 그릴 것의 전부다.
        /// </summary>
        private void DrawOutpostChart(Rect chart, in LastShiftHullSchematic schematic)
        {
            Fill(chart, new Color(0.05f, 0.06f, 0.09f, 1f));

            var table = LastShiftOutpost.Specs;
            for (var index = 0; index < table.Length; index++)
            {
                var spec = table[index];
                var rect = schematic.ToScreenRect(spec);
                var isAnchor = index == LastShiftOutpost.AnchorIndex;

                Fill(rect, index == hoveredIndex ? HoverColor : isAnchor ? SalvageColor : ModuleColor);
                Outline(rect, HullColor, 1f);

                if (rect.height < 12f || rect.width < 24f) continue;
                GUI.Label(rect, LastShiftOutpost.NameOf(index), centeredStyle);
            }

            for (var index = 0; index < outpostFreeFaces.Count; index++)
                Fill(schematic.ToScreenBand(outpostFreeFaces[index], 3f), FreeFaceColor);

            DrawOutpostCandidate(schematic);

            GUI.Label(new Rect(chart.x + 6f, chart.y + 4f, 260f, 16f), "선수 ←  ·  원반 바깥 좌현", smallStyle);
            GUI.Label(new Rect(chart.x + 6f, chart.yMax - 18f, chart.width - 12f, 16f),
                "드래그 이동(1m 격자) · 같은 자리 다시 클릭 = 확정 · 휠/R 회전 · 우클릭 취소 · Del 마지막 해제",
                smallStyle);
        }

        /// <summary>후보 골조와 계류면. 붙는 면이 흰 선이다 — 선체 탭의 문 표시와 같은 자리다.</summary>
        private void DrawOutpostCandidate(in LastShiftHullSchematic schematic)
        {
            var candidate = outpostCursor.Candidate;
            var rect = schematic.ToScreenRect(candidate);
            var ok = outpostCursor.CanCommit &&
                     LastShiftMaterials.CanAfford(outpostCursor.Kind.MaterialCost);
            var tint = ok ? OkColor : BadColor;

            Fill(rect, new Color(tint.r, tint.g, tint.b, 0.35f));
            Outline(rect, tint, armed ? 3f : 1f);

            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            var mooring = candidate.DoorPlane == LastShiftDoorPlane.AlongX
                ? schematic.ToScreenRect(
                    candidate.DoorPlaneCoordinate, candidate.DoorPlaneCoordinate,
                    candidate.DoorCenter - half, candidate.DoorCenter + half)
                : schematic.ToScreenRect(
                    candidate.DoorCenter - half, candidate.DoorCenter + half,
                    candidate.DoorPlaneCoordinate, candidate.DoorPlaneCoordinate);

            Fill(new Rect(mooring.x - 1.5f, mooring.y - 1.5f,
                    Mathf.Max(mooring.width, 3f), Mathf.Max(mooring.height, 3f)),
                Color.white);
        }

        /// <summary>
        /// 거점 미리보기 숫자. <b>선체보다 줄이 적다</b> — 구역·이탈·최장 동선이 없다(§4.4).
        /// 없는 값을 <c>0</c> 으로 띄우면 거점에도 그 판정이 있는 것으로 읽힌다.
        /// </summary>
        private void DrawOutpostReadout(Rect column)
        {
            var candidate = outpostCursor.Candidate;
            var kind = outpostCursor.Kind;
            var parent = candidate.ParentIndex < 0 ? "없음" : LastShiftOutpost.NameOf(candidate.ParentIndex);

            var line = column.y;
            void Row(string text, GUIStyle style = null)
            {
                GUI.Label(new Rect(column.x + 4f, line, column.width - 8f, 20f), text, style ?? bodyStyle);
                line += 20f;
            }

            Row("미리보기", headingStyle);
            line += 4f;
            Row($"{kind.Name} {kind.LengthX:0.#}×{kind.WidthZ:0.#}m");
            Row($"자재 {kind.MaterialCost} · 회전 {outpostCursor.QuarterTurns * 90}°");
            Row($"x {candidate.MinX:0.#}~{candidate.MaxX:0.#}");
            Row($"z {candidate.MinZ:0.#}~{candidate.MaxZ:0.#}");
            Row($"계류 상대 {parent}");
            line += 6f;

            Row($"사슬 깊이 {outpostCursor.ChainDepth}/{LastShiftOutpost.MaxChainDepth}");
            Row($"자재 {LastShiftMaterials.Balance} → {LastShiftMaterials.Balance - kind.MaterialCost}");
            line += 6f;

            var affordable = LastShiftMaterials.CanAfford(kind.MaterialCost);
            var ok = outpostCursor.CanCommit && affordable;
            var previous = GUI.color;
            GUI.color = ok ? OkColor : BadColor;
            Row(ok
                ? armed ? "한 번 더 클릭 = 확정" : "계류 가능 — 클릭 또는 Enter"
                : affordable
                    ? LastShiftPlacementCommands.Reason(outpostCursor.Rejection, outpostCursor.Faults)
                    : "자재가 모자란다");
            GUI.color = previous;

            line += 6f;
            Row($"자유면 {outpostFreeFaces.Count}구간 · 세운 골조 {LastShiftOutpost.PieceCount}");
            Row(LastShiftOutpost.PieceCount > 0
                ? $"Del 환수 {LastShiftOutpost.PaidFor(LastShiftOutpost.Count - 1)}"
                : "뜯을 골조 없음", smallStyle);

            if (hoveredIndex >= 0 && hoveredIndex < LastShiftOutpost.Count)
                Row($"짚은 것 — {LastShiftOutpost.NameOf(hoveredIndex)} #{hoveredIndex}", smallStyle);

            if (lastResult.Length > 0)
                GUI.Label(new Rect(column.x + 4f, column.yMax - 60f, column.width - 8f, 56f), lastResult, bodyStyle);
        }

        /// <summary>원반 테두리. <b>바깥은 시작 배가 안 쓰는 자리다</b>(§4.3-6) — 점선으로 두른다.</summary>
        private void DrawRim(in LastShiftHullSchematic schematic)
        {
            const int steps = LastShiftHullShell.SegmentCount * 2;
            for (var step = 0; step < steps; step++)
            {
                var point = schematic.RimPoint(step, steps);
                Fill(new Rect(point.x - 1f, point.y - 1f, 2f, 2f), RimColor);
            }
        }

        private void DrawZones(in LastShiftHullSchematic schematic)
        {
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var rect = schematic.ToScreenRect((LastShiftZone)zone);
                Fill(rect, ZoneColors[zone % ZoneColors.Length]);
                Fill(new Rect(rect.x, rect.y, 1f, rect.height), GridColor);
                GUI.Label(new Rect(rect.x, rect.yMax + 1f, rect.width, 14f),
                    LastShiftZoneAtlas.ShortLabelOf((LastShiftZone)zone), centeredStyle);
            }

            Outline(schematic.HullInteriorRect, GridColor, 1f);
        }

        private void DrawCompartments(in LastShiftHullSchematic schematic)
        {
            var table = LastShiftCompartments.Specs;
            for (var index = 0; index < table.Length; index++)
            {
                var spec = table[index];
                var rect = schematic.ToScreenRect(spec);
                var isModule = index >= LastShiftCompartments.FixedCount;

                Fill(rect, index == hoveredIndex ? HoverColor : isModule ? ModuleColor : FixedColor);
                Outline(rect, HullColor, 1f);

                if (rect.height < 12f || rect.width < 24f) continue;

                GUI.Label(rect, ShortName(spec), centeredStyle);
                if (isModule)
                    GUI.Label(new Rect(rect.x + 2f, rect.y + 1f, 24f, 12f),
                        $"D{LastShiftCompartments.DoorDepth(index)}", smallStyle);
            }
        }

        /// <summary>도면 칸에 들어갈 만큼 짧은 이름. 이름 정본은 <see cref="LastShiftCompartments.NameOf(in LastShiftCompartmentSpec)"/> 다.</summary>
        private static string ShortName(in LastShiftCompartmentSpec spec)
        {
            if (!spec.IsFixed)
            {
                var catalogIndex = LastShiftCompartments.CatalogIndexOf(spec.Index);
                return catalogIndex >= 0 ? LastShiftModuleCatalog.At(catalogIndex).Name : "모듈";
            }

            var name = LastShiftCompartments.NameOf(spec);
            return name.StartsWith("Compartment_") ? name.Substring("Compartment_".Length) : name;
        }

        /// <summary>
        /// <b>이 하나가 개편의 실질 이득 전부다</b>(§4.3-1). 붙일 수 있는 변 구간을 굵은 선으로
        /// 긋는다 — <c>1</c>인칭이 절대 못 보여주던 것이고, 이걸 빼면 플레이어는 도면을 켜 놓고도
        /// 여전히 벽을 눈으로 훑는다(§8-5).
        /// </summary>
        private void DrawFreeFaces(in LastShiftHullSchematic schematic)
        {
            for (var index = 0; index < freeFaces.Count; index++)
                Fill(schematic.ToScreenBand(freeFaces[index], 3f), FreeFaceColor);
        }

        /// <summary>후보 발자국과 문. L0/L1 위반이면 빨강이고 사유는 오른쪽 칸에 한 줄로 적힌다(§4.3-2).</summary>
        private void DrawCandidate(in LastShiftHullSchematic schematic)
        {
            var candidate = cursor.Candidate;
            var rect = schematic.ToScreenRect(candidate);
            var ok = cursor.CanCommit && LastShiftMaintenance.CanAfford(cursor.Kind.MaintenanceCost);
            var tint = ok ? OkColor : BadColor;

            Fill(rect, new Color(tint.r, tint.g, tint.b, 0.35f));
            Outline(rect, tint, armed ? 3f : 1f);

            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            var door = candidate.DoorPlane == LastShiftDoorPlane.AlongX
                ? schematic.ToScreenRect(
                    candidate.DoorPlaneCoordinate, candidate.DoorPlaneCoordinate,
                    candidate.DoorCenter - half, candidate.DoorCenter + half)
                : schematic.ToScreenRect(
                    candidate.DoorCenter - half, candidate.DoorCenter + half,
                    candidate.DoorPlaneCoordinate, candidate.DoorPlaneCoordinate);

            Fill(new Rect(door.x - 1.5f, door.y - 1.5f, Mathf.Max(door.width, 3f), Mathf.Max(door.height, 3f)),
                Color.white);
        }

        // ── 미리보기 숫자 ───────────────────────────────────────────────────

        private void DrawReadout(Rect column)
        {
            var candidate = cursor.Candidate;
            var verdict = cursor.Verdict;
            var parent = candidate.ParentIndex < 0
                ? "선체"
                : ShortName(LastShiftCompartments.At(candidate.ParentIndex));

            var line = column.y;
            void Row(string text, GUIStyle style = null)
            {
                GUI.Label(new Rect(column.x + 4f, line, column.width - 8f, 20f), text, style ?? bodyStyle);
                line += 20f;
            }

            Row("미리보기", headingStyle);
            line += 4f;
            Row($"{cursor.Kind.Name} {cursor.Kind.LengthX:0.#}×{cursor.Kind.WidthZ:0.#}m");
            Row($"값 {cursor.Kind.MaintenanceCost} · 회전 {cursor.QuarterTurns * 90}°");
            Row($"x {candidate.MinX:0.#}~{candidate.MaxX:0.#}");
            Row($"z {candidate.MinZ:0.#}~{candidate.MaxZ:0.#}");
            Row($"붙는 곳 {parent}");
            line += 6f;

            Row($"구역 {LastShiftZoneAtlas.ShortLabelOf(verdict.Zone)} · 깊이 {verdict.DoorDepth}/{LastShiftPlacementRules.MaxDoorDepth}");
            Row($"최장 이탈 {verdict.EgressSeconds:0.00}s (한도 {LastShiftPlacementRules.TraverseLimitSeconds:0.#})");
            Row($"최장 동선 {verdict.LongestPairMeters:0.#}m");
            Row($"여력 {LastShiftMaintenance.Balance} → {LastShiftMaintenance.Balance - cursor.Kind.MaintenanceCost}");
            line += 6f;

            var affordable = LastShiftMaintenance.CanAfford(cursor.Kind.MaintenanceCost);
            var ok = cursor.CanCommit && affordable;
            var previous = GUI.color;
            GUI.color = ok ? OkColor : BadColor;
            Row(ok
                ? armed ? "한 번 더 클릭 = 확정" : "배치 가능 — 클릭 또는 Enter"
                : affordable ? Reason(verdict, cursor.Faults) : "여력이 모자란다");
            GUI.color = previous;

            line += 6f;
            Row($"자유면 {freeFaces.Count}구간 · 놓인 모듈 {LastShiftCompartments.ModuleCount}");
            Row(RefundHint(), smallStyle);

            // 표 길이를 같이 본다 — 탭을 옮기면 짚은 번호가 <b>다른 표의 인덱스</b>로 남고,
            // 거점 표가 더 짧으면 그 번호가 선체 표 밖을 가리킨다.
            if (hoveredIndex >= 0 && hoveredIndex < LastShiftCompartments.Count)
            {
                var hovered = LastShiftCompartments.At(hoveredIndex);
                Row($"짚은 것 — {ShortName(hovered)} #{hoveredIndex}", smallStyle);
            }

            if (lastResult.Length > 0)
                GUI.Label(new Rect(column.x + 4f, column.yMax - 60f, column.width - 8f, 56f), lastResult, bodyStyle);
        }

        /// <summary>
        /// 마지막 모듈을 뜯으면 얼마가 돌아오는가. <b>뜯기 전에 적는다</b> — 같은 기항이면 전액,
        /// 출항한 뒤면 절반이라(조항 M-4) 그 차이를 누른 뒤에 알면 이미 늦다.
        ///
        /// <b>아직 마지막 하나만이다.</b> §4.3-7 은 지은 모듈을 아무거나 골라 뜯는 것을 적었지만,
        /// 그건 서버에 새 동사를 하나 더 여는 일이라 §4.6 의 "네트워크 <c>0</c>" 밖이다 —
        /// 도면은 짚은 모듈을 알려 주고, 뜯는 것은 잎부터다.
        /// </summary>
        private static string RefundHint()
        {
            var slot = LastShiftCompartments.ModuleCount - 1;
            if (!LastShiftMaintenance.TryGetPurchase(slot, out var purchase)) return "뜯을 모듈 없음";

            return $"Del 환수 {LastShiftMaintenance.RefundFor(purchase)}";
        }

        /// <summary>
        /// 왜 안 들어가는가. 문구 정본은 <see cref="LastShiftPlacementCommands.Reason"/> 로
        /// 옮겼다 — 서버가 낸 거부도 같은 문장이 돼야 하고, 그 문장을 만드는 자리가 화면 쪽에
        /// 있으면 세션 없는 서버 경로가 화면을 참조하게 된다.
        /// </summary>
        public static string Reason(in LastShiftPlacementVerdict verdict, LastShiftPlacementFault faults) =>
            LastShiftPlacementCommands.Reason(verdict, faults);

        // ── 그리기 도구 ─────────────────────────────────────────────────────

        private void Fill(Rect rect, Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, fillTexture);
            GUI.color = previous;
        }

        private void Outline(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
