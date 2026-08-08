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

        /// <summary>단일 클라이언트에서 쓰는 주인 번호. 네트워크가 붙으면 <c>OwnerClientId</c> 가 이 자리다.</summary>
        public const int LocalClientId = 0;

        [Tooltip("선체 판과 구획 루트를 담은 칸. 비면 이름으로 찾는다.")]
        [SerializeField] private Transform shipRoot;

        [Tooltip("모듈 프리팹·머티리얼. 비면 조립기가 그레이박스로 세운다.")]
        [SerializeField] private LastShiftModulePalette palette;

        [Tooltip("이 화면을 여닫는 키. 기항에서만 눌린다는 전제라 판 안 조작과 안 겹친다.")]
        [SerializeField] private Key toggleKey = Key.B;

        private readonly LastShiftPlacementCursor cursor = new();

        private bool open;
        private GameObject preview;
        private Material previewMaterial;
        private string lastResult = string.Empty;

        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;

        /// <summary>지금 배치 화면이 열려 있는가.</summary>
        public bool IsOpen => open;

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
            if (!LastShiftPlacementAuthority.TryClaim(LocalClientId))
            {
                lastResult = "다른 승무원이 배치 중이다";
                return false;
            }

            open = true;
            lastResult = string.Empty;
            return true;
        }

        public void Close()
        {
            if (!open) return;

            open = false;
            LastShiftPlacementAuthority.Release(LocalClientId);
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
            if (!cursor.TryCommit(out var index, out var verdict))
            {
                lastResult = Reason(verdict, cursor.Faults);
                return false;
            }

            var report = Rebuild();
            lastResult = $"배치 확정 #{index} · 구역 {verdict.Zone} · 깊이 {verdict.DoorDepth} · " +
                         $"이탈 {verdict.EgressSeconds:0.0}s · 문 {report.Cut}/{report.Doorways}" +
                         (report.Missing > 0 ? $" · 벽 못 찾음 {report.Missing}" : string.Empty);
            return true;
        }

        /// <summary>
        /// 마지막에 놓은 모듈을 뺀다. <b>잎부터 빼는 것은 표가 강제한다</b> —
        /// 자식이 달린 칸은 <see cref="LastShiftCompartments.TryRemove"/> 가 거부한다.
        /// </summary>
        public bool UndoLast()
        {
            if (LastShiftCompartments.ModuleCount <= 0)
            {
                lastResult = "뺄 모듈이 없다";
                return false;
            }

            if (!LastShiftCompartments.TryRemove(LastShiftCompartments.Count - 1))
            {
                lastResult = "자식이 달린 모듈은 못 뺀다";
                return false;
            }

            var report = Rebuild();
            lastResult = $"모듈 해제 · 남은 {LastShiftCompartments.ModuleCount} · 문 {report.Cut}/{report.Doorways}";
            return true;
        }

        private LastShiftBakedDoorwayReport Rebuild()
        {
            if (shipRoot == null) return default;

            LastShiftModuleAssembler.Rebuild(shipRoot, palette, out var doorways);
            return doorways;
        }

        // ── 입력 ────────────────────────────────────────────────────────────

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                if (open) Close();
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

            GUI.Box(new Rect(16f, 320f, 520f, 214f), GUIContent.none);
            GUI.Label(new Rect(28f, 328f, 480f, 24f),
                $"기항 배치 — {cursor.Kind.Name} {cursor.Kind.LengthX:0.#}×{cursor.Kind.WidthZ:0.#}m " +
                $"· 회전 {cursor.QuarterTurns * 90}°", headingStyle);

            var candidate = cursor.Candidate;
            var verdict = cursor.Verdict;
            var parent = candidate.ParentIndex < 0
                ? "선체"
                : LastShiftCompartments.NameOf(LastShiftCompartments.At(candidate.ParentIndex));

            GUI.Label(new Rect(28f, 354f, 480f, 22f),
                $"자리 x {candidate.MinX:0.#}~{candidate.MaxX:0.#} · z {candidate.MinZ:0.#}~{candidate.MaxZ:0.#} · 부모 {parent}",
                bodyStyle);
            GUI.Label(new Rect(28f, 374f, 480f, 22f),
                $"구역 {verdict.Zone} · 깊이 {verdict.DoorDepth}/{LastShiftPlacementRules.MaxDoorDepth} · " +
                $"이탈 {verdict.EgressSeconds:0.0}/{LastShiftPlacementRules.TraverseLimitSeconds:0.0}s · " +
                $"최장 쌍 {verdict.LongestPairMeters:0.#}m", bodyStyle);

            var ok = cursor.CanCommit;
            var previous = GUI.color;
            GUI.color = ok ? new Color(0.45f, 1f, 0.75f) : new Color(1f, 0.55f, 0.4f);
            GUI.Label(new Rect(28f, 396f, 480f, 22f),
                ok ? "배치 가능 — Enter" : Reason(verdict, cursor.Faults), bodyStyle);
            GUI.color = previous;

            GUI.Label(new Rect(28f, 420f, 480f, 22f),
                $"놓인 모듈 {LastShiftCompartments.ModuleCount}", bodyStyle);
            GUI.Label(new Rect(28f, 442f, 480f, 40f),
                "Tab 모듈 · Z/X 회전 · 방향키 이동(1m) · Enter 확정 · Delete 해제 · Esc 닫기", bodyStyle);

            if (lastResult.Length > 0)
                GUI.Label(new Rect(28f, 488f, 480f, 40f), lastResult, bodyStyle);
        }

        /// <summary>
        /// 왜 안 들어가는가. <b>사유를 다 적는다</b> — 하나만 적으면 그걸 고칠 때마다 다음
        /// 사유를 새로 만나고, 몇 개가 남았는지가 화면에서 안 보인다(판정기가 사유를 모아서
        /// 돌려주는 것과 같은 이유다).
        /// </summary>
        public static string Reason(in LastShiftPlacementVerdict verdict, LastShiftPlacementFault faults)
        {
            var text = string.Empty;

            void Add(string reason) => text = text.Length == 0 ? reason : text + " · " + reason;

            var rejection = verdict.Rejection;
            if ((rejection & LastShiftPlacementRejection.OverlapsPlacement) != 0) Add("다른 방과 겹친다");
            if ((rejection & LastShiftPlacementRejection.OverlapsHullInterior) != 0) Add("선체를 파고든다");
            if ((rejection & LastShiftPlacementRejection.ChainBroken) != 0) Add("선체까지 사슬이 안 닿는다");
            if ((rejection & LastShiftPlacementRejection.ChainTooDeep) != 0) Add("사슬이 너무 깊다");
            if ((rejection & LastShiftPlacementRejection.EgressOverLimit) != 0) Add("이탈 한도를 넘는다");

            if ((faults & LastShiftPlacementFault.ParentMissing) != 0) Add("붙을 상대가 없다");
            if ((faults & LastShiftPlacementFault.DoorOffOwnFace) != 0) Add("문이 자기 벽에서 벗어났다");
            if ((faults & LastShiftPlacementFault.DoorOffParentFace) != 0) Add("문이 벽에 안 닿는다");
            if ((faults & LastShiftPlacementFault.DoorOutsideParentSpan) != 0) Add("문이 벽 끝을 지나쳤다");

            return text.Length == 0 ? "배치 가능" : text;
        }
    }
}
