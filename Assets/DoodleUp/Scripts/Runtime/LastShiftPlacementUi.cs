using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 기항 배치 화면. <b>이 컴포넌트가 자유 배치 사슬을 처음으로 끝까지 잇는다</b> —
    /// 커서(<see cref="LastShiftPlacementCursor"/>) → 판정(<see cref="LastShiftPlacementVerdict"/>)
    /// → 표(<see cref="LastShiftCompartments"/>) → 씬(<see cref="LastShiftModuleAssembler"/>)
    /// → 벽뚫기(<see cref="LastShiftBakedDoorways"/>).
    ///
    /// <b>규칙을 하나도 안 갖는다.</b> 여기 있는 것은 키를 커서 함수로 옮기는 일과, 커서가
    /// 이미 계산해 둔 값을 화면에 적는 일뿐이다. 판정을 여기서 한 줄이라도 다시 하면 화면이
    /// 통과라고 적은 배치가 표에서 물리는 자리가 생긴다.
    ///
    /// <b>IMGUI 다.</b> <see cref="LastShiftSandboxController"/> 의 HUD 와 같은 방식이다 —
    /// 기항 화면의 실제 모습은 <c>voyage-run-structure-v1.md</c> §4 가 정할 것이고 그건 아트·
    /// 기획 몫이라, 지금 캔버스 계층을 세우면 그 작업이 통째로 버려진다.
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

        private readonly LastShiftPlacementCursor cursor = new();

        private bool open;

        /// <summary>커서를 서버에 청구해 두고 승낙을 기다리는 중인가.</summary>
        private bool awaitingCursor;

        private GameObject preview;
        private Material previewMaterial;
        private string lastResult = string.Empty;

        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;

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

        // ── 흐름 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 화면을 연다. <b>커서 소유권을 못 잡으면 안 열린다</b> — 옆 사람이 배치 중이면
        /// 화면이 뜨는 것 자체가 틀린 신호다(§12-9, <see cref="LastShiftPlacementAuthority"/>).
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
        public bool UndoLast()
        {
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

            if (keyboard[Key.Tab].wasPressedThisFrame) cursor.SelectNext(keyboard[Key.LeftShift].isPressed ? -1 : 1);
            if (keyboard[Key.Z].wasPressedThisFrame) cursor.Rotate(-1);
            if (keyboard[Key.X].wasPressedThisFrame) cursor.Rotate(1);

            var stepX = (keyboard[Key.RightArrow].wasPressedThisFrame ? 1 : 0) -
                        (keyboard[Key.LeftArrow].wasPressedThisFrame ? 1 : 0);
            var stepZ = (keyboard[Key.UpArrow].wasPressedThisFrame ? 1 : 0) -
                        (keyboard[Key.DownArrow].wasPressedThisFrame ? 1 : 0);
            cursor.Nudge(stepX, stepZ);

            if (keyboard[Key.Enter].wasPressedThisFrame) Confirm();
            if (keyboard[Key.Delete].wasPressedThisFrame) UndoLast();
            if (keyboard[Key.Escape].wasPressedThisFrame) Close();

            UpdatePreview();
        }

        // ── 미리보기 ────────────────────────────────────────────────────────

        /// <summary>
        /// 후보 발자국을 반투명 상자로 세운다. 색이 판정 결과다 — 초록이 들어가는 자리,
        /// 붉은색이 물리는 자리다.
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
                if (shipRoot != null) preview.transform.SetParent(shipRoot, false);
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

        private void OnGUI()
        {
            headingStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };

            if (!open)
            {
                GUI.Label(new Rect(16f, Screen.height - 28f, 420f, 22f),
                    $"기항 배치 — {toggleKey}", bodyStyle);
                return;
            }

            GUI.Box(new Rect(16f, 320f, 520f, 236f), GUIContent.none);
            GUI.Label(new Rect(28f, 328f, 480f, 24f),
                $"기항 {LastShiftMaintenance.PortIndex} — 여력 {LastShiftMaintenance.Balance} " +
                $"(수입 {LastShiftMaintenance.LastPortIncome} + 이월 {LastShiftMaintenance.LastCarriedOver})",
                headingStyle);

            var candidate = cursor.Candidate;
            var verdict = cursor.Verdict;
            var parent = candidate.ParentIndex < 0
                ? "선체"
                : LastShiftCompartments.NameOf(LastShiftCompartments.At(candidate.ParentIndex));

            GUI.Label(new Rect(28f, 352f, 480f, 22f),
                $"{cursor.Kind.Name} {cursor.Kind.LengthX:0.#}×{cursor.Kind.WidthZ:0.#}m · " +
                $"값 {cursor.Kind.MaintenanceCost} · 회전 {cursor.QuarterTurns * 90}°", bodyStyle);
            GUI.Label(new Rect(28f, 374f, 480f, 22f),
                $"자리 x {candidate.MinX:0.#}~{candidate.MaxX:0.#} · z {candidate.MinZ:0.#}~{candidate.MaxZ:0.#} · 부모 {parent}",
                bodyStyle);
            GUI.Label(new Rect(28f, 396f, 480f, 22f),
                $"구역 {verdict.Zone} · 깊이 {verdict.DoorDepth}/{LastShiftPlacementRules.MaxDoorDepth} · " +
                $"이탈 {verdict.EgressSeconds:0.0}/{LastShiftPlacementRules.TraverseLimitSeconds:0.0}s · " +
                $"최장 쌍 {verdict.LongestPairMeters:0.#}m", bodyStyle);

            var affordable = LastShiftMaintenance.CanAfford(cursor.Kind.MaintenanceCost);
            var ok = cursor.CanCommit && affordable;
            var previous = GUI.color;
            GUI.color = ok ? new Color(0.45f, 1f, 0.75f) : new Color(1f, 0.55f, 0.4f);
            GUI.Label(new Rect(28f, 418f, 480f, 22f),
                ok ? "배치 가능 — Enter"
                   : affordable ? Reason(verdict, cursor.Faults) : "여력이 모자란다", bodyStyle);
            GUI.color = previous;

            GUI.Label(new Rect(28f, 440f, 480f, 22f),
                $"놓인 모듈 {LastShiftCompartments.ModuleCount}{RefundHint()}", bodyStyle);
            GUI.Label(new Rect(28f, 462f, 480f, 40f),
                "Tab 모듈 · Z/X 회전 · 방향키 이동(1m) · Enter 확정 · Delete 해제 · Esc 닫기", bodyStyle);

            if (lastResult.Length > 0)
                GUI.Label(new Rect(28f, 508f, 480f, 40f), lastResult, bodyStyle);
        }

        /// <summary>
        /// 마지막 모듈을 뜯으면 얼마가 돌아오는가. <b>뜯기 전에 적는다</b> — 같은 기항이면 전액,
        /// 출항한 뒤면 절반이라(조항 M-4) 그 차이를 누른 뒤에 알면 이미 늦다.
        /// </summary>
        private static string RefundHint()
        {
            var slot = LastShiftCompartments.ModuleCount - 1;
            if (!LastShiftMaintenance.TryGetPurchase(slot, out var purchase)) return string.Empty;

            return $" · Delete 환수 {LastShiftMaintenance.RefundFor(purchase)}";
        }

        /// <summary>
        /// 왜 안 들어가는가. 문구 정본은 <see cref="LastShiftPlacementCommands.Reason"/> 로
        /// 옮겼다 — 서버가 낸 거부도 같은 문장이 돼야 하고, 그 문장을 만드는 자리가 화면 쪽에
        /// 있으면 세션 없는 서버 경로가 화면을 참조하게 된다.
        /// </summary>
        public static string Reason(in LastShiftPlacementVerdict verdict, LastShiftPlacementFault faults) =>
            LastShiftPlacementCommands.Reason(verdict, faults);
    }
}
