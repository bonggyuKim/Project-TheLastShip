using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 튜토리얼 단계 — <c>docs/tutorial-o3-free-placement-farming-deposit-v1.md</c> §2 의 열 줄
    /// 그대로다. <b>번호가 문서의 번호와 같아야 한다</b>: 조항 <c>T-9</c> 의 로그가
    /// <c>step=&lt;n&gt;</c> 로 나가고 §6 의 판정 셋이 전부 그 번호로 구간을 자른다 —
    /// 여기서 한 칸이라도 어긋나면 판정이 다른 구간을 재게 된다.
    /// </summary>
    public enum LastShiftTutorialStep
    {
        /// <summary>튜토리얼이 안 돌고 있다. 파일에 안 실리는 유일한 값이다.</summary>
        None = 0,

        /// <summary><c>1</c>. 조종석 전면 스크린 밖에 잔해가 떠 있다.</summary>
        SightSalvage = 1,

        /// <summary><c>2</c>. 개구부 너머 광장. 왼쪽 앞 문이 에어록 홀이다.</summary>
        CrossPlaza = 2,

        /// <summary><c>3</c>. 홀 안. 바닥 우물에 발을 딛으면 <c>SuitOxygen</c> 이 처음 뜬다.</summary>
        AirlockHall = 3,

        /// <summary><c>4</c>. 자재를 뜯는다. 두 덩이째에서 손이 찬다.</summary>
        Harvest = 4,

        /// <summary><c>5</c>. 복귀. 홀 바닥에 서는 순간 잔액이 튄다 — <b>적재의 경계</b>다.</summary>
        Deposit = 5,

        /// <summary><c>6</c>. 두 번째 왕복. 필드가 소진된다 — <b>왕복이 루프의 단위다</b>.</summary>
        SecondTrip = 6,

        /// <summary><c>7</c>. 도면이 자동으로 열린다. 거점 탭·카탈로그 <c>1</c>종.</summary>
        Schematic = 7,

        /// <summary><c>8</c>. 골조를 한 번 돌려 세운다. 자재 <c>4 → 0</c>.</summary>
        RotateFrame = 8,

        /// <summary><c>9</c>. 선체 탭 해제. 여력 잔액이 처음 뜬다(조항 <c>O-2</c>).</summary>
        HullUnlocked = 9,

        /// <summary><c>10</c>. 손을 뗀다. 여기서부터 규칙은 플레이어가 만난다.</summary>
        HandsOff = 10
    }

    /// <summary>
    /// 한 프레임의 관측 한 벌. <b>상태기가 씬을 안 본다</b> — 단계 전이 판정이 전부 값이라
    /// EditMode 에서 씬 없이 재고, 판정에 쓰는 신호가 무엇인지가 형으로 남는다.
    ///
    /// 담긴 것이 전부 <b>이미 있는 상태</b>인 것이 요점이다(기획 §5 "새 신호원이 없다").
    /// 좌표 판정 셋은 <see cref="LastShiftPlazaLayout.TryResolveSpace"/> 와
    /// <see cref="LastShiftAirlock.IsOutside"/> 가 이미 내리고 있고, 수 셋은 잔해·원장이 든다.
    /// </summary>
    public readonly struct LastShiftTutorialObservation
    {
        public LastShiftTutorialObservation(
            bool crewLeftCockpit, bool crewInAirlockHall, bool crewOutside,
            int carried, int carryCapacity, int remaining, int balance)
        {
            CrewLeftCockpit = crewLeftCockpit;
            CrewInAirlockHall = crewInAirlockHall;
            CrewOutside = crewOutside;
            Carried = carried;
            CarryCapacity = Mathf.Max(1, carryCapacity);
            Remaining = remaining;
            Balance = balance;
        }

        /// <summary>승무원 하나라도 조종석 공간 밖에 있는가. <c>1 → 2</c> 신호다.</summary>
        public bool CrewLeftCockpit { get; }

        /// <summary>승무원 하나라도 에어록 홀 안에 있는가. <c>2 → 3</c> 신호다.</summary>
        public bool CrewInAirlockHall { get; }

        /// <summary>승무원 하나라도 선외에 있는가. <c>3 → 4</c> 신호이자 "우물을 넘었다" 다.</summary>
        public bool CrewOutside { get; }

        public int Carried { get; }
        public int CarryCapacity { get; }
        public int Remaining { get; }

        /// <summary><see cref="LastShiftMaterials.Balance"/>. 적재가 실제로 일어났는지의 유일한 증거다.</summary>
        public int Balance { get; }
    }

    /// <summary>
    /// 튜토리얼 상태기 — 단계 <c>O-3</c>. 정본은
    /// <c>docs/tutorial-o3-free-placement-farming-deposit-v1.md</c> 이고, 기획 §5 가 "진짜 새
    /// 코드는 상태기 하나다" 로 지목한 그 자리다.
    ///
    /// <b>이 카드가 세우는 것은 <c>1</c>~<c>6</c>단계(파밍·적재)다.</b> <c>7</c>~<c>10</c>은
    /// 도면·거점 쪽이라 <see cref="LastShiftTutorialStep.Schematic"/> 진입까지만 여기서 내고,
    /// 그 뒤 전이는 후속 카드가 <see cref="AdvanceTo"/> 로 붙인다 — 단계 값을 미리 다 세워 두는
    /// 이유는 조항 <c>T-9</c> 의 로그 번호와 §6 판정 구간이 열 단계를 전제하기 때문이다.
    ///
    /// <b>상태기가 아무것도 강제하지 않는다.</b> 잠금(조항 <c>T-4</c>)도 도면 자동 개방도
    /// 여기서 하지 않고, 각 자리가 <see cref="Step"/> 을 읽어 스스로 판단한다 — 상태기가 UI 를
    /// 부르기 시작하면 튜토리얼을 끄는 것이 "상태기를 안 돌린다" 가 아니라 "상태기가 부르는
    /// 곳마다 분기" 가 된다.
    ///
    /// <see cref="LastShiftSalvage"/>·<see cref="LastShiftMaterials"/> 와 같은 규약으로 정적이다.
    /// </summary>
    public static class LastShiftTutorial
    {
        /// <summary>이 카드가 전이를 내는 마지막 단계. 여기 진입까지가 파밍·적재 구간이다.</summary>
        public const LastShiftTutorialStep FarmingLastStep = LastShiftTutorialStep.Schematic;

        private static bool armed;
        private static float elapsed;
        private static float stepElapsed;

        /// <summary>지금 단계. <see cref="LastShiftTutorialStep.None"/> 이면 안 돌고 있다.</summary>
        public static LastShiftTutorialStep Step { get; private set; }

        /// <summary>
        /// 이 세이브에서 튜토리얼을 이미 끝냈는가 — 조항 <c>T-6</c>. 세션 메모리가 아니라
        /// 파일에 실린다(<see cref="LastShiftCampaignSave.TutorialCompleted"/>).
        /// </summary>
        public static bool HasCompleted { get; private set; }

        /// <summary>
        /// 튜토리얼 기항의 인원수 — 조항 <c>T-5</c> 의 배수 하나다. 최소 <c>1</c> 이다.
        /// <b>산 인원을 센다</b>: 죽은 승무원 몫까지 잔해를 늘리면 남은 사람이 왕복을 네 번
        /// 돌아야 하고, 그러면 <c>T-5</c> 가 지키려던 "한 사람당 왕복 두 번" 이 깨진다.
        /// </summary>
        public static int CrewCount { get; private set; } = 1;

        /// <summary><c>1</c>단계 진입 이후 누적 시간. §6 판정 <c>1</c>(전체 <c>120</c>초)이 읽는 값이다.</summary>
        public static float ElapsedSeconds => elapsed;

        /// <summary>지금 단계에 들어온 뒤 누적 시간. §6 판정 <c>2</c>·<c>3</c> 이 구간을 자르는 값이다.</summary>
        public static float StepElapsedSeconds => stepElapsed;

        /// <summary>이번 항해가 튜토리얼 항해인가. 아직 안 끝냈고 항해가 무장돼 있다.</summary>
        public static bool IsArmed => armed && !HasCompleted;

        /// <summary>
        /// 지금 기항이 튜토리얼 기항인가 — 조항 <c>T-5</c>·<c>T-8</c> 의 예외가 걸리는 조건이다.
        /// <b>출항하면 꺼진다</b>(<see cref="LeavePort"/>): 예외가 둘째 기항까지 따라가면
        /// 잔해가 계속 <c>4 × 인원수</c> 로 뜨고 산소가 마른 대가도 영영 안 물린다.
        /// </summary>
        public static bool IsTutorialPort => IsArmed && Step != LastShiftTutorialStep.None;

        /// <summary>지금 단계 전이를 내고 있는가.</summary>
        public static bool IsRunning => IsTutorialPort;

        // ── 바깥에서 들어오는 사실 ──────────────────────────────────────────

        /// <summary>
        /// 승무원 수를 갱신한다. 부르는 자리는 구간 판정 직전이다
        /// (<c>LastShiftSandboxController.SettleVerdict</c>) — 잔해가 그 뒤에 뜨므로
        /// <see cref="LastShiftSalvage.FieldChunks"/> 가 읽을 때는 이미 최신이다.
        /// </summary>
        public static void SetCrewCount(int count) => CrewCount = Mathf.Max(1, count);

        /// <summary>
        /// 새 항해가 시작한다. <b>이미 끝냈으면 무장하지 않는다</b>(조항 <c>T-6</c>) — 그래서
        /// 두 번째 판은 잔해가 평시 <c>4</c>덩이로 뜨고 <c>O-7</c> 도 원래대로 문다.
        ///
        /// 부르는 자리가 <see cref="LastShiftVoyage.BeginVoyage"/> 의 <b>맨 끝</b>인 것이
        /// 조건이다: 그 안의 <c>EnterSegment</c> 가 <see cref="LeavePort"/> 를 부르므로
        /// 앞에서 무장하면 그 자리에서 도로 꺼진다.
        /// </summary>
        public static void BeginVoyage()
        {
            Step = LastShiftTutorialStep.None;
            elapsed = 0f;
            stepElapsed = 0f;
            armed = !HasCompleted;
        }

        /// <summary>
        /// 기항에 들어왔다 — <c>1</c>단계가 여기서 열린다. 부르는 자리는
        /// <see cref="LastShiftVoyage.SettleSegment"/> 의 잔해 생성 <b>직후</b>다.
        ///
        /// <b>둘째 기항 이후에는 안 연다.</b> 튜토리얼은 첫 기항 하나이고, 출항이
        /// <see cref="LeavePort"/> 로 무장을 이미 껐다.
        /// </summary>
        public static void ArriveAtPort()
        {
            if (!IsArmed || Step != LastShiftTutorialStep.None) return;
            Enter(LastShiftTutorialStep.SightSalvage);
        }

        /// <summary>
        /// 출항한다. 튜토리얼은 첫 기항 하나이므로 <b>끝났든 아니든 여기서 닫힌다</b>.
        /// 중간에 출항한 판은 완료로 안 적는다 — 조항 <c>T-6</c> 의 플래그는 <c>10</c>단계에
        /// 도달한 판만 받는다.
        /// </summary>
        public static void LeavePort()
        {
            if (Step != LastShiftTutorialStep.None)
                Debug.Log($"[LAST_SHIFT_TUTORIAL] step={(int)Step} action=LEAVE elapsed={elapsed:F1}");
            Step = LastShiftTutorialStep.None;
            armed = false;
        }

        // ── 전이 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 한 프레임을 돌린다. <b>단계는 되감기지 않는다</b> — 조항 <c>T-8</c> 이 자동 복귀
        /// (<c>O-7</c>)에서 명시적으로 요구한 것이고, 되감으면 잃은 몫이 잔해로 돌아간 순간
        /// <c>6</c>단계가 <c>4</c>단계로 내려가 로그 구간이 겹친다.
        ///
        /// 조건이 여러 개 한꺼번에 참이면 <b>여러 단계를 한 번에 통과시킨다</b>. 손이 찬 채로
        /// 홀에 서 있는 상태(복원·재접속)에서 상태기만 <c>1</c>단계에 남는 것을 막는다.
        /// </summary>
        public static void Observe(in LastShiftTutorialObservation observation, float deltaTime)
        {
            if (!IsRunning) return;

            if (deltaTime > 0f)
            {
                elapsed += deltaTime;
                stepElapsed += deltaTime;
            }

            while (Step < FarmingLastStep && Satisfies(Step, observation))
                Enter(Step + 1);
        }

        /// <summary>
        /// 이 단계를 떠날 조건이 찼는가. <b>전부 기존 상태 조회다</b>(기획 §5).
        /// </summary>
        private static bool Satisfies(LastShiftTutorialStep step, in LastShiftTutorialObservation o) => step switch
        {
            // 조종석을 벗어난다. 광장은 조종석과 같은 구역이지만(개구부라 문이 없다) 공간이
            // 달라서 발자국 표가 둘을 가른다.
            LastShiftTutorialStep.SightSalvage => o.CrewLeftCockpit,

            LastShiftTutorialStep.CrossPlaza => o.CrewInAirlockHall,

            // 바닥 우물을 넘는 것이 곧 선외다. 산소 게이지가 뜨는 조건과 같은 판정을 쓴다 —
            // 따로 재면 화면에 게이지가 뜬 단계와 상태기가 센 단계가 갈린다.
            LastShiftTutorialStep.AirlockHall => o.CrewOutside,

            // 손이 찬다. 세 번째가 안 뜯기는 것이 §1-1 이 짚은 오학습 지점이고, 그 순간이
            // 곧 <c>5</c>단계 진입이다.
            LastShiftTutorialStep.Harvest => o.Carried >= o.CarryCapacity,

            // 적재의 경계를 넘었다. 잔액이 실제로 오른 것만 신호로 쓴다 — 위치로 재면
            // 반입 조건이 두 벌이 된다(<see cref="LastShiftSalvage.Deposit"/> 주석).
            LastShiftTutorialStep.Deposit => o.Balance > 0 && o.Carried <= 0,

            // 필드를 비웠고 들고 있는 것도 없다. 왕복 두 번이 여기서 닫힌다(조항 T-1).
            LastShiftTutorialStep.SecondTrip => o.Remaining <= 0 && o.Carried <= 0,

            _ => false
        };

        /// <summary>
        /// 단계를 밖에서 밀어 올린다 — <c>7</c>~<c>10</c> 은 도면·거점 쪽 신호라 그 카드가
        /// 자기 자리에서 부른다. <b>뒤로는 안 간다.</b>
        /// </summary>
        public static void AdvanceTo(LastShiftTutorialStep step)
        {
            if (!IsRunning || step <= Step || step > LastShiftTutorialStep.HandsOff) return;
            Enter(step);
        }

        /// <summary>
        /// 튜토리얼이 끝났다 — 조항 <c>T-6</c> 의 플래그가 여기서 선다. <c>10</c>단계에 도달한
        /// 판만 부른다. 이후 <see cref="IsTutorialPort"/> 가 거짓이 되어 <c>T-5</c>·<c>T-8</c>
        /// 예외도 같이 끝난다.
        /// </summary>
        public static void MarkCompleted()
        {
            if (HasCompleted) return;
            Debug.Log($"[LAST_SHIFT_TUTORIAL] step={(int)Step} action=COMPLETE elapsed={elapsed:F1}");
            HasCompleted = true;
            armed = false;
            Step = LastShiftTutorialStep.None;
        }

        private static void Enter(LastShiftTutorialStep step)
        {
            Step = step;
            stepElapsed = 0f;
            // 조항 T-9 — 형식은 logging-guide.md 규약 그대로다. 단계당 한 줄이라 열 줄이
            // 전부이고, §6 의 판정 셋이 이 줄들만으로 재진다.
            Debug.Log($"[LAST_SHIFT_TUTORIAL] step={(int)step} action=ENTER elapsed={elapsed:F1}");
        }

        // ── 세이브 ──────────────────────────────────────────────────────────

        /// <summary>파일에서 완료 플래그를 되세운다(조항 <c>T-6</c>).</summary>
        public static void RestoreCompleted(bool completed)
        {
            HasCompleted = completed;
            if (!completed) return;
            armed = false;
            Step = LastShiftTutorialStep.None;
        }

        public static void Clear()
        {
            armed = false;
            elapsed = 0f;
            stepElapsed = 0f;
            Step = LastShiftTutorialStep.None;
            HasCompleted = false;
            CrewCount = 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
