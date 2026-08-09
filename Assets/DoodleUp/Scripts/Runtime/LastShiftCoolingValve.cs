using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 냉각실 수동 순환 밸브 — <c>C-3</c> 유지 동사(hold-to-sustain).
    /// 기획 정본은 <c>docs/interaction-verb-diversification-v1.md</c> §4.3 이다.
    ///
    /// <b>왜 새 동사가 필요했는가.</b> §3 이 문법 축을 넷으로 갈라 보니 "시간 형태 = 붙잡음"
    /// 과 "소비 대상 = 사람" 두 칸이 비어 있었고, 그 둘은 같은 동사 하나로 채워진다. 기존 아홉
    /// 동사 중 사람을 자리에 묶는 것이 하나도 없었다 — 조종석 <c>hold 8s</c> 조차 걸어 두고
    /// 떠나는 것이라, <c>2</c>인 플레이에서 "한 명을 못 쓴다" 는 비용이 발생하는 순간이
    /// 존재하지 않았다.
    ///
    /// <b>왜 <see cref="LastShiftZoneDoor"/>·<see cref="LastShiftDeckHatch"/> 가 아닌가.</b>
    /// 그 둘은 <c>토글</c>이다 — 한 번 누르면 상태가 남고 누른 사람은 떠난다. 밸브는 상태를
    /// 남기지 않는다(§4.3 "손을 떼거나 밸브에서 벗어나면 즉시 <c>0</c>"). 토글 위에 얹으면
    /// "켜 두고 떠나기" 가 즉시 최적해가 되어 이 동사가 채우려던 칸이 도로 빈다.
    ///
    /// <b>상태 정본은 <see cref="LastShiftSandboxController"/> 다.</b> 문·해치와 같은 이유이며,
    /// 이 컴포넌트는 좌표·사거리·연출만 갖는다. 밸브가 자기 홀더 목록을 따로 들면 서버와
    /// 클라이언트의 "지금 잡혀 있는가" 가 갈리고, 그 값이 곧 열 tick 의 분기라 두 쪽 열이
    /// 다르게 흐른다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftCoolingValve : MonoBehaviour
    {
        /// <summary>손잡이 중심 높이. 서 있는 눈높이(<c>1.65</c>)보다 낮은 가슴께다.</summary>
        public const float HandleHeight = 1.05f;

        /// <summary>우현 벽 안쪽 면에서 손잡이 중심까지. 몸이 벽에 끼지 않을 만큼만 띄운다.</summary>
        public const float WallStandoffZ = 0.45f;

        /// <summary>
        /// 냉각실 선미 끝(<c>x = 5</c>)에서 밸브까지. <b>이 값이 <c>1.1</c> 인 것은 미관이 아니라
        /// 사거리 배타성이다.</b>
        ///
        /// 냉각실은 <c>5m</c> 밖에 안 되는데 양 끝에 문이 있고 문 사거리가 <c>1.8m</c> 라,
        /// x 만 보면 밸브가 들어갈 자리가 없다(<c>x ≤ 1.8</c> 과 <c>x ≥ 3.2</c> 가 문 차지다).
        /// 실제로 겹치지 않는 근거는 <b>문 사거리의 z 창</b>이다 — 개구부 <c>2</c>(<c>x = 0</c>)는
        /// <c>z = +2.2</c>, 개구부 <c>3</c>(<c>x = 5</c>)은 <c>z = -2.2</c> 에 있고 각각
        /// <c>±1.8</c> 만 인정한다. 이 밸브는 <c>x = 3.9</c> · <c>z = +2.55</c> 라 앞 문과는 x 로,
        /// 뒤 문과는 z 로 갈린다. <c>LastShiftVerbDemandTests</c> 가 이 배타성을 고정한다.
        ///
        /// 냉각 스택(<c>x 1.4..3.6</c>, 같은 벽)과도 겹치지 않는다 — 그 선미 쪽 끝에서
        /// <c>0.3m</c> 떨어져 붙어 있고, 그림상 스택에 딸린 수동 우회 밸브로 읽힌다.
        /// </summary>
        public const float SternStandoffX = 1.1f;

        /// <summary>
        /// 조작 사거리(xz 평면). <see cref="LastShiftDeckHatch.ReachDistance"/> 와 같은 <c>1.2m</c> 다.
        ///
        /// 잡기 사거리(<see cref="LastShiftPlayerController.GrabDistance"/> <c>2.2</c>)보다 짧아야
        /// "부품을 잡으려다 밸브를 잡는" 사고가 안 난다. 냉각통 정위치
        /// (<see cref="LastShiftShipDimensions.CoolingNominal"/>)와는 <c>4.1m</c> 떨어져 있어
        /// 두 사거리가 겹치지 않는다 — 이 거리가 §4.3 이 요구한 "밸브를 잡으면 냉각통을 못
        /// 가져온다" 를 좌표로 성립시킨다.
        /// </summary>
        public const float ReachDistance = 1.2f;

        /// <summary>
        /// 밸브가 놓인 자리. 냉각실(<c>x 0..5</c>) 선미 쪽 우현 벽이며 냉각 스택 옆이다.
        ///
        /// <b>구역 한가운데가 아니라 벽에 붙는 것이 중요하다.</b> 붙잡은 사람은 이동할 수 없고
        /// (§4.3 제약), 방 한가운데면 그 사람이 냉각실 동선 — 개구부 <c>2</c>(<c>z = +2.2</c>)에서
        /// 개구부 <c>3</c>(<c>z = -2.2</c>)으로 비스듬히 가로지르는 그 선 — 위에 못 박혀 서서
        /// 다른 승무원의 통행을 막는다.
        ///
        /// 씬 좌표의 최종 확정은 §7-5 로 <c>game-planning</c> 후속 카드에 남아 있다. 여기 값은
        /// 사거리 배타성(<see cref="SternStandoffX"/>)과 §4.3 의 거리 계산이 함께 성립하는
        /// 초기값이다.
        /// </summary>
        public static Vector3 Position => new(
            LastShiftShipDimensions.RoomMaxX(LastShiftZone.Cooling) - SternStandoffX,
            HandleHeight,
            LastShiftShipDimensions.RoomMaxZ(LastShiftZone.Cooling) - WallStandoffZ);

        /// <summary>밸브가 속한 구역. 프롬프트와 로그가 같은 이름을 쓴다.</summary>
        public static LastShiftZone Zone => LastShiftZoneAtlas.Resolve(Position);

        [SerializeField] private Transform lever;

        private LastShiftSandboxController sandbox;

        /// <summary>0 = 놓은 자리, 1 = 붙잡아 돌린 자리. 연출 전용이며 판정에 안 쓴다.</summary>
        private float engageAmount;

        private LastShiftSandboxController Sandbox =>
            sandbox != null ? sandbox : sandbox = FindFirstObjectByType<LastShiftSandboxController>();

        /// <summary>
        /// 지금 누군가 잡고 있는가. sandbox 가 없는 최소 조립에서는 <c>false</c> 다 —
        /// 상태 정본이 없으면 효과도 없어야 한다.
        /// </summary>
        public bool IsHeld => Sandbox != null && Sandbox.IsCoolingValveHeld;

        public float EngageAmount => engageAmount;

        public void Configure(Transform valveLever)
        {
            lever = valveLever;
            SnapToState();
        }

        private void Awake()
        {
            SnapToState();
        }

        public void SnapToState()
        {
            engageAmount = IsHeld ? 1f : 0f;
            ApplyLeverPose();
        }

        private void Update()
        {
            var target = IsHeld ? 1f : 0f;
            if (Mathf.Approximately(engageAmount, target)) return;

            // 손잡이는 개폐물이 아니라 손의 위치 표시라, 문 <c>0.8초</c> 가 아니라 눈에 보이는
            // 즉시성 쪽으로 짧게 잡는다. 효과 자체는 이미 tick 첫 프레임부터 걸려 있다.
            engageAmount = Mathf.MoveTowards(engageAmount, target, Time.deltaTime * 6f);
            ApplyLeverPose();
        }

        private void ApplyLeverPose()
        {
            if (lever != null) lever.localRotation = Quaternion.Euler(0f, 0f, -70f * engageAmount);
        }

        /// <summary>이 위치에서 밸브에 손이 닿는가. 해치와 같은 이유로 y 를 보지 않는다.</summary>
        public static bool IsWithinReach(Vector3 position)
        {
            var valve = Position;
            return Mathf.Abs(position.x - valve.x) <= ReachDistance &&
                   Mathf.Abs(position.z - valve.z) <= ReachDistance;
        }

        /// <summary>
        /// 이 위치에서 조작할 수 있는 밸브. 밸브는 배에 하나뿐이지만 문·해치와 같은 형태의
        /// 진입점을 둔다 — 프롬프트 호출부가 셋을 같은 방식으로 묻고, 밸브가 둘이 되는 날
        /// 여기 한 곳만 바뀐다.
        /// </summary>
        public static LastShiftCoolingValve FindOperable(Vector3 position)
        {
            if (!IsWithinReach(position)) return null;
            return FindFirstObjectByType<LastShiftCoolingValve>();
        }
    }
}
