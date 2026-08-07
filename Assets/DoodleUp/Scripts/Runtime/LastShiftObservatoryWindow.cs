using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 관측실 창의 좌표 정본. <b>이 방에는 창이 없었다</b> — 관측실은 <c>x -35~-32</c> 라
    /// 좌현 창 띠(<see cref="LastShiftHullFrames.WindowKeepOutHalfX"/> 로 <c>|x| &lt;= 25</c>)
    /// 밖이고, 방 바깥은 원반 껍질 속이라 벽을 뚫어도 회색 자투리만 보인다. 지금 "관측" 을
    /// 말하는 것은 조준 콘솔과 별표뿐이라 이름과 실제가 안 맞는다
    /// (아트 정본 <c>last-shift-bow-chain-dressing-v1.md</c> §7-6).
    ///
    /// <b>창 하나로는 안 되고 셋이 한 벌이다.</b> 방 끝벽에 구멍만 내면 껍질 속을 보게 되고,
    /// 테두리에 유리만 넣으면 방에서 그 유리가 안 보인다. 그래서
    /// <list type="number">
    ///   <item>관측실 선수 끝벽(<c>x = -35</c>)에 창 개구부</item>
    ///   <item>그 앞 시선 통로에 골조 금지(<see cref="IsSightKeepOut"/>)</item>
    ///   <item>원반 선수 테두리에 유리 판(<see cref="SegmentIsBowBay"/>)과 그 밖의 배경막</item>
    /// </list>
    /// 셋을 같은 상수에서 뽑는다. 좌현 창이 <see cref="LastShiftHullFrames"/> 에서 같은
    /// 방식으로 묶여 있고, 그 셋 중 하나만 리터럴로 갈리면 창에 회색 보가 뜬다.
    ///
    /// <b>유리 판을 안 만든다.</b> 이 배의 창은 전부 뚫린 개구부다(좌현 <c>OuterHull_Front*</c>
    /// 는 <c>0.6~2.1</c> 이 그냥 비어 있다). 형태·마감은 아트 몫이고(§7-6 "창 형태/배치는
    /// art"), 여기서 정하는 것은 <b>구멍의 좌표</b>뿐이다.
    /// </summary>
    public static class LastShiftObservatoryWindow
    {
        public const LastShiftCompartment Compartment = LastShiftCompartment.Observatory;

        /// <summary>창이 뚫리는 벽. 관측실 선수 끝벽이고 원반 선수 가장자리에 가장 가깝다.</summary>
        public static float WallX => LastShiftCompartments.Of(Compartment).MinX;

        /// <summary>
        /// 개구부 폭(<c>z</c>). 방 폭이 <c>4m</c> 라 양쪽에 <c>0.8m</c> 씩 벽이 남는다 —
        /// 통짜 유리로 만들면 끝벽이 사라져 방이 아니라 발코니가 된다.
        /// </summary>
        public const float OpeningWidth = 2.4f;

        /// <summary>
        /// 개구부 아랫단. 좌현 창 문턱(<c>0.6</c>)보다 높다 — 저중력에서 뜬 몸이 그대로
        /// 미끄러져 나가지 않을 만큼은 턱이 있어야 하고, 앉아서 보는 방이 아니라 서서
        /// 보는 방이다.
        /// </summary>
        public const float SillHeight = 0.9f;

        /// <summary>개구부 윗단. 문 구멍(<c>2.2</c>)보다 높고 천장(<c>3.0</c>) 아래로 <c>0.6</c> 남긴다.</summary>
        public const float HeadHeight = 2.4f;

        /// <summary>
        /// 시선이 개구부에서 퍼지는 기울기. <b>방 뒤벽에 선 사람 기준</b>이다 — 문 쪽 벽
        /// (<c>x = -32</c>)에서 개구부까지 <c>3m</c> 이고 개구부 반폭이 <c>1.2m</c> 라
        /// <c>1.2 / 3 = 0.4</c> 가 그 사람이 실제로 훑는 각이다. <c>0.5</c> 로 올려 둔 것은
        /// 테두리 판이 <c>7.5°</c> 단위라 <c>0.4</c> 에서는 바깥쪽 판 한 장이 <c>0.08m</c>
        /// 차이로 걸리거나 안 걸리기 때문이다 — 그 정도 여유가 없으면 원반 치수를 조금만
        /// 건드려도 유리 한 장이 조용히 불투명 판으로 바뀐다.
        ///
        /// 유리에 얼굴을 붙이면 이보다 넓게 보이지만 그건 어느 창이나 그렇다. 이 값을 그
        /// 극단에 맞추면 선수 테두리 절반이 유리가 되고, 원반 실루엣이 창으로 갈린다.
        /// </summary>
        public const float SightSpread = 0.5f;

        /// <summary>이 <c>x</c> 에서 창이 훑는 <c>z</c> 반폭. 개구부 앞에서만 뜻이 있다.</summary>
        public static float SightHalfZAt(float x) =>
            OpeningWidth * 0.5f + Mathf.Max(0f, WallX - x) * SightSpread;

        /// <summary>
        /// 이 평면 좌표가 창 앞인가. 좌현 창의 <see cref="LastShiftHullFrames.IsWindowKeepOut"/>
        /// 와 같은 용도다 — 여기 골조를 세우면 창에서 회색 보가 우주에 떠 있는 것으로 보인다.
        /// 다른 점은 부채꼴이라는 것뿐이고, 그건 이 창이 띠가 아니라 구멍 하나이기 때문이다.
        /// </summary>
        public static bool IsSightKeepOut(float x, float z) =>
            x < WallX && Mathf.Abs(z) <= SightHalfZAt(x);

        /// <summary>
        /// 이 테두리 세그먼트가 <b>선수 창 판</b>인가. 좌현 창과 같은 규칙으로 중점으로
        /// 판정한다 — 씬 빌더가 판을 중점에 놓기 때문에 끝점으로 재면 반 칸 어긋난다.
        ///
        /// 좌현 창(<see cref="LastShiftHullFrames.SegmentIsWindowBay"/>)과 <b>따로</b> 센다.
        /// 둘을 한 판정으로 합치면 "창 판은 끊기지 않은 호 하나" 라는 좌현 쪽 불변식이
        /// 깨지고, 그 불변식은 조종석에서 보는 별 띠가 안 잘린다는 뜻이라 유지해야 한다.
        /// </summary>
        public static bool SegmentIsBowBay(int segment)
        {
            var start = LastShiftHullShell.SegmentStart(segment);
            var end = LastShiftHullShell.SegmentStart((segment + 1) % LastShiftHullShell.SegmentCount);
            var middle = (start + end) * 0.5f;
            return IsSightKeepOut(middle.x, middle.y);
        }

        public static int BowBaySegmentCount
        {
            get
            {
                var count = 0;
                for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
                    if (SegmentIsBowBay(segment)) count++;
                return count;
            }
        }

        /// <summary>선수 창의 이음매. 좌현과 같이 연속 <c>n</c>장이면 <c>n+1</c>곳이다.</summary>
        public static int[] BowMullionSeams()
        {
            var count = LastShiftHullShell.SegmentCount;
            var seam = new bool[count];
            for (var segment = 0; segment < count; segment++)
            {
                if (!SegmentIsBowBay(segment)) continue;
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

        /// <summary>선수 유리가 실제로 덮는 <c>z</c> 반폭. 이음매 좌표에서 실측한다.</summary>
        public static float GlassHalfZ
        {
            get
            {
                var result = 0f;
                foreach (var seam in BowMullionSeams())
                    result = Mathf.Max(result, Mathf.Abs(LastShiftHullShell.SegmentStart(seam).y));
                return result;
            }
        }

        /// <summary>
        /// 배경막이 선 <c>x</c>. 좌현 배경막이 단축 반지름 밖 <c>2m</c> 인 것과 같은 규칙이다 —
        /// 원반 안에 두면 껍질에 갇혀 창에서 안 보인다.
        /// </summary>
        public static float BackdropX => -LastShiftHullShell.SemiMajorX - 2f;

        /// <summary>
        /// 배경막 반폭(<c>z</c>). 유리 폭의 두 배에 여유를 얹는다 — 비스듬한 시선이 유리를
        /// 지나면서 벌어지는 만큼이고, 그보다 좁으면 창 구석에서 배경막 가장자리가 보인다.
        /// </summary>
        public static float BackdropHalfZ => GlassHalfZ * 2f + 2f;

        /// <summary>
        /// 별 판 앞면이 넘어서면 안 되는 <c>x</c>. 좌현
        /// <see cref="LastShiftHullFrames.WindowStarNearestZ"/> 와 같은 이유다 — 별이 유리보다
        /// 앞에 서면 승무원과 유리 사이에 별이 떠 있는 것으로 보인다.
        /// </summary>
        public static float StarNearestX => -LastShiftHullShell.SemiMajorX - 0.5f;
    }
}
