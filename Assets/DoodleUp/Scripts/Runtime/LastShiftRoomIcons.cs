using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 지도 머리표 첫 줄에 서는 <b>방 아이콘</b> — <c>8x8</c> 격자 실루엣을 사각형 조각으로 편다.
    ///
    /// <b>스프라이트도 폰트 글리프도 안 쓴다.</b> 지도를 그리는 <see cref="LastShiftUiLayer"/> 가
    /// 내주는 것은 단색 사각형(<c>Panel</c>)과 <c>LegacyRuntime.ttf</c> 글자(<c>Label</c>) 둘뿐이라,
    /// 아트가 만든 아이콘을 넣으려면 레이어 API 확장 + 아틀라스 + 프리팹 배선이 딸려 온다. 픽토그램
    /// 글리프(<c>⚡ ❄ 🛏</c>)는 그 폰트에 없어서 플랫폼에 따라 두부(<c>□</c>)로 뜨는데, 처음 하는
    /// 사람에게 두부가 뜨는 것은 아이콘이 없는 것보다 나쁘다. <b>사각형 조합은 지도가 이미 하고
    /// 있는 일이다</b> — 방 테두리도 문 선도 사람 표식도 전부 <c>Panel</c> 조각이다.
    ///
    /// <b>나중에 스프라이트로 갈아 끼울 자리다.</b> 아이콘이 여기 한 자리에서만 나오므로, 스프라이트가
    /// 생기면 이 클래스 안쪽만 바뀌고 부르는 쪽은 그대로다 — <see cref="LastShiftRoomLabels"/> 가
    /// 이름에 대해 세운 규약과 같다.
    ///
    /// <b>격자를 사각형으로 펴는 방법이 조각 수를 정한다.</b> 채워진 셀을 하나씩 그리면 방 여섯 +
    /// 승강구에 <c>200</c> 조각이 넘는다. 여기서는 <b>가로로 먼저 늘리고 그 띠를 세로로 늘린다</b> —
    /// 승강구 사다리처럼 세로로 긴 그림이 레일 두 개로 접혀서, 일곱 아이콘이 다 떠도 조각이
    /// <c>40</c> 안쪽이다. 접는 방식이 격자마다 몇 조각이 되는지는
    /// <c>LastShiftRoomIconTests</c> 가 잰다.
    ///
    /// 기획 정본: <c>docs/onboarding-map-icons-and-waypoint-v1.md</c> §3.3.
    /// </summary>
    public static class LastShiftRoomIcons
    {
        /// <summary>격자 한 변. 아이콘 상자 한 변을 이만큼 나눈 것이 셀 하나다.</summary>
        public const int GridSize = 8;

        /// <summary>
        /// 아이콘 하나가 쓰는 조각 수의 상한. 부르는 쪽이 잡아 두는 배열 크기이고,
        /// 일곱 격자 중 어느 것도 이 수를 안 넘는다는 것은 검사가 지킨다.
        /// </summary>
        public const int MaxBands = 8;

        /// <summary>광장 — 하치대에 쌓인 자재.</summary>
        private static readonly string[] Plaza =
        {
            "........",
            "........",
            "..##.##.",
            "..##.##.",
            "........",
            "########",
            "########",
            "........"
        };

        /// <summary>조종석 — 전면 스크린과 그 앞자리.</summary>
        private static readonly string[] Cockpit =
        {
            "........",
            "########",
            "########",
            "........",
            "...##...",
            "..####..",
            "..####..",
            "........"
        };

        /// <summary>
        /// 산소실 — 봄베 한 쌍. 목이 좁고 몸통이 넓다.
        ///
        /// <b>냉각실과 갈리는 근거는 실루엣의 굵기다</b> — 여기가 굵은 세로 둘이고 냉각실이
        /// 얇은 세로 넷이다. 이 둘이 일곱 중 가장 헷갈릴 조합이라, 격자를 손보게 되면
        /// 그 대비부터 지켜야 한다.
        /// </summary>
        private static readonly string[] LifeSupport =
        {
            ".##..##.",
            ".##..##.",
            "###..###",
            "###..###",
            "###..###",
            "###..###",
            "###..###",
            "........"
        };

        /// <summary>전력실 — 배터리. 위 단자 + 테두리 + 안쪽 눈금.</summary>
        private static readonly string[] Power =
        {
            "...##...",
            "########",
            "#......#",
            "#.####.#",
            "#.####.#",
            "#......#",
            "########",
            "........"
        };

        /// <summary>냉각실 — 방열핀 넷과 위 캡.</summary>
        private static readonly string[] Cooling =
        {
            "........",
            "#######.",
            "#######.",
            "#.#.#.#.",
            "#.#.#.#.",
            "#.#.#.#.",
            "#.#.#.#.",
            "........"
        };

        /// <summary>숙소 — 침대. 베개 + 매트리스.</summary>
        private static readonly string[] Quarters =
        {
            "........",
            "........",
            "##......",
            "##......",
            "########",
            "########",
            "#......#",
            "........"
        };

        /// <summary>중앙 승강구 — 위로 난 사다리.</summary>
        private static readonly string[] Shaft =
        {
            "#.####.#",
            "#......#",
            "#.####.#",
            "#......#",
            "#.####.#",
            "#......#",
            "#.####.#",
            "#......#"
        };

        /// <summary>이번 격자에서 이미 어느 조각에 먹힌 셀. 접는 동안만 쓰고 매번 지운다.</summary>
        private static readonly bool[] Taken = new bool[GridSize * GridSize];

        /// <summary>
        /// 이 방 격자가 <see cref="GridSize"/> 정사각인가. <b>줄 수나 줄 길이가 어긋나면 셀
        /// 좌표가 그림과 갈린다</b> — 짧은 줄은 읽다가 터지고, 긴 줄은 남는 칸이 조용히 안
        /// 그려져서 아이콘이 문서와 다른 모양이 된다. 격자를 손볼 때 그 어긋남을 잡는 자리다.
        /// </summary>
        public static bool IsSquareGrid(LastShiftPlazaSpace space) => Square(MaskOf(space));

        /// <summary>승강구 격자도 같은 정사각인가.</summary>
        public static bool ShaftIsSquareGrid() => Square(Shaft);

        private static bool Square(string[] mask)
        {
            if (mask.Length != GridSize) return false;
            foreach (var row in mask)
                if (row.Length != GridSize) return false;
            return true;
        }

        /// <summary>이 방 격자의 <paramref name="column"/>·<paramref name="row"/> 셀이 채워졌는가.</summary>
        public static bool Filled(LastShiftPlazaSpace space, int column, int row) =>
            Lit(MaskOf(space), column, row);

        /// <summary>승강구 격자의 셀 하나.</summary>
        public static bool ShaftFilled(int column, int row) => Lit(Shaft, column, row);

        /// <summary>
        /// 방 아이콘을 <paramref name="box"/> 안의 사각형 조각으로 편다.
        /// 쓴 조각 수를 돌려주고, <paramref name="into"/> 가 모자라면 거기서 멈춘다 —
        /// <see cref="MaxBands"/> 짜리 배열을 넘기면 잘릴 일이 없다.
        /// </summary>
        public static int Bands(LastShiftPlazaSpace space, Rect box, Rect[] into) =>
            Fold(MaskOf(space), box, into);

        /// <summary>승강구 아이콘을 조각으로 편다. 방과 같은 규약이다.</summary>
        public static int ShaftBands(Rect box, Rect[] into) => Fold(Shaft, box, into);

        private static string[] MaskOf(LastShiftPlazaSpace space) => space switch
        {
            LastShiftPlazaSpace.Plaza => Plaza,
            LastShiftPlazaSpace.CockpitRoom => Cockpit,
            LastShiftPlazaSpace.LifeSupportRoom => LifeSupport,
            LastShiftPlazaSpace.PowerRoom => Power,
            LastShiftPlazaSpace.CoolingRoom => Cooling,
            _ => Quarters
        };

        private static bool Lit(string[] mask, int column, int row) =>
            column >= 0 && column < GridSize && row >= 0 && row < GridSize
            && mask[row][column] == '#';

        /// <summary>
        /// 격자를 조각으로 접는다. <b>가로로 먼저, 그다음 세로로.</b> 순서가 뒤집히면 사다리가
        /// 가로 발판 넷 + 레일 여덟 조각으로 흩어진다 — 같은 그림이 조각 수만 늘어난다.
        /// </summary>
        private static int Fold(string[] mask, Rect box, Rect[] into)
        {
            System.Array.Clear(Taken, 0, Taken.Length);

            var cellWidth = box.width / GridSize;
            var cellHeight = box.height / GridSize;
            var count = 0;

            for (var row = 0; row < GridSize; row++)
            for (var column = 0; column < GridSize; column++)
            {
                if (!Free(mask, column, row)) continue;
                if (count >= into.Length) return count;

                var width = 1;
                while (Free(mask, column + width, row)) width++;

                var height = 1;
                while (SpanFree(mask, column, width, row + height)) height++;

                for (var r = row; r < row + height; r++)
                for (var c = column; c < column + width; c++)
                    Taken[r * GridSize + c] = true;

                into[count++] = new Rect(
                    box.xMin + column * cellWidth,
                    box.yMin + row * cellHeight,
                    width * cellWidth,
                    height * cellHeight);
            }

            return count;
        }

        private static bool Free(string[] mask, int column, int row) =>
            Lit(mask, column, row) && !Taken[row * GridSize + column];

        private static bool SpanFree(string[] mask, int column, int width, int row)
        {
            if (row >= GridSize) return false;
            for (var c = column; c < column + width; c++)
                if (!Free(mask, c, row)) return false;
            return true;
        }
    }
}
