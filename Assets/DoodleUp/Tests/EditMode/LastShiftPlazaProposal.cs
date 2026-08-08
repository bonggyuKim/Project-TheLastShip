using DoodleUp.Runtime;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 선수 조종석 + 중앙 광장 도안(<c>docs/bow-cockpit-central-plaza-layout-v1.md</c>)의 좌표표.
    /// §7-2("원반 파라미터 확정과 구획 내접·겹침 정밀 검증")가 답해야 하는 대상이 이것이다.
    ///
    /// <b>왜 <c>Runtime</c> 이 아니라 테스트에 있는가.</b> 도안 §0 이 "도안까지만이다 — 씬 구현은
    /// 이 문서 승인 후 별도 카드" 라고 못박았다. 이 표를 지금 <see cref="LastShiftHullShell"/>·
    /// <see cref="LastShiftCompartments"/> 에 넣으면 아직 서 있는 배(원반 <c>84x40</c>,
    /// 회랑 둘, 선수 사슬 넷)가 좌표만 바뀐 채로 무너진다. 그래서 정본은 안 건드리고 <b>제안표를
    /// 따로 세워 그 위에서만 재는</b> 자리를 만든다 — §17.7-2 가 상부 회랑 좌표를 그렇게 다뤘던
    /// 것과 같은 종류다.
    ///
    /// 재는 자(선분 대 <c>AABB</c>, 내접 다각형, 게이지 판독)는 전부 정본 쪽을 그대로 쓴다. 도안이
    /// 개구부·배플·게이지·문 좌표를 하나도 안 옮기기 때문에(도안 §2.3·§2.4) <c>T4</c>/<c>T5</c> 는
    /// <b>표본 공간만 바뀐 같은 검사</b>다.
    /// </summary>
    public static class LastShiftPlazaProposal
    {
        // ── 원반 파라미터 (§7-2 확정) ────────────────────────────────────────
        // 도안 초안은 a=24 / b=20 / 중심 x=+7 이었고, 그 셋 중 <b>b 하나만</b> 움직였다.
        // 근거는 아래 세 검사가 초안에서 실제로 낸 값이다(LastShiftPlazaProposalTests).
        //
        //   의무실 (+19~+23, +11~+15)  r^2 = 1.0069  → 타원 <b>밖</b>. 초안이 못 세운다
        //   정비창 (-11~ -8, -13~ -9)  내접 여유 0.119m  → 판 두께(0.2)보다 얇다
        //   격납고 ( +7~+17, +10~+18)  내접 여유 0.132m  → 같은 이유
        //
        // 여유가 판 두께보다 얇으면 "타원 안" 이라는 판정이 씬에서 거짓이 된다 — 구획 바깥
        // 판(0.2)과 테두리 판(0.2)이 서로를 파고든다. 그래서 요구 여유를 두 판의 합으로 둔다.
        //
        // <b>길이가 아니라 폭으로 푼 것은 도안 §6-5 가 지정한 순서다</b> — "장축을 다시 늘리면
        // §29 의 일자형 진단으로 되돌아간다. b 를 20 → 22~24 로 키워 폭으로 보상하는 쪽을
        // 권한다". 그 권고 구간의 하한이 실제로 충분했다: b=21 은 의무실 여유가 0.471m 로
        // 요구선 아래이고, b=22 에서 최빡빡(에어록)이 0.758m 가 된다.
        //
        // 그래서 전장·선수·선미는 도안이 사용자에게 설명한 값 그대로다(x -17 ~ +31, 48m).
        // 사용자 확인 항목 §7-7(c)("원반이 84m → 48m 로 짧아지는 것")이 안 흔들린다.

        /// <summary>장축 반지름. 도안 초안값 유지 — 전장 <c>48m</c> 은 사용자 확인 대상이었다.</summary>
        public const float SemiMajorX = 24f;

        /// <summary>단축 반지름. 초안 <c>20</c> → 확정 <c>22</c>. 전폭 <c>44m</c>.</summary>
        public const float SemiMinorZ = 22f;

        /// <summary>도안 초안의 단축 반지름. 이 값에서 무엇이 밖으로 나갔는지를 회귀로 고정한다.</summary>
        public const float DraftSemiMinorZ = 20f;

        /// <summary>
        /// 타원 중심 <c>x</c>. 원점이 아닌 이유는 스파인이 더 이상 원점 대칭이 아니기 때문이다 —
        /// 조종석이 선수로 나가고 큰 방 둘이 선미로 오면서 무게중심이 뒤로 갔다.
        /// </summary>
        public const float CenterX = 7f;

        public const float BowX = CenterX - SemiMajorX;
        public const float SternX = CenterX + SemiMajorX;
        public const float OverallLength = SemiMajorX * 2f;
        public const float OverallWidth = SemiMinorZ * 2f;
        public const float AspectRatio = SemiMajorX / SemiMinorZ;

        /// <summary>
        /// 구획 발자국이 내접 다각형 안쪽으로 최소한 확보해야 하는 거리. 구획 바깥 판과 테두리
        /// 판이 각각 <see cref="LastShiftCompartments.PanelThickness"/> 이므로
        /// 그 합이다. 이보다 얇으면 두 판이 씬에서 서로를 파고든다.
        /// </summary>
        public const float MinInscribedClearance =
            LastShiftCompartments.PanelThickness * 2f;

        /// <summary>테두리를 근사하는 판의 수. 정본과 같은 값을 쓴다 — 새그 비교가 성립해야 한다.</summary>
        public const int SegmentCount = LastShiftHullShell.SegmentCount;

        // ── 중앙 광장 (§2.3) ─────────────────────────────────────────────────

        /// <summary>광장 <c>x</c> 하한. 조종석 방 선미벽이자 개구부 <c>0</c> 의 평면이다.</summary>
        public const float PlazaMinX = -11f;

        /// <summary>광장 <c>x</c> 상한. 전력실 선수벽이자 개구부 <c>1</c> 의 평면이다.</summary>
        public const float PlazaMaxX = -5f;

        public const float PlazaMinZ = -9f;
        public const float PlazaMaxZ = 9f;

        public const float PlazaWidthZ = PlazaMaxZ - PlazaMinZ;
        public const float PlazaLengthX = PlazaMaxX - PlazaMinX;

        // ── 발자국표 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 도안이 세우는 평면 발자국 하나. 압력 스파인과 보조 구획을 <b>같은 표</b>에 넣는 이유는
        /// 겹침·내접·점유율이 셋 다 "무엇이 원반 바닥을 차지하는가" 만 묻기 때문이다. 이 표는
        /// 압력 편입 여부를 말하지 않는다 — 그건 §9.3 이 이미 답했다.
        /// </summary>
        public readonly struct Footprint
        {
            public Footprint(string name, float minX, float maxX, float minZ, float maxZ,
                bool openInP0 = true, bool protrudes = false)
            {
                Name = name;
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
                OpenInP0 = openInP0;
                Protrudes = protrudes;
            }

            public string Name { get; }
            public float MinX { get; }
            public float MaxX { get; }
            public float MinZ { get; }
            public float MaxZ { get; }

            /// <summary>P0 에서 드나드는가. 확장 검토 §2.2 의 잠긴 셋만 <c>false</c> 다.</summary>
            public bool OpenInP0 { get; }

            /// <summary>원반 밖으로 나가는 것이 의도인가. 조종석 방 하나뿐이다(§2.1).</summary>
            public bool Protrudes { get; }

            public float LengthX => MaxX - MinX;

            /// <summary>폭. 축 규약대로 <c>z</c> 범위다 — 확장 검토 §1.3 표가 센 것이 이 값이다.</summary>
            public float WidthZ => MaxZ - MinZ;

            public float Area => LengthX * WidthZ;

            /// <summary>좌현(<c>z &lt; 0</c>) 쪽 발자국. 광장·스파인처럼 축을 걸친 것이 있어 면적으로 센다.</summary>
            public float PortArea => LengthX * Mathf.Max(0f, Mathf.Min(MaxZ, 0f) - MinZ);
        }

        /// <summary>
        /// 도안 §2.4 표 + 압력 스파인. 통로 A 는 여기 없다 — 광장이 흡수했다(§2.3).
        /// 통로 B 는 남으므로 폭 <c>3.6m</c> 그대로 들어간다.
        /// </summary>
        public static readonly Footprint[] Footprints =
        {
            // 압력 스파인. 좌표 변경 없음(§2.4 마지막 단락).
            new("조종석 방", -19f, -11f, -3f, 3f, protrudes: true),
            new("중앙 광장", PlazaMinX, PlazaMaxX, PlazaMinZ, PlazaMaxZ),
            new("전력실 방", -5f, 0f, -3f, 3f),
            new("냉각실 방", 0f, 5f, -3f, 3f),
            new("통로 B", 5f, 11f, -3f, 0.6f),
            new("산소실 방", 11f, 19f, -3f, 3f),

            // FORE 클러스터 — 부모는 전부 광장.
            new("관측실", -8f, -5f, -14f, -9f),
            new("서버/통신실", -8f, -5f, 9f, 14f, openInP0: false),
            new("정비창", -11f, -8f, -13f, -9f),
            new("에어록", -14f, -11f, -9f, -6f),

            // AFT 클러스터 — 부모는 산소실 방 또는 그 사슬.
            new("격납고", 7f, 17f, 10f, 18f),
            new("수경재배·산소재생실", 11f, 17f, 3f, 9f, openInP0: false),
            new("화물칸", 7f, 15f, -13f, -5f),
            new("화장실", 19f, 21f, -3f, 3f),
            new("구명정", 21f, 25f, -2f, 2f),
            new("숙소", 19f, 25f, 4f, 10f),
            new("휴게실", 19f, 25f, -10f, -4f),
            new("의무실", 19f, 23f, 11f, 15f, openInP0: false)
        };

        // ── 원반 기하 ────────────────────────────────────────────────────────
        // 정본(LastShiftHullShell)을 못 쓰는 이유는 중심 x 하나뿐이다 — 그쪽은 원점 대칭을
        // 전제로 상수를 짜 두었고, 그 전제가 이 도안에서 깨진다. 식은 같은 것을 쓴다.

        public static float NormalizedRadiusSquared(float x, float z)
        {
            var nx = (x - CenterX) / SemiMajorX;
            var nz = z / SemiMinorZ;
            return nx * nx + nz * nz;
        }

        /// <summary>발자국 네 모서리 중 가장 바깥의 정규화 반지름 제곱.</summary>
        public static float WorstCornerRadiusSquared(in Footprint footprint)
        {
            var worst = 0f;
            foreach (var corner in Corners(footprint))
                worst = Mathf.Max(worst, NormalizedRadiusSquared(corner.x, corner.y));
            return worst;
        }

        public static Vector2 SegmentStart(int index, float semiMinorZ = SemiMinorZ)
        {
            var radians = Mathf.PI * 2f * index / SegmentCount;
            return new Vector2(CenterX + SemiMajorX * Mathf.Cos(radians), semiMinorZ * Mathf.Sin(radians));
        }

        /// <summary>
        /// 점에서 <b>실제로 서는 내접 다각형</b>까지의 부호 있는 거리. 양수면 안이고 그 값이
        /// 그대로 여유다. <see cref="LastShiftHullShell.InscribedContains"/> 가 예/아니오만
        /// 답하는 자리에서 <b>얼마나</b> 를 답한다 — 파라미터를 정하려면 통과 여부가 아니라
        /// 어느 구획이 몇 미터 남겼는지를 봐야 한다.
        /// </summary>
        public static float InscribedMargin(float x, float z, float semiMinorZ = SemiMinorZ)
        {
            var margin = float.MaxValue;
            for (var index = 0; index < SegmentCount; index++)
            {
                var start = SegmentStart(index, semiMinorZ);
                var edge = SegmentStart((index + 1) % SegmentCount, semiMinorZ) - start;
                var offset = new Vector2(x, z) - start;
                // t 가 증가하는 방향이 반시계이므로 안쪽이 외적 양수인 쪽이다(정본과 같은 규약).
                margin = Mathf.Min(margin, (edge.x * offset.y - edge.y * offset.x) / edge.magnitude);
            }
            return margin;
        }

        /// <summary>발자국 네 모서리 중 가장 얇은 내접 여유.</summary>
        public static float InscribedMargin(in Footprint footprint, float semiMinorZ = SemiMinorZ)
        {
            var margin = float.MaxValue;
            foreach (var corner in Corners(footprint))
                margin = Mathf.Min(margin, InscribedMargin(corner.x, corner.y, semiMinorZ));
            return margin;
        }

        /// <summary>맞닿는 면은 겹침이 아니다 — 정본 <c>VolumesOverlap</c> 과 같은 열린 구간 비교다.</summary>
        public static bool Overlap(in Footprint a, in Footprint b) =>
            a.MinX < b.MaxX - Epsilon && b.MinX < a.MaxX - Epsilon &&
            a.MinZ < b.MaxZ - Epsilon && b.MinZ < a.MaxZ - Epsilon;

        /// <summary>이 <c>z</c> 에서 발자국이 원반에 드는 <c>x</c>. 조종석 돌출 길이가 여기서 나온다.</summary>
        public static float BowEntryX(float z) =>
            CenterX - SemiMajorX * Mathf.Sqrt(Mathf.Max(0f, 1f - z * z / (SemiMinorZ * SemiMinorZ)));

        public static float MaxChordSag(float semiMinorZ = SemiMinorZ)
        {
            var worst = 0f;
            for (var index = 0; index < SegmentCount; index++)
            {
                var start = SegmentStart(index, semiMinorZ);
                var end = SegmentStart((index + 1) % SegmentCount, semiMinorZ);
                var radians = Mathf.PI * 2f * (index + 0.5f) / SegmentCount;
                var mid = new Vector2(
                    CenterX + SemiMajorX * Mathf.Cos(radians), semiMinorZ * Mathf.Sin(radians));
                worst = Mathf.Max(worst, Vector2.Distance((start + end) * 0.5f, mid));
            }
            return worst;
        }

        private static Vector2[] Corners(in Footprint footprint) => new[]
        {
            new Vector2(footprint.MinX, footprint.MinZ),
            new Vector2(footprint.MinX, footprint.MaxZ),
            new Vector2(footprint.MaxX, footprint.MinZ),
            new Vector2(footprint.MaxX, footprint.MaxZ)
        };

        private const float Epsilon = 0.0001f;
    }
}
