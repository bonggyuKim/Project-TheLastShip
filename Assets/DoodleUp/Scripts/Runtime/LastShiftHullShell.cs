using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 원반(<c>UFO</c>) 외피 타원의 좌표 정본. 기획 정본은
    /// <c>docs/corridor-4p-redesign-v1.md</c> §26(방향)과 §27.2(치수 확정)다.
    ///
    /// <b>이것은 껍질이지 위상이 아니다.</b> §26.5 가 못박은 대로 <c>Resolve()</c>·<c>RG-1</c>·
    /// 시선차단·<c>SIMUL_ZONES</c> 는 전부 핵심 4구역의 <b>내부</b> 치수와 인접 관계로
    /// 계산되고, 타원은 그 바깥을 감싸는 외곽선일 뿐이다. 그래서 여기 있는 값 중 어느 것도
    /// 압력 판정에 들어가지 않는다 — 이 클래스가 <see cref="LastShiftZoneAtlas"/> 를
    /// 참조하지 않는 것이 그 경계를 코드에서 지키는 자리다.
    ///
    /// <b>방을 곡선에 맞추지 않는다</b>(§27.3). 구획 좌표는 §17.4/§21 그대로 두고 외피만
    /// 씌운다 — 좌표를 곡선에 맞춰 재배치하면 이미 통과한 겹침 검증(§21.1)이 통째로
    /// 다시 대상이 되는데, 그 비용이 타원을 조금 줄이는 이득보다 크다는 것이 §27.2 의
    /// 판단이다. 직사각형 방과 타원 사이 자투리를 무엇으로 채울지는 <c>art</c> 몫이다
    /// (§27.7-1) — 여기서는 테두리 선만 정본으로 둔다.
    ///
    /// 축 규약은 선체와 같다 — x = 장축(전장), z = 단축(전폭), y = 높이. 중심은 원점이고,
    /// 그 이유는 핵심 스파인(<c>x = ±19</c>)이 이미 원점 대칭이라 좌표계가 그대로 맞기
    /// 때문이다(§27.2).
    /// </summary>
    public static class LastShiftHullShell
    {
        /// <summary>
        /// 장축 반지름. §27.2 확정값 <c>42m</c>(전장 <c>84m</c>)이고, 이 값을 정한 것은
        /// 격납고의 먼 모서리 <c>(-27, +14)</c> 다 — <c>x</c> 극단 구획(관측실·구명정)은
        /// <c>z</c> 가 <c>±2</c> 라 타원 끝의 뾰족한 부분에 자연히 맞고, 격납고만
        /// <c>x</c> 가 중간이면서 <c>z</c> 가 커서 경계에 가장 먼저 걸린다.
        /// </summary>
        public const float SemiMajorX = 42f;

        /// <summary>단축 반지름. §27.2 확정값 <c>20m</c>(전폭 <c>40m</c>).</summary>
        public const float SemiMinorZ = 20f;

        public const float OverallLength = SemiMajorX * 2f;
        public const float OverallWidth = SemiMinorZ * 2f;

        /// <summary>
        /// 종횡비 <c>2.1:1</c>. §26.4 가 정원(正圓)을 기각한 근거가 이 값이다 — 스파인+클러스터의
        /// 실사용 폭(<c>z ±14</c>)에 비해 길이(<c>x ±35</c>)가 훨씬 길어서, 정원에 넣으면
        /// 안 쓰는 껍질 부피가 지나치게 커진다.
        /// </summary>
        public const float AspectRatio = SemiMajorX / SemiMinorZ;

        /// <summary>외피 판 두께. 구획과 같은 두께를 쓴다.</summary>
        public const float PanelThickness = LastShiftCompartments.PanelThickness;

        /// <summary>
        /// 테두리 벽의 높이. 구획 내부 높이에 위아래 판 두께를 더한 값이라 원반 테두리가
        /// 보조 구획 고리와 같은 층에서 끝난다 — §26.3 이 말한 "테두리 = 보조 구획 고리" 가
        /// 형상에서 성립하는 자리다.
        ///
        /// <b>렌즈 단면(가운데가 부푼 세로 프로파일)은 여기 없다.</b> §27.7-1 이 메시·자투리
        /// 처리를 <c>art</c> 로 남겼고, 그레이박스가 답해야 하는 것은 평면 실루엣뿐이다.
        /// 세로 프로파일을 지금 상수로 박으면 아트가 그 값을 정본으로 오인한다.
        /// </summary>
        public const float RimHeight = LastShiftCompartments.InteriorHeight + PanelThickness * 2f;

        /// <summary>테두리 벽 밑면의 y. 구획 바닥 슬래브 밑면과 같다.</summary>
        public const float RimBaseY = -PanelThickness;

        /// <summary>
        /// 테두리를 근사하는 직선 판의 개수. 그레이박스는 곡면 메시가 아니라 현(chord)으로
        /// 두른다 — 판 하나의 최대 새그(<see cref="MaxChordSag"/>)가 판 두께보다 작으면
        /// 눈으로는 곡선이고, 그 이상 쪼개는 것은 아트가 실제 메시를 넣을 때 할 일이다.
        /// </summary>
        public const int SegmentCount = 48;

        /// <summary>
        /// 정규화 반지름 제곱. <c>1</c> 이 타원 위이고 그보다 작으면 안이다. 거리로 안 재는
        /// 이유는 타원에서 "얼마나 안쪽인가" 를 재는 정직한 척도가 이것뿐이기 때문이다 —
        /// 유클리드 거리는 장축·단축에서 서로 다른 뜻이 된다.
        /// </summary>
        public static float NormalizedRadiusSquared(float x, float z)
        {
            var nx = x / SemiMajorX;
            var nz = z / SemiMinorZ;
            return nx * nx + nz * nz;
        }

        /// <summary>이 평면 좌표가 외피 안인가.</summary>
        public static bool Contains(float x, float z) => NormalizedRadiusSquared(x, z) <= 1f;

        /// <summary>
        /// 직사각형 발자국이 통째로 외피 안인가. 네 모서리만 본다 — 타원은 볼록이라
        /// 모서리가 전부 안이면 사각형 전체가 안이다.
        /// </summary>
        public static bool ContainsFootprint(float minX, float maxX, float minZ, float maxZ) =>
            WorstCornerRadiusSquared(minX, maxX, minZ, maxZ) <= 1f;

        /// <summary>
        /// 발자국 네 모서리 중 가장 바깥의 정규화 반지름 제곱. 어느 구획이 타원을 가장
        /// 빡빡하게 쓰는지가 이 값으로 갈리고, §27.2 가 격납고를 지목한 근거도 이것이다.
        /// </summary>
        public static float WorstCornerRadiusSquared(float minX, float maxX, float minZ, float maxZ)
        {
            var x = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX));
            var z = Mathf.Max(Mathf.Abs(minZ), Mathf.Abs(maxZ));
            return NormalizedRadiusSquared(x, z);
        }

        /// <summary>구획 발자국의 여유. <c>0</c> 이면 모서리가 타원 위, 음수면 밖이다.</summary>
        public static float FootprintMargin(in LastShiftCompartmentSpec spec) =>
            1f - WorstCornerRadiusSquared(spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ);

        /// <summary>매개변수 <c>t</c>(라디안) 위치의 테두리 점.</summary>
        public static Vector2 PointAt(float radians) =>
            new(SemiMajorX * Mathf.Cos(radians), SemiMinorZ * Mathf.Sin(radians));

        /// <summary>세그먼트 <paramref name="index"/> 의 시작 점. 끝 점은 다음 index 의 시작 점이다.</summary>
        public static Vector2 SegmentStart(int index) =>
            PointAt(Mathf.PI * 2f * index / SegmentCount);

        /// <summary>
        /// 실제로 세워지는 다각형(내접 현) 안인가. <see cref="Contains"/> 는 이상적인 타원을
        /// 보지만 씬에 서는 것은 직선 판 <see cref="SegmentCount"/> 장이고, 그 다각형은
        /// 타원보다 <see cref="MaxChordSag"/> 만큼 안쪽에 있다 — 타원 경계에 아슬아슬하게
        /// 붙은 발자국은 이상적인 검사를 통과하고도 실제 판에 잘린다.
        /// </summary>
        public static bool InscribedContains(float x, float z)
        {
            var point = new Vector2(x, z);
            for (var index = 0; index < SegmentCount; index++)
            {
                var start = SegmentStart(index);
                var edge = SegmentStart((index + 1) % SegmentCount) - start;
                var offset = point - start;
                // 매개변수 t 가 증가하는 방향이 반시계이므로 안쪽은 외적이 양수인 쪽이다.
                if (edge.x * offset.y - edge.y * offset.x < 0f) return false;
            }
            return true;
        }

        /// <summary>
        /// 주어진 <paramref name="x"/> 에서 <b>좌현</b>(<c>z &lt; 0</c>) 테두리의 z. 이상적인
        /// 타원이 아니라 <b>실제로 서는 내접 다각형(현)</b>의 값이다 — 테두리에 붙여 세우는
        /// 것(§29.4-(2) 관측 회랑)은 타원이 아니라 판을 기준으로 놓여야 판 뒤로 삐져나오지
        /// 않는다. <see cref="InscribedContains"/> 가 검사인 것과 같은 자리의 좌표 버전이다.
        ///
        /// 좌현 반쪽에서는 매개변수 <c>t</c> 가 커질수록 x 가 단조 증가하므로
        /// (<c>t=180°</c> 에서 <c>x=-42</c>, <c>t=360°</c> 에서 <c>x=+42</c>) 세그먼트를 훑어
        /// x 구간을 찾는다. 닫힌 식으로 안 푸는 이유는 <see cref="MaxChordSag"/> 와 같다 —
        /// 답해야 하는 것이 타원이 아니라 현이다.
        /// </summary>
        public static float PortEdgeZ(float x)
        {
            var clamped = Mathf.Clamp(x, -SemiMajorX, SemiMajorX);
            for (var index = SegmentCount / 2; index < SegmentCount; index++)
            {
                var start = SegmentStart(index);
                var end = SegmentStart((index + 1) % SegmentCount);
                if (clamped < start.x || clamped > end.x) continue;
                return Mathf.Lerp(start.y, end.y, Mathf.InverseLerp(start.x, end.x, clamped));
            }
            return -SemiMinorZ;
        }

        /// <summary>직사각형 발자국 네 모서리가 전부 내접 다각형 안인가.</summary>
        public static bool InscribedContainsFootprint(float minX, float maxX, float minZ, float maxZ) =>
            InscribedContains(minX, minZ) && InscribedContains(minX, maxZ) &&
            InscribedContains(maxX, minZ) && InscribedContains(maxX, maxZ);

        /// <summary>
        /// 현 근사의 최대 새그 — 판 하나의 중점이 진짜 타원에서 얼마나 안쪽으로 들어가는가.
        /// 이 값이 판 두께보다 작아야 테두리가 다각형이 아니라 곡선으로 읽힌다.
        ///
        /// 식으로 안 적고 세그먼트를 실제로 도는 이유는 <b>매개변수 <c>t</c> 가 기하 각이
        /// 아니기 때문이다</b> — 종횡비 <c>2.1</c> 에서 <c>t</c> 등분은 호 길이 등분이 아니라,
        /// 곡률 반지름과 반각을 곱하는 근사식이 어느 자리에서 얼마나 틀리는지가 종횡비에
        /// 따라 달라진다. <see cref="SegmentCount"/> 를 줄이려는 다음 사람이 근사식이 아니라
        /// 실제 수치를 보게 둔다.
        /// </summary>
        public static float MaxChordSag
        {
            get
            {
                var worst = 0f;
                for (var index = 0; index < SegmentCount; index++)
                {
                    var start = SegmentStart(index);
                    var end = SegmentStart((index + 1) % SegmentCount);
                    var mid = PointAt(Mathf.PI * 2f * (index + 0.5f) / SegmentCount);
                    worst = Mathf.Max(worst, Vector2.Distance((start + end) * 0.5f, mid));
                }
                return worst;
            }
        }
    }
}
