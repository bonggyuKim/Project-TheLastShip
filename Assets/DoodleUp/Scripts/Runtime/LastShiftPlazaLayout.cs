using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>광장 둘레에 놓인 고정 공간. 인덱스 순서는 <see cref="LastShiftPlazaLayout.Footprints"/> 와 같다.</summary>
    public enum LastShiftPlazaSpace
    {
        Plaza = 0,
        CockpitRoom = 1,
        LifeSupportRoom = 2,
        PowerRoom = 3,
        CoolingRoom = 4,
        AirlockHall = 5,
        Quarters = 6
    }

    /// <summary>
    /// 광장 변에 난 구멍의 종류. <b>압력 판정에 들어가는 것은 <see cref="PressureDoor"/> 셋뿐이다</b> —
    /// 나머지 둘(<see cref="PlainDoor"/>)은 같은 구역 안 생활 동선이고, <see cref="Opening"/> 은
    /// 문짝 자체가 없다.
    /// </summary>
    public enum LastShiftPlazaDoorKind
    {
        /// <summary>문 없는 개구부. 조종석↔광장 하나뿐이고, 그래서 둘이 같은 구역이다.</summary>
        Opening = 0,
        PressureDoor = 1,
        PlainDoor = 2
    }

    /// <summary>고정 공간 하나의 평면 발자국. 높이가 같이 있는 이유는 §2.2 가 본선/부속을 천장으로 가르기 때문이다.</summary>
    public readonly struct LastShiftPlazaFootprint
    {
        public LastShiftPlazaFootprint(LastShiftPlazaSpace space, float minX, float maxX,
            float minZ, float maxZ, float height, LastShiftZone zone)
        {
            Space = space;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            Height = height;
            Zone = zone;
        }

        public LastShiftPlazaSpace Space { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        /// <summary>천장 내면. 본선 <c>3.2m</c> / 부속 <c>3.0m</c>.</summary>
        public float Height { get; }

        /// <summary>속한 압력 구역. 광장·에어록 홀·숙소가 조종석 구역인 것이 조항 <c>S-1</c> 이다(§3).</summary>
        public LastShiftZone Zone { get; }

        public float LengthX => MaxX - MinX;
        public float WidthZ => MaxZ - MinZ;
        public float Area => LengthX * WidthZ;

        public bool Contains(float x, float z) =>
            x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        /// <summary>발자국 네 모서리. 이탈 거리와 원반 내접이 둘 다 모서리만 본다.</summary>
        public Vector2 Corner(int index) => index switch
        {
            0 => new Vector2(MinX, MinZ),
            1 => new Vector2(MinX, MaxZ),
            2 => new Vector2(MaxX, MinZ),
            _ => new Vector2(MaxX, MaxZ)
        };
    }

    /// <summary>
    /// 광장 변 위의 구멍 하나. <b>평면이 광장 변과 자기 방 경계에 동시에 얹혀 있다</b> — 그게
    /// "경유 방이 없다" 의 좌표 형태이고, <c>LastShiftPlazaLayoutTests</c> 가 여섯 전부에서 그것을 잰다.
    /// </summary>
    public readonly struct LastShiftPlazaDoor
    {
        public LastShiftPlazaDoor(LastShiftPlazaSpace space, bool planeIsX, float plane,
            float center, LastShiftPlazaDoorKind kind, Vector2 gauge)
        {
            Space = space;
            PlaneIsX = planeIsX;
            Plane = plane;
            Center = center;
            Kind = kind;
            Gauge = gauge;
        }

        public LastShiftPlazaSpace Space { get; }

        /// <summary><c>true</c> 면 평면이 <c>x = Plane</c>, <c>false</c> 면 <c>z = Plane</c> 이다.</summary>
        public bool PlaneIsX { get; }

        public float Plane { get; }

        /// <summary>구멍 중심의 <b>평면 위 좌표</b>. <see cref="PlaneIsX"/> 면 <c>z</c>, 아니면 <c>x</c> 다.</summary>
        public float Center { get; }

        public LastShiftPlazaDoorKind Kind { get; }

        /// <summary>
        /// 게이지 위치. 압력문에만 있고 나머지는 <see cref="Vector2.zero"/> 가 아니라
        /// <see cref="HasGauge"/> 로 가른다 — 원점은 광장 한가운데라 값으로 구분이 안 된다.
        ///
        /// <b>문틀이 아니라 문 너머 방 안쪽 끝벽이다</b>(§4.1). 이 이설이 <c>SIMUL_ZONES ≤ 2</c> 의
        /// 장치 1 이고, 게이지가 보이는 광장 영역을 구멍을 통과하는 쐐기로 줄인다.
        /// </summary>
        public Vector2 Gauge { get; }

        public bool HasGauge => Kind == LastShiftPlazaDoorKind.PressureDoor;

        public float MinSpan => Center - LastShiftZoneDoor.OpeningWidth * 0.5f;
        public float MaxSpan => Center + LastShiftZoneDoor.OpeningWidth * 0.5f;

        /// <summary>광장 쪽에서 본 문 중심의 평면 좌표. 이탈 경로가 지나는 점이다.</summary>
        public Vector2 Waypoint => PlaneIsX ? new Vector2(Plane, Center) : new Vector2(Center, Plane);
    }

    /// <summary>
    /// 중앙 광장 허브 배치의 좌표 정본(<c>docs/central-plaza-hub-layout-v1.md</c> §2.2·§2.3 확정표).
    ///
    /// <b>이제 이것이 서 있는 배다.</b> 일자 스파인(<c>38 x 6m</c>, 통로 둘·배플 둘·개구부 다섯)은
    /// 폐지됐고, <see cref="LastShiftShipDimensions"/>·<see cref="LastShiftZoneAtlas"/>·
    /// <see cref="LastShiftHullShell"/> 이 전부 이 표에서 파생한다. 씬 빌더도 여기를 읽는다 —
    /// 좌표가 두 벌이 되지 않도록 §9.3 이 여섯 항목을 <b>같은 커밋</b>으로 묶어 둔 결과다.
    ///
    /// <b>테스트가 아니라 <c>Runtime</c> 에 있다.</b> 폐기된 <c>LastShiftPlazaProposal</c> 은
    /// "승인 전 도안" 이라 테스트에 있었고, 이 표는 승인된 확정안이다. 확정안을 테스트에 두면
    /// 씬 빌더가 그것을 참조할 수 없다.
    ///
    /// 축 규약은 선체와 같다 — <c>x</c> = 전장(음수가 선수), <c>z</c> = 전폭(음수가 좌현), <c>y</c> = 높이.
    /// </summary>
    public static class LastShiftPlazaLayout
    {
        // ── 중앙 광장과 코어 ─────────────────────────────────────────────────

        public const float PlazaMinX = -6f;
        public const float PlazaMaxX = 6f;
        public const float PlazaMinZ = -6f;
        public const float PlazaMaxZ = 6f;

        /// <summary>광장 둘레 길이. 자유면 산수(§5.1)가 이 값에서 고정 구조물 <c>30m</c> 를 뺀다.</summary>
        public const float PlazaPerimeter =
            (PlazaMaxX - PlazaMinX) * 2f + (PlazaMaxZ - PlazaMinZ) * 2f;

        /// <summary>
        /// 중앙 코어의 반폭. 확정값 <c>2.0m</c>(<c>4 x 4</c>), 바닥에서 천장까지.
        ///
        /// <b>이 값은 아트 판단으로 못 줄인다</b>(§6.4). 게이지 셋이 동시에 읽히는 광장 영역의
        /// 실측 범위가 <c>x [-1.73, +1.72]</c>·<c>z [-1.53, +1.52]</c> 이고(격자 <c>0.05m</c>,
        /// 코어 제외 <c>51,200</c>점), <c>4 x 4</c> 는 그것을 <c>x</c> 로 <c>0.27m</c>·<c>z</c> 로
        /// <c>0.47m</c> 여유를 두고 덮는다. <c>3.2 x 3.2</c> 로 줄이면 위반이 <c>128</c>점 남는다.
        /// 형상·표면·거기 붙는 것은 자유지만 점유 자체가 <c>SIMUL_ZONES ≤ 2</c> 의 성립 조건이다.
        /// </summary>
        public const float CoreHalfExtent = 2f;

        public static bool InsideCore(float x, float z) =>
            x >= -CoreHalfExtent && x <= CoreHalfExtent &&
            z >= -CoreHalfExtent && z <= CoreHalfExtent;

        /// <summary>코어가 광장 바닥에서 공제하는 면적.</summary>
        public const float CoreArea = CoreHalfExtent * 2f * (CoreHalfExtent * 2f);

        // ── 고정 구조물 일곱 ─────────────────────────────────────────────────
        // 높이가 두 갈래인 것이 규약이다(§2.2 마지막 단락) — 본선 3.2m / 부속 3.0m 라
        // 광장에 서면 어느 문이 압력 계열이고 어느 문이 생활 계열인지가 천장으로 먼저 읽힌다.

        private const float MainHeight = LastShiftShipPhysics.CeilingInnerHeight;
        private const float AnnexHeight = LastShiftCompartments.InteriorHeight;

        /// <summary>
        /// §2.2 좌표표 그대로. <b>순서가 <see cref="LastShiftPlazaSpace"/> 와 같아야 한다</b> —
        /// <see cref="Of"/> 가 인덱스로 바로 집는다.
        /// </summary>
        public static readonly LastShiftPlazaFootprint[] Footprints =
        {
            new(LastShiftPlazaSpace.Plaza, PlazaMinX, PlazaMaxX, PlazaMinZ, PlazaMaxZ,
                MainHeight, LastShiftZone.Cockpit),
            new(LastShiftPlazaSpace.CockpitRoom, -14f, -6f, -3f, 3f,
                MainHeight, LastShiftZone.Cockpit),
            new(LastShiftPlazaSpace.LifeSupportRoom, 6f, 14f, -3f, 3f,
                MainHeight, LastShiftZone.LifeSupport),
            new(LastShiftPlazaSpace.PowerRoom, -3f, 3f, -11f, -6f,
                MainHeight, LastShiftZone.Power),
            new(LastShiftPlazaSpace.CoolingRoom, -3f, 3f, 6f, 11f,
                MainHeight, LastShiftZone.Cooling),
            new(LastShiftPlazaSpace.AirlockHall, -11f, -3f, -12f, -6f,
                AnnexHeight, LastShiftZone.Cockpit),
            new(LastShiftPlazaSpace.Quarters, 3f, 9f, 6f, 10f,
                AnnexHeight, LastShiftZone.Cockpit)
        };

        public static LastShiftPlazaFootprint Of(LastShiftPlazaSpace space) => Footprints[(int)space];

        // ── 문 여섯. 전부 광장 변 위에 있다(§2.3) ────────────────────────────

        /// <summary>
        /// 구멍 폭·높이는 정본을 그대로 쓴다 — 통로 체계가 폐지돼도 문 자체의 치수는 안 바뀐다.
        /// <b>압력문이 셋으로 현행과 같아서 <see cref="LastShiftZoneDoor"/> 인스턴스 수가 안 움직인다.</b>
        /// </summary>
        public static readonly LastShiftPlazaDoor[] Doors =
        {
            new(LastShiftPlazaSpace.CockpitRoom, planeIsX: true, plane: -6f, center: 0f,
                LastShiftPlazaDoorKind.Opening, Vector2.zero),
            new(LastShiftPlazaSpace.LifeSupportRoom, planeIsX: true, plane: 6f, center: 0f,
                LastShiftPlazaDoorKind.PressureDoor, new Vector2(14f, 0f)),
            new(LastShiftPlazaSpace.PowerRoom, planeIsX: false, plane: -6f, center: 0f,
                LastShiftPlazaDoorKind.PressureDoor, new Vector2(0f, -11f)),
            new(LastShiftPlazaSpace.CoolingRoom, planeIsX: false, plane: 6f, center: 0f,
                LastShiftPlazaDoorKind.PressureDoor, new Vector2(0f, 11f)),
            new(LastShiftPlazaSpace.AirlockHall, planeIsX: false, plane: -6f, center: -4.5f,
                LastShiftPlazaDoorKind.PlainDoor, Vector2.zero),
            new(LastShiftPlazaSpace.Quarters, planeIsX: false, plane: 6f, center: 4.5f,
                LastShiftPlazaDoorKind.PlainDoor, Vector2.zero)
        };

        public static LastShiftPlazaDoor DoorOf(LastShiftPlazaSpace space)
        {
            foreach (var door in Doors)
                if (door.Space == space)
                    return door;
            throw new System.ArgumentException($"{space} 에 광장 문이 없다.", nameof(space));
        }

        // ── 압력 경계 (조항 S-1) ─────────────────────────────────────────────
        // 압력문 셋이 곧 경계 셋이다. 일자 스파인에서는 경계가 사슬(조종석-전력실-냉각실-
        // 산소실)이었고 경계 b 가 구역 b 와 b+1 을 갈랐는데, 방사형에서는 <b>전부 광장을
        // 물고 있는 별</b>이다 — 낮은 쪽이 언제나 조종석 구역이다.
        //
        // 번호를 구역 번호에서 하나 뺀 값으로 잡는 것이 유일한 자유도였고, 그렇게 잡으면
        // <c>HighZoneOf(b) = (LastShiftZone)(b + 1)</c> 이 식 그대로 살아남는다. 문 상태
        // 스냅샷(<see cref="LastShiftDoorState"/>)과 세이브 파일이 경계 번호로 실려 있어
        // 번호를 흔들면 옛 판이 다른 문을 닫은 채 복원된다.

        /// <summary>압력 경계 수. 압력문 셋과 같고 <see cref="LastShiftZoneAtlas.BoundaryCount"/> 의 정본이다.</summary>
        public const int PressureBoundaryCount = 3;

        /// <summary>경계 <paramref name="boundary"/> 너머의 구역. 이쪽 편은 언제나 조종석 구역이다.</summary>
        public static LastShiftZone HighZoneOf(int boundary) =>
            (LastShiftZone)(Mathf.Clamp(boundary, 0, PressureBoundaryCount - 1) + 1);

        /// <summary>구역이 쓰는 방. 광장·에어록 홀·숙소는 조종석 구역이지만 방으로는 조종석 방이 대표다.</summary>
        public static LastShiftPlazaSpace RoomOf(LastShiftZone zone) => zone switch
        {
            LastShiftZone.Cockpit => LastShiftPlazaSpace.CockpitRoom,
            LastShiftZone.Power => LastShiftPlazaSpace.PowerRoom,
            LastShiftZone.Cooling => LastShiftPlazaSpace.CoolingRoom,
            _ => LastShiftPlazaSpace.LifeSupportRoom
        };

        /// <summary>이 경계에 달린 압력문. 평면이 <c>x</c> 인지 <c>z</c> 인지가 문마다 다르다.</summary>
        public static LastShiftPlazaDoor BoundaryDoor(int boundary) => DoorOf(RoomOf(HighZoneOf(boundary)));

        /// <summary>
        /// 이 경계의 문 중심. <b>스칼라 <c>x</c> 하나로 못 적는다</b> — 전력실 문은 <c>z = -6</c>,
        /// 냉각실 문은 <c>z = +6</c>, 산소실 문만 <c>x = +6</c> 이다. 일자 스파인에서 경계를
        /// <c>float BoundaryX</c> 로 들고 있던 자리가 전부 이 값으로 넘어온다.
        /// </summary>
        public static Vector2 BoundaryWaypoint(int boundary) => BoundaryDoor(boundary).Waypoint;

        // ── 발자국 전체의 경계 상자 ──────────────────────────────────────────

        /// <summary>고정 구조물 일곱을 전부 담는 최소 상자. 자유 배치가 "선체 안" 을 묻는 자리다.</summary>
        public static float MinX => BoundsOf(zone: null).xMin;
        public static float MaxX => BoundsOf(zone: null).xMax;
        public static float MinZ => BoundsOf(zone: null).yMin;
        public static float MaxZ => BoundsOf(zone: null).yMax;

        /// <summary>
        /// 한 압력 구역이 차지하는 평면 상자. 조종석 구역만 넷(광장·조종석 방·에어록 홀·숙소)의
        /// 합집합이라 <b>상자가 실제 점유보다 크다</b> — 구역 소속 판정은 반드시
        /// <see cref="ResolveZone"/> 를 쓰고, 이 상자는 바닥·이름표처럼 "대충 그 근처" 면
        /// 되는 자리에만 쓴다.
        /// </summary>
        public static Rect ZoneBounds(LastShiftZone zone) => BoundsOf(zone);

        private static Rect BoundsOf(LastShiftZone? zone)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var footprint in Footprints)
            {
                if (zone.HasValue && footprint.Zone != zone.Value) continue;
                minX = Mathf.Min(minX, footprint.MinX);
                maxX = Mathf.Max(maxX, footprint.MaxX);
                minZ = Mathf.Min(minZ, footprint.MinZ);
                maxZ = Mathf.Max(maxZ, footprint.MaxZ);
            }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        // ── 구역 판정 (§6.2) ─────────────────────────────────────────────────

        /// <summary>
        /// 위치 → 고정 공간. <b>이것이 <c>x</c> 하나로 구역을 정하던 자리를 대체한다.</b>
        /// 전력실(<c>z [-11,-6]</c>)과 냉각실(<c>z [+6,+11]</c>)이 같은 <c>x</c> 범위
        /// <c>[-3,+3]</c> 를 쓰므로 <see cref="LastShiftZoneAtlas.ResolveHull"/> 의 밴드 훑기로는
        /// 못 가른다 — 자유 배치 타당성 검토 §2.2 가 적어 둔 부채이고 이 배치가 그 시점을 앞당긴다.
        ///
        /// 기획이 요구한 것은 <c>O(1)</c> 하나다(§6.2, 압력 시뮬이 매 tick 도는 자리). 고정 공간이
        /// 일곱으로 <b>상수</b>이므로 표를 통째로 훑어도 <c>O(1)</c> 이고, 실제로 재는 것은 부동소수
        /// 비교 스물여덟 번이다. 공간 분할 자료 구조를 세우는 것은 자유 배치 모듈이 붙는
        /// <see cref="LastShiftPlacedModules"/> 쪽 일이고 고정 표는 여기서 끝난다.
        ///
        /// <b>경계 위의 점은 먼저 선언된 공간이 가져간다.</b> 광장이 표의 첫 줄인 것이 그 규칙의
        /// 실체다 — 문 평면 여섯이 전부 광장 변과 같은 값이라 동점이 실제로 관측되고, 규칙을
        /// 안 정하면 배열 순서라는 우연이 답을 정한다.
        /// </summary>
        public static bool TryResolveSpace(float x, float z, out LastShiftPlazaSpace space)
        {
            foreach (var footprint in Footprints)
            {
                if (!footprint.Contains(x, z)) continue;
                space = footprint.Space;
                return true;
            }

            space = LastShiftPlazaSpace.Plaza;
            return false;
        }

        /// <summary>
        /// 위치 → 압력 구역. 고정 발자국 밖이면 조종석 구역으로 떨어진다 — 배 밖 좌표를
        /// 물어보는 자리(유령·선외 활동)가 있고, 그쪽은 압력이 아니라 진공 판정이 답한다.
        /// </summary>
        public static LastShiftZone ResolveZone(float x, float z) =>
            TryResolveSpace(x, z, out var space) ? Of(space).Zone : LastShiftZone.Cockpit;

        // ── 게이지 판독 (§4) ─────────────────────────────────────────────────

        /// <summary>
        /// 광장 점 <c>(px, pz)</c> 에서 이 문의 게이지가 읽히는가. 문 구멍을 통과하는 직선이
        /// 있는지만 본다.
        ///
        /// <b>차폐만 재고 각크기는 안 잰다</b>(§4.2 마지막 문단, <c>CT-14</c> 와 같은 구멍).
        /// 광장 구석에서 문을 매우 비스듬히 보는 선은 실제로는 안 읽힐 수 있으므로 이 판정은
        /// <b>보수적인 쪽으로 틀린다</b> — 실제 동시 판독은 여기서 세는 것보다 적다.
        /// </summary>
        public static bool GaugeVisible(float px, float pz, in LastShiftPlazaDoor door)
        {
            if (!door.HasGauge) return false;

            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            if (door.PlaneIsX)
            {
                // 보는 사람과 게이지가 문 평면의 반대쪽에 있어야 한다. 같은 쪽이면 광선이
                // 구멍을 아예 안 지나므로 아래 보간이 평면 밖 좌표를 답한다.
                if ((px - door.Plane) * (door.Gauge.x - door.Plane) >= 0f) return false;
                var t = (door.Plane - px) / (door.Gauge.x - px);
                return Mathf.Abs(pz + (door.Gauge.y - pz) * t) <= half + GaugeEpsilon;
            }

            if ((pz - door.Plane) * (door.Gauge.y - door.Plane) >= 0f) return false;
            var s = (door.Plane - pz) / (door.Gauge.y - pz);
            return Mathf.Abs(px + (door.Gauge.x - px) * s) <= half + GaugeEpsilon;
        }

        /// <summary>이 광장 점에서 동시에 읽히는 압력 구역 수. <c>SIMUL_ZONES</c> 가 세는 값이다.</summary>
        public static int SimultaneousZoneReadings(float px, float pz)
        {
            var count = 0;
            foreach (var door in Doors)
                if (GaugeVisible(px, pz, door))
                    count++;
            return count;
        }

        // ── 이탈 거리 (§6.1) ─────────────────────────────────────────────────

        /// <summary>
        /// 구역별 최악 이탈 거리. 자기 공간의 가장 먼 구석에서 광장 쪽 문을 지나 <b>다른 구역으로
        /// 나가는 압력문</b>까지의 꺾인 직선이다.
        ///
        /// <b>문 통과 페널티는 안 들어간다</b> — §6.1 표가 그렇게 잰 개산이고, 가드레일 <c>(1)</c>
        /// 판정 시간은 <see cref="LastShiftPlacementRules.EgressSeconds"/> 가 거기에 압력문
        /// <c>0.8초</c> 를 더해서 낸다.
        ///
        /// <b>광장 안 직선이 코어를 안 피해 간다.</b> 코어 <c>4 x 4</c> 를 우회하면 거리가 조금
        /// 늘지만, 이 값의 용도가 래칫이라 우회를 넣으면 코어 치수가 이탈 시간의 입력이 된다 —
        /// 게임플레이 가드레일 둘이 서로를 물게 만들지 않는다.
        /// </summary>
        public static float WorstEgressMeters(LastShiftZone zone)
        {
            var worst = 0f;
            foreach (var footprint in Footprints)
            {
                if (footprint.Zone != zone) continue;
                for (var corner = 0; corner < 4; corner++)
                    worst = Mathf.Max(worst, EgressMetersFrom(footprint, footprint.Corner(corner)));
            }
            return worst;
        }

        /// <summary>배 전체 최악 이탈. §6.1 이 <c>17.03m</c> / <c>4.26초</c> 로 확정한 값이다.</summary>
        public static float WorstEgressMeters()
        {
            var worst = 0f;
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                worst = Mathf.Max(worst, WorstEgressMeters((LastShiftZone)zone));
            return worst;
        }

        /// <summary>이탈 거리 → 개산 시간. 보행만이고 문 페널티는 빠져 있다(§6.1 표의 정의).</summary>
        public static float EgressWalkSeconds(float meters) => meters / LastShiftPlayerController.MoveSpeed;

        /// <summary>
        /// 이 압력문이 <paramref name="zone"/> 의 이탈구인가. 문 하나가 <b>두 구역의 경계</b>라
        /// 양쪽 다에 해당한다 — 전력실 문은 전력실이 나가는 문이면서 동시에 광장(조종석 구역)이
        /// 나가는 문이다. 이 양면성을 안 적어 두면 단칸 구역의 이탈이 "자기 문을 지나 다시
        /// 남의 문까지" 로 계산돼 두 배로 부풀어 오른다.
        /// </summary>
        public static bool IsExitFor(in LastShiftPlazaDoor door, LastShiftZone zone) =>
            door.Kind == LastShiftPlazaDoorKind.PressureDoor &&
            (Of(door.Space).Zone == zone || Of(LastShiftPlazaSpace.Plaza).Zone == zone);

        private static float EgressMetersFrom(in LastShiftPlazaFootprint footprint, Vector2 from)
        {
            var onPlaza = footprint.Space == LastShiftPlazaSpace.Plaza;
            var own = onPlaza ? default : DoorOf(footprint.Space);

            var best = float.MaxValue;
            foreach (var door in Doors)
            {
                if (!IsExitFor(door, footprint.Zone)) continue;

                // 광장에 서 있으면 이미 허브다. 자기 문이 곧 이탈구인 단칸 구역도 곧장 간다.
                // 나머지(조종석 방·에어록 홀·숙소)만 자기 문을 먼저 지나 광장을 가로지른다.
                var meters = onPlaza || door.Space == footprint.Space
                    ? Vector2.Distance(from, door.Waypoint)
                    : Vector2.Distance(from, own.Waypoint) +
                      Vector2.Distance(own.Waypoint, door.Waypoint);
                best = Mathf.Min(best, meters);
            }

            return best == float.MaxValue ? 0f : best;
        }

        // ── 원반 껍질 (§7 미결 1) ────────────────────────────────────────────
        // 타원에서 원으로 간다. 종횡비가 6.33:1 에서 1.22:1 로 뒤집혔으므로 장축을 따로 둘
        // 이유가 없어졌고, 허브 앤 스포크는 원형 실루엣이 맞다(§0-8).
        //
        // 반지름은 취향이 아니라 아래 네 항의 합이다. 근거를 값이 아니라 식으로 적어 두는
        // 이유는 발자국이 한 번만 움직여도 이 값이 따라와야 하기 때문이다.

        /// <summary>
        /// 구획 발자국이 내접 다각형 안쪽으로 확보해야 하는 최소 거리. 구획 바깥 판과 테두리 판이
        /// 각각 <see cref="LastShiftCompartments.PanelThickness"/> 이므로 그 합이다. 이보다 얇으면
        /// 두 판이 씬에서 서로를 파고든다 — 폐기된 도안이 <c>b</c> 를 <c>20 → 22</c> 로 민 근거와 같다.
        /// </summary>
        public const float MinInscribedClearance = LastShiftCompartments.PanelThickness * 2f;

        /// <summary>
        /// 원반 안에 남겨 두는 확장 여지. 카탈로그 모듈의 <b>최소 변</b>이 <c>2m</c> 이므로
        /// (<see cref="LastShiftModuleFootprint"/> 의 문 통과 조건에서 나온 값) 이만큼이면
        /// 고정 방 바깥 면에 가장 얇은 모듈이 한 겹 붙어도 원반 안이다.
        ///
        /// <b>이 한 겹이 확장 검토 §7-(a)("확장 모듈은 원반 밖에 붙는다")를 완화한다.</b> 폐기가
        /// 아니라 완화다 — 두 겹째부터는 여전히 밖이고, 원반을 더 키워서 그것까지 담는 것은
        /// 안 쓰는 껍질 부피를 그만큼 늘리는 값이라 하지 않는다.
        /// </summary>
        public const float ExpansionAllowance = 2f;

        /// <summary>테두리를 근사하는 직선 판의 수. 정본과 같은 값을 써야 새그 비교가 성립한다.</summary>
        public const int SegmentCount = LastShiftHullShell.SegmentCount;

        /// <summary>
        /// 원반 반지름. 확정값 <c>19m</c>(전장·전폭 <c>38m</c>, 정원).
        ///
        /// <code>
        ///   최원 모서리   에어록 홀 (-11, -12)          16.279m
        ///   판 두께 둘    MinInscribedClearance          0.400m
        ///   확장 한 겹    ExpansionAllowance             2.000m
        ///   내접 보정     / cos(pi / 48)                x1.00215
        ///                                             ----------
        ///   요구 하한                                   18.719m  ->  올림 19m
        /// </code>
        ///
        /// 실제로 남는 내접 여유는 최원 모서리에서 <c>2.68m</c> 다(요구 <c>2.40m</c> 대비 <c>+0.28</c>).
        /// <see cref="LastShiftHullShell"/> 이 이 값을 그대로 반지름으로 쓴다 — 껍질에 리터럴을
        /// 남겨 두면 발자국이 움직여도 원이 안 따라온다.
        /// </summary>
        public const float HullRadius = 19f;

        public const float HullDiameter = HullRadius * 2f;

        /// <summary>점에서 내접 다각형까지의 부호 있는 거리. 양수면 안이고 그 값이 그대로 여유다.</summary>
        public static float InscribedMargin(float x, float z) =>
            HullRadius * Mathf.Cos(Mathf.PI / SegmentCount) - Mathf.Sqrt(x * x + z * z);

        /// <summary>발자국 네 모서리 중 가장 얇은 내접 여유.</summary>
        public static float InscribedMargin(in LastShiftPlazaFootprint footprint)
        {
            var margin = float.MaxValue;
            for (var corner = 0; corner < 4; corner++)
            {
                var point = footprint.Corner(corner);
                margin = Mathf.Min(margin, InscribedMargin(point.x, point.y));
            }
            return margin;
        }

        // ── 광장 둘레 자유면 (§5.1) ──────────────────────────────────────────

        /// <summary>광장 변. 인덱스가 <see cref="FreeSpansOn"/> 의 입력이다.</summary>
        public enum PlazaSide
        {
            /// <summary>선수 <c>x = -6</c>. 구간은 <c>z</c> 축이다.</summary>
            Bow = 0,
            /// <summary>선미 <c>x = +6</c>.</summary>
            Stern = 1,
            /// <summary>좌현 <c>z = -6</c>. 구간은 <c>x</c> 축이다.</summary>
            Port = 2,
            /// <summary>우현 <c>z = +6</c>.</summary>
            Starboard = 3
        }

        /// <summary>
        /// 광장 한 변에서 고정 구조물이 안 먹은 구간 중 <see cref="LastShiftZoneDoor.OpeningWidth"/>
        /// 이상인 것. <b>이 값이 §5 의 답을 산수로 만든다</b> — 여섯 구간 <c>18m</c> 뿐이라 광장이
        /// 붙이는 자리를 독점하면 확장이 여섯 번에 끝나고, <c>72</c>기항 캠페인이 그 위에 안 선다.
        /// </summary>
        public static (float Min, float Max)[] FreeSpansOn(PlazaSide side)
        {
            var alongZ = side == PlazaSide.Bow || side == PlazaSide.Stern;
            var lo = alongZ ? PlazaMinZ : PlazaMinX;
            var hi = alongZ ? PlazaMaxZ : PlazaMaxX;

            var occupied = new System.Collections.Generic.List<(float Min, float Max)>();
            foreach (var footprint in Footprints)
            {
                if (footprint.Space == LastShiftPlazaSpace.Plaza) continue;
                if (!TouchesSide(footprint, side)) continue;
                var min = alongZ ? footprint.MinZ : footprint.MinX;
                var max = alongZ ? footprint.MaxZ : footprint.MaxX;
                occupied.Add((Mathf.Max(lo, min), Mathf.Min(hi, max)));
            }
            occupied.Sort((a, b) => a.Min.CompareTo(b.Min));

            var spans = new System.Collections.Generic.List<(float, float)>();
            var cursor = lo;
            foreach (var (min, max) in occupied)
            {
                if (min > cursor && min - cursor >= LastShiftZoneDoor.OpeningWidth - Epsilon)
                    spans.Add((cursor, min));
                cursor = Mathf.Max(cursor, max);
            }
            if (hi > cursor && hi - cursor >= LastShiftZoneDoor.OpeningWidth - Epsilon)
                spans.Add((cursor, hi));

            return spans.ToArray();
        }

        private static bool TouchesSide(in LastShiftPlazaFootprint footprint, PlazaSide side) => side switch
        {
            PlazaSide.Bow => Mathf.Abs(footprint.MaxX - PlazaMinX) < Epsilon,
            PlazaSide.Stern => Mathf.Abs(footprint.MinX - PlazaMaxX) < Epsilon,
            PlazaSide.Port => Mathf.Abs(footprint.MaxZ - PlazaMinZ) < Epsilon,
            _ => Mathf.Abs(footprint.MinZ - PlazaMaxZ) < Epsilon
        };

        /// <summary>맞닿는 면은 겹침이 아니다 — 정본 <c>VolumesOverlap</c> 과 같은 열린 구간 비교다.</summary>
        public static bool Overlap(in LastShiftPlazaFootprint a, in LastShiftPlazaFootprint b) =>
            a.MinX < b.MaxX - Epsilon && b.MinX < a.MaxX - Epsilon &&
            a.MinZ < b.MaxZ - Epsilon && b.MinZ < a.MaxZ - Epsilon;

        private const float Epsilon = 0.0001f;

        /// <summary>
        /// 게이지 가시 판정의 여유. <b>좌표 격자와 따로 둔 이유가 실측이다</b> — 격자 <c>0.05m</c>
        /// 표본 <c>57,600</c>개에서 투영값이 구멍 반폭 <c>0.8</c> 에 가장 가까이 붙은 것이
        /// <c>2.99e-4</c> 다. 공용 <see cref="Epsilon"/>(<c>1e-4</c>)은 그 절반 안쪽이라 아슬아슬하고,
        /// <c>float</c> 누적 오차는 이 식에서 <c>1e-6</c> 수준이다. 그 사이를 고른다.
        /// </summary>
        private const float GaugeEpsilon = 1e-5f;
    }
}
