using System.Collections.Generic;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 기항에서 고를 수 있는 확장 모듈 한 종류. <b>발자국과 문과 가격이다</b> — 효과도, 잠금
    /// 조건도, 아이콘도 없다. 효과는 <c>docs/port-module-catalog-v1.md</c> §3.3 이 종류마다
    /// 적어 두었고 수치는 <c>game-balance</c> 몫이라 아직 코드에 없다(같은 문서 §6 의 P-2).
    ///
    /// <b>기준 자세는 "문이 <c>MinX</c> 면 한가운데" 다.</b> 배치 커서가 이 자세에서
    /// <c>90°</c> 4단으로 돌린다(<see cref="LastShiftModuleFootprint.Rotated"/>). 종류마다
    /// 기준 자세가 다르면 회전 횟수가 종류마다 다른 뜻을 갖게 되고, 그러면 "두 번 돌렸다"
    /// 를 화면에 적을 수 없다.
    /// </summary>
    public readonly struct LastShiftModuleKind
    {
        public LastShiftModuleKind(string name, float lengthX, float widthZ, int maintenanceCost, float doorOffset = 0f)
        {
            Name = name;
            LengthX = lengthX;
            WidthZ = widthZ;
            MaintenanceCost = maintenanceCost;
            DoorOffset = doorOffset;
        }

        /// <summary>화면에 뜨는 이름. <b>표에는 안 들어간다</b> — 표의 칸 이름은
        /// <see cref="LastShiftCompartments.ModuleName"/> 규칙이고, 거기에 문자열을 들이면
        /// 배치 판정 루프가 GC 를 만든다(추정 §3.2).</summary>
        public string Name { get; }

        public float LengthX { get; }
        public float WidthZ { get; }

        /// <summary>
        /// 이 모듈을 세우는 데 드는 <b>정비 여력</b>(<c>docs/port-module-catalog-v1.md</c> §4.3).
        /// 한 기항의 여력 수입은 <c>래치 수 + 1</c> 이라 최대 <c>5</c> 이고, 쓰지 않은 것은
        /// 다음 기항으로 넘어간다(조항 M-1) — <b>그래서 가격 <c>5</c> 짜리를 사려면 한 기항을
        /// 통째로 쓰거나 두 기항에 걸쳐 모아야 한다.</b> 잔액을 들고 이 값을 빼는 쪽은 아직
        /// 없다(같은 문서 §6 의 P-1, <c>game-tech-director</c> 별도 카드).
        /// </summary>
        public int MaintenanceCost { get; }

        /// <summary>기준 자세에서 문이 <c>MinX</c> 면 어디에 오는가. <c>0</c> 이 면 한가운데다.</summary>
        public float DoorOffset { get; }

        public LastShiftModuleFootprint Footprint =>
            new(LengthX, WidthZ, LastShiftModuleFace.MinX, DoorOffset);
    }

    /// <summary>
    /// 기항 고정 목록. <b>랜덤 상점이 아니다</b> — 목록은 매 기항 같고 달라지는 것은
    /// "내가 몇 개를 놓을 수 있는가" 다(<c>docs/voyage-run-structure-v1.md</c> §4.2).
    ///
    /// <b>여섯은 기획 정본이다</b> — <c>docs/port-module-catalog-v1.md</c> §3.3. 앞서 있던
    /// 그레이박스 다섯(연결칸·저장고·작업칸·관측칸·거주칸)은 치수 이전에 <b>이름이 고정 구획과
    /// 부딪혀서</b> 통째로 갈렸다(같은 문서 §3.1): 배에 이미 정비창·관측실·숙소가 있는데
    /// 카탈로그가 작업칸·관측칸·거주칸을 팔면 플레이어가 자기가 무엇을 샀는지 모른다.
    /// <b>조항 C-1 — 카탈로그 이름은 <see cref="LastShiftCompartment"/> 열하나와 안 겹친다.</b>
    ///
    /// 목록 순서는 <b>가격 오름차순</b>이다. 첫 칸이 화면이 열릴 때 커서가 물고 있는 것이고
    /// 사슬 표본이 쓰는 것이므로, 가장 싸고 가장 안 위험한 것이 와야 한다.
    ///
    /// 지켜야 하는 제약은 둘이다:
    /// <list type="number">
    /// <item>치수가 <see cref="GridMeters"/> 의 배수여야 커서 스냅이 경계를 격자에 얹는다.</item>
    /// <item>문이 놓이는 면의 자유축이 <c>LastShiftZoneDoor.OpeningWidth</c> 보다 넓어야
    /// 한다(<see cref="LastShiftModuleFootprint.DoorFits"/>). 그래서 최소 변이 <c>2m</c> 다.</item>
    /// </list>
    /// </summary>
    public static class LastShiftModuleCatalog
    {
        /// <summary>
        /// 배치 격자. <c>1m</c> 다 — 확장 검토 §3.3 이 <c>45°</c> 를 기각한 것과 같은 이유로
        /// 연속 좌표를 안 쓴다. 격자에 안 얹힌 모듈은 벽이 <c>3cm</c> 어긋난 채로 서고,
        /// 그 틈은 씬에서만 보이며 표에서는 안 보인다.
        /// </summary>
        public const float GridMeters = 1f;

        // ── 항목 번호 ───────────────────────────────────────────────────────
        // 효과(LastShiftModuleEffects)가 종류를 번호로 가른다. 이름 문자열로 가르면 화면
        // 문구를 고치는 날 효과가 조용히 꺼지고, 그건 테스트가 이름을 같이 안 보면 안 잡힌다.
        // 순서는 가격 오름차순이므로 번호도 가격 오름차순이다.

        /// <summary>`0` 연결 통로 — 효과 없음.</summary>
        public const int Corridor = 0;

        /// <summary>`1` 산소 재생기실 — 편입 구역 누출 감속.</summary>
        public const int OxygenRecycler = 1;

        /// <summary>`2` 방열 라디에이터실 — <c>EngineHeat</c> 상승 감속.</summary>
        public const int Radiator = 2;

        /// <summary>`3` 예비 전력실 — 미연결 <c>BusPower</c> 하한 상향 + 예비 배터리.</summary>
        public const int ReservePower = 3;

        /// <summary>`4` 보급 저장고 — 세 계통 예비 아이템 비치.</summary>
        public const int SupplyDepot = 4;

        /// <summary>`5` 정거장 골조 — 효과 없음. 종착 정산 전용.</summary>
        public const int StationFrame = 5;

        /// <summary>
        /// <c>docs/port-module-catalog-v1.md</c> §3.3 표 그대로. <c>LengthX</c> 는 문에서
        /// 멀어지는 깊이이고 <c>WidthZ</c> 는 접면 폭이다.
        ///
        /// <b>효과가 없는 둘(연결 통로 · 정거장 골조)이 일부러 들어 있다.</b> 여섯 중 넷만
        /// 시뮬레이션 훅을 요구하므로, 훅이 하나도 안 붙은 상태에서도 목록이 성립한다 —
        /// 같은 문서 §6 이 P-1(여력 잔액)을 재미 판정 지점으로 잡은 근거다.
        /// </summary>
        private static readonly LastShiftModuleKind[] entries =
        {
            new("연결 통로", 4f, 2f, 1),
            new("산소 재생기실", 5f, 4f, 2),
            new("방열 라디에이터실", 3f, 6f, 2),
            new("예비 전력실", 4f, 4f, 2),
            new("보급 저장고", 6f, 6f, 3),
            new("정거장 골조", 8f, 10f, 5)
        };

        public static IReadOnlyList<LastShiftModuleKind> Entries => entries;

        public static int Count => entries.Length;

        public static LastShiftModuleKind At(int index) => entries[index];

        /// <summary>목록을 순환으로 읽는다. 화면에서 다음/이전을 누르는 자리가 쓴다.</summary>
        public static int Wrap(int index)
        {
            var count = entries.Length;
            return ((index % count) + count) % count;
        }
    }
}
