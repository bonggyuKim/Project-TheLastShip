using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 구간 하나가 끝났을 때 항해가 어디로 가는가. <b>새 판정 이름이 아니다</b> —
    /// <see cref="LastShiftVerdict"/> 는 그대로 두고(<c>voyage-run-structure-v1.md</c> §0-5),
    /// 이 열거는 그 판정을 §5 표의 세 갈래로 옮긴 것뿐이다.
    /// </summary>
    public enum LastShiftSegmentTransition
    {
        /// <summary>아직 구간 판정이 안 났다.</summary>
        Pending,

        /// <summary>기항으로 간다. 래치 수만큼 여력이 들어온다.</summary>
        ToPort,

        /// <summary>견인되어 기항으로 간다 — 수입은 <c>0</c> 이고 잔액은 안 건드린다(§5).</summary>
        TowedToPort,

        /// <summary>구간 <c>3</c> 종착 도킹. 기항이 없다 — 래치는 정거장 모듈이 된다(§4.1).</summary>
        VoyageComplete,

        /// <summary>항해가 여기서 끝났다. 질식(§5) 또는 종착 구간 실패.</summary>
        VoyageLost
    }

    /// <summary>
    /// 항해 진행 — <b>구간 회차와 그 구간이 끝났을 때의 전이</b>.
    /// <c>docs/voyage-run-structure-v1.md</c> §3(구간 <c>3</c> + 기항 <c>2</c>, 고정 순서)과
    /// §5(구간 실패의 세 갈래)를 코드로 옮긴 자리이고, <b>여력 원장으로 들어가는 유일한 문</b>이다
    /// — 게임 코드에서 <see cref="LastShiftMaintenance.ArriveAtPort"/> 를 부르는 곳은 여기 하나다.
    ///
    /// <b>왜 원장과 따로인가.</b> <see cref="LastShiftMaintenance"/> 는 "여력이 얼마인가" 만 알고
    /// "지금 몇 번째 구간인가" 는 모른다. 둘을 합치면 원장이 판정 열거를 알아야 하고, 그러면
    /// 기항 화면이 구간 판정 규칙을 끌고 들어오게 된다. <b>여기가 판정을 알고 원장이 값만 안다.</b>
    ///
    /// <b>이월 이중 지급을 회차로 막는다.</b> 기항 회차는 구간 회차보다 앞설 수 없다 — 구간
    /// <c>N</c> 을 끝내면 기항 <c>N</c> 이다. 그래서 같은 구간을 다시 판정해도(디버그 <c>R</c>,
    /// 서버 스냅샷 재도착) <see cref="LastShiftMaintenance.PortIndex"/> 가 이미 그 회차라 수입이
    /// 두 번 안 들어온다. 별도 플래그를 안 두는 이유다 — 플래그와 원장이 갈리면 그 순간
    /// 무한 자원이 된다.
    ///
    /// <see cref="LastShiftMaintenance"/> 와 같은 규약으로 정적이다 — 씬 없이 EditMode 에서
    /// 전부 재고, 도메인 리로드를 끈 에디터에서 지난 항해가 다음 판에 남지 않도록
    /// <see cref="ResetOnEnterPlayMode"/> 를 단다.
    /// </summary>
    public static class LastShiftVoyage
    {
        /// <summary>한 항해의 구간 수(§3.1). 자극이 <c>3</c> 종뿐이라 넷 이상이면 같은 사고가 두 번 온다(§3.2).</summary>
        public const int SegmentCount = 3;

        /// <summary>구간 회차는 <c>1</c> 부터 센다. 기항 회차(<see cref="LastShiftMaintenance.PortIndex"/>)와 같은 축이다.</summary>
        public const int FirstSegment = 1;

        /// <summary>
        /// 고정 순서 <c>프리셋1 → 2 → 3</c>(§3.3). enum 순서가 곧 구간 순서다 — 개수를
        /// 리터럴로 적으면 프리셋이 늘 때 조용히 빠진다.
        /// </summary>
        private static readonly LastShiftPreset[] SegmentPresets =
            (LastShiftPreset[])System.Enum.GetValues(typeof(LastShiftPreset));

        /// <summary>지금 몇 번째 구간인가(<c>1</c>~<see cref="SegmentCount"/>).</summary>
        public static int SegmentIndex { get; private set; } = FirstSegment;

        /// <summary>가장 최근 구간 판정이 항해를 어디로 보냈는가.</summary>
        public static LastShiftSegmentTransition LastTransition { get; private set; }

        /// <summary>그 판정 순간의 래치 수. 화면이 "래치 N/4 → 여력 N+1" 을 적는 자리다.</summary>
        public static int LastLatchCount { get; private set; }

        /// <summary>
        /// 항해 루프가 돌고 있는가. <b>배치 화면의 임시 다리가 읽는 값이다</b>
        /// (<see cref="LastShiftPlacementUi"/>) — 항해가 기항을 열고 있으면 그 다리는 아무 일도
        /// 안 해야 한다.
        /// </summary>
        public static bool IsRunning { get; private set; }

        /// <summary>종착 구간인가. 여기 판정에는 기항이 없다(§4.1).</summary>
        public static bool IsFinalSegment => SegmentIndex >= SegmentCount;

        /// <summary>구간 판정이 났는가.</summary>
        public static bool IsSegmentSettled => LastTransition != LastShiftSegmentTransition.Pending;

        /// <summary>항해가 끝났는가. 다음 입력은 새 항해다.</summary>
        public static bool IsVoyageOver =>
            LastTransition is LastShiftSegmentTransition.VoyageComplete
                or LastShiftSegmentTransition.VoyageLost;

        /// <summary>구간 회차 → 자극 프리셋. 범위 밖은 양 끝으로 붙인다.</summary>
        public static LastShiftPreset PresetOf(int segment) =>
            SegmentPresets[Mathf.Clamp(segment - FirstSegment, 0, SegmentPresets.Length - 1)];

        /// <summary>프리셋 → 구간 회차. 디버그 키가 단일 구간에 직접 들어갈 때 쓴다(§8-2).</summary>
        public static int SegmentOf(LastShiftPreset preset) =>
            Mathf.Clamp((int)preset + FirstSegment, FirstSegment, SegmentCount);

        /// <summary>이번 구간의 자극 프리셋.</summary>
        public static LastShiftPreset CurrentPreset => PresetOf(SegmentIndex);

        /// <summary>
        /// 다음에 들어갈 구간의 프리셋. 항해가 끝났거나 종착 구간이면 <b>새 항해의 구간 <c>1</c></b>
        /// 이다 — 결과 화면이 "다음" 으로 적는 값이고, 실제로 다음에 서는 판과 같아야 한다.
        /// </summary>
        public static LastShiftPreset NextPreset =>
            IsVoyageOver || IsFinalSegment ? PresetOf(FirstSegment) : PresetOf(SegmentIndex + 1);

        /// <summary>
        /// 새 항해를 시작한다 — 구간 <c>1</c>, 여력 <c>0</c>(조항 M-2).
        /// <b>배치 표는 안 건드린다</b> — <see cref="LastShiftMaintenance.BeginVoyage"/> 와 같은
        /// 이유이고, 씬에 선 모듈을 거두는 것은 부르는 쪽 몫이다.
        /// </summary>
        public static void BeginVoyage()
        {
            LastShiftMaintenance.BeginVoyage();
            // 자재도 항해 단위 자원이다(조항 O-1 · 이월은 M-1 과 같다). 여력과 나란히 두는
            // 이유는 둘이 서로 환산되지 않아도 <b>리셋 시점은 하나</b>여야 하기 때문이다 —
            // 갈라 두면 새 항해의 첫 기항에 지난 항해 자재가 남는다.
            LastShiftMaterials.BeginVoyage();
            // 거점도 항해 단위다(조항 O-6 — 계류 골조는 항해 시작 시 기본 지급). 자재와 같은
            // 줄에 두는 이유도 같다: 자재만 비우고 거점을 남기면 새 항해가 <b>공짜 골조</b>로
            // 시작하고, 첫 기항 튜토리얼이 살 것을 잃는다.
            LastShiftOutpost.BeginVoyage();
            LastLatchCount = 0;
            EnterSegment(FirstSegment);
            // 튜토리얼 무장은 <b>맨 끝</b>이어야 한다 — 바로 위 EnterSegment 가 출항으로 치고
            // LastShiftTutorial.LeavePort() 를 부르므로, 앞에 두면 그 자리에서 도로 꺼진다.
            // 이미 끝낸 세이브면 무장 자체가 안 선다(조항 T-6).
            LastShiftTutorial.BeginVoyage();
            OpenFirstPort();
        }

        /// <summary>
        /// 새 항해는 <b>정박한 채로</b> 시작한다 — 아직 출항 전이다.
        ///
        /// <b>이 줄이 없어서 프롤로그가 안 떴다.</b> 기상 도입부를 여는 유일한 문이
        /// <see cref="LastShiftTutorial.ArriveAtPort"/> 이고, 그것을 부르는 자리가
        /// <see cref="SettleSegment"/> 하나뿐이었다. 그런데 새 항해는 바로 위
        /// <see cref="EnterSegment"/> 로 <b>이미 출항한 상태</b>가 되므로, 실제로는 첫 구간을
        /// 몇 분간 날아 도킹에 성공해야만 프롤로그가 뜬다 — 방을 만들자마자 시작한 판에서
        /// 아무 연출도 안 나오던 것이 그 상태다. 지금까지의 온보딩 검사가 이 간극을 못 잡은
        /// 이유는 전부 <c>BeginVoyage</c> 직후 <c>SettleSegment</c> 를 손으로 강제했기 때문이다.
        ///
        /// <b>기항을 별도 상태로 새로 만들지 않았다.</b> 이 구조에서 기항은 이미 "구간 N 위에
        /// 얹힌 상태" 이고(<see cref="SettleSegment"/> 도 회차를 안 올리고 기항을 연다), 여기에
        /// 네 번째 상태를 더하면 곧 갈아치울 시스템(도킹 제거 마이그레이션)에 부채만 남는다.
        ///
        /// <b>여력 수입은 안 넣는다.</b> <see cref="LastShiftMaintenance.ArriveAtPort"/> 는 구간
        /// 하나를 날아낸 대가라, 아직 아무것도 안 난 항해에 주면 공짜 수입이 된다. 항해 시작의
        /// 여력은 <see cref="LastShiftMaintenance.BeginVoyage"/> 가 이미 <c>0</c> 으로 잡았다.
        ///
        /// 순서가 뒤집히면 안 된다 — 잔해 총량이 조항 <c>T-5</c> 의 인원 배수를 타므로
        /// (<see cref="LastShiftSalvage.FieldChunks"/>) 튜토리얼 1단계가 이미 열려 있어야
        /// 그 배수가 걸린다. <see cref="SettleSegment"/> 안의 순서와 같은 이유다.
        /// </summary>
        private static void OpenFirstPort()
        {
            LastShiftTutorial.ArriveAtPort();
            LastShiftSalvage.ArriveAtPort(CurrentPreset);
        }

        /// <summary>
        /// 구간 하나에 들어간다. 회차만 옮기고 <b>원장은 안 건드린다</b> — 여력이 들어오는
        /// 자리는 <see cref="SettleSegment"/> 하나뿐이다.
        /// </summary>
        public static void EnterSegment(int segment)
        {
            SegmentIndex = Mathf.Clamp(segment, FirstSegment, SegmentCount);
            LastTransition = LastShiftSegmentTransition.Pending;
            IsRunning = true;
            // 출항하면 선외가 닫힌다 — 조항 O-4(구간 중 에어록 봉인)와 O-5(잔해는 정박한
            // 동안만 뜬다). 게이트를 조회로만 두면 밖에 나간 채로 출항하는 상태가 남고,
            // 그 순간 RG-1 이탈 시간 계산의 종점이 배 밖이 된다.
            LastShiftAirlock.SealForSegment();
            LastShiftSalvage.LeavePort();
            // 튜토리얼은 첫 기항 하나다. 출항하면 조항 T-5·T-8 예외도 같이 닫힌다 —
            // 안 닫으면 둘째 기항 잔해가 계속 4 × 인원수 로 뜨고 O-7 대가가 영영 안 물린다.
            LastShiftTutorial.LeavePort();
        }

        /// <summary>
        /// 결과 화면에서 다음으로 넘어간다. <b>항해가 끝났으면 새 항해이고</b>(§6 사례 D
        /// "R 을 누르면 배가 완전히 새것이다"), 아니면 다음 구간이다.
        /// </summary>
        public static void Advance()
        {
            if (IsVoyageOver) BeginVoyage();
            else if (IsSegmentSettled) EnterSegment(SegmentIndex + 1);
            else EnterSegment(SegmentIndex);
        }

        /// <summary>
        /// 구간 판정을 항해 전이로 옮기고, 기항이면 원장에 수입을 넣는다(§5 표).
        ///
        /// <list type="bullet">
        /// <item><b>질식은 항해를 끝낸다.</b> 승무원이 죽은 것과 배가 못 간 것은 다르다.</item>
        /// <item><b>표류·추력 부족은 견인되어 기항으로 간다.</b> 수입만 <c>0</c> 이고
        /// 모아 둔 잔액은 그대로다(조항 M-3).</item>
        /// <item><b>종착 구간에는 기항이 없다.</b> 래치는 거기서 정거장 모듈이 된다(§4.1).</item>
        /// </list>
        /// </summary>
        /// <returns>이번 구간이 항해를 보낸 곳. 판정이 아직 안 났으면 <see cref="LastShiftSegmentTransition.Pending"/>.</returns>
        public static LastShiftSegmentTransition SettleSegment(LastShiftVerdict verdict, int latches)
        {
            if (!LastShiftVerdictResolver.IsResolved(verdict)) return LastShiftSegmentTransition.Pending;

            var towed = verdict is LastShiftVerdict.FailureAdrift
                or LastShiftVerdict.FailureInsufficientThrust;

            LastTransition = verdict == LastShiftVerdict.FailureAsphyxiation
                ? LastShiftSegmentTransition.VoyageLost
                : IsFinalSegment
                    ? towed ? LastShiftSegmentTransition.VoyageLost : LastShiftSegmentTransition.VoyageComplete
                    : towed ? LastShiftSegmentTransition.TowedToPort : LastShiftSegmentTransition.ToPort;

            LastLatchCount = Mathf.Clamp(latches, 0, LastShiftMaintenance.MaxLatches);
            IsRunning = true;

            var entersPort = LastTransition is LastShiftSegmentTransition.ToPort
                or LastShiftSegmentTransition.TowedToPort;
            // 기항 회차가 이미 이 구간 몫을 받았으면 다시 안 넣는다 — 같은 구간 재판정에서
            // 수입이 두 번 들어오는 것을 막는 유일한 조건이다.
            if (entersPort && LastShiftMaintenance.PortIndex < SegmentIndex)
            {
                LastShiftMaintenance.ArriveAtPort(LastLatchCount, towed);
                // 잔해는 직전 구간의 자극이 남긴 것이다(§4.2) — 여기서는 구간 회차가 아직
                // 안 올라갔으므로 CurrentPreset 이 그대로 방금 끝난 구간이다. 견인이어도
                // 잔해는 뜬다: 견인의 대가는 여력 수입 0 이고(조항 M-3), 자재는 여력이
                // 아니라서(조항 O-1) 그 대가에 안 걸린다 — 걸면 최악 항해의 회복 경로가
                // 통째로 사라져 RG-3 의 항해판 위반이 된다.
                // 튜토리얼이 먼저다 — 잔해 총량이 조항 T-5 의 인원 배수를 타므로
                // (LastShiftSalvage.FieldChunks) 1단계가 이미 열려 있어야 배수가 걸린다.
                LastShiftTutorial.ArriveAtPort();
                LastShiftSalvage.ArriveAtPort(CurrentPreset);
            }

            return LastTransition;
        }

        /// <summary>
        /// 서버가 정한 항해 진행을 그대로 앉힌다. <b>클라이언트 전용이고 원장을 안 건드린다</b> —
        /// 여기서 <see cref="SettleSegment"/> 를 다시 돌리면 <see cref="LastShiftMaintenance.ArriveAtPort"/>
        /// 가 클라이언트에서 한 번 더 돌아 잔액이 서버보다 커진다. 원장은 원장대로
        /// <see cref="LastShiftMaintenance.ApplyNetworkLedger"/> 가 받는다.
        ///
        /// 시뮬레이션이 서버에만 도는 것과 같은 이유다 — 클라이언트의
        /// <see cref="LastShiftSandboxController"/> 는 <c>enabled = IsServer</c> 로 꺼져 있어서
        /// 구간 판정 자체를 안 내고, 그래서 이 값들은 받아 적는 것 말고 나올 데가 없다.
        /// </summary>
        public static void ApplyNetworkState(
            int segment, LastShiftSegmentTransition transition, int latches, bool running)
        {
            SegmentIndex = Mathf.Clamp(segment, FirstSegment, SegmentCount);
            LastTransition = transition;
            LastLatchCount = Mathf.Clamp(latches, 0, LastShiftMaintenance.MaxLatches);
            IsRunning = running;
        }

        /// <summary>전부 되돌린다. 원장까지 비운다 — 테스트와 플레이 진입이 부른다.</summary>
        public static void Clear()
        {
            LastShiftMaintenance.Clear();
            LastShiftMaterials.Clear();
            LastShiftOutpost.ClearPieces();
            LastShiftSalvage.Clear();
            LastShiftAirlock.Clear();
            LastShiftTutorial.Clear();
            SegmentIndex = FirstSegment;
            LastTransition = LastShiftSegmentTransition.Pending;
            LastLatchCount = 0;
            IsRunning = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
