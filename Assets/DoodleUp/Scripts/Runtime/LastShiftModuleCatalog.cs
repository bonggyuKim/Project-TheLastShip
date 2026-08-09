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
    /// <b>열은 기획 정본이다 — 카탈로그 v2</b>(<c>docs/core-four-rooms-and-hull-schematic-v1.md</c>
    /// §3.3, 구명정 제거 확정판은 <c>docs/outboard-outpost-and-map-final-v1.md</c> §9 의 M-0).
    /// 고정 구획 열하나 중 열이 자유 배치로 이관되므로 <b>이름 충돌이라는 문제 자체가
    /// 사라졌고</b>, 추상어로 지었던 셋(<c>산소 재생기실</c>·<c>보급 저장고</c>·<c>정거장 골조</c>)은
    /// 존재 이유를 잃어 기능이 같은 이관 방에 자리를 넘겼다 — 수경재배·화물칸·격납고다.
    ///
    /// <b>조항 K-1(<c>C-1</c> 대체) — 카탈로그 이름은 고정 <c>4</c>실(조종석·산소실·중앙 광장·
    /// 숙소)과만 안 겹치면 된다.</b> 이관 열의 이름은 카탈로그가 그대로 쓴다. <b>구명정은 어느
    /// 쪽에도 없다</b>(확정판 §2.1) — 카탈로그에 넣었으면 효과가 <c>0</c>인 최저가 항목이
    /// 관측실 옆에 하나 더 서서 "기능 없는 방을 여력 받고 팔지 않는다"가 스스로 깨졌을 자리다.
    ///
    /// <b>고정 표(<see cref="LastShiftCompartments.FixedCount"/>)는 아직 <c>11</c> 이다.</b>
    /// 표를 <c>4</c> 로 줄이는 것은 M-2 이고, 그 전까지 같은 방이 배에도 서 있고 카탈로그에도
    /// 있다 — 목록이 읽히는지를 먼저 보려고 일부러 그 순서로 쪼갰다(확정판 §9).
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

        /// <summary>`0` 연결 통로 — 효과 없음. 순수 지오메트리.</summary>
        public const int Corridor = 0;

        /// <summary>`1` 관측실 — 효과 없음. 창과 시야뿐인 최저가 둘째 칸.</summary>
        public const int Observatory = 1;

        /// <summary>`2` 예비 전력실 — 미연결 <c>BusPower</c> 하한 상향 + 예비 배터리.</summary>
        public const int ReservePower = 2;

        /// <summary>`3` 방열 라디에이터실 — <c>EngineHeat</c> 상승 감속.</summary>
        public const int Radiator = 3;

        /// <summary>`4` 서버/통신실 — 효과 미정(맵 개편 §7-3, <c>game-balance</c>).</summary>
        public const int ServerRoom = 4;

        /// <summary>`5` 정비창 — 효과 미정(맵 개편 §7-3).</summary>
        public const int Workshop = 5;

        /// <summary>`6` 의무실 — 효과 미정(맵 개편 §7-3).</summary>
        public const int MedBay = 6;

        /// <summary>`7` 수경재배 — 편입 구역 누출 감속. 예전 `산소 재생기실` 자리를 물려받았다.</summary>
        public const int Hydroponics = 7;

        /// <summary>`8` 화물칸 — 세 계통 예비 아이템 비치. 예전 `보급 저장고` 자리다.</summary>
        public const int CargoBay = 8;

        /// <summary>`9` 격납고 — 효과 없음. 종착 정산 전용. 예전 `정거장 골조` 자리다.</summary>
        public const int Hangar = 9;

        /// <summary>
        /// 맵 개편 §3.3 표 그대로. <c>LengthX</c> 는 문에서 멀어지는 깊이이고 <c>WidthZ</c> 는
        /// 접면 폭이다. <b>발자국은 이관 방의 현행 치수를 한 칸도 안 바꿨다</b> — 그래야 아트가
        /// 기존 그레이박스를 그대로 프리팹으로 뽑고, "여력을 다 쓰면 배가 원래 모양으로
        /// 복원된다" 가 성립한다.
        ///
        /// <b>효과가 없는 다섯이 일부러 들어 있다.</b> 둘(연결 통로 · 격납고)은 설계상 영영
        /// 없는 것이고, 셋(서버/통신실 · 정비창 · 의무실)은 수치가 아직 <c>game-balance</c>
        /// 미결이다(맵 개편 §7-3). 훅이 안 붙은 상태에서도 목록이 성립하는 것이
        /// <c>port-module-catalog-v1.md</c> §6 이 P-1 을 재미 판정 지점으로 잡은 근거다.
        ///
        /// 가격은 면적 기준이다 — <c>~16m²</c> 이하가 <c>1~2</c>, <c>~36m²</c> 가 <c>3</c>,
        /// <c>80m²</c> 가 <c>5</c>. 네 단(<c>1/2/3/5</c>)은 정본 §4.3 그대로 유지된다.
        /// </summary>
        private static readonly LastShiftModuleKind[] entries =
        {
            new("연결 통로", 4f, 2f, 1),
            new("관측실", 3f, 4f, 1),
            new("예비 전력실", 4f, 4f, 2),
            new("방열 라디에이터실", 3f, 6f, 2),
            new("서버/통신실", 4f, 6f, 2),
            new("정비창", 5f, 5f, 2),
            new("의무실", 5f, 5f, 2),
            new("수경재배", 6f, 6f, 3),
            new("화물칸", 8f, 8f, 3),
            new("격납고", 8f, 10f, 5)
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
