using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 방 벽과 원반 외피 사이 자투리를 채우는 <b>비게임플레이 구조체</b>의 좌표 정본.
    /// 기획 정본은 <c>docs/corridor-4p-redesign-v1.md</c> §27.3 이다 — "직사각형 방과 타원
    /// 외피 사이 빈 공간은 비-게임플레이 구조체(격벽 프레임, 배관 등)로 채우면 된다".
    ///
    /// <b>여기 있는 것은 전부 승무원이 못 가는 자리다.</b> 그 바깥에는 바닥이 없다 — 갑판은
    /// 방과 회랑 밑에만 깔린다. 그래서 이 구조체에는 콜라이더를 안 붙이고(씬 빌더가
    /// <c>CreateDecorCube</c> 를 쓴다), 압력·시선차단·<c>RG-1</c> 어느 검사에도 안 들어간다.
    /// 하는 일은 하나다: 창 너머가 아닌 <b>실내에서 밖을 볼 일이 없는 방향</b>으로도 배가
    /// 속이 빈 껍질이 아니라 골조를 가진 구조물로 읽히게 하는 것.
    ///
    /// <b>좌현 창 앞은 비운다.</b> 이 배의 좌현 창 너머는 진짜 우주가 아니라
    /// <see cref="WindowBackdropZ"/> 에 선 배경막(<c>SpaceVoid</c>)과 그 앞의 별 판이다
    /// (§21.2 가 서버/통신실을 우현으로 뒤집은 것과 같은 제약). 배경막보다 <b>앞</b>에
    /// 구조체를 세우면 창에서 회색 보가 우주에 떠 있는 것으로 보이고, 뒤에 세우면 어차피
    /// 배경막에 가려 안 보인다 — 어느 쪽이든 이득이 없다. 그래서 골조는
    /// <see cref="IsWindowKeepOut"/> 구간을 통째로 건너뛴다.
    ///
    /// <b>테두리 판은 이제 예외가 아니다.</b> 예전에는 같은 금지를 테두리에도 걸어 판
    /// <c>48</c>장 중 <c>10</c>장을 안 세웠는데, 그 근거는 배경막이 원반 <b>안</b>(<c>-9.1</c>)에
    /// 있던 시절 것이었다. §28.6-4 가 배경막을 <c>-22</c>(원반 밖)로 밀면서 근거가 사라졌고,
    /// §29.4-(1) 이 그 구간을 <see cref="SegmentIsWindowBay"/> 창 판으로 닫기로 확정했다.
    ///
    /// 축 규약은 선체와 같다 — x = 장축, z = 단축, y = 높이.
    /// </summary>
    public static class LastShiftHullFrames
    {
        /// <summary>방사형 격벽 프레임 수. 타원을 <c>15°</c> 씩 나눈다.</summary>
        public const int RibCount = 24;

        /// <summary>프레임 판 두께. 테두리 판(<c>0.2</c>)보다 두꺼워야 골조로 읽힌다.</summary>
        public const float RibSection = 0.3f;

        /// <summary>
        /// 프레임과 방·회랑 사이에 두는 여유. 방은 자기 벽 두께만큼 이미 바깥으로 나와
        /// 있으므로 그 위에 얹는 값이다 — <c>0</c> 이면 프레임이 방 벽에 맞닿아 붙어
        /// 그레이박스에서 어느 쪽 판인지 구분이 안 된다.
        /// </summary>
        public const float Clearance = 0.5f;

        /// <summary>
        /// 프레임을 세울 최소 길이. 이보다 짧으면 안 세운다 — 방이 외피에 거의 닿는 각도
        /// (격납고 어깨 쪽)에서 손톱만 한 판이 남는 것을 막는다.
        /// </summary>
        public const float MinRibLength = 1.5f;

        /// <summary>거들 링(둘레 보)이 놓이는 타원 배율. 프레임들을 가로로 묶는다.</summary>
        public const float RingScale = 0.78f;

        /// <summary>안쪽 끝을 찾을 때의 배율 보폭. 작을수록 프레임이 방에 가까이 붙는다.</summary>
        private const float ProbeStep = 0.005f;

        /// <summary>프레임 판 밑면·윗면. 테두리와 같은 층이라 골조가 껍질과 한 덩어리로 읽힌다.</summary>
        public const float BaseY = LastShiftHullShell.RimBaseY;

        public const float Height = LastShiftHullShell.RimHeight;

        /// <summary>거들 링 보의 y 중심. 프레임 판 윗단이다.</summary>
        public const float RingBeamY = BaseY + Height - RibSection * 0.5f;

        // ── 창 배경막 ────────────────────────────────────────────────────────
        // 씬 빌더의 SpaceVoid/StarField 가 쓰는 값과 같아야 한다. 리터럴을 양쪽에 두면
        // 배경막이 넓어질 때 프레임만 옛 폭을 믿고 창 앞으로 밀고 들어온다.

        /// <summary>
        /// 창 앞 구조체 금지 구간의 반폭. <b>배경막 반폭과 다른 값이다</b> — 예전에는 둘이
        /// 같은 상수였는데, 배경막을 원반 밖으로 밀면서 폭이 <c>45</c> 로 커졌다. 그 값을
        /// 그대로 금지 구간에 쓰면 좌현 절반의 자투리 구조체가 통째로 사라진다.
        /// 금지 구간은 배경막 크기가 아니라 <b>창이 실제로 훑는 범위</b>다.
        /// </summary>
        public const float WindowKeepOutHalfX = (LastShiftShipDimensions.InteriorLength + 12f) * 0.5f;

        /// <summary>
        /// 배경막 반폭. <c>45m</c> 다. 창에서 비스듬히 보는 시선이 배경막 가장자리를 넘지
        /// 않으려면 이만큼 필요하다 — 조종석 <c>x=-11, z=+3</c> 에서 창 <c>x=-19</c> 를
        /// 지나는 시선이 <c>z=-22</c> 평면에서 <c>x=-43.8</c> 에 닿는다. 예전 <c>25</c> 는
        /// 배경막이 <c>-9.1</c> 에 있을 때도 이미 <c>-26.9</c> 가 필요해 아슬아슬했다.
        /// </summary>
        public const float WindowBackdropHalfX = 45f;

        /// <summary>
        /// 배경막이 선 z. <b>원반 외피 바깥</b>이다 — 단축 반지름 <c>20</c> 에 여유 <c>2</c>.
        /// 예전에는 좌현 벽에서 <c>6m</c> 밖(<c>-9.1</c>)이었는데, 그 자리는 원반 안쪽이라
        /// 외피가 생긴 뒤로는 배경막이 껍질 속에 갇혀 있었다.
        /// </summary>
        public const float WindowBackdropZ = -LastShiftHullShell.SemiMinorZ - 2f;

        /// <summary>
        /// 별 판 <b>앞면</b>이 넘어서는 안 되는 z. 배경막과 달리 별 판은 두께가 있고 배경막
        /// 앞으로 흩뿌려지므로 판 앞면이 원반 안으로 들어올 수 있다 — §29.4-(1) 로 좌현
        /// 테두리에 유리가 생긴 뒤에는 그 별이 <b>창 앞에 떠 있는 것</b>으로 보인다.
        /// 아트 정본(<c>last-shift-hull-finish-v1.md</c> §5.2)이 "어색하면 별 판 최근접 z 를
        /// 밀어라, 그건 좌표라 tech 소관" 으로 남긴 자리다.
        ///
        /// 단축 반지름 밖 <c>0.5m</c>. 씬 빌더는 여기서 별 판 반두께를 더 뺀 자리를 상한으로
        /// 쓴다 — 중심이 아니라 앞면이 기준이라야 큰 별이 유리를 뚫지 않는다.
        /// </summary>
        public const float WindowStarNearestZ = -LastShiftHullShell.SemiMinorZ - 0.5f;

        /// <summary>
        /// 창이 보고 있는 좌현 구간인가. <b>구조체 금지에만 쓴다</b> — 골조(늑골·거들)가
        /// 여기 서면 창에서 회색 보가 우주에 떠 있는 것으로 보인다. 테두리 판은 이 금지를
        /// 안 받는다(§29.4-(1)); 대신 같은 구간이 <see cref="SegmentIsWindowBay"/> 로 창 판이
        /// 된다. 수평 호라서 렌즈 세로 프로파일 결정과 무관하다.
        /// </summary>
        public static bool IsWindowKeepOut(float x, float z) =>
            z < -LastShiftShipDimensions.HalfWidth && Mathf.Abs(x) <= WindowKeepOutHalfX;

        /// <summary>
        /// 이 테두리 세그먼트가 <b>창 판</b>인가. 판을 안 세우는 자리가 아니라, 불투명 판
        /// 대신 개구부 프리팹(<c>LSHull_WindowBay</c>)이 서는 자리다 — 테두리는 어느 쪽이든
        /// <see cref="LastShiftHullShell.SegmentCount"/> 장이 전부 선다.
        ///
        /// 판정은 세그먼트 <b>중점</b>으로 한다. 씬 빌더가 판을 중점에 놓기 때문에, 끝점으로
        /// 재면 호 경계에서 판 위치와 판정이 반 칸 어긋난다.
        /// </summary>
        public static bool SegmentIsWindowBay(int segment)
        {
            var start = LastShiftHullShell.SegmentStart(segment);
            var end = LastShiftHullShell.SegmentStart((segment + 1) % LastShiftHullShell.SegmentCount);
            var middle = (start + end) * 0.5f;
            return IsWindowKeepOut(middle.x, middle.y);
        }

        /// <summary>창 판 수. 로그·검사·씬 빌더가 같은 값을 봐야 한다.</summary>
        public static int WindowBaySegmentCount
        {
            get
            {
                var count = 0;
                for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
                    if (SegmentIsWindowBay(segment)) count++;
                return count;
            }
        }

        /// <summary>
        /// 멀리언이 서는 이음매. <b>세그먼트가 아니라 그 경계</b>라서 연속 <c>n</c>장이면
        /// <c>n+1</c>곳이다(양 끝 포함) — 양 끝 멀리언이 유리와 불투명 판의 경계를 마감한다.
        ///
        /// 인덱스를 리터럴로 박지 않는다. <see cref="LastShiftHullShell.SegmentCount"/> 나
        /// <see cref="WindowKeepOutHalfX"/> 가 바뀌면 번호가 통째로 밀린다.
        /// </summary>
        public static int[] WindowMullionSeams()
        {
            var count = LastShiftHullShell.SegmentCount;
            var seam = new bool[count];
            for (var segment = 0; segment < count; segment++)
            {
                if (!SegmentIsWindowBay(segment)) continue;
                seam[segment] = true;
                seam[(segment + 1) % count] = true;
            }

            var total = 0;
            foreach (var flag in seam)
                if (flag) total++;

            var result = new int[total];
            var index = 0;
            for (var segment = 0; segment < count; segment++)
                if (seam[segment]) result[index++] = segment;
            return result;
        }

        /// <summary>
        /// 이 평면 좌표에 구조체를 세워도 되는가. 조건 넷을 전부 만족해야 한다 —
        /// 실제로 세워지는 다각형 안이고, 창 앞이 아니고, 선체 바깥이고, 어떤 방·회랑
        /// 발자국에도 여유 안쪽으로 들어가지 않는다.
        /// </summary>
        public static bool IsFree(float x, float z)
        {
            if (!LastShiftHullShell.InscribedContains(x, z)) return false;
            if (IsWindowKeepOut(x, z)) return false;

            // 관측실 선수 창(§7-6). 좌현 창과 같은 이유로 그 앞에는 아무것도 안 선다.
            // 판정 자체는 LastShiftObservatoryWindow 가 갖는다 — 창 좌표 셋(개구부·금지
            // 구간·테두리 유리)이 한 자리에 모여 있어야 하나만 갈리는 일이 안 생긴다.
            if (LastShiftObservatoryWindow.IsSightKeepOut(x, z)) return false;

            // 선체 본체. 판 바깥면에서 다시 여유를 둔다.
            if (Mathf.Abs(x) <= LastShiftShipDimensions.EndWallX + Clearance &&
                Mathf.Abs(z) <= LastShiftShipDimensions.SideWallZ + Clearance) return false;

            var margin = LastShiftCompartments.PanelThickness + Clearance;
            foreach (var spec in LastShiftCompartments.Specs)
                if (x >= spec.MinX - margin && x <= spec.MaxX + margin &&
                    z >= spec.MinZ - margin && z <= spec.MaxZ + margin) return false;

            foreach (var leg in LastShiftUpperGallery.Legs)
                if (x >= leg.MinX - margin && x <= leg.MaxX + margin &&
                    z >= leg.MinZ - margin && z <= leg.MaxZ + margin) return false;

            // 관측 회랑(§29.4-(2)). 지금은 이 회랑이 통째로 IsWindowKeepOut 안에 있어서 위
            // 조건에서 이미 걸러지지만, 그 사실에 기대지 않는다 — 창 구간이 좁아지거나
            // 회랑이 길어지면 골조가 회랑 한가운데를 관통하게 되고, 그때 원인이 여기가
            // 아니라 창 상수에 있는 것으로 보인다.
            foreach (var leg in LastShiftObservationGallery.Legs)
                if (x >= leg.MinX - margin && x <= leg.MaxX + margin &&
                    z >= leg.MinZ - margin && z <= leg.MaxZ + margin) return false;

            return true;
        }

        /// <summary>프레임 <paramref name="rib"/> 의 매개변수 각.</summary>
        public static float RibAngle(int rib) => Mathf.PI * 2f * rib / RibCount;

        /// <summary>프레임 바깥 끝. 테두리 위다.</summary>
        public static Vector2 RibOuter(int rib) => LastShiftHullShell.PointAt(RibAngle(rib));

        /// <summary>
        /// 프레임 안쪽 끝. <b>식이 아니라 실측이다</b> — 테두리에서 원점 쪽으로 걸어 들어가다
        /// 처음 막히는 자리에서 멈춘다. 어느 각에서 무엇이 먼저 걸리는지는 방 열한 개와
        /// 회랑 다리 다섯의 배치가 정하는 것이라 닫힌 식으로 안 나온다.
        /// </summary>
        public static Vector2 RibInner(int rib)
        {
            var angle = RibAngle(rib);
            var scale = 1f;
            for (var probe = 1f; probe > 0f; probe -= ProbeStep)
            {
                var point = PointAt(angle, probe);
                if (!IsFree(point.x, point.y)) break;
                scale = probe;
            }
            return PointAt(angle, scale);
        }

        /// <summary>이 프레임을 세우는가. 바깥 끝이 막혔거나 남는 길이가 너무 짧으면 안 세운다.</summary>
        public static bool RibIsBuildable(int rib)
        {
            var outer = RibOuter(rib);
            if (!IsFree(outer.x, outer.y)) return false;
            return Vector2.Distance(outer, RibInner(rib)) >= MinRibLength;
        }

        /// <summary>거들 링 세그먼트의 시작 점.</summary>
        public static Vector2 RingSegmentStart(int segment) =>
            PointAt(Mathf.PI * 2f * segment / LastShiftHullShell.SegmentCount, RingScale);

        /// <summary>
        /// 이 거들 세그먼트를 세우는가. 양 끝과 중점이 전부 빈 자리여야 한다 — 끝만 보면
        /// 방을 가로지르는 현이 통과한다.
        /// </summary>
        public static bool RingSegmentIsBuildable(int segment)
        {
            var start = RingSegmentStart(segment);
            var end = RingSegmentStart((segment + 1) % LastShiftHullShell.SegmentCount);
            var middle = PointAt(
                Mathf.PI * 2f * (segment + 0.5f) / LastShiftHullShell.SegmentCount, RingScale);
            return IsFree(start.x, start.y) && IsFree(end.x, end.y) && IsFree(middle.x, middle.y);
        }

        /// <summary>실제로 세워지는 프레임 수. 로그와 검사가 같은 값을 봐야 한다.</summary>
        public static int BuildableRibCount
        {
            get
            {
                var count = 0;
                for (var rib = 0; rib < RibCount; rib++)
                    if (RibIsBuildable(rib)) count++;
                return count;
            }
        }

        /// <summary>실제로 세워지는 거들 세그먼트 수.</summary>
        public static int BuildableRingSegmentCount
        {
            get
            {
                var count = 0;
                for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
                    if (RingSegmentIsBuildable(segment)) count++;
                return count;
            }
        }

        private static Vector2 PointAt(float radians, float scale) => new(
            LastShiftHullShell.SemiMajorX * scale * Mathf.Cos(radians),
            LastShiftHullShell.SemiMinorZ * scale * Mathf.Sin(radians));
    }
}
