using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 갑판 하부 우회 통로와 에어록의 좌표 정본. 기획 정본은
    /// <c>docs/corridor-4p-redesign-v1.md</c> §5(개념)와 §23(경로 확정)이다.
    ///
    /// <b>왜 갑판 아래인가.</b> §23.2 가 실측으로 닫은 결론이다 — 평면에는 자리가 없다.
    /// 우현은 수경재배·산소재생실이 우회로가 지나야 할 <c>x</c> 구간을 막고, 좌현은 벽이
    /// 아니라 전장 전체에 걸친 창이다(§21.2 에서 서버/통신실을 우현으로 뒤집은 것과 같은
    /// 제약). <c>y &lt; 0</c> 만 비어 있었고, 폭 <c>0.9m</c>·웅크림 이동이라는 §5 의 기존
    /// 전제와 오히려 더 맞는다.
    ///
    /// <b>이 통로의 존재 이유는 대안 경로다</b>(<c>concept-draft.md</c> §9 의 4인 게이트).
    /// §1 이 짚은 대로 방 증설만으로는 위상이 일직선이라 대안 경로가 안 생기고, 오히려
    /// 강제 통과 지점이 늘어 병목이 심해진다. 그래서 이 덕트는 <b>전력실·냉각실 둘 다를</b>
    /// 건너뛴다 — 두 방 내부에는 진입점을 두지 않는다(§5).
    ///
    /// <b>비용이 없으면 장식이다.</b> §5 가 못박은 대로 산소 비용이 있어야 "급할 때만 쓰는
    /// 진짜 우회로" 가 되고, 없으면 주 통로 설계(배플·게이지·판독 3단) 전체가 의미를 잃는다.
    /// 그래서 이 공간은 비가압이고(<see cref="IsUnpressurized"/>), 안에 있는 동안
    /// <see cref="LastShiftCrewOxygen"/> 이 예비 산소를 태운다. 웅크림 속도
    /// (<see cref="LastShiftShipPhysics.CrouchSpeed"/>)가 두 번째 비용축이다.
    ///
    /// 축 규약은 선체와 같다 — x = 전장, z = 전폭, y = 높이. 좌표는 전부 선체 치수에서
    /// 파생한다. 리터럴을 적으면 전장이 또 바뀔 때(36→38 이 이미 한 번 있었다) 덕트만
    /// 제자리에 남아 진입점이 방 밖으로 빠진다.
    /// </summary>
    public static class LastShiftBypassDuct
    {
        /// <summary>
        /// 덕트 단면(폭·높이 공통). §5 확정값 <c>0.9m</c> 이고
        /// <see cref="LastShiftShipPhysics.CrouchHeight"/> 와 같은 값이어야 한다 —
        /// 웅크린 승무원이 지나는 최소 단면이 곧 이 통로의 정의다.
        /// </summary>
        public const float Section = LastShiftShipPhysics.CrouchHeight;

        /// <summary>덕트 판 두께. 선체와 같다.</summary>
        public const float PanelThickness = LastShiftShipDimensions.HullThickness;

        /// <summary>
        /// 덕트 바닥의 y. 갑판 슬래브(두께 <see cref="LastShiftShipDimensions.HullThickness"/>)
        /// 아래로 여유 <c>0.1m</c> 를 두고 내려간다.
        ///
        /// 여기서 갑판까지 올라오는 높이가 <c>1.2m</c> 이고, 저중력 점프 정점
        /// <c>1.494m</c>(§23.6 실측) 안이라 사다리 같은 새 조작 동사가 필요 없다.
        /// 여유가 <c>0.294m</c> 로 얇아 승강구 바닥에 단을 하나 둔다(<see cref="StepHeight"/>).
        /// </summary>
        public const float FloorY = -(LastShiftShipDimensions.HullThickness + 0.1f + Section);

        /// <summary>덕트 천장 안쪽 면의 y.</summary>
        public const float CeilingY = FloorY + Section;

        /// <summary>
        /// 승강구 바닥의 단 높이. <c>CharacterController.stepOffset</c> 기본값과 같아서
        /// 점프가 아니라 <b>걸어서</b> 오르내리는 높이다(§23.6 권고). 급할 때 점프 타이밍을
        /// 놓쳐도 나올 수 있고, 하강도 <c>1.2m</c> 낙하가 아니라 두 단이 된다.
        /// </summary>
        public const float StepHeight = 0.3f;

        /// <summary>
        /// 덕트가 달리는 z. 선체 중심에서 살짝 우현으로 밀어 <see cref="ForeShaftZ"/> 와
        /// 함께 <c>L</c> 자를 만든다.
        /// </summary>
        public const float RunZ = 1.5f;

        /// <summary>선수 쪽 승강구의 z. <see cref="RunZ"/> 와 달라야 꺾임이 생긴다.</summary>
        public const float ForeShaftZ = -1.5f;

        /// <summary>
        /// 선수 쪽 진입점 x. §5 가 정한 대로 <b>조종석 방 안</b>(개구부 0 근처)이고,
        /// 전력실·냉각실 내부가 아니다. 방 끝에서 <c>1m</c> 들어간 자리다.
        /// </summary>
        public static float ForeShaftX => LastShiftShipDimensions.RoomMaxX(LastShiftZone.Cockpit) - 1f;

        /// <summary>선미 쪽 진입점 x. 산소실 방 안(개구부 4 근처)이다.</summary>
        public static float AftShaftX => LastShiftShipDimensions.RoomMinX(LastShiftZone.LifeSupport) + 1f;

        /// <summary>
        /// 꺾임 횟수. §8 미결 4 에 대한 답이고 <b>1</b> 이다(§23.4). <c>2</c>회로 늘려도
        /// 경로 길이가 같아 통행 비용이 안 늘고, 관통선은 <c>1</c>회로 이미 차단된다 —
        /// 근거 없는 꺾임을 더할 이유가 없다. 우회로의 비용은 산소와 웅크림 속도가 전담한다.
        /// </summary>
        public const int BendCount = 1;

        /// <summary>에어록 한 변. §17.4 표의 <c>3×3×3</c> 이다.</summary>
        public const float AirlockSize = 3f;

        /// <summary>
        /// 에어록 중심 x. <c>L</c> 자 모서리(선수 승강구와 같은 x)에서 아래로 분기한다 —
        /// §23.5 가 확정한 자리다.
        /// </summary>
        public static float AirlockCenterX => ForeShaftX;

        /// <summary>에어록 중심 z. 모서리의 z 다.</summary>
        public const float AirlockCenterZ = RunZ;

        /// <summary>
        /// 에어록 천장 y. 덕트 바닥에 붙어 <b>안쪽 해치</b>가 된다. 바깥 해치는 그 <c>3m</c>
        /// 아래 배 밑면이다 — 같은 층에 뒀다면 이중 해치 자리를 따로 만들어야 했는데,
        /// 갑판 하부에서는 위아래로 자연스럽게 나뉜다(§23.5).
        /// </summary>
        public const float AirlockCeilingY = FloorY;

        public const float AirlockFloorY = AirlockCeilingY - AirlockSize;

        /// <summary>
        /// 덕트·에어록은 비가압이다. §5 가 <c>SuitOxygen</c> 소모를 규정했으므로 그 안은
        /// 진공과 같이 다루고, 승강구는 압력 경계가 된다.
        ///
        /// <b>압력존에는 안 들어간다</b>(§24, 4구역 고정). <c>ZonePressure</c> 배열에 슬롯을
        /// 만들지 않고, 대신 "여기 있으면 진공" 이라는 판정만 준다 — 구역을 늘리면
        /// <c>Resolve()</c>·게이지·<c>SIMUL_ZONES</c>·<c>RG-1</c> 이 전부 따라와야 한다.
        /// </summary>
        public const bool IsUnpressurized = true;

        /// <summary>
        /// 이 좌표가 덕트 또는 에어록 안인가. <see cref="LastShiftZoneAtlas.Resolve"/> 는 x
        /// 하나로만 구역을 정하므로 갑판 아래를 구분하지 못한다 — 그대로 두면 덕트 안
        /// 승무원이 머리 위 방의 압력을 그대로 받아 산소를 안 태운다.
        /// </summary>
        public static bool Contains(Vector3 position)
        {
            if (position.y > CeilingY || position.y < AirlockFloorY) return false;

            if (position.y >= FloorY)
            {
                var half = Section * 0.5f;
                // 선수 수직 구간(z 로 달리는 짧은 다리)
                if (Mathf.Abs(position.x - ForeShaftX) <= half &&
                    position.z >= Mathf.Min(ForeShaftZ, RunZ) - half &&
                    position.z <= Mathf.Max(ForeShaftZ, RunZ) + half) return true;

                // 선미로 달리는 긴 구간
                return Mathf.Abs(position.z - RunZ) <= half &&
                       position.x >= ForeShaftX - half &&
                       position.x <= AftShaftX + half;
            }

            var airlockHalf = AirlockSize * 0.5f;
            return Mathf.Abs(position.x - AirlockCenterX) <= airlockHalf &&
                   Mathf.Abs(position.z - AirlockCenterZ) <= airlockHalf;
        }

        /// <summary>
        /// 두 승강구를 잇는 통행 거리. 주 통로와 비교해 우회로가 실제로 더 먼지 재는 값이고,
        /// 테스트가 이 성질을 고정한다 — 우회로가 더 짧아지면 주 통로가 장식이 된다.
        /// </summary>
        public static float TravelDistance =>
            Mathf.Abs(RunZ - ForeShaftZ) + Mathf.Abs(AftShaftX - ForeShaftX);

        /// <summary>주 통로로 같은 두 지점을 잇는 직선 거리.</summary>
        public static float MainCorridorDistance => Mathf.Abs(AftShaftX - ForeShaftX);
    }
}
