using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 구역 경계의 문. 기획 v0.3 §2.2.1 / §2.2.2 의 격리 동사를 실제 조작으로 만든다.
    ///
    /// 압력 평준화의 정본은 <see cref="LastShiftSandboxController"/> 의 문 상태이고, 이 컴포넌트는
    /// 그 값을 향해 <b>따라가기만</b> 한다. 방향을 하나로 둔 이유는 네트워크다 — 클라이언트에서는
    /// sandbox 가 꺼져 있고(<c>enabled = IsServer</c>) 문 상태가 스냅샷으로만 들어오므로, 문이
    /// 자기 상태를 따로 들고 있으면 서버와 클라이언트의 문이 서로 다른 그림을 그린다.
    ///
    /// 열림 플래그는 <b>조작하는 순간</b> 뒤집히고 판은 0.8초에 걸쳐 움직인다. 문서의 "즉시 효과 =
    /// 압력 교환 정지" 를 그대로 옮긴 것이다. 판이 다 닫힐 때까지 기다렸다 끊으면 격리를 눌러 놓고
    /// 0.8초 동안 아무 일도 일어나지 않아, 이 동사가 "지금 끊었다" 로 읽히지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftZoneDoor : MonoBehaviour
    {
        // ── 문 구멍 규격 ────────────────────────────────────────────────────
        // 씬 빌더가 벌크헤드를 이 규격만큼 비우고, 이 컴포넌트가 같은 값으로 판과 차단 콜라이더를
        // 만든다. 두 벌이 되면 "그림상 열려 있는데 못 지나가는" 문이 생긴다.

        /// <summary>통과 지점의 폭(z). 어깨 폭(CharacterController 지름 0.56)의 약 3배다.</summary>
        public const float OpeningWidth = 1.6f;

        /// <summary>문 높이. 서 있는 승무원(1.7)이 고개를 숙이지 않고 지나간다.</summary>
        public const float OpeningHeight = 2.2f;

        /// <summary>벌크헤드 두께. 문 판과 차단 콜라이더도 같은 두께를 쓴다.</summary>
        public const float PanelThickness = 0.15f;

        /// <summary>판이 열릴 때 z 로 물러나는 거리. 판 하나가 구멍 절반을 덮으므로 반폭이다.</summary>
        public const float PanelTravel = OpeningWidth * 0.5f;

        /// <summary>
        /// 판을 벌크헤드 면 위로 띄우는 x 오프셋. 0 으로 두면 열렸을 때 판이 옆 벽 안으로
        /// 파고들어 서로 z-fighting 한다. 한쪽 면에 매단 미닫이로 두면 열린 판이 벽에
        /// 겹쳐 붙은 것으로 읽힌다.
        /// </summary>
        public const float PanelFaceOffset = 0.13f;

        /// <summary>
        /// 이 경계의 광장 문. 판·문틀·인방·차단 콜라이더·조작 사거리가 전부 여기서 나온다.
        ///
        /// <b>평면 축이 문마다 다르다.</b> 전력실 문은 <c>z = -6</c>, 냉각실 문은 <c>z = +6</c>,
        /// 산소실 문만 <c>x = +6</c> 이다 — 일자 스파인에서 셋 다 <c>x</c> 평면이던 전제가
        /// 방사형에서 깨졌다. 씬 빌더는 <c>z</c> 평면 문을 <b>yaw 90°</b> 로 세워 이 컴포넌트의
        /// 로컬 계산(판이 로컬 <c>z</c> 로 물러난다)이 축과 무관하게 그대로 성립하게 한다.
        /// </summary>
        public static LastShiftPlazaDoor DoorOf(int boundary) => LastShiftZoneAtlas.BoundaryDoor(boundary);

        /// <summary>이 문이 덮는 구멍의 <b>평면 위</b> 중심. 평면이 x 면 z 값이고, 아니면 x 값이다.</summary>
        public static float CenterOf(int boundary) => DoorOf(boundary).Center;

        /// <summary>이 문이 덮는 구멍의 평면 위 중심.</summary>
        public float Center => CenterOf(boundary);

        /// <summary>문 중심의 평면 좌표. 씬 배치와 조작 대상 판정이 같은 점을 봐야 한다.</summary>
        public Vector2 Waypoint => DoorOf(boundary).Waypoint;

        [SerializeField] private int boundary;
        [SerializeField] private Transform panelFore;
        [SerializeField] private Transform panelAft;
        [SerializeField] private BoxCollider blocker;

        private LastShiftSandboxController sandbox;

        /// <summary>0 = 완전히 닫힘, 1 = 완전히 열림. 판 위치와 통행 가능 여부가 이 값에서 나온다.</summary>
        private float openAmount = 1f;

        public int Boundary => boundary;

        /// <summary>
        /// 시뮬레이션이 보는 열림 여부. 판이 아직 움직이는 중이어도 이 값이 정본이다.
        /// sandbox 는 지연 조회한다 — EditMode 조립과 씬 빌드에서는 Awake 순서가 보장되지 않고,
        /// 그때 캐시가 null 로 굳으면 문이 영원히 "항상 열림" 이 된다.
        /// </summary>
        public bool IsOpen => Sandbox == null || Sandbox.IsDoorOpen(boundary);

        private LastShiftSandboxController Sandbox =>
            sandbox != null ? sandbox : sandbox = FindFirstObjectByType<LastShiftSandboxController>();

        public float OpenAmount => openAmount;

        /// <summary>판이 아직 목표에 닿지 않았는가. 연출 확인용이며 판정에는 쓰지 않는다.</summary>
        public bool IsMoving => !Mathf.Approximately(openAmount, IsOpen ? 1f : 0f);

        /// <summary>이 경계가 잇는 두 구역 이름. 프롬프트에 그대로 쓴다.</summary>
        public string BoundaryLabel =>
            $"{LastShiftZoneAtlas.ShortLabelOf(LastShiftZoneAtlas.LowZoneOf(boundary))}↔" +
            $"{LastShiftZoneAtlas.ShortLabelOf(LastShiftZoneAtlas.HighZoneOf(boundary))}";

        public void Configure(int boundaryIndex, Transform fore, Transform aft, BoxCollider doorBlocker)
        {
            boundary = boundaryIndex;
            panelFore = fore;
            panelAft = aft;
            blocker = doorBlocker;
        }

        private void Awake()
        {
            openAmount = IsOpen ? 1f : 0f;
            ApplyPanelPose();
        }

        private void Update()
        {
            var target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(openAmount, target))
            {
                // 0.8초에 0→1 을 지나가므로 속도는 1/0.8 이다. 열 때와 닫을 때가 같은 시간이어야
                // "닫는 데 얼마나 걸리는지" 를 한 번 배우면 그대로 쓸 수 있다.
                var step = Time.deltaTime / LastShiftRecoveryTuning.ZoneDoorTransitionSeconds;
                openAmount = Mathf.MoveTowards(openAmount, target, step);
                ApplyPanelPose();
            }
        }

        private void ApplyPanelPose()
        {
            var offset = PanelTravel * openAmount;
            if (panelFore != null)
                panelFore.localPosition = new Vector3(PanelFaceOffset, OpeningHeight * 0.5f, -PanelTravel * 0.5f - offset);
            if (panelAft != null)
                panelAft.localPosition = new Vector3(PanelFaceOffset, OpeningHeight * 0.5f, PanelTravel * 0.5f + offset);

            // 통행 차단은 완전히 닫혔을 때만 건다. 움직이는 콜라이더로 막으면 CharacterController 가
            // 판에 끼거나 밀려나고, 문틈으로 빠져나가는 순간이 사라진다. 여기서 막고 싶은 것은
            // "닫힌 문은 못 지나간다" 하나이지 "닫히는 동안 밀려난다" 가 아니다.
            if (blocker != null) blocker.enabled = openAmount <= 0.001f;
        }

        /// <summary>
        /// 문 조작. 성공하면 true.
        ///
        /// 살아 있는 승무원인지를 여기서 본다. 사망 시 <see cref="LastShiftPlayerController"/> 가
        /// 꺼지므로 솔로에서는 입력 자체가 오지 않지만, 네트워크에서는 서버 RPC 가 별도 경로로
        /// 들어온다. 조작 진입점이 둘이면 조건도 둘이어야 하는 것이 아니라, 진입점이 모이는
        /// 이 자리에 한 번 두는 것이 맞다(기획 §4.4 — 유령은 배를 만질 수 없다).
        /// </summary>
        public bool TryOperate(LastShiftPlayerController crewMember)
        {
            if (crewMember == null || Sandbox == null) return false;
            var crew = crewMember.GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead)
            {
                Debug.Log($"[LAST_SHIFT_DOOR] boundary={boundary} action=toggle result=REJECT reason=crew-dead");
                return false;
            }
            if (!IsWithinReach(crewMember.transform.position)) return false;

            Sandbox.SetDoorOpen(boundary, !IsOpen);
            return true;
        }

        /// <summary>
        /// 조작 사거리. 문 평면에서 <b>법선 방향</b>으로 떨어진 거리와 평면 위 폭으로 본다.
        /// 안팎을 구분하지 않는 것이 요점이다 — 격리는 걸어 잠그는 쪽에서만 풀 수 있으면 안
        /// 된다. 갇힌 쪽에서 열 수 없으면 그건 격리가 아니라 사형이고, 문서가 격리를
        /// "되돌리기 가능" 으로 둔 이유다.
        ///
        /// <b>축을 문에서 뽑는다.</b> <c>x</c> 로 고정해 두면 <c>z</c> 평면에 선 전력실·냉각실
        /// 문이 광장 어디에서나 사거리 안으로 판정되고, 두 문이 서로의 프롬프트를 가로챈다.
        /// </summary>
        public bool IsWithinReach(Vector3 position)
        {
            var door = DoorOf(boundary);
            var through = door.PlaneIsX ? position.x : position.z;
            var free = door.PlaneIsX ? position.z : position.x;
            return Mathf.Abs(through - door.Plane) <= ReachDistance &&
                   Mathf.Abs(free - door.Center) <= OpeningWidth * 0.5f + 1.0f;
        }

        /// <summary>문 앞이라고 인정하는 x 거리. 잡기 사거리(2.2)보다 짧게 두어 대상이 겹치지 않는다.</summary>
        public const float ReachDistance = 1.8f;

        /// <summary>
        /// 살아 있는 문 목록. 프롬프트가 매 프레임 <see cref="FindOperable"/> 를 부르므로
        /// 씬 전수 조회를 그때마다 돌리면 안 된다. 문은 씬 빌드 시점에만 생기고 사라지므로
        /// 자기 등록/해제로 목록을 유지하는 편이 정확하고 싸다.
        /// </summary>
        private static readonly System.Collections.Generic.List<LastShiftZoneDoor> Live = new();

        private void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        private void OnDisable()
        {
            Live.Remove(this);
        }

        /// <summary>
        /// 이 위치에서 조작할 수 있는 문. 등록된 문만 훑으므로 문 수에 비례하고 씬 크기와는
        /// 무관하다 — 배가 커져 문이 늘어도 비용이 오브젝트 수를 따라가지 않는다.
        /// </summary>
        public static LastShiftZoneDoor FindOperable(Vector3 position)
        {
            // EditMode 조립처럼 OnEnable 이 아직 돌지 않은 구성에서는 목록이 비어 있을 수 있다.
            // 그때만 전수 조회로 되돌아간다.
            var doors = Live.Count > 0
                ? Live.ToArray()
                : FindObjectsByType<LastShiftZoneDoor>(FindObjectsSortMode.None);
            LastShiftZoneDoor best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var door in doors)
            {
                if (door == null || !door.IsWithinReach(position)) continue;
                var distance = LastShiftZoneAtlas.DistanceToBoundaryPlane(door.boundary, position);
                if (distance >= bestDistance) continue;
                best = door;
                bestDistance = distance;
            }
            return best;
        }
    }
}
