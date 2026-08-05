using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선체 치수의 정본. docs/ship-scale-and-density-v1.md 의 확정 치수를 여기 한 곳에 두고
    /// 씬 빌더·구역 판정·안전 경계·스폰·시뮬레이션이 전부 여기서 파생한다.
    ///
    /// 이 파일이 생긴 이유는 12.5m → 36m 확대다. 그 전에는 같은 치수가 씬 빌더에만 리터럴로
    /// 35곳 흩어져 있었고, 그 중 같은 숫자 <c>4f</c> 가 어떤 자리에서는 구역 원점이고 다른
    /// 자리에서는 바닥 폭이었다. 일괄 치환이 불가능하고 하나만 빠뜨리면 조용히 어긋난다.
    /// 다음 치수 조정이 실제로 상수 두어 개로 끝나려면 파생 관계를 코드로 적어 두어야 한다.
    ///
    /// 축 규약 — x = 전장(구역이 늘어선 축), z = 전폭, y = 높이.
    /// 치수는 전부 <b>내부</b> 기준이고 선체 판은 그 바깥에 붙는다.
    /// </summary>
    public static class LastShiftShipDimensions
    {
        /// <summary>선체 판 두께. 벽·천장·바닥 판이 모두 이 두께다.</summary>
        public const float HullThickness = 0.2f;

        /// <summary>
        /// 내부 전장(x). 확정 치수 36m — 구역 간 이동 시간을 HoldDuration 8초에서 역산한
        /// 대역 (2.0, 3.6]초 안에 넣기 위한 값이다.
        /// </summary>
        public const float InteriorLength = 36f;

        /// <summary>
        /// 내부 전폭(z). 확정 치수 6.0m — 36m 에 4.9m 면 종횡비 7.3:1 직선 관이 되어
        /// 조종석에서 산소실 끝까지 한눈에 보이고, 국소 정보 규칙이 화면에서 거짓이 된다.
        /// 통로를 방 중심에서 어긋나게 놓아 시선을 꺾으려면 폭에 여유가 필요하다.
        /// </summary>
        public const float InteriorWidth = 6f;

        /// <summary>천장 내면. 정본은 <see cref="LastShiftShipPhysics"/> 다 — 점프 정점이 여기 걸린다.</summary>
        public const float CeilingInnerHeight = LastShiftShipPhysics.CeilingInnerHeight;

        public const float HalfLength = InteriorLength * 0.5f;
        public const float HalfWidth = InteriorWidth * 0.5f;

        /// <summary>선수·선미 끝벽의 x 중심. 판 안쪽 면이 내부 끝과 맞는다.</summary>
        public const float EndWallX = HalfLength + HullThickness * 0.5f;

        /// <summary>좌·우 긴 벽의 z 중심.</summary>
        public const float SideWallZ = HalfWidth + HullThickness * 0.5f;

        /// <summary>끝벽이 덮어야 하는 z 길이. 좌우 벽 바깥면까지 닿아야 모서리에 틈이 없다.</summary>
        public const float EndWallSpan = InteriorWidth + 2f * HullThickness;

        /// <summary>긴 벽이 덮어야 하는 x 길이.</summary>
        public const float SideWallSpan = InteriorLength + 2f * HullThickness;

        /// <summary>
        /// 구역 경계 x. 원점 대칭이고 조종석↔엔진실이 -이 값, 엔진실↔산소실이 +이 값이다.
        /// 7m 는 확정 치수에서 나온다 — 조종석 11m / 엔진실 14m / 산소실 11m 이고 최대:최소가
        /// 1.27배다. 압력 평준화를 부피 가중 없이 정규화 값의 상대 교환으로 두어도 되는 근거가
        /// 이 비율이다. 한 구역이 다른 구역의 3배를 넘으면 그때 EQUALIZE_RATE 를 재검토해야 한다.
        /// </summary>
        public const float ZoneBoundaryX = 7f;

        public static float ZoneMinX(LastShiftZone zone) => zone switch
        {
            LastShiftZone.Cockpit => -HalfLength,
            LastShiftZone.Utility => -ZoneBoundaryX,
            _ => ZoneBoundaryX
        };

        public static float ZoneMaxX(LastShiftZone zone) => zone switch
        {
            LastShiftZone.Cockpit => -ZoneBoundaryX,
            LastShiftZone.Utility => ZoneBoundaryX,
            _ => HalfLength
        };

        public static float ZoneLength(LastShiftZone zone) => ZoneMaxX(zone) - ZoneMinX(zone);

        public static float ZoneCenterX(LastShiftZone zone) => (ZoneMinX(zone) + ZoneMaxX(zone)) * 0.5f;

        // ── 통로와 개구부 ────────────────────────────────────────────────────
        // 여기서 정본으로 두는 것은 통로 폭이 아니라 GAP_Z 다. "통로 폭 ≥ 개구부 폭 × 2" 는
        // 프록시고, 실제 조건은 한 통로 안 두 개구부의 z 구간이 겹치지 않는 것이다. 폭을
        // 리터럴로 두면 개구부 폭이 바뀔 때 그 조건이 조용히 깨진다 — 게다가 간격 0 은
        // 유한 개 레이캐스트로 잡히지 않아 검증기가 PASS 를 뱉는다. 그래서 간격을 먼저
        // 정하고 폭을 거기서 뽑는다.

        /// <summary>
        /// 한 통로 안 두 개구부의 z 구간 간격. 확정 조건 0.4m — 0 이면 두 구간이 한 점에서
        /// 맞닿아 실플레이에서는 안 보이지만 검증이 그 칼날 틈을 못 잡는다.
        /// </summary>
        public const float OpeningGapZ = 0.4f;

        /// <summary>개구부 폭(z). 런타임 정본은 <see cref="LastShiftZoneDoor.OpeningWidth"/> 이고 여기서는 파생 계산에만 쓴다.</summary>
        public const float OpeningWidth = LastShiftZoneDoor.OpeningWidth;

        /// <summary>
        /// 통로 폭(z). 개구부 둘이 <see cref="OpeningGapZ"/> 만큼 떨어져 나란히 들어가는 폭이다.
        /// 확정값 3.6m 이 여기서 나온다 — 1.6 × 2 + 0.4.
        /// </summary>
        public const float PassageWidth = OpeningWidth * 2f + OpeningGapZ;

        /// <summary>통로 길이(x). 판독선 거리와 같은 값이다(기획 §2.3 — 6m).</summary>
        public const float PassageLength = 6f;

        /// <summary>
        /// 통로 중심의 z 오프셋. 통로가 선체 한쪽 벽에 붙으므로 반폭에서 통로 반폭을 뺀 값이다.
        /// 6.0 폭 / 3.6 통로에서 1.2m 다. 통로 A 가 +이 값, 통로 B 가 -이 값이다.
        /// </summary>
        public const float PassageOffsetZ = HalfWidth - PassageWidth * 0.5f;

        /// <summary>통로 중심에서 개구부 중심까지의 z 오프셋. (개구부 폭 + 간격) / 2 = 1.0m.</summary>
        public const float OpeningOffsetZ = (OpeningWidth + OpeningGapZ) * 0.5f;

        /// <summary>개구부 개수. 조종석|통로A|엔진실|통로B|산소실 배치의 접합부가 넷이다.</summary>
        public const int OpeningCount = 4;

        /// <summary>
        /// 개구부 중심 z. 통로 오프셋과 개구부 오프셋의 조합이며 리터럴을 적지 않는다 —
        /// 개구부 폭이나 선체 폭이 바뀌면 네 값이 함께 따라와야 한다.
        ///
        /// 0: 조종석↔통로A (+2.2) / 1: 통로A↔엔진실 (+0.2)
        /// 2: 엔진실↔통로B (-2.2) / 3: 통로B↔산소실 (-0.2)
        ///
        /// 한 통로 안에서 z 가 어긋나 있는 것이 요점이다. 통로 A 의 0↔1 이 겹치면 조종석에서
        /// 엔진실이 그대로 보이고, 그러면 "구역에 가야 진단이 읽힌다" 가 화면에서 거짓이 된다.
        /// </summary>
        public static float OpeningCenterZ(int opening) => opening switch
        {
            0 => PassageOffsetZ + OpeningOffsetZ,
            1 => PassageOffsetZ - OpeningOffsetZ,
            2 => -PassageOffsetZ - OpeningOffsetZ,
            _ => -PassageOffsetZ + OpeningOffsetZ
        };

        /// <summary>개구부 z 구간의 하한.</summary>
        public static float OpeningMinZ(int opening) => OpeningCenterZ(opening) - OpeningWidth * 0.5f;

        /// <summary>개구부 z 구간의 상한.</summary>
        public static float OpeningMaxZ(int opening) => OpeningCenterZ(opening) + OpeningWidth * 0.5f;

        // ── 배 안의 고정 지점 ────────────────────────────────────────────────
        // 전부 구역 중심에서 상대 배치다. 리터럴 x 를 쓰지 않는 이유는 전장이 바뀌면
        // "조종석 안" 이 다른 좌표가 되기 때문이다. 상대로 두면 따라온다.

        /// <summary>
        /// 승무원 시작 위치. 조종석 중심에서 선미 쪽으로 2.4m — 시작하자마자 끝벽을 보고 서
        /// 있지 않으면서 도킹 트리거(반경 1.6) 밖이다.
        /// </summary>
        public static Vector3 SpawnPoint => new(CockpitCenterX + 2.4f, 0.1f, 0f);

        /// <summary>에어락/도킹 지점. 조종석 콘솔 앞이다.</summary>
        public static Vector3 DockingPoint => new(CockpitCenterX - 0.75f, 0.9f, 0f);

        public static float CockpitCenterX => ZoneCenterX(LastShiftZone.Cockpit);
        public static float UtilityCenterX => ZoneCenterX(LastShiftZone.Utility);
        public static float LifeSupportCenterX => ZoneCenterX(LastShiftZone.LifeSupport);

        /// <summary>
        /// 운석 충돌 지점. 조종석 바깥 선체다. 파공 자체는 산소실(PatchPlate 자리)에 생기지만
        /// 충돌은 반대편에서 온다 — 손상 지점과 수리 부품이 같은 자리에 있으면 "가지러 간다"가
        /// 사라진다.
        /// </summary>
        public static Vector3 MeteorImpactPoint => new(CockpitCenterX - 1.3f, 1.1f, 0f);

        // ── 부품 정위치 ──────────────────────────────────────────────────────
        // 부품이 어느 구역에 속하는지가 게임 규칙이다(PatchPlate 가 산소실에 있어야 파공
        // 수리가 "산소실까지 간다" 가 된다). 그래서 좌표가 아니라 구역 중심 기준으로 둔다.

        public static Vector3 BatteryNominal => new(UtilityCenterX + 1.7f, 0.38f, 0.8f);
        public static Vector3 CoolingNominal => new(UtilityCenterX, 0.55f, -1.3f);
        public static Vector3 PatchPlateNominal => new(LifeSupportCenterX + 0.5f, 0.65f, -1.6f);

        /// <summary>
        /// Tether 받침대. 조종석↔엔진실 경계 바로 안쪽(조종석 쪽)이다. Tether 는 어떤
        /// 프리셋에서도 loose 로 남는 유일한 상시 grab 대상이므로 시작 위치에서 걸어서
        /// 금방 닿아야 한다.
        /// </summary>
        public static Vector3 TetherRackPosition => new(-ZoneBoundaryX - 0.55f, 0.60f, -1.28f);

        public static Vector3 TetherRackScale => new(0.5f, 1.2f, 0.9f);

        public static Vector3 TetherNominal =>
            TetherRackPosition + new Vector3(0f, TetherRackScale.y * 0.5f + 0.125f, 0f);

        // ── 시뮬레이션이 쓰는 거리 척도 ──────────────────────────────────────
        // 손상 판정은 "충돌 지점에서 얼마나 가까운가"를 보는데, 그 '가까움'의 기준이 배
        // 크기에 걸린다. 고정 5m/6m 를 남겨 두면 배가 2.9배가 되는 순간 모든 근접 항이
        // 0 으로 붕괴해 지배 문제가 아예 판정되지 않는다. 12.5m 시절 값(5/6/4)을
        // 전장 대비 비율로 옮겨 적은 것이므로 그 시절 동작과 상대적으로 같다.

        /// <summary>승무원↔충돌 지점 근접 척도. 옛 5m / 12.5m.</summary>
        public static float CrewProximityRange => InteriorLength * 0.40f;

        /// <summary>부품↔충돌 지점 노출 척도. 옛 6m / 12.5m.</summary>
        public static float ItemExposureRange => InteriorLength * 0.48f;

        /// <summary>정위치 이탈을 1.0 으로 보는 거리. 옛 4m / 12.5m.</summary>
        public static float DisplacementFullScale => InteriorLength * 0.32f;

        /// <summary>
        /// 물건이 이 밖으로 나가면 서버가 제자리로 되돌린다. 선체보다 넉넉히 두어 콜라이더
        /// 틈으로 튄 물건이 스스로 돌아올 여유를 주고, 도보 회수가 불가능한 범위만 막는다.
        /// 고정 16x11x12 를 남겨 두면 36m 배에서는 산소실 물건이 전부 경계 밖으로 판정돼
        /// 매 tick 제자리로 튕겨 돌아온다.
        /// </summary>
        public static Bounds ItemSafetyBounds => new(
            new Vector3(0f, CeilingInnerHeight * 0.78f, 0f),
            new Vector3(InteriorLength + 4f, 11f, InteriorWidth + 6f));
    }
}
