using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선체 도면의 투영 — 월드 <c>xz</c> 를 화면 사각형에 얹는다. <b>이것이 도면의 전부다</b>:
    /// 구획표가 이미 <c>AABB</c> 목록이므로 도면 그리기는 변환 하나 + 사각형 <c>N</c>개다
    /// (<c>docs/core-four-rooms-and-hull-schematic-v1.md</c> §4.6).
    ///
    /// <b>축 규약.</b> 위에서 내려다본 직교 투영이다 — 선수(<c>-x</c>)가 왼쪽, 선미(<c>+x</c>)가
    /// 오른쪽, 우현(<c>+z</c>)이 위다. §4.2 의 그림이 그 방향이고, Unity 씬 뷰의 Top 방향과도
    /// 같아서 좌표를 눈으로 대조할 수 있다.
    ///
    /// <b>등방 배율이다.</b> 가로세로를 따로 늘리면 <c>5×5</c> 정비창이 도면에서 직사각형으로
    /// 보이고, 그러면 "두 자리를 나란히 대 본다"(§4.1-1)가 눈으로 성립하지 않는다.
    ///
    /// <b><see cref="MonoBehaviour"/> 가 아니다.</b> 값 넷을 든 구조체라 화면 없이 EditMode 에서
    /// 왕복(월드→화면→월드)을 잰다.
    /// </summary>
    public readonly struct LastShiftHullSchematic
    {
        /// <summary>
        /// 원반 테두리 바깥으로 두는 여백(m). 테두리가 화면 끝에 딱 붙으면 실선이 잘려 보인다.
        /// </summary>
        public const float DefaultMarginMeters = 3f;

        /// <summary>도면이 덮는 월드 반경(<c>x</c>). 원반 테두리가 기준선이다(§4.3-6).</summary>
        public static float DefaultHalfLengthX => LastShiftHullShell.SemiMajorX + DefaultMarginMeters;

        public static float DefaultHalfWidthZ => LastShiftHullShell.SemiMinorZ + DefaultMarginMeters;

        /// <summary>화면 사각형 하나에 원반 전체를 얹는다.</summary>
        public LastShiftHullSchematic(Rect screen)
            : this(screen, DefaultHalfLengthX, DefaultHalfWidthZ)
        {
        }

        /// <summary>
        /// 원점을 가운데 두고 <paramref name="halfLengthX"/>×<paramref name="halfWidthZ"/> 를
        /// 얹는다. 배율은 둘 중 <b>작은 쪽</b>이라 도면이 화면 밖으로 안 나간다.
        /// </summary>
        public LastShiftHullSchematic(Rect screen, float halfLengthX, float halfWidthZ)
            : this(screen, halfLengthX, halfWidthZ, Vector2.zero)
        {
        }

        /// <summary>
        /// 화면 가운데에 오는 월드 좌표를 정해서 얹는다.
        ///
        /// <b>선외 거점 탭이 이 생성자를 쓴다</b>(<see cref="LastShiftOutpost"/>). 거점은 원반
        /// 바깥 선수 좌현에 있어서 원점을 가운데 두면 화면의 한 귀퉁이에 몰리고, 그러면 배치
        /// 대상이 몇 픽셀짜리 점이 된다. <b>배율은 안 건드린다</b> — 두 탭이 같은 자로 그려야
        /// 골조 크기가 탭을 옮길 때 안 바뀐다.
        /// </summary>
        public LastShiftHullSchematic(Rect screen, float halfLengthX, float halfWidthZ, Vector2 worldCenter)
        {
            Screen = screen;
            HalfLengthX = Mathf.Max(halfLengthX, 0.001f);
            HalfWidthZ = Mathf.Max(halfWidthZ, 0.001f);
            WorldCenter = worldCenter;
            PixelsPerMeter = Mathf.Min(screen.width / (HalfLengthX * 2f), screen.height / (HalfWidthZ * 2f));
        }

        public Rect Screen { get; }

        public float HalfLengthX { get; }

        public float HalfWidthZ { get; }

        /// <summary>화면 한가운데에 오는 월드 <c>(x, z)</c>. 기본값은 원점이다.</summary>
        public Vector2 WorldCenter { get; }

        /// <summary>미터당 화면 픽셀. 배율이 하나라 <c>x</c>·<c>z</c> 가 같이 늘고 준다.</summary>
        public float PixelsPerMeter { get; }

        /// <summary>월드 원점의 화면 좌표.</summary>
        public Vector2 Center => Screen.center;

        /// <summary>월드 평면 좌표 → 화면. GUI 좌표는 <c>y</c> 가 아래로 자라므로 <c>z</c> 를 뒤집는다.</summary>
        public Vector2 ToScreen(float worldX, float worldZ) => new(
            Screen.center.x + (worldX - WorldCenter.x) * PixelsPerMeter,
            Screen.center.y - (worldZ - WorldCenter.y) * PixelsPerMeter);

        public Vector2 ToScreen(Vector3 world) => ToScreen(world.x, world.z);

        /// <summary>화면 → 월드 평면 좌표. <c>y</c> 는 갑판(<c>0</c>)이다.</summary>
        public Vector3 ToWorld(Vector2 screenPoint) => new(
            WorldCenter.x + (screenPoint.x - Screen.center.x) / PixelsPerMeter,
            0f,
            WorldCenter.y + (Screen.center.y - screenPoint.y) / PixelsPerMeter);

        /// <summary>월드 <c>AABB</c> → 화면 사각형. 구획 하나가 도면에서 차지하는 자리다.</summary>
        public Rect ToScreenRect(float minX, float maxX, float minZ, float maxZ)
        {
            var topLeft = ToScreen(minX, maxZ);
            var bottomRight = ToScreen(maxX, minZ);
            return new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);
        }

        public Rect ToScreenRect(in LastShiftCompartmentSpec spec) =>
            ToScreenRect(spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ);

        /// <summary>선체 내부 영역(방·통로가 타일링한 <c>38×6</c>).</summary>
        public Rect HullInteriorRect => ToScreenRect(
            -LastShiftShipDimensions.HalfLength, LastShiftShipDimensions.HalfLength,
            -LastShiftShipDimensions.HalfWidth, LastShiftShipDimensions.HalfWidth);

        /// <summary>자유면 한 구간을 두께 <paramref name="thickness"/> 픽셀 띠로 눕힌다.</summary>
        public Rect ToScreenBand(in LastShiftFreeFace face, float thickness)
        {
            var start = ToScreen(face.Start);
            var end = ToScreen(face.End);
            var minX = Mathf.Min(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var width = Mathf.Abs(end.x - start.x);
            var height = Mathf.Abs(end.y - start.y);

            return face.OnXFace
                ? new Rect(minX - thickness * 0.5f, minY, thickness, height)
                : new Rect(minX, minY - thickness * 0.5f, width, thickness);
        }

        /// <summary>
        /// 압력 구역 하나가 도면에서 차지하는 세로 띠. <b>선체 내부만 칠한다</b> — 구역은
        /// 스파인 구간이고, 붙은 모듈의 귀속은 사슬이 정하지 좌표가 정하지 않는다(조항 F-1).
        /// </summary>
        public Rect ToScreenRect(LastShiftZone zone) => ToScreenRect(
            LastShiftShipDimensions.ZoneMinX(zone), LastShiftShipDimensions.ZoneMaxX(zone),
            -LastShiftShipDimensions.HalfWidth, LastShiftShipDimensions.HalfWidth);

        /// <summary>원반 테두리 위 <paramref name="step"/> 번째 점. 그레이박스는 현으로 두른다.</summary>
        public Vector2 RimPoint(int step, int steps)
        {
            var radians = Mathf.PI * 2f * step / Mathf.Max(steps, 1);
            var point = LastShiftHullShell.PointAt(radians);
            return ToScreen(point.x, point.y);
        }
    }
}
