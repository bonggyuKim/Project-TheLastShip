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

        // ── 방과 통로 ────────────────────────────────────────────────────────
        // 여기서 구역(zone)과 방(room)을 구분한다. 통로가 생기기 전에는 둘이 같아서 구역만
        // 있으면 됐지만, 지금은 갈라졌다. "조종석 안" 을 구역으로 재면 통로 A 도 조종석이라
        // 통로 한가운데에 놓인 좌표가 검사를 통과한다 — 스폰이 실제로 그렇게 됐었다.
        // 물건과 사람이 실제로 설 수 있는 자리를 정할 때는 반드시 방 범위를 봐야 한다.
        //
        //   방   조종석 -18~-10 / 엔진실 -4~+4 / 산소실 +10~+18   (각 8m)
        //   통로 A -10~-4 / B +4~+10                              (각 6m)
        //   구역 조종석 -18~-4 / 엔진실 -4~+4 / 산소실 +4~+18      (14/8/14)

        /// <summary>통로 길이(x). 판독선 거리와 같은 값이다(기획 §2.3 — 6m).</summary>
        public const float PassageLength = 6f;

        /// <summary>방 하나의 x 길이. 전장에서 통로 둘을 빼고 셋으로 나눈다 — (36 - 12) / 3 = 8m.</summary>
        public const float RoomLength = (InteriorLength - 2f * PassageLength) / 3f;

        public static float RoomMinX(LastShiftZone zone) => zone switch
        {
            LastShiftZone.Cockpit => -HalfLength,
            LastShiftZone.Utility => -RoomLength * 0.5f,
            _ => HalfLength - RoomLength
        };

        public static float RoomMaxX(LastShiftZone zone) => RoomMinX(zone) + RoomLength;

        public static float RoomCenterX(LastShiftZone zone) => RoomMinX(zone) + RoomLength * 0.5f;

        /// <summary>통로 x 범위. 0 = 통로 A(조종석↔엔진실), 1 = 통로 B(엔진실↔산소실).</summary>
        public static float PassageMinX(int passage) =>
            passage <= 0 ? RoomMaxX(LastShiftZone.Cockpit) : RoomMaxX(LastShiftZone.Utility);

        public static float PassageMaxX(int passage) => PassageMinX(passage) + PassageLength;

        public static float PassageCenterX(int passage) => PassageMinX(passage) + PassageLength * 0.5f;

        /// <summary>통로 중심 z. 통로 A 가 +쪽, B 가 -쪽 벽에 붙는다.</summary>
        public static float PassageCenterZ(int passage) =>
            passage <= 0 ? PassageOffsetZ : -PassageOffsetZ;

        public static float PassageMinZ(int passage) => PassageCenterZ(passage) - PassageWidth * 0.5f;

        public static float PassageMaxZ(int passage) => PassageCenterZ(passage) + PassageWidth * 0.5f;

        /// <summary>
        /// 구역 경계 x. 원점 대칭이고 조종석↔엔진실이 -이 값, 엔진실↔산소실이 +이 값이다.
        ///
        /// 4m 인 이유는 <b>문이 달리는 자리와 압력 판정 자리가 같아야 하기 때문</b>이다. 통로가
        /// 생기면서 문은 통로 끝(엔진실 방 경계)에 붙었고, 판정 경계를 통로 한가운데(±7)에
        /// 두면 문을 닫아도 그 평면에는 차단물이 없어 압력이 안 끊긴다. 조작 사거리도 3m 밖이라
        /// 문에 손이 닿지 않는다.
        ///
        /// 부피는 조종석 14m / 엔진실 8m / 산소실 14m 로 1.75배 차이다. 3배를 넘으면 그때
        /// EQUALIZE_RATE 를 부피 가중으로 재검토해야 하고, 지금은 안 걸린다.
        ///
        /// <see cref="RoomMaxX"/>(Utility) 와 같은 값이지만 여기서는 const 로 둔다 — 압력 판정의
        /// <see cref="LastShiftZoneAtlas.CockpitMaxX"/> 가 컴파일 타임 상수를 요구하기 때문이다.
        /// 둘이 어긋나지 않는 것은 테스트로 고정한다.
        /// </summary>
        public const float ZoneBoundaryX = RoomLength * 0.5f;

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

        // ── 시선 차단 배플 ───────────────────────────────────────────────────
        // <b>이 볼륨은 장식이 아니라 A3 성립 조건이다. 옮기면 T4 가 FAIL 한다.</b>
        //
        // GAP_Z 0.4 는 축과 나란한 시선만 막는다. 비스듬한 광선은 두 개구부가 z 로 안 겹쳐도
        // 통과한다 — 조종석 우현 (-11, 2.7) 에서 엔진실 중앙을 보면 개구부 0 을 z 2.36 으로,
        // 개구부 1 을 z 0.34 로 지나간다. 방 바닥의 약 21% 가 그런 자리다.
        //
        // 이것을 <b>방 안</b> 가구로 막을 수는 없다. 새는 자리에는 문턱 앞이 포함되고, 개구부 0
        // 자체가 우현 z [1.4, 3.0] 이라 그 띠를 바닥부터 천장까지 막으면 통로 입구가 함께
        // 막힌다(남는 폭 0.10m, 승무원 지름 0.56m). 그래서 차단은 통로 안에서 한다.
        //
        // 통로 한가운데(t = 0.5)에 세우는 이유는 계산에서 나온다. 두 개구부를 모두 지나는
        // 직선은 통로 중앙에서 반드시
        //     z ∈ [ (개구부A 하한 + 개구부B 하한)/2 , (개구부A 상한 + 개구부B 상한)/2 ]
        // 안에 있다. 폭은 개구부 폭과 같은 1.6m 다(선형 보간이라 구간 길이가 보존된다).
        // 그 구간만 막으면 <b>통과 가능한 직선이 하나도 남지 않는다</b> — 유한 개 표본이 아니라
        // 구간 전체다. 반면 걸어서 지나가는 사람은 직선이 아니어도 되므로 양옆 1.0m 씩 남는
        // 자리로 돌아간다. t = 0.5 가 그 남는 폭(양쪽 1.0m)을 최대로 만든다 — 한쪽으로 밀면
        // 좁은 쪽이 0.5m 까지 줄어 승무원이 못 지나간다.

        /// <summary>배플이 막아야 하는 z 중심. 두 개구부 중심의 중점이고 통로 중심과 같다.</summary>
        public static float BaffleCenterZ(int passage) =>
            (OpeningCenterZ(BaffleNearOpening(passage)) + OpeningCenterZ(BaffleFarOpening(passage))) * 0.5f;

        /// <summary>배플 폭(z). 선형 보간이 구간 길이를 보존하므로 개구부 폭과 같다.</summary>
        public const float BaffleWidth = OpeningWidth;

        /// <summary>배플 두께(x). 얇아도 되지만 광선이 아니라 사람이 보기에 설비로 읽힐 두께다.</summary>
        public const float BaffleThickness = 0.4f;

        /// <summary>배플 양옆에 남는 통행 폭. 승무원 지름 0.56m 보다 넉넉해야 한다.</summary>
        public const float BaffleFreeStrip = (PassageWidth - BaffleWidth) * 0.5f;

        /// <summary>통로의 방 쪽 개구부(문이 없는 쪽).</summary>
        public static int BaffleNearOpening(int passage) => passage <= 0 ? 0 : 3;

        /// <summary>통로의 경계 쪽 개구부(문이 달린 쪽).</summary>
        public static int BaffleFarOpening(int passage) => passage <= 0 ? 1 : 2;

        public static float BaffleMinZ(int passage) => BaffleCenterZ(passage) - BaffleWidth * 0.5f;
        public static float BaffleMaxZ(int passage) => BaffleCenterZ(passage) + BaffleWidth * 0.5f;

        // ── 배 안의 고정 지점 ────────────────────────────────────────────────
        // 전부 <b>방</b> 중심에서 상대 배치다. 리터럴 x 를 쓰지 않는 이유는 전장이 바뀌면
        // "조종석 안" 이 다른 좌표가 되기 때문이고, 구역 중심이 아니라 방 중심을 쓰는 이유는
        // 통로가 생기면서 둘이 갈라졌기 때문이다. 구역 중심은 조종석 -11 / 산소실 +11 인데
        // 그 자리는 방 끝에서 1m 떨어진 자리이고, 거기서 몇 미터만 더 밀면 통로 안이다.
        // 실제로 스폰(+2.4)이 -8.6 으로 통로 한가운데에 들어가 있었고, 구역으로 재는 검사는
        // 통로도 조종석 구역이라 그걸 통과시켰다.

        /// <summary>
        /// 승무원 시작 위치. 조종석 <b>방</b> 중심에서 선미 쪽으로 2.4m = x -11.6 이다.
        /// 시작하자마자 끝벽을 보고 서 있지 않으면서 도킹 트리거 밖이다.
        ///
        /// 여유(방 기준):
        ///   통로 A 입구(x -10)까지        1.6m
        ///   도킹 트리거(x -14.75, r 1.6)  3.15m 떨어짐 → 여유 1.55m
        ///   4인 슬롯 z 폭 ±1.275, 방 z ±3.0 → 여유 1.725m
        /// 셋 중 가장 좁은 것이 1.55m 다. 예전 배치는 통로 입구까지 여유가 0.1m 였다.
        /// </summary>
        public static Vector3 SpawnPoint => new(CockpitCenterX + 2.4f, 0.1f, 0f);

        /// <summary>에어락/도킹 지점. 조종석 콘솔 앞이다.</summary>
        public static Vector3 DockingPoint => new(CockpitCenterX - 0.75f, 0.9f, 0f);

        /// <summary>
        /// 방 중심 x. 이름은 구역이지만 값은 방이다 — 부르는 자리가 전부 "그 방 안 어딘가"를
        /// 뜻하기 때문이다. 압력 판정처럼 구역 범위가 필요한 곳은 <see cref="ZoneCenterX"/> 를
        /// 직접 쓴다. 기획 §2.3 이 확정한 조종석 -14 / 산소실 +14 가 이 값이다.
        /// </summary>
        public static float CockpitCenterX => RoomCenterX(LastShiftZone.Cockpit);
        public static float UtilityCenterX => RoomCenterX(LastShiftZone.Utility);
        public static float LifeSupportCenterX => RoomCenterX(LastShiftZone.LifeSupport);

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
        /// Tether 받침대. 조종석 <b>방</b> 안, 선미 쪽 좌현 벽에 붙인다. Tether 는 어떤
        /// 프리셋에서도 loose 로 남는 유일한 상시 grab 대상이므로 시작 위치에서 걸어서
        /// 금방 닿아야 한다 — 그 의미를 지키려고 방 안에서 스폰 쪽 끝을 골랐다.
        ///
        /// 예전 좌표 <c>(-ZoneBoundaryX - 0.55, 0.60, -1.28)</c> 는 통로가 생기기 전
        /// "경계 바로 안쪽" 이었는데, 통로가 그 자리를 가져가면서 x -4.55 는 통로 A 의 x 범위
        /// 안이고 z -1.28 은 통로 z 범위 [-0.6, +3.0] 밖 — 즉 통로 옆 솔리드 안이었다.
        ///
        /// 새 좌표 (-10.6, 0.60, -1.3). <b>진짜 제약은 방 안이라는 것이 아니라 사거리다</b> —
        /// 씬 빌더가 적어 둔 대로 Tether 는 스폰 자리에서 조준해 바로 잡히는 거리
        /// (<see cref="LastShiftPlayerController.GrabDistance"/> 2.2m) 안에 있어야 하고,
        /// PlayMode 테스트가 "스폰 조준에 Tether 가 먼저 걸린다"를 전제로 서 있다. 방 좌현
        /// 벽(z -2.35)에 붙이면 그 거리가 2.74m 가 되어 조건이 깨진다. 그래서 벽은 벽이되
        /// 스폰과 같은 x 대역인 방 선미 벽 쪽을 골랐다.
        ///
        /// 여유(전부 실측):
        ///   조준점(-11.6, 1.65, 0) → 정위치(-10.6, 1.325, -1.3)   1.67m  (한도 2.2, 여유 0.53)
        ///   스폰(-11.6, 0.1, 0) → 받침대                          1.72m  (겹침 방지 하한 1.6 밖)
        ///   받침대 선미면 -10.35 → 방 끝 -10                       0.35m
        ///   받침대 좌현면 -1.75 → 좌현 벽 -3.0                     1.25m
        ///   도킹 트리거(-14.75, r 1.6) → 받침대 최근접면 -10.85    4.15m  (여유 2.55)
        /// 개구부 0 은 우현 z [1.4, 3.0] 이라 이 받침대(좌현)와 겹치지 않고, 차단 볼륨도 우현이다.
        /// </summary>
        public static Vector3 TetherRackPosition => new(
            RoomMaxX(LastShiftZone.Cockpit) - 0.6f, 0.60f, -1.3f);

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
