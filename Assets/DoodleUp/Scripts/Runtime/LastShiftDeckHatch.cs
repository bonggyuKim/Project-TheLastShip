using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 승강구 해치의 개폐 상태. 문(<see cref="LastShiftDoorState"/>)과 <b>따로</b> 든다.
    ///
    /// 같은 배열에 얹지 않는 것이 요점이다. <c>LastShiftDoorState</c> 는 압력 평준화가 매 tick
    /// 읽는 <b>구역 경계</b> 배열이고(§24 가 <c>4</c>구역으로 고정한 그것), 거기에 항목을 늘리면
    /// <c>BoundaryCount</c>·<c>Resolve()</c>·게이지·<c>SIMUL_ZONES</c>·<c>RG-1</c> 이 전부 따라온다.
    /// 승강구는 구역과 구역을 잇는 것이 아니라 <b>구역과 비가압 공간</b>을 잇는다.
    /// </summary>
    public struct LastShiftHatchState
    {
        public bool ForeOpen;
        public bool AftOpen;

        /// <summary>
        /// 기본은 전부 <b>닫힘</b>이다. 문의 기본값(<see cref="LastShiftDoorState.AllOpen"/>)과
        /// 반대인 것은 실수가 아니다 — 열린 해치는 갑판에 뚫린 구멍이고, 저중력에서 뜬 물건이
        /// 아무도 열지 않은 구멍으로 떨어지면 그건 플레이어의 판단이 아니라 사고다.
        /// </summary>
        public static LastShiftHatchState AllClosed => default;

        public bool this[int shaft]
        {
            get => shaft <= LastShiftBypassDuct.ForeShaft ? ForeOpen : AftOpen;
            set
            {
                if (shaft <= LastShiftBypassDuct.ForeShaft) ForeOpen = value;
                else AftOpen = value;
            }
        }
    }

    /// <summary>
    /// 갑판 승강구의 해치. 기획 정본은 <c>docs/corridor-4p-redesign-v1.md</c> §23.6 이다.
    ///
    /// <b>왜 <see cref="LastShiftZoneDoor"/> 가 아닌가.</b> 그쪽은 구역 경계 전용이다 — 경계 번호로
    /// 자기 자리를 잡고(<c>BoundaryX</c>), 개폐가 곧 두 구역 사이 압력 교환의 on/off 이며, 판이 z 로
    /// 미끄러지는 수직 벽이다. 승강구는 셋 다 다르다. 경계 번호가 없고(갑판 하부는 구역이 아니다),
    /// 열려도 압력 교환 대상이 없고(§24 — 덕트는 <c>ZonePressure</c> 슬롯이 없다), 판이 수평이다.
    /// 억지로 얹으면 <c>boundary</c> 에 가짜 번호가 들어가고 평준화가 그 번호를 읽는다.
    ///
    /// <b>그래도 압력 경계다.</b> §5 가 우회 통로에 <c>SuitOxygen</c> 소모를 규정했으므로 덕트는
    /// 비가압이고, 그러면 갑판과 덕트 사이의 이 판이 압력 경계다. §23.6 이 <c>DOOR_TIME</c> 을
    /// 기존 문과 같은 <c>0.8초</c> 로 둔 근거가 그것이다 — 수평이냐 수직이냐는 경계 판정과 무관하다.
    /// 이 프로젝트에서 압력 경계가 구역 경계 밖으로 나가는 첫 사례이고, 그 경계선을 여기 한 자리에
    /// 둔다: <b>진공 판정은 하되 <c>ZonePressure</c> 에는 안 들어간다.</b>
    ///
    /// 상태 정본은 <see cref="LastShiftSandboxController"/> 이고 이 컴포넌트는 그 값을 향해
    /// 따라가기만 한다 — 문과 같은 이유다. 클라이언트에서는 sandbox 가 꺼져 있고 상태가 스냅샷으로만
    /// 들어오므로, 해치가 자기 상태를 따로 들면 서버와 클라이언트의 갑판 구멍이 서로 다르게 뚫린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftDeckHatch : MonoBehaviour
    {
        /// <summary>
        /// 갑판에 뚫리는 구멍의 한 변. 덕트 단면과 <b>같은 값이어야 한다</b> — 좁으면 웅크려도
        /// 못 들어가고, 넓으면 통로보다 갑판이 더 뚫려 저중력 낙하 위험만 커진다.
        /// </summary>
        public const float OpeningSpan = LastShiftBypassDuct.Section;

        /// <summary>
        /// 해치 판 두께. 열렸을 때 판이 갑판 위에 얹혀 있으므로 <c>CharacterController.stepOffset</c>
        /// 기본값(<c>0.3</c>)보다 한참 낮아야 한다 — 넘으면 열어 둔 해치 판이 걸림돌이 된다.
        /// </summary>
        public const float PanelThickness = 0.08f;

        /// <summary>판이 열릴 때 x 로 물러나는 거리. 한 짝이 구멍을 통째로 덮으므로 한 변만큼이다.</summary>
        public const float PanelTravel = OpeningSpan;

        /// <summary>
        /// 조작 사거리(xy 아닌 xz 평면). <b>y 를 안 본다</b>가 요점이다 — 갑판 위에서도, 덕트 안에서도
        /// 같은 해치를 조작할 수 있어야 한다. 아래에서 못 열면 우회로에 들어간 승무원이 자기 뒤로
        /// 닫힌 해치에 갇히고, 그건 우회로가 아니라 함정이다(<see cref="LastShiftZoneDoor.IsWithinReach"/>
        /// 가 안팎을 구분하지 않는 것과 같은 이유).
        ///
        /// 문(<c>1.8</c>)보다 짧다. 문은 벽면 하나를 기준으로 재지만 해치는 <c>0.9m</c> 구멍
        /// 하나이고, 무엇보다 승강구가 <b>방 안에</b> 있다(§5) — 넉넉하게 잡으면 스폰 지점과
        /// 부품 자리가 사거리에 들어와 서 있기만 해도 해치 프롬프트가 뜬다. 실제로
        /// <c>1.6</c> 에서는 조종석 스폰 지점(선수 승강구에서 <c>1.6m</c>)이 걸렸다.
        /// 잡기 사거리(<c>2.2</c>)보다도 짧아야 "부품을 잡으려다 해치를 여는" 사고가 안 난다.
        /// </summary>
        public const float ReachDistance = 1.2f;

        [SerializeField] private int shaft;
        [SerializeField] private Transform panel;
        [SerializeField] private BoxCollider blocker;

        private LastShiftSandboxController sandbox;

        /// <summary>0 = 완전히 닫힘, 1 = 완전히 열림.</summary>
        private float openAmount;

        public int Shaft => shaft;

        /// <summary>이 해치가 뚫린 갑판 좌표.</summary>
        public Vector3 Mouth => LastShiftBypassDuct.ShaftMouth(shaft);

        /// <summary>
        /// 시뮬레이션이 보는 열림 여부. sandbox 는 지연 조회한다 — EditMode 조립과 씬 빌드에서는
        /// Awake 순서가 보장되지 않고, 그때 캐시가 null 로 굳으면 해치가 영원히 안 열린다.
        ///
        /// sandbox 가 없는 최소 조립의 기본값이 <b>닫힘</b>인 것은 문(항상 열림)과 반대다.
        /// 갑판 구멍의 안전한 쪽은 막혀 있는 쪽이다.
        /// </summary>
        public bool IsOpen => Sandbox != null && Sandbox.IsHatchOpen(shaft);

        private LastShiftSandboxController Sandbox =>
            sandbox != null ? sandbox : sandbox = FindFirstObjectByType<LastShiftSandboxController>();

        public float OpenAmount => openAmount;

        /// <summary>판이 아직 목표에 닿지 않았는가. 연출 확인용이며 판정에는 쓰지 않는다.</summary>
        public bool IsMoving => !Mathf.Approximately(openAmount, IsOpen ? 1f : 0f);

        /// <summary>이 승강구가 열리는 방의 이름. 프롬프트에 그대로 쓴다.</summary>
        public string ShaftLabel => LastShiftZoneAtlas.ShortLabelOf(LastShiftZoneAtlas.Resolve(Mouth));

        public void Configure(int shaftIndex, Transform hatchPanel, BoxCollider hatchBlocker)
        {
            shaft = shaftIndex;
            panel = hatchPanel;
            blocker = hatchBlocker;
            // 조립 직후 바로 맞춘다. AddComponent 가 Awake 를 먼저 돌리므로 그때는 판도 차단면도
            // 아직 null 이고, 여기서 안 맞추면 씬 빌드가 "판은 닫힌 자리인데 차단면은 꺼진"
            // 프리팹을 구워 저장한다 — 갑판에 구멍이 뚫린 채로 배포되는 것과 같다.
            SnapToState();
        }

        private void Awake()
        {
            SnapToState();
        }

        /// <summary>
        /// 판과 차단 콜라이더를 지금 상태에 즉시 맞춘다. <see cref="Update"/> 가 안 도는 자리
        /// (씬 빌드, EditMode 조립)에서 쓰는 경계이며 개폐 소요 <c>0.8초</c>를 건너뛴다.
        /// </summary>
        public void SnapToState()
        {
            openAmount = IsOpen ? 1f : 0f;
            ApplyPanelPose();
        }

        private void Update()
        {
            var target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target)) return;

            // §23.6 이 DOOR_TIME 을 기존 문과 같게 둔 그 값이다. 상수를 따로 만들지 않는 것이
            // 그 결론을 코드에서 지키는 자리다 — 갈라 두면 한쪽만 조정되고 문서가 거짓이 된다.
            var step = Time.deltaTime / LastShiftRecoveryTuning.ZoneDoorTransitionSeconds;
            openAmount = Mathf.MoveTowards(openAmount, target, step);
            ApplyPanelPose();
        }

        private void ApplyPanelPose()
        {
            // 판은 갑판 위로 미끄러진다. 슬래브 아래로 밀어 넣으면 덕트 천장(-0.3)까지 0.1m 뿐이라
            // 판이 들어갈 자리가 없고, 결국 슬래브를 뚫고 지나가는 그림이 된다.
            if (panel != null)
                panel.localPosition = new Vector3(PanelTravel * openAmount, PanelThickness * 0.5f, 0f);

            // 문과 같은 규칙 — 완전히 닫혔을 때만 막는다. 여기서는 그 규칙이 곧 낙하 규칙이 된다:
            // 여는 순간부터 구멍이고, 판 위에 있던 물건은 그때 떨어진다. 0.8초 동안 판이 물러나는
            // 것을 보면서도 못 막게 두는 편이 "열었더니 떨어졌다" 를 배우기에 낫다.
            if (blocker != null) blocker.enabled = openAmount <= 0.001f;
        }

        /// <summary>
        /// 해치 조작. 성공하면 true. 살아 있는 승무원인지를 여기서 본다 —
        /// <see cref="LastShiftZoneDoor.TryOperate"/> 와 같은 이유로 진입점이 모이는 자리에 한 번 둔다.
        /// </summary>
        public bool TryOperate(LastShiftPlayerController crewMember)
        {
            if (crewMember == null || Sandbox == null) return false;
            var crew = crewMember.GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead)
            {
                Debug.Log($"[LAST_SHIFT_HATCH] shaft={shaft} action=toggle result=REJECT reason=crew-dead");
                return false;
            }
            if (!IsWithinReach(crewMember.transform.position)) return false;

            // 인터록 — 에어록 안쪽 해치가 열려 있으면 갑판 구멍을 못 연다
            // (<see cref="LastShiftAirlock"/> 주석의 셋째 조건). 반대 방향은 에어록 쪽이 막는다.
            // 여기서 막는 것이 <see cref="LastShiftBypassDuct.DeepestFallY"/> 를 지킨다 —
            // 둘이 동시에 열리면 갑판에서 떨어진 물건이 에어록 바닥까지 3m 더 내려간다.
            if (!IsOpen && LastShiftAirlock.IsInnerHatchOpen)
            {
                Debug.Log($"[LAST_SHIFT_HATCH] shaft={shaft} action=toggle result=REJECT reason=airlock-inner-open");
                return false;
            }

            Sandbox.SetHatchOpen(shaft, !IsOpen);
            return true;
        }

        /// <summary>이 위치에서 이 해치를 조작할 수 있는가. 높이를 안 보는 이유는 <see cref="ReachDistance"/> 참조.</summary>
        public bool IsWithinReach(Vector3 position)
        {
            var mouth = Mouth;
            return Mathf.Abs(position.x - mouth.x) <= ReachDistance &&
                   Mathf.Abs(position.z - mouth.z) <= ReachDistance;
        }

        /// <summary>
        /// 살아 있는 해치 목록. 프롬프트가 매 프레임 <see cref="FindOperable"/> 를 부르므로
        /// 씬 전수 조회를 그때마다 돌리지 않는다 — 문과 같은 구조다.
        /// </summary>
        private static readonly System.Collections.Generic.List<LastShiftDeckHatch> Live = new();

        private void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        private void OnDisable()
        {
            Live.Remove(this);
        }

        /// <summary>
        /// 이 위치에서 조작할 수 있는 해치. 문과 사거리가 겹치지 않는다 — 승강구는 방 한가운데
        /// (경계에서 <c>6m</c> 넘게 떨어진 자리)에 있고 문 사거리는 경계에서 <c>1.8m</c> 다.
        /// 그래서 호출부에서 어느 쪽을 먼저 보든 결과가 같다.
        /// </summary>
        public static LastShiftDeckHatch FindOperable(Vector3 position)
        {
            // EditMode 조립처럼 OnEnable 이 아직 돌지 않은 구성에서는 목록이 비어 있을 수 있다.
            var hatches = Live.Count > 0
                ? Live.ToArray()
                : FindObjectsByType<LastShiftDeckHatch>(FindObjectsSortMode.None);
            LastShiftDeckHatch best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var hatch in hatches)
            {
                if (hatch == null || !hatch.IsWithinReach(position)) continue;
                var mouth = hatch.Mouth;
                var distance = new Vector2(position.x - mouth.x, position.z - mouth.z).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = hatch;
                bestDistance = distance;
            }
            return best;
        }
    }
}
