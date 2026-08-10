using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선체 치수의 정본. 좌표 정본은 <see cref="LastShiftPlazaLayout"/>(중앙 광장 허브 배치,
    /// <c>docs/central-plaza-hub-layout-v1.md</c> §2.2·§2.3)이고 이 파일은 그것을 <b>파생</b>한다.
    ///
    /// <b>일자 스파인이 폐지됐다.</b> 예전 이 파일은 <c>38 x 6m</c> 직선 관에 방 넷·통로 둘·
    /// 배플 둘·개구부 다섯을 스스로 계산해 들고 있었고, 그 체계 전체가 §3.4 에서 폐기됐다.
    /// 남은 것은 좌표를 <b>안 적는</b> 파생뿐이다 — 방 범위는 광장 발자국표에서 뽑고, 압력
    /// 경계는 광장 변의 압력문 셋이며, 껍질은 <see cref="LastShiftHullShell"/> 의 정원이다.
    ///
    /// <b>"선체 내부" 를 덮는 직사각형은 이제 <c>경계 상자</c>다.</b> 방사형 발자국은 플러스
    /// 모양이라 배를 정확히 덮는 사각형이 없다 — 그래서 <see cref="HalfLength"/>·
    /// <see cref="HalfWidth"/> 는 <b>고정 공간 일곱을 담는 최소 상자</b>의 반폭이고, "이 밖은
    /// 확실히 배 밖" 이라는 뜻만 갖는다. <b>"이 안은 배 안" 이라는 뜻은 없다</b> — 팔 사이 빈
    /// 사분면이 상자 안에 있기 때문이다. 침범·붙임 판정처럼 그 구분이 필요한 자리는 사각형이
    /// 아니라 <see cref="LastShiftPlazaLayout.Footprints"/> 를 직접 훑는다.
    ///
    /// 축 규약 — x = 전장(음수가 선수), z = 전폭(음수가 좌현), y = 높이.
    /// 치수는 전부 <b>내부</b> 기준이고 선체 판은 그 바깥에 붙는다.
    /// </summary>
    public static class LastShiftShipDimensions
    {
        /// <summary>선체 판 두께. 벽·천장·바닥 판이 모두 이 두께다.</summary>
        public const float HullThickness = 0.2f;

        /// <summary>
        /// 내부 전장(x). <b>고정 발자국 경계 상자</b>의 x 폭이고 확정 치수 <c>28m</c> 다
        /// (조종석 방 선수 <c>-14</c> ~ 산소실 선미 <c>+14</c>).
        ///
        /// 원반 외경 <c>38m</c> 와 다른 것이 정상이다 — 그 차이가 §7 의 확장 여지이고,
        /// 고정 방 바깥 면에 가장 얇은 모듈이 한 겹 붙어도 원반 안이라는 §9.2 의 결론이
        /// 그 여백에서 나온다.
        /// </summary>
        /// <b><c>28 -> 32</c></b>(2026-08-10). 기능실 확장으로 발자국 경계 상자가 커졌다 —
        /// 조종석 <c>-16</c> ~ 산소실 <c>+16</c>. 이 값이 안 따라오면 선체가 발자국보다 좁아
        /// 방이 배 밖으로 판정된다.
        public const float InteriorLength = 32f;

        /// <summary>
        /// 내부 전폭(z). 경계 상자의 z 폭이며 <b>원점 대칭이 아니다</b> — 에어록 홀이
        /// <c>-12</c> 까지 내려오고 냉각실은 <c>+11</c> 에서 끝난다. 반폭 하나로 접는 자리가
        /// 많아 <b>더 먼 쪽</b>(<c>12</c>)의 두 배로 둔다: 상자가 넓은 쪽으로 틀리면 "배 밖"
        /// 판정이 보수적일 뿐이지만, 좁은 쪽으로 틀리면 에어록 홀이 배 밖으로 판정된다.
        /// </summary>
        /// <b><c>24 -> 28</c></b>(2026-08-10). 전력실 <c>-14</c> ~ 냉각실 <c>+14</c>.
        public const float InteriorWidth = 28f;

        /// <summary>천장 내면. 정본은 <see cref="LastShiftShipPhysics"/> 다 — 점프 정점이 여기 걸린다.</summary>
        public const float CeilingInnerHeight = LastShiftShipPhysics.CeilingInnerHeight;

        public const float HalfLength = InteriorLength * 0.5f;
        public const float HalfWidth = InteriorWidth * 0.5f;

        /// <summary>선수·선미 끝벽의 x 중심. 판 안쪽 면이 경계 상자 끝과 맞는다.</summary>
        public const float EndWallX = HalfLength + HullThickness * 0.5f;

        /// <summary>좌·우 벽의 z 중심.</summary>
        public const float SideWallZ = HalfWidth + HullThickness * 0.5f;

        /// <summary>끝벽이 덮어야 하는 z 길이. 좌우 벽 바깥면까지 닿아야 모서리에 틈이 없다.</summary>
        public const float EndWallSpan = InteriorWidth + 2f * HullThickness;

        /// <summary>긴 벽이 덮어야 하는 x 길이.</summary>
        public const float SideWallSpan = InteriorLength + 2f * HullThickness;

        // ── 방 ───────────────────────────────────────────────────────────────
        // 통로가 사라지면서 방과 구역의 구분이 다시 단순해졌다. 압력 구역 넷 중 셋
        // (전력실·냉각실·산소실)은 방 하나가 곧 구역 전체이고, 조종석 구역만 넷
        // (광장·조종석 방·에어록 홀·숙소)의 합집합이다 — 조항 S-1.
        //
        // <b>그래서 x 하나로 방을 못 가른다.</b> 전력실과 냉각실이 같은 x 범위 [-3, +3] 을
        // z 좌우로 나눠 가지므로 z 접근자가 같이 있어야 한다. 예전 파일에 z 접근자가 없던
        // 것은 일자 스파인에서 모든 방이 전폭을 다 썼기 때문이고, 그 전제가 §3.4 에서 깨졌다.

        /// <summary>이 구역을 대표하는 방의 발자국. 조종석 구역은 <b>조종석 방</b>이지 광장이 아니다.</summary>
        public static LastShiftPlazaFootprint RoomOf(LastShiftZone zone) =>
            LastShiftPlazaLayout.Of(LastShiftPlazaLayout.RoomOf(zone));

        public static float RoomMinX(LastShiftZone zone) => RoomOf(zone).MinX;
        public static float RoomMaxX(LastShiftZone zone) => RoomOf(zone).MaxX;
        public static float RoomCenterX(LastShiftZone zone) => (RoomMinX(zone) + RoomMaxX(zone)) * 0.5f;

        public static float RoomMinZ(LastShiftZone zone) => RoomOf(zone).MinZ;
        public static float RoomMaxZ(LastShiftZone zone) => RoomOf(zone).MaxZ;
        public static float RoomCenterZ(LastShiftZone zone) => (RoomMinZ(zone) + RoomMaxZ(zone)) * 0.5f;

        /// <summary>방 하나의 x 길이. 조종석·산소실 <c>8m</c>, 전력실·냉각실 <c>6m</c> 다.</summary>
        public static float RoomLengthOf(LastShiftZone zone) => RoomOf(zone).LengthX;

        /// <summary>방 중심. 벽에 붙이는 것들이 전부 이 점에서 상대 배치된다.</summary>
        public static Vector2 RoomCenter(LastShiftZone zone) =>
            new(RoomCenterX(zone), RoomCenterZ(zone));

        // ── 구역 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 구역이 차지하는 평면 상자. <b>소속 판정에 쓰면 안 된다</b> — 조종석 구역은 광장·
        /// 조종석 방·에어록 홀·숙소의 합집합이라 상자가 실제 점유보다 크고, 그 상자 안에는
        /// 전력실·냉각실도 들어온다. 소속은 <see cref="LastShiftZoneAtlas.ResolveHull"/> 이 답한다.
        /// </summary>
        public static Rect ZoneBounds(LastShiftZone zone) => LastShiftPlazaLayout.ZoneBounds(zone);

        public static float ZoneMinX(LastShiftZone zone) => ZoneBounds(zone).xMin;
        public static float ZoneMaxX(LastShiftZone zone) => ZoneBounds(zone).xMax;
        public static float ZoneLength(LastShiftZone zone) => ZoneBounds(zone).width;
        public static float ZoneCenterX(LastShiftZone zone) => ZoneBounds(zone).center.x;

        // ── 배 안의 고정 지점 ────────────────────────────────────────────────
        // 전부 <b>방</b> 중심에서 상대 배치다(§9.3-5 — 상대 위치를 유지한 채 통째로 이전).
        // 리터럴 좌표를 쓰지 않는 이유는 그대로다: 발자국이 움직이면 "조종석 안" 이 다른
        // 좌표가 된다. 통로가 폐지되면서 "구역 중심이 통로 한가운데" 라는 옛 함정은
        // 사라졌지만, 조종석 구역 중심은 이제 <b>광장·에어록 홀까지 포함한 상자</b>의
        // 중심이라 여전히 방 밖이다 — 그래서 계속 방 중심을 쓴다.

        /// <summary>
        /// 승무원 시작 위치. 조종석 <b>방</b> 중심에서 선미 쪽으로 <c>2.4m</c> = <c>x -7.6</c> 이다.
        /// 시작하자마자 끝벽을 보고 서 있지 않으면서 도킹 트리거 밖이고, 광장 개구부
        /// (<c>x -6</c>)까지 <c>1.6m</c> 남는다 — 옛 배치의 "통로 A 입구까지 1.6m" 와 같은 여유다.
        /// </summary>
        public static Vector3 SpawnPoint => new(CockpitCenterX + 2.4f, 0.1f, 0f);

        /// <summary>에어락/도킹 지점. 조종석 콘솔 앞이다.</summary>
        public static Vector3 DockingPoint => new(CockpitCenterX - 0.75f, 0.9f, 0f);

        /// <summary>방 중심 x. 부르는 자리가 전부 "그 방 안 어딘가" 를 뜻한다.</summary>
        public static float CockpitCenterX => RoomCenterX(LastShiftZone.Cockpit);
        public static float PowerCenterX => RoomCenterX(LastShiftZone.Power);
        public static float CoolingCenterX => RoomCenterX(LastShiftZone.Cooling);
        public static float LifeSupportCenterX => RoomCenterX(LastShiftZone.LifeSupport);

        /// <summary>
        /// 운석 충돌 지점. 조종석 바깥 선체다. 파공 자체는 산소실(PatchPlate 자리)에 생기지만
        /// 충돌은 반대편에서 온다 — 손상 지점과 수리 부품이 같은 자리에 있으면 "가지러 간다"가
        /// 사라진다.
        /// </summary>
        public static Vector3 MeteorImpactPoint => new(CockpitCenterX - 1.3f, 1.1f, 0f);

        // ── 부품 정위치 ──────────────────────────────────────────────────────
        // 부품이 어느 구역에 속하는지가 게임 규칙이다(PatchPlate 가 산소실에 있어야 파공
        // 수리가 "산소실까지 간다" 가 된다). 그래서 좌표가 아니라 방 중심 기준으로 둔다.
        //
        // <b>z 오프셋이 방 폭 안에 들어가는지가 새 제약이다.</b> 전력실·냉각실은 z 폭이
        // 5m(±2.5) 라 옛 ±3 폭 시절 오프셋을 그대로 쓰면 벽을 파고든다.

        public static Vector3 BatteryNominal => new(PowerCenterX + 1.7f, 0.38f, RoomCenterZ(LastShiftZone.Power) + 0.8f);
        public static Vector3 CoolingNominal => new(CoolingCenterX, 0.55f, RoomCenterZ(LastShiftZone.Cooling) - 1.3f);
        public static Vector3 PatchPlateNominal => new(LifeSupportCenterX + 0.5f, 0.65f, -1.6f);

        /// <summary>
        /// Tether 받침대. 조종석 <b>방</b> 안, 광장 쪽 끝의 좌현 벽에 붙인다. Tether 는 어떤
        /// 프리셋에서도 loose 로 남는 유일한 상시 grab 대상이므로 시작 위치에서 걸어서
        /// 금방 닿아야 한다.
        ///
        /// <b>진짜 제약은 방 안이라는 것이 아니라 사거리다</b> — Tether 는 스폰 자리에서
        /// 조준해 바로 잡히는 거리(<see cref="LastShiftPlayerController.GrabDistance"/> <c>2.2m</c>)
        /// 안에 있어야 하고, PlayMode 테스트가 "스폰 조준에 Tether 가 먼저 걸린다"를 전제로
        /// 서 있다. 그래서 스폰과 같은 x 대역인 방 선미 끝(광장 쪽)을 고르고 z 로만 민다.
        ///
        /// <b>상대 위치를 유지한 채 통째로 옮겼다</b>(§9.3-5). 방 선미면에서 <c>0.6m</c> 안쪽,
        /// 좌현으로 <c>1.3m</c> 라는 옛 관계가 그대로다 — 조종석 방 발자국이 <c>x[-18,-10]</c>
        /// 에서 <c>x[-14,-6]</c> 으로 통째로 <c>+4</c> 밀렸을 뿐 형상이 같아서(<c>8 x 6</c>)
        /// 실측 여유가 전부 보존된다.
        ///
        ///   조준점(-7.6, 1.65, 0) → 정위치(-6.6, 1.325, -1.3)   1.67m  (한도 2.2, 여유 0.53)
        ///   스폰(-7.6, 0.1, 0) → 받침대                          1.72m  (겹침 방지 하한 1.6 밖)
        ///   받침대 선미면 -6.35 → 방 끝 -6.0                      0.35m
        ///   받침대 좌현면 -1.75 → 좌현 벽 -3.0                    1.25m
        /// 광장 개구부는 z [-0.8, +0.8] 이라 이 받침대(좌현)와 겹치지 않는다.
        /// </summary>
        public static Vector3 TetherRackPosition => new(
            RoomMaxX(LastShiftZone.Cockpit) - 0.6f, 0.60f, -1.3f);

        public static Vector3 TetherRackScale => new(0.5f, 1.2f, 0.9f);

        public static Vector3 TetherNominal =>
            TetherRackPosition + new Vector3(0f, TetherRackScale.y * 0.5f + 0.125f, 0f);

        // ── 시뮬레이션이 쓰는 거리 척도 ──────────────────────────────────────
        // 손상 판정은 "충돌 지점에서 얼마나 가까운가"를 보는데, 그 '가까움'의 기준이 배
        // 크기에 걸린다. <b>기준을 광장이 아니라 원반 지름으로 옮겼다</b> — 광장은 배의
        // 한 칸이라 그것을 척도로 쓰면 방사형 팔 끝(x ±14)이 전부 척도 밖으로 나간다.
        // 옛 값(38m 전장 기준 15.2 / 18.24 / 12.16)과 같은 크기가 유지되도록 원반 지름
        // 38m 에 같은 비율을 건다.

        /// <summary>승무원↔충돌 지점 근접 척도.</summary>
        public static float CrewProximityRange => LastShiftHullShell.OverallLength * 0.40f;

        /// <summary>부품↔충돌 지점 노출 척도.</summary>
        public static float ItemExposureRange => LastShiftHullShell.OverallLength * 0.48f;

        /// <summary>정위치 이탈을 1.0 으로 보는 거리.</summary>
        public static float DisplacementFullScale => LastShiftHullShell.OverallLength * 0.32f;

        /// <summary>
        /// 물건이 이 밖으로 나가면 서버가 제자리로 되돌린다. 원반 지름보다 넉넉히 두어
        /// 콜라이더 틈으로 튄 물건이 스스로 돌아올 여유를 주고, 도보 회수가 불가능한 범위만
        /// 막는다. <b>광장 치수로 잡으면 안 된다</b> — 그러면 조종석·산소실 물건이 전부 경계
        /// 밖으로 판정돼 매 tick 제자리로 튕겨 돌아온다.
        /// </summary>
        public static Bounds ItemSafetyBounds => new(
            new Vector3(0f, CeilingInnerHeight * 0.78f, 0f),
            new Vector3(LastShiftHullShell.OverallLength + 4f, 11f, LastShiftHullShell.OverallWidth + 4f));
    }
}
