using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 튜토리얼 <c>1</c>~<c>6</c>단계 — 파밍·적재.
    /// 정본은 <c>docs/tutorial-o3-free-placement-farming-deposit-v1.md</c> 다.
    ///
    /// <b>여기서 재는 것은 넷이다.</b>
    /// <list type="number">
    /// <item><b>왕복이 두 번이다</b>(조항 <c>T-1</c>). 한 번 다녀와서는 필드가 안 비고
    /// 단계가 <c>6</c>에 머문다 — 초안 §5.2 가 한 번만 시켰던 그 오학습이 여기서 막힌다.</item>
    /// <item><b>인원 배수 하나로 <c>4</c>인이 덮인다</b>(조항 <c>T-5</c>). 총량이 인원에
    /// 비례하고, 그래서 한 사람당 왕복 수가 인원과 무관하게 <c>2</c>다.</item>
    /// <item><b>튜토리얼 기항에서만 <c>O-7</c> 이 잔해로 되돌린다</b>(조항 <c>T-8</c>).
    /// 총량이 필요량과 같은 필드라 안 되돌리면 판이 진행 불능이 된다. <b>출항하면 예외도
    /// 끝난다</b> — 이게 안 닫히면 조문이 항해 전체로 새어 나간다.</item>
    /// <item><b>완료 플래그가 파일에 실린다</b>(조항 <c>T-6</c>). 껐다 켜도 다시 안 뜬다.</item>
    /// </list>
    ///
    /// 정적 상태를 만지므로 항해를 앞뒤로 다 비운다(<see cref="LastShiftVoyage.Clear"/> 가
    /// 튜토리얼까지 비운다).
    /// </summary>
    public sealed class LastShiftTutorialTests
    {
        [SetUp]
        public void ClearBefore() => LastShiftVoyage.Clear();

        [TearDown]
        public void ClearAfter() => LastShiftVoyage.Clear();

        // ── 도구 ────────────────────────────────────────────────────────────

        /// <summary>구간 하나를 성공으로 끝내 기항을 연다. 잔해가 여기서 뜬다.</summary>
        private static void ArriveAtFirstPort(int crew = 1)
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftTutorial.SetCrewCount(crew);
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 1);
        }

        /// <summary>지금 잔해·원장 상태를 그대로 담은 관측. 좌표 신호 셋만 인자로 받는다.</summary>
        private static LastShiftTutorialObservation Now(
            bool leftCockpit = false, bool inHall = false, bool outside = false) =>
            new(leftCockpit, inHall, outside,
                LastShiftSalvage.Carried, LastShiftSalvage.CarryCapacity,
                LastShiftSalvage.Remaining, LastShiftMaterials.Balance);

        /// <summary>손이 찰 때까지 뜯는다. 쿨다운을 돌려 주므로 실제 동사 경로 그대로다.</summary>
        private static void FillHands()
        {
            while (LastShiftSalvage.Carried < LastShiftSalvage.CarryCapacity &&
                   LastShiftSalvage.Remaining > 0)
            {
                LastShiftSalvage.Tick(LastShiftSalvage.HarvestSeconds);
                Assert.That(LastShiftSalvage.TryHarvest(LastShiftSalvage.FieldCenter), Is.True);
            }
        }

        /// <summary>선외로 나가 손을 채우고 돌아와 반입한다 — 왕복 한 번.</summary>
        private static void RunOneTrip()
        {
            LastShiftTutorial.Observe(Now(true, true, true), 1f);
            FillHands();
            LastShiftTutorial.Observe(Now(true, true, true), 1f);
            LastShiftSalvage.Deposit();
            LastShiftTutorial.Observe(Now(true, true, false), 1f);
        }

        // ── 진입 (§2 표 1) ──────────────────────────────────────────────────

        /// <summary>첫 기항이 열리면 <c>1</c>단계가 같이 열린다. 아무도 안 눌러도 그렇다.</summary>
        [Test]
        public void TheFirstPortOpensStepOne()
        {
            ArriveAtFirstPort();

            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.SightSalvage));
            Assert.That(LastShiftTutorial.IsTutorialPort, Is.True);
        }

        /// <summary>
        /// 단계가 <c>1</c>→<c>6</c> 으로 순서대로 간다. <b>신호가 전부 기존 상태 조회다</b> —
        /// 좌표 셋과 수 셋뿐이고 새 신호원이 없다(기획 §5).
        /// </summary>
        [Test]
        public void StepsAdvanceInOrderThroughFarmingAndDeposit()
        {
            ArriveAtFirstPort();

            LastShiftTutorial.Observe(Now(leftCockpit: true), 1f);
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.CrossPlaza),
                "조종석을 벗어나면 2단계다");

            LastShiftTutorial.Observe(Now(true, inHall: true), 1f);
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.CentralLift));

            LastShiftTutorial.Observe(Now(true, true, outside: true), 1f);
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.Harvest),
                "우물을 넘으면 4단계 — 산소 게이지가 뜨는 판정과 같은 신호다");

            FillHands();
            LastShiftTutorial.Observe(Now(true, true, true), 1f);
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.Deposit),
                "손이 차는 것이 5단계 진입이다 (§1-1)");

            LastShiftSalvage.Deposit();
            LastShiftTutorial.Observe(Now(true, true, false), 1f);
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.SecondTrip),
                "잔액이 실제로 오른 것만 적재의 증거다");
        }

        /// <summary>
        /// 조항 <c>T-1</c> — <b>왕복 한 번으로는 안 끝난다.</b> <c>ChunksPerField 4</c> 에
        /// <c>BaseCarryCapacity 2</c> 이므로 필드를 비우는 데 반드시 두 번이다.
        /// </summary>
        [Test]
        public void OneRoundTripIsNotEnoughToLeaveStepSix()
        {
            ArriveAtFirstPort();

            RunOneTrip();
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.SecondTrip));
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(2), "왕복 한 번은 절반이다");

            RunOneTrip();
            Assert.That(LastShiftSalvage.Remaining, Is.Zero);
            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(LastShiftSalvage.ChunksPerField));
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.Schematic),
                "필드를 비우면 도면이 열린다 — 7단계 전이는 후속 카드가 받는다");
        }

        /// <summary>
        /// 화면이 "이번 기항 반입" 을 왕복 두 번치로 적는다. 덮어쓰면 두 번 다녀온 뒤에도
        /// <c>2</c> 로 남아 조항 <c>T-1</c> 이 가르치려는 것과 화면이 어긋난다.
        /// </summary>
        [Test]
        public void PortSalvageAccumulatesAcrossBothTrips()
        {
            ArriveAtFirstPort();

            RunOneTrip();
            RunOneTrip();

            Assert.That(LastShiftMaterials.LastPortSalvaged, Is.EqualTo(4));
            Assert.That(LastShiftMaterials.DepositRevision, Is.EqualTo(2), "배지가 두 번 튄다 (조항 T-2)");
            Assert.That(LastShiftMaterials.LastDeposited, Is.EqualTo(2));
        }

        // ── 조항 T-5 ────────────────────────────────────────────────────────

        /// <summary>
        /// <b>인원이 늘어도 한 사람당 왕복은 두 번이다.</b> 총량이 <c>4 × 인원수</c> 이므로
        /// <c>4</c>인이면 <c>16</c>덩이이고, 넷이 각자 두 번 다녀오면 정확히 빈다.
        /// </summary>
        [TestCase(1, 4)]
        [TestCase(2, 8)]
        [TestCase(4, 16)]
        public void TutorialFieldScalesWithCrew(int crew, int chunks)
        {
            ArriveAtFirstPort(crew);

            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(chunks));
            Assert.That(chunks / (crew * LastShiftSalvage.BaseCarryCapacity), Is.EqualTo(2),
                "한 사람당 왕복 두 번이 인원과 무관하게 유지된다");
        }

        /// <summary>둘째 기항은 평시다 — 배수가 항해 전체로 새어 나가면 안 된다.</summary>
        [Test]
        public void TheSecondPortIsBackToTheNormalField()
        {
            ArriveAtFirstPort(4);
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(16));

            LastShiftVoyage.EnterSegment(2);
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 1);

            Assert.That(LastShiftTutorial.IsTutorialPort, Is.False, "출항이 튜토리얼을 닫는다");
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(LastShiftSalvage.ChunksPerField));
        }

        // ── 조항 T-8 ────────────────────────────────────────────────────────

        /// <summary>
        /// 튜토리얼 기항에서 산소가 마르면 <b>들고 있던 몫이 잔해로 돌아간다.</b> 총량이
        /// 골조 가격과 정확히 같아서, 안 돌려주면 그 판은 골조를 영영 못 산다.
        /// </summary>
        [Test]
        public void TutorialPortReturnsAbandonedChunksToTheField()
        {
            ArriveAtFirstPort();
            FillHands();
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(2));

            var lost = LastShiftSalvage.AbandonCarried();

            Assert.That(lost, Is.EqualTo(2));
            Assert.That(LastShiftSalvage.Carried, Is.Zero, "들고 있던 것은 그대로 잃는다");
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(LastShiftSalvage.ChunksPerField),
                "필드 총량이 회복되어 골조를 살 길이 남는다");
        }

        /// <summary>
        /// 평시에는 안 돌아간다 — 원래 조문(<c>O-7</c>)이다. 돌려주면 "산소가 허락하는 만큼
        /// 최대한 뜯고 오기" 가 다시 최적이 된다.
        /// </summary>
        [Test]
        public void NormalPortStillLosesTheAbandonedChunks()
        {
            ArriveAtFirstPort();
            LastShiftVoyage.EnterSegment(2);
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 1);
            FillHands();

            LastShiftSalvage.AbandonCarried();

            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(2), "잃은 몫은 잔해로 안 돌아온다");
        }

        /// <summary>
        /// 자동 복귀가 나도 <b>단계는 되감기지 않는다</b>(조항 <c>T-8</c> 마지막 줄).
        /// <c>6</c>단계에서 마르면 <c>6</c>단계에서 다시 나간다.
        /// </summary>
        [Test]
        public void AutoReturnDoesNotRewindTheStep()
        {
            ArriveAtFirstPort();
            RunOneTrip();
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.SecondTrip));

            FillHands();
            LastShiftSalvage.AbandonCarried();
            LastShiftTutorial.Observe(Now(true, true, false), 1f);

            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.SecondTrip));
        }

        // ── 조항 T-6 ────────────────────────────────────────────────────────

        /// <summary>완료 플래그가 파일을 건넌다. 세션 메모리 결정은 폐기됐다.</summary>
        [Test]
        public void CompletionSurvivesTheSaveFile()
        {
            ArriveAtFirstPort();
            LastShiftTutorial.AdvanceTo(LastShiftTutorialStep.HandsOff);
            LastShiftTutorial.MarkCompleted();

            var json = LastShiftSaveFormat.Write(LastShiftSaveCapture.Capture(null, false));
            LastShiftVoyage.Clear();
            Assert.That(LastShiftTutorial.HasCompleted, Is.False, "비웠으니 꺼져 있어야 한다");

            var load = LastShiftSaveFormat.Read(json);
            LastShiftSaveCapture.Restore(load, null);

            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.Loaded));
            Assert.That(LastShiftTutorial.HasCompleted, Is.True);
        }

        /// <summary>끝낸 세이브의 새 항해는 튜토리얼이 안 열리고 잔해도 평시 총량이다.</summary>
        [Test]
        public void AFinishedSaveNeverArmsTheTutorialAgain()
        {
            LastShiftTutorial.RestoreCompleted(true);

            ArriveAtFirstPort(4);

            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.None));
            Assert.That(LastShiftTutorial.IsTutorialPort, Is.False);
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(LastShiftSalvage.ChunksPerField),
                "인원 배수도 같이 꺼진다");
        }

        /// <summary>옛 파일에는 키가 없다 — <c>false</c> 로 읽히고 그것이 "아직 안 했다" 다.</summary>
        [Test]
        public void ASaveWithoutTheKeyReadsAsNotCompleted()
        {
            var load = LastShiftSaveFormat.Read("{\"SchemaA\":1,\"SchemaB\":1,\"Campaign\":{}}");
            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.Loaded));
            Assert.That(load.File.Campaign.TutorialCompleted, Is.False);
        }
    }
}
