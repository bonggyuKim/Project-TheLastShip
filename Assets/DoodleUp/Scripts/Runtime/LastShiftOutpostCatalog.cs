using System.Collections.Generic;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선외 거점에 세울 수 있는 구조물 한 종류. <b><see cref="LastShiftModuleKind"/> 와 일부러
    /// 다른 형이다</b> — 저쪽은 값이 <c>MaintenanceCost</c>(정비 여력)이고 이쪽은
    /// <see cref="MaterialCost"/>(자재)다.
    ///
    /// <b>조항 <c>O-2</c> 를 형으로 만든 것이 요지다.</b> "여력은 배에 붙이는 것만, 자재는 고치고
    /// 채우고 거점을 키우는 것만 산다. 한 항목이 둘을 같이 요구하지 않는다" 를 지키는 가장 싼
    /// 방법은 <b>한 항목이 두 값을 들 수 없게 두는 것</b>이다. 카탈로그를 하나로 합치고 값 필드를
    /// 둘 두면, 언젠가 "여력 2 + 자재 3" 짜리 항목이 조용히 들어오고 그때 조항이 깨진 것을
    /// 알려 줄 자리가 없다(<c>docs/outboard-outpost-and-map-final-v1.md</c> §3.4 의 <c>AND</c> 금지).
    ///
    /// 기준 자세는 선체 카탈로그와 같다 — <b>계류면이 <c>MinX</c> 면 한가운데</b>. 커서가 그
    /// 자세에서 <c>90°</c> 4단으로 돌린다.
    /// </summary>
    public readonly struct LastShiftOutpostKind
    {
        public LastShiftOutpostKind(string name, float lengthX, float widthZ, int materialCost, float doorOffset = 0f)
        {
            Name = name;
            LengthX = lengthX;
            WidthZ = widthZ;
            MaterialCost = materialCost;
            DoorOffset = doorOffset;
        }

        /// <summary>화면에 뜨는 이름. 표에는 안 들어간다 — 선체 카탈로그와 같은 이유다.</summary>
        public string Name { get; }

        public float LengthX { get; }
        public float WidthZ { get; }

        /// <summary>
        /// 세우는 데 드는 <b>자재</b>(<see cref="LastShiftMaterials"/>). 여력을 인자로도 안 받는다.
        /// </summary>
        public int MaterialCost { get; }

        /// <summary>기준 자세에서 계류면이 <c>MinX</c> 면 어디에 오는가. <c>0</c> 이 면 한가운데다.</summary>
        public float DoorOffset { get; }

        /// <summary>
        /// 계류면 규약은 선체 문과 같은 자료형을 쓴다 — 붙임 검사
        /// (<see cref="LastShiftModuleAttachment"/>)와 자유면(<see cref="LastShiftFreeFaces"/>)이
        /// 그 형 위에 이미 서 있고, 거점은 <b>같은 코드를 쓰는 것</b>이 설계의 요지다(§4.4).
        /// </summary>
        public LastShiftModuleFootprint Footprint =>
            new(LengthX, WidthZ, LastShiftModuleFace.MinX, DoorOffset);
    }

    /// <summary>
    /// 거점 배치 탭의 목록. <b>지금 한 종뿐이고 그것이 확정 상태다</b> —
    /// <c>docs/tutorial-o3-free-placement-farming-deposit-v1.md</c> §2 의 <c>7</c>단계가
    /// "카탈로그 <c>1</c>종(계류 골조)" 을 화면 요건으로 적는다.
    ///
    /// <b>확장 넷은 일부러 안 넣었다.</b> <c>docs/outboard-outpost-and-map-final-v1.md</c> §4.3 의
    /// 인양 팔 · 자재 정리대 · 산소 충전대 · 잔해 예인기는 <b>효과와 값이 전부 <c>game-balance</c>
    /// 미결</b>이고, 여기에 임시 수치로 넣으면 그 수치가 곧 정본으로 읽힌다. 목록을 늘리는 것은
    /// 배열에 줄을 더하는 일이라 그 카드가 이 파일만 고치면 된다.
    ///
    /// <b>값이 <c>4</c> 인 것은 밸런스가 아니라 연출 장치다</b>(조항 <c>T-5</c>, 튜토리얼 §2-1).
    /// 잔해 한 필드가 <see cref="LastShiftSalvage.ChunksPerField"/> = <c>4</c> 덩이이므로 가격을
    /// 정확히 필드 전량과 같게 두면, 골조를 사는 순간 자재가 <c>4 → 0</c> 이 되고 그 화면에서
    /// 여력 잔액이 처음 뜬다 — 조항 <c>O-2</c>("자재가 <c>0</c> 인데 방은 지을 수 있다")를
    /// 설명 없이 한 번에 가르치는 자리다. 공짜로 주면 그 장면이 통째로 사라진다.
    /// </summary>
    public static class LastShiftOutpostCatalog
    {
        /// <summary>배치 격자. 선체와 같은 <c>1m</c> 다 — 두 탭이 같은 손동작이어야 한다(§5.1).</summary>
        public const float GridMeters = LastShiftModuleCatalog.GridMeters;

        /// <summary>`0` 계류 골조 — 거점의 뿌리. 자유면을 처음 만든다(§4.3 표 1행).</summary>
        public const int MooringFrame = 0;

        /// <summary>
        /// <b>발자국이 정사각이 아닌 것이 요건이다</b>(조항 <c>T-3</c>, 튜토리얼 §8 미결 <c>2</c>).
        /// 튜토리얼 <c>8</c>단계는 "처음엔 발자국이 안 맞아 확정이 안 먹고, 돌리면 초록" 으로
        /// 회전을 가르치는데, 정사각이면 <c>90°</c> 를 돌려도 발자국이 그대로라 <b>가르칠 실패가
        /// 아예 안 일어난다.</b>
        ///
        /// <c>4 × 2m</c> 인 것은 짧은 변이 문 구멍 폭(<c>LastShiftZoneDoor.OpeningWidth</c>)보다
        /// 넓어야 계류면이 성립하기 때문이고(<see cref="LastShiftModuleFootprint.DoorFits"/>),
        /// 긴 변이 잔해 한 면(<see cref="LastShiftOutpost.AnchorSpan"/>)과 같으면 회전이 두 자세
        /// 모두 붙어 버려 <c>T-3</c> 이 다시 안 선다.
        /// </summary>
        private static readonly LastShiftOutpostKind[] entries =
        {
            new("계류 골조", 4f, 2f, LastShiftSalvage.ChunksPerField)
        };

        public static IReadOnlyList<LastShiftOutpostKind> Entries => entries;

        public static int Count => entries.Length;

        public static LastShiftOutpostKind At(int index) => entries[index];

        /// <summary>목록을 순환으로 읽는다. 화면에서 다음/이전을 누르는 자리가 쓴다.</summary>
        public static int Wrap(int index)
        {
            var count = entries.Length;
            return ((index % count) + count) % count;
        }
    }
}
