using System.Collections.Generic;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 기항에서 고를 수 있는 확장 모듈 한 종류. <b>발자국과 문뿐이다</b> — 값도, 잠금 조건도,
    /// 아이콘도 없다. 그 셋은 기항 화면(<c>docs/voyage-run-structure-v1.md</c> §4.2)이 정할
    /// 것이고, 그 화면이 아직 없다.
    ///
    /// <b>기준 자세는 "문이 <c>MinX</c> 면 한가운데" 다.</b> 배치 커서가 이 자세에서
    /// <c>90°</c> 4단으로 돌린다(<see cref="LastShiftModuleFootprint.Rotated"/>). 종류마다
    /// 기준 자세가 다르면 회전 횟수가 종류마다 다른 뜻을 갖게 되고, 그러면 "두 번 돌렸다"
    /// 를 화면에 적을 수 없다.
    /// </summary>
    public readonly struct LastShiftModuleKind
    {
        public LastShiftModuleKind(string name, float lengthX, float widthZ, float doorOffset = 0f)
        {
            Name = name;
            LengthX = lengthX;
            WidthZ = widthZ;
            DoorOffset = doorOffset;
        }

        /// <summary>화면에 뜨는 이름. <b>표에는 안 들어간다</b> — 표의 칸 이름은
        /// <see cref="LastShiftCompartments.ModuleName"/> 규칙이고, 거기에 문자열을 들이면
        /// 배치 판정 루프가 GC 를 만든다(추정 §3.2).</summary>
        public string Name { get; }

        public float LengthX { get; }
        public float WidthZ { get; }

        /// <summary>기준 자세에서 문이 <c>MinX</c> 면 어디에 오는가. <c>0</c> 이 면 한가운데다.</summary>
        public float DoorOffset { get; }

        public LastShiftModuleFootprint Footprint =>
            new(LengthX, WidthZ, LastShiftModuleFace.MinX, DoorOffset);
    }

    /// <summary>
    /// 기항 고정 목록. <b>랜덤 상점이 아니다</b> — 목록은 매 기항 같고 달라지는 것은
    /// "내가 몇 개를 놓을 수 있는가" 다(<c>docs/voyage-run-structure-v1.md</c> §4.2).
    ///
    /// <b>여기 다섯은 tech 가 고른 그레이박스 치수이고 기획 정본이 아니다.</b> 배치 흐름이
    /// 끝까지 도는 것을 보려면 고를 것이 있어야 해서 넣은 값이다 — 종류·크기·가격은
    /// <c>game-planning</c> 이 정한다. 지금 지켜야 하는 제약은 둘뿐이다:
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

        private static readonly LastShiftModuleKind[] entries =
        {
            new("연결칸", 2f, 2f),
            new("저장고", 3f, 3f),
            new("작업칸", 4f, 4f),
            new("관측칸", 3f, 5f),
            new("거주칸", 6f, 4f)
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
