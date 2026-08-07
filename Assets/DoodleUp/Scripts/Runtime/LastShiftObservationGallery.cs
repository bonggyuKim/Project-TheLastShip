using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 관측 회랑을 이루는 축 정렬 칸 하나. 호(弧)를 이 칸들의 계단으로 근사한다 —
    /// §28.2 가 사선 다리를 기각한 이유(겹침 검증이 <c>AABB</c> 비교라 사선은 검증이
    /// 성립하지 않는다)가 여기도 그대로 적용되고, §29.4-(2) 가 "축정렬 세그먼트로 쪼개서
    /// 통과시킬 것" 이라고 적은 것이 이 구조다.
    ///
    /// <b>바깥 끝이 둘인 것이 이 구조체의 요점이다.</b> <see cref="OuterZ"/> 는 승무원이
    /// 실제로 설 수 있는 끝(칸 안에서 가장 <b>얕은</b> 테두리 위치)이라 발자국·겹침 검증이
    /// 이 값을 본다. <see cref="SlabOuterZ"/> 는 바닥·천장 슬래브가 닿는 끝(가장 <b>깊은</b>
    /// 테두리 위치)이라 계단 한 칸 안에서 테두리와 바닥 사이에 틈이 안 남는다. 둘을 하나로
    /// 합치면 얕은 쪽을 쓰면 바닥에 틈이 생기고, 깊은 쪽을 쓰면 발자국이 테두리 판을 뚫는다.
    /// </summary>
    public readonly struct LastShiftObservationBand
    {
        public LastShiftObservationBand(string name, int run,
            float minX, float maxX, float outerZ, float slabOuterZ, float innerZ)
        {
            Name = name;
            Run = run;
            MinX = minX;
            MaxX = maxX;
            OuterZ = outerZ;
            SlabOuterZ = slabOuterZ;
            InnerZ = innerZ;
        }

        public string Name { get; }

        /// <summary>이 칸이 속한 구간. <see cref="LastShiftObservationGallery.ArcRun"/> 등.</summary>
        public int Run { get; }

        public float MinX { get; }
        public float MaxX { get; }

        /// <summary>걸어 나갈 수 있는 바깥 끝. 칸 안에서 가장 얕은 테두리 판 안쪽 면이다.</summary>
        public float OuterZ { get; }

        /// <summary>바닥·천장 슬래브의 바깥 끝. 칸 안에서 가장 깊은 테두리 판 안쪽 면이다.</summary>
        public float SlabOuterZ { get; }

        /// <summary>안쪽 끝. 호 구간이면 회랑 폭만큼, 착륙 구간이면 선체·구획 면까지다.</summary>
        public float InnerZ { get; }

        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (OuterZ + InnerZ) * 0.5f;
        public float LengthX => MaxX - MinX;
        public float DepthZ => InnerZ - OuterZ;

        /// <summary>겹침 검증이 보는 발자국. 슬래브가 아니라 걸을 수 있는 범위다.</summary>
        public LastShiftGalleryLeg Footprint => new(Name, MinX, MaxX, OuterZ, InnerZ);
    }

    /// <summary>
    /// 좌현 관측 회랑의 좌표 정본. 기획 정본은 <c>docs/corridor-4p-redesign-v1.md</c>
    /// §29.4-(2) 이고, 이 배에서 승무원이 걸을 수 있는 <b>첫 번째 곡선 동선</b>이다
    /// (§29.6 판정기준 4).
    ///
    /// <b>경로는 ㄷ 자다.</b> 조종석 좌현 벽에서 테두리까지 나가고(선미 쪽 착륙 구간),
    /// 테두리 안쪽을 호로 따라 선수 쪽으로 달리고(호 구간), 화물칸 좌현 면으로 다시 들어온다
    /// (선수 쪽 착륙 구간). 세 구간이 x 로 나란히 놓이므로 <b>칸 하나의 구조가 세 구간에서
    /// 같다</b> — 다른 것은 안쪽 끝이 회랑 폭이냐 벽면이냐뿐이다.
    ///
    /// <b>왜 관측실이 아니라 화물칸인가.</b> §29.7-2 가 이 접속점을 <c>tech</c> 실측으로
    /// 남겼고, 실측 결과가 갈랐다 — 테두리 창 호(<see cref="LastShiftHullFrames.SegmentIsWindowBay"/>)는
    /// <c>|x| ≤ 25</c> 구간에만 있다. 관측실(<c>x -35~-32</c>)까지 끌면 회랑 바깥면의 절반이
    /// 불투명 테두리 판이 되어 §29.4-(2) 의 "회랑 바깥면이 곧 창면" 이 성립하지 않는다.
    /// 화물칸(중심 <c>x -23</c>)은 창 호 안이다. 사슬 위상으로도 화물칸이 맞다 — 화물칸의
    /// 안쪽 문은 조종석 구역으로 나 있어서 이 회랑이 <b>조종석 ↔ 화물칸 국소 고리</b> 하나만
    /// 만들지만, 관측실은 정비창·화물칸 둘을 건너뛰어 §15.2 언락 사슬을 우회한다.
    ///
    /// <b>압력존과 무관하다</b>(§24, §29.4-(2) 셋째 항목). <c>LastShiftZoneDoor</c> 를 안 쓰고
    /// <c>ZonePressure</c> 슬롯도 안 만든다 — 상부 회랑(<see cref="LastShiftUpperGallery"/>)과
    /// 같은 취급이다. 양 끝이 <b>둘 다 조종석 구역 쪽</b>이라 §28.3 이 상부 회랑에서 만들어 낸
    /// "주 통로를 안 거치는 구역 간 우회로" 가 여기서는 안 생긴다.
    ///
    /// <b>대칭 회랑을 선미에 안 놓는다</b>(§29.5-3, 기획 판단). 대칭으로 만들면 그 순간
    /// 구역 간 우회로 판단이 다시 열린다.
    ///
    /// 축 규약은 선체와 같다 — x = 전장, z = 전폭, y = 높이. 리터럴 좌표를 안 적는 이유는
    /// <see cref="LastShiftUpperGallery"/> 와 같다.
    /// </summary>
    public static class LastShiftObservationGallery
    {
        /// <summary>회랑 폭. 상부 회랑과 같은 <c>2m</c> 다 — 같은 배의 같은 등급 동선이다.</summary>
        public const float Width = LastShiftUpperGallery.Width;

        public const float InteriorHeight = LastShiftCompartments.InteriorHeight;

        public const float PanelThickness = LastShiftCompartments.PanelThickness;

        /// <summary>회랑 오브젝트 이름의 접두. 씬 로그와 검증기가 같은 문자열을 봐야 한다.</summary>
        public const string RootName = "ObservationGallery";

        /// <summary>
        /// 계단 한 칸이 테두리에서 벌어질 수 있는 최대량. <b>판 두께가 상한인 것에 이유가 있다</b> —
        /// 바닥·천장 슬래브는 칸 안에서 가장 깊은 테두리 위치까지 나가므로, 이 값이 판 두께를
        /// 넘으면 슬래브가 테두리 판 <b>바깥면</b>을 뚫고 나와 원반 실루엣에 혹이 생긴다.
        /// 칸 수는 이 예산에서 역산한다(<see cref="BandCountFor"/>) — 개수를 상수로 박으면
        /// <see cref="LastShiftHullShell.SegmentCount"/> 나 구획 좌표가 바뀔 때 조용히 틀린다.
        /// </summary>
        public const float MaxRimStep = PanelThickness;

        // ── 구간 ─────────────────────────────────────────────────────────────

        public const int RunCount = 3;

        /// <summary>화물칸으로 들어가는 선수 쪽 착륙 구간.</summary>
        public const int CargoLandingRun = 0;

        /// <summary>테두리 호를 따르는 가운데 구간. 곡선인 것은 이 구간뿐이다.</summary>
        public const int ArcRun = 1;

        /// <summary>조종석 좌현 벽으로 들어가는 선미 쪽 착륙 구간.</summary>
        public const int CockpitLandingRun = 2;

        // ── 양 끝 접속점 ─────────────────────────────────────────────────────

        /// <summary>조종석 쪽 착륙 구간의 중심 x. 조종석 방 중심이라 문이 그 벽 한가운데 온다.</summary>
        public static float CockpitLandingCenterX => LastShiftShipDimensions.CockpitCenterX;

        /// <summary>화물칸 쪽 착륙 구간의 중심 x. 화물칸 방 중심이다.</summary>
        public static float CargoLandingCenterX =>
            LastShiftCompartments.Of(LastShiftCompartment.CargoBay).CenterX;

        /// <summary>
        /// 조종석 쪽 회랑이 닿는 z. 선체 좌현 긴 벽의 <b>바깥</b> 면이다 — 안쪽 면을 쓰면
        /// 회랑이 벽 두께만큼 선체 안으로 파고들어 <c>LegOverlapsHullInterior</c> 에 걸린다.
        /// </summary>
        public static float CockpitAttachZ =>
            -LastShiftShipDimensions.SideWallZ - LastShiftShipDimensions.HullThickness * 0.5f;

        /// <summary>
        /// 화물칸 쪽 회랑이 닿는 z. 화물칸 좌현 <b>벽 판의 바깥 면</b>이다 — 방 내부 면
        /// (<c>MinZ</c>)을 쓰면 회랑 바닥이 그 벽 판과 같은 자리에 겹친다. 화물칸 바닥
        /// 슬래브도 판 두께만큼 밖으로 나와 있으므로 두 바닥이 여기서 정확히 맞물린다.
        /// </summary>
        public static float CargoAttachZ =>
            LastShiftCompartments.Of(LastShiftCompartment.CargoBay).MinZ - LastShiftCompartments.PanelThickness;

        /// <summary>문 좌표를 맞출 때 쓰는 화물칸 면. 벽이 놓인 면은 방 내부 면 쪽이다.</summary>
        public static float CargoDoorwayFaceZ =>
            LastShiftCompartments.Of(LastShiftCompartment.CargoBay).MinZ;

        public static float MinX => CargoLandingCenterX - Width * 0.5f;
        public static float MaxX => CockpitLandingCenterX + Width * 0.5f;

        /// <summary>착륙 구간 사이의 호 구간 x 범위.</summary>
        public static float ArcMinX => CargoLandingCenterX + Width * 0.5f;
        public static float ArcMaxX => CockpitLandingCenterX - Width * 0.5f;

        /// <summary>
        /// 테두리 판 <b>안쪽 면</b>의 z. 판은 현 위에 두께 <see cref="LastShiftHullShell.PanelThickness"/>
        /// 로 <b>중심</b>이 놓이므로(씬 빌더 <c>CreateDiscHull</c>) 반 두께만큼 안으로 들어온다.
        /// 회랑 바깥면이 곧 이 면이다 — 그래서 별도 창 구조가 필요 없다(§29.4-(2) 둘째 항목).
        /// </summary>
        public static float RimInnerZ(float x) =>
            LastShiftHullShell.PortEdgeZ(x) + LastShiftHullShell.PanelThickness * 0.5f;

        private static readonly LastShiftObservationBand[] bands = BuildBands();
        private static readonly LastShiftGalleryLeg[] legs = BuildLegs();

        /// <summary>선수(작은 x)에서 선미(큰 x) 순으로 늘어선 계단 칸들.</summary>
        public static LastShiftObservationBand[] Bands => bands;

        /// <summary>겹침 검증용 발자국. 상부 회랑과 같은 자료형이라 같은 검사를 돌릴 수 있다.</summary>
        public static LastShiftGalleryLeg[] Legs => legs;

        public static int BandCount => bands.Length;

        private static LastShiftObservationBand[] BuildBands()
        {
            var result = new System.Collections.Generic.List<LastShiftObservationBand>();
            AppendRun(result, CargoLandingRun, "CargoLanding", MinX, ArcMinX, CargoAttachZ);
            AppendRun(result, ArcRun, "Arc", ArcMinX, ArcMaxX, float.NaN);
            AppendRun(result, CockpitLandingRun, "CockpitLanding", ArcMaxX, MaxX, CockpitAttachZ);
            return result.ToArray();
        }

        /// <summary>
        /// 구간 하나를 칸으로 쪼갠다. <paramref name="attachZ"/> 가 <c>NaN</c> 이면 호 구간이고,
        /// 안쪽 끝이 벽면이 아니라 회랑 폭에서 나온다.
        ///
        /// 구간마다 칸 수를 따로 세는 것이 <b>경계를 저절로 맞춘다</b> — 회랑 전체를 균등
        /// 분할하면 착륙 구간의 경계가 칸 한가운데에 떨어져, 그 칸이 회랑이면서 동시에
        /// 착륙 구간이 된다.
        /// </summary>
        private static void AppendRun(System.Collections.Generic.List<LastShiftObservationBand> into,
            int run, string name, float minX, float maxX, float attachZ)
        {
            var count = BandCountFor(minX, maxX);
            var width = (maxX - minX) / count;

            for (var index = 0; index < count; index++)
            {
                var bandMinX = minX + width * index;
                var bandMaxX = index == count - 1 ? maxX : bandMinX + width;

                // 좌현에서 x 가 커질수록 테두리가 깊어진다(|z| 증가). 얕은 쪽이 걸을 수 있는
                // 끝이고 깊은 쪽이 슬래브 끝이라, 어느 x 가 어느 쪽인지는 부호로 정하지 않고
                // 실제 값으로 고른다 — 배가 반대로 뒤집혀도 같은 코드가 맞는다.
                var atMin = RimInnerZ(bandMinX);
                var atMax = RimInnerZ(bandMaxX);
                var outerZ = Mathf.Max(atMin, atMax);
                var slabOuterZ = Mathf.Min(atMin, atMax);

                into.Add(new LastShiftObservationBand(
                    $"{name}_{index:00}", run, bandMinX, bandMaxX,
                    outerZ, slabOuterZ, float.IsNaN(attachZ) ? outerZ + Width : attachZ));
            }
        }

        /// <summary>
        /// 이 구간을 몇 칸으로 쪼개야 <see cref="MaxRimStep"/> 예산을 지키는가. 식이 아니라
        /// 실측인 이유는 <see cref="LastShiftHullShell.MaxChordSag"/> 와 같다 — 현의 기울기가
        /// 구간 안에서 균일하지 않아서, 평균으로 잡으면 가장 가파른 칸이 예산을 넘는다.
        /// </summary>
        private static int BandCountFor(float minX, float maxX)
        {
            for (var count = 1; count <= MaxBandCount; count++)
            {
                var width = (maxX - minX) / count;
                var worst = 0f;
                for (var index = 0; index < count; index++)
                {
                    var bandMinX = minX + width * index;
                    worst = Mathf.Max(worst,
                        Mathf.Abs(RimInnerZ(bandMinX + width) - RimInnerZ(bandMinX)));
                }

                if (worst <= MaxRimStep) return count;
            }

            return MaxBandCount;
        }

        /// <summary>칸 수 상한. 예산을 못 맞추더라도 정적 초기화가 안 멈추게 하는 안전판이다.</summary>
        private const int MaxBandCount = 64;

        private static LastShiftGalleryLeg[] BuildLegs()
        {
            var result = new LastShiftGalleryLeg[bands.Length];
            for (var index = 0; index < bands.Length; index++) result[index] = bands[index].Footprint;
            return result;
        }

        /// <summary>구간 하나에 속한 칸들. 첫 칸·끝 칸을 집을 때 쓴다.</summary>
        public static LastShiftObservationBand FirstBandOf(int run)
        {
            foreach (var band in bands)
                if (band.Run == run) return band;
            return bands[0];
        }

        public static LastShiftObservationBand LastBandOf(int run)
        {
            var result = bands[0];
            foreach (var band in bands)
                if (band.Run == run) result = band;
            return result;
        }

        // ── 문 ───────────────────────────────────────────────────────────────

        /// <summary>
        /// 조종석 쪽 문이 뚫리는 선체 좌현 벽의 z. 이 벽은 눈높이가 통째로 창인 구간이라
        /// (<c>OuterHull_Front*</c>) 씬 빌더가 여기 뚫는 것은 <b>문턱 판</b>뿐이다 — 창 위
        /// 인방을 잘라 내면 좌현 창 띠가 한 자리에서 끊긴다.
        /// </summary>
        public static float CockpitDoorwayFaceZ => -LastShiftShipDimensions.SideWallZ;

        /// <summary>
        /// 조종석 쪽 문이 실제로 뚫리는가. 조종석은 잠긴 구획이 아니라 시작 지점이라 언제나
        /// 열려 있다 — 이 문이 안 열리면 회랑 전체가 승무원이 못 가는 자리가 되고, 그러면
        /// §29.6-4 의 "실제로 걸을 수 있는" 이 거짓이 된다.
        /// </summary>
        public const bool CockpitDoorwayIsOpen = true;

        /// <summary>
        /// 이 구획 면에 뚫려야 하는 회랑 문의 자유축 좌표. 잠긴 구획은 구멍이 아니라 메운
        /// 판이다(§15.2) — 화물칸은 지금 잠겨 있으므로 회랑의 선수 쪽 끝은 막힌 문이고,
        /// 언락되면 그때 고리가 닫힌다. 상부 회랑의 분기 셋과 같은 규칙이다.
        /// </summary>
        public static float[] DoorwaysOn(LastShiftCompartment compartment,
            LastShiftDoorPlane plane, float faceCoordinate)
        {
            if (compartment != LastShiftCompartment.CargoBay) return System.Array.Empty<float>();
            if (plane != LastShiftDoorPlane.AlongZ) return System.Array.Empty<float>();
            if (Mathf.Abs(faceCoordinate - CargoDoorwayFaceZ) >= 0.001f) return System.Array.Empty<float>();
            if (!LastShiftCompartments.Of(LastShiftCompartment.CargoBay).IsPassable)
                return System.Array.Empty<float>();

            return new[] { CargoLandingCenterX };
        }

        // ── 성질 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 호 구간에서 중심선이 옆으로 흐르는 총량. <b>§29.6-4 를 숫자로 만드는 자리다</b> —
        /// 칸 하나하나는 축 정렬이지만 이어 붙인 동선은 아니고, 그 "아님" 의 크기가 이 값이다.
        /// <c>0</c> 이면 계단이 평평해져 그냥 직선 복도가 된 것이다.
        /// </summary>
        public static float ArcCenterlineDrift =>
            Mathf.Abs(LastBandOf(ArcRun).CenterZ - FirstBandOf(ArcRun).CenterZ);

        /// <summary>호 구간 길이(x).</summary>
        public static float ArcLength => ArcMaxX - ArcMinX;

        /// <summary>
        /// 통행 거리 — 조종석 벽에서 화물칸 면까지 회랑만 타고 간 길이다. 착륙 구간 둘의
        /// 깊이와 호 구간 길이의 합이고, 계단의 z 변화는 호 구간 안에서 상쇄되지 않으므로
        /// 따로 더하지 않는다.
        /// </summary>
        public static float TravelDistance =>
            Mathf.Abs(CockpitAttachZ - LastBandOf(CockpitLandingRun).CenterZ) +
            ArcLength +
            Mathf.Abs(CargoAttachZ - FirstBandOf(CargoLandingRun).CenterZ);

        /// <summary>
        /// 회랑 칸이 구획 볼륨을 침범하는가. 상부 회랑과 같은 규칙이다 — 맞닿는 면은 침범이
        /// 아니다.
        /// </summary>
        public static bool BandOverlapsCompartment(in LastShiftObservationBand band,
            in LastShiftCompartmentSpec spec) =>
            LastShiftUpperGallery.LegOverlapsCompartment(band.Footprint, spec);
    }
}
