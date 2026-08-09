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

        /// <summary>
        /// 덕트 껍데기 판의 두께. 갑판 슬래브 아래 남은 여유(<see cref="FloorY"/> 식의 <c>0.1</c>)가
        /// 그대로 판 두께다.
        ///
        /// <b>선체 두께를 그대로 쓸 수 없다.</b> <c>0.2</c> 로 두면 천장 판이 슬래브를 파고들고,
        /// 피하려고 안쪽으로 밀면 내부 높이가 웅크림 높이(<c>0.9</c>) 아래로 내려가 통로가 자기
        /// 정의를 못 지킨다 — 여유가 애초에 <c>0.1</c> 인 것이 이 값의 근거다.
        /// </summary>
        public const float PanelThickness = -CeilingY - LastShiftShipDimensions.HullThickness;

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

        /// <summary>승강구 개수. 양 끝 둘이고, 이 값이 <see cref="LastShiftDeckHatch"/> 의 해치 수다.</summary>
        public const int ShaftCount = 2;

        /// <summary>선수 승강구의 번호. 조종석 방 안쪽이다.</summary>
        public const int ForeShaft = 0;

        /// <summary>선미 승강구의 번호. 산소실 방 안쪽이다.</summary>
        public const int AftShaft = 1;

        public static float ShaftX(int shaft) => shaft <= ForeShaft ? ForeShaftX : AftShaftX;

        /// <summary>승강구의 z. 선수는 꺾인 다리 끝, 선미는 긴 구간 끝이라 서로 다르다.</summary>
        public static float ShaftZ(int shaft) => shaft <= ForeShaft ? ForeShaftZ : RunZ;

        /// <summary>갑판 윗면의 y. 승강구가 뚫리는 면이고 해치가 놓이는 면이다.</summary>
        public const float DeckY = 0f;

        /// <summary>
        /// 승강구 입구의 좌표. 씬 빌더가 갑판 구멍·해치를, 런타임이 조작 사거리를 여기서 뽑는다 —
        /// 두 벌이 되면 그림상 뚫린 자리와 조작되는 자리가 어긋난다.
        /// </summary>
        public static Vector3 ShaftMouth(int shaft) => new(ShaftX(shaft), DeckY, ShaftZ(shaft));

        /// <summary>
        /// 이 구역 안에 승강구가 있는가. 갑판 슬래브를 뚫는 쪽에서 쓴다. 번호를 직접 적지 않고
        /// <see cref="LastShiftZoneAtlas.Resolve"/> 로 되묻는 이유는 진입점 x 가 방 치수 파생이라
        /// 선체가 또 늘어나면 어느 구역에 뚫려야 하는지도 같이 움직이기 때문이다.
        /// </summary>
        public static bool TryShaftInZone(LastShiftZone zone, out Vector3 mouth)
        {
            for (var shaft = 0; shaft < ShaftCount; shaft++)
            {
                var candidate = ShaftMouth(shaft);
                if (LastShiftZoneAtlas.Resolve(candidate) != zone) continue;
                mouth = candidate;
                return true;
            }

            mouth = default;
            return false;
        }

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
        /// 승강구 목 — 덕트 천장에서 갑판까지의 짧은 수직 구간. <see cref="Contains"/> 가
        /// <see cref="CeilingY"/> 에서 잘리므로 이 구간이 판정에서 빈다.
        ///
        /// <b>기하로만 본다.</b> 해치가 닫혀 있으면 여기 설 수 없으므로 개폐 상태를 안 봐도 된다 —
        /// 진공 판정을 매 tick 도는 자리에 씬 조회를 들이지 않는 것이 이 선택의 이유다.
        /// 갑판 위(<c>y = 0</c>, 발밑 기준)는 <b>안</b> 잡는다. 닫힌 해치 판 위에 선 승무원은
        /// 발이 정확히 갑판 면이고, 그걸 잡으면 방 안에서 산소가 타기 시작한다.
        /// </summary>
        public static bool ShaftContains(Vector3 position)
        {
            if (position.y >= DeckY || position.y < FloorY) return false;

            var half = Section * 0.5f;
            for (var shaft = 0; shaft < ShaftCount; shaft++)
            {
                if (Mathf.Abs(position.x - ShaftX(shaft)) <= half &&
                    Mathf.Abs(position.z - ShaftZ(shaft)) <= half) return true;
            }

            return false;
        }

        /// <summary>
        /// 이 좌표의 승무원이 비가압 공간에 있는가. 덕트·에어록 본체와 승강구 목을 합쳐 본다 —
        /// <see cref="LastShiftSandboxController.IsZoneVacuum(Vector3)"/> 가 부르는 단일 진입점이다.
        /// </summary>
        public static bool IsUnpressurizedSpace(Vector3 position) =>
            IsUnpressurized && (Contains(position) || ShaftContains(position));

        /// <summary>
        /// 에어록 안쪽 해치가 지금 봉인인가. <b>예전에는 <c>const true</c> 였고 그 옆에 "EVA 감압
        /// 시퀀스는 별도 카드" 라고 적혀 있었다 — <see cref="LastShiftAirlock"/> 이 그 카드다.</b>
        ///
        /// 상수를 상태로 바꾸면서도 이 이름을 남기는 이유는, 이 값을 읽는 쪽이 묻는 것이
        /// "지금 어느 단계인가" 가 아니라 <b>"갑판 아래로 뚫려 있는가"</b> 하나이기 때문이다.
        /// 단계는 에어록이 알고, 여기서 필요한 것은 그 결론뿐이다.
        /// </summary>
        public static bool AirlockInnerHatchSealed => !LastShiftAirlock.IsInnerHatchOpen;

        /// <summary>
        /// 에어록 안 계단의 단 수. 안쪽 해치가 열리면 최저점이 에어록 바닥으로 <c>3m</c>
        /// 내려가는데, 그 <c>3m</c> 를 한 번에 뛰어오를 수는 없다(점프 정점 <c>1.49m</c>).
        ///
        /// <b>사다리를 안 만드는 것이 요점이다.</b> 승강구가 새 조작 동사 대신 단
        /// (<see cref="StepHeight"/>)을 쓴 것과 같은 선택이고(§23.6), 단 둘이면 상승이
        /// <see cref="AirlockStepRise"/> 로 갈라져 점프 정점 안에 들어온다.
        /// </summary>
        public const int AirlockStepCount = 2;

        /// <summary>에어록 안에서 한 단을 오르는 높이. 바닥 → 단 → 단 → 덕트 바닥으로 균등하다.</summary>
        public const float AirlockStepRise = AirlockSize / (AirlockStepCount + 1);

        /// <summary>
        /// 갑판 구멍으로 떨어진 물건이 닿는 가장 낮은 바닥. 승강구를 개통하면서 실제로 위험해지는
        /// 것은 "저중력에서 뜬 물건이 회수 불가가 된다" 하나이고, 그 답이 이 값이다.
        ///
        /// <b>안쪽 해치가 열리면 최저점이 에어록 바닥으로 내려간다.</b> 이건 회귀가 아니라
        /// 설계이고, 세 가지가 함께 그것을 손실이 아니게 만든다 — (1) 인터록상 갑판 승강구
        /// 해치와 동시에 열리지 않으므로(<see cref="LastShiftAirlock.CanOpenInner"/>) 갑판에서
        /// 떨어진 물건이 그리로 갈 길 자체가 없고, (2) 그래도 덕트 안 물건은 떨어질 수 있는데
        /// 에어록 계단이 <see cref="RecoveryRise"/> 를 점프 정점 안으로 잘라 두며,
        /// (3) 안쪽 해치가 열리는 것은 기항뿐이라 <c>300</c>초 시계가 안 돈다.
        /// </summary>
        public static float DeepestFallY => AirlockInnerHatchSealed ? FloorY : AirlockFloorY;

        /// <summary>
        /// 최저점에서 갑판까지 되올라오는 경로에서 <b>한 번에 뛰어야 하는 최대 상승.</b>
        /// 예전에는 최저점이 하나뿐이라 총 상승과 같았는데, 에어록이 열리면서 경로가 두 구간
        /// (에어록 계단 → 덕트 바닥 → 승강구 단 → 갑판)이 되어 <b>가장 높은 한 걸음</b>이
        /// 판정 대상이 됐다. 이 값이 점프 정점 안이면 회수는 우회일 뿐 손실이 아니다.
        /// </summary>
        public static float RecoveryRise => Mathf.Max(
            DeckY - FloorY - StepHeight,
            AirlockInnerHatchSealed ? 0f : AirlockStepRise);

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
