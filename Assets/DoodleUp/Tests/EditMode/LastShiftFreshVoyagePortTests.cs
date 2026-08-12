using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 새 항해는 <b>정박한 채로</b> 시작한다 — 아직 출항 전이다.
    ///
    /// <b>이 파일이 막는 것은 검사 방식 자체의 구멍이다.</b> 기상 도입부를 여는 유일한 문이
    /// <c>LastShiftTutorial.ArriveAtPort</c> 이고 그것을 부르는 자리가
    /// <c>LastShiftVoyage.SettleSegment</c> 하나뿐이었는데, 새 항해는 <c>EnterSegment</c> 로
    /// 이미 출항한 상태가 되어 프롤로그가 안 열렸다. 그런데 지금까지의 온보딩 검사는 전부
    /// <c>BeginVoyage</c> 직후 <c>SettleSegment</c> 를 <b>손으로 강제</b>해서 그 간극을
    /// 뛰어넘었다 — 검사는 초록인데 게임에서는 아무 연출도 안 나오는 상태였다.
    ///
    /// 그래서 여기서는 <b>BeginVoyage 하나만 부르고</b> 아무것도 강제하지 않는다.
    /// </summary>
    public sealed class LastShiftFreshVoyagePortTests
    {
        [SetUp]
        public void SetUp()
        {
            LastShiftVoyage.Clear();
            LastShiftTutorial.Clear();
            LastShiftWakeSequence.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            LastShiftVoyage.Clear();
            LastShiftTutorial.Clear();
            LastShiftWakeSequence.Clear();
        }

        /// <summary>
        /// <b>PM 수용기준.</b> 새 항해를 열자마자 도입부가 돌고 있어야 한다 —
        /// <c>SettleSegment</c> 를 부르기 전에.
        /// </summary>
        [Test]
        public void AFreshVoyageOpensWithThePrologueAlreadyRunning()
        {
            LastShiftVoyage.BeginVoyage();

            Assert.That(LastShiftWakeSequence.IsRunning, Is.True,
                "BeginVoyage 직후인데 도입부가 안 돈다 — 새 항해가 이미 출항한 상태로 열렸다");
        }

        /// <summary>튜토리얼 1단계도 같이 열린다. 프롤로그만 뜨고 단계가 안 서면 반쪽이다.</summary>
        [Test]
        public void TheTutorialOpensAtTheSameMoment()
        {
            LastShiftVoyage.BeginVoyage();

            Assert.That(LastShiftTutorial.IsArmed, Is.True, "무장이 안 됐다");
            Assert.That(LastShiftTutorial.Step, Is.Not.EqualTo(LastShiftTutorialStep.None),
                "1단계가 안 열렸다");
            Assert.That(LastShiftTutorial.IsTutorialPort, Is.True,
                "튜토리얼 기항으로 안 잡힌다 — 잔해 총량이 인원 배수를 못 탄다");
        }

        /// <summary>
        /// 잔해도 같이 뜬다. 1단계가 "잔해를 본다" 라서, 볼 것이 없으면 그 단계를 못 넘는다.
        /// </summary>
        [Test]
        public void TheSalvageFieldIsThereToLookAt()
        {
            LastShiftVoyage.BeginVoyage();

            Assert.That(LastShiftSalvage.HasField, Is.True, "1단계가 볼 잔해가 없다");
            Assert.That(LastShiftSalvage.Remaining, Is.GreaterThan(0), "잔해가 비어 있다");
        }

        /// <summary>
        /// <b>여력 수입은 안 들어온다.</b> 기항 수입은 구간 하나를 날아낸 대가라, 아직 아무것도
        /// 안 난 항해에 주면 공짜 수입이 된다.
        /// </summary>
        [Test]
        public void NoMaintenanceIncomeArrivesBeforeAnythingIsFlown()
        {
            LastShiftVoyage.BeginVoyage();

            Assert.That(LastShiftMaintenance.Balance, Is.Zero,
                "날아낸 구간이 없는데 여력이 들어왔다");
        }

        /// <summary>
        /// 첫 구간을 실제로 끝내도 <b>두 번 안 열린다</b>. 열린 뒤에는 단계가 <c>None</c> 이
        /// 아니라서 문이 닫혀 있다.
        /// </summary>
        [Test]
        public void SettlingTheFirstSegmentDoesNotReopenThePrologue()
        {
            LastShiftVoyage.BeginVoyage();
            var stepAfterOpen = LastShiftTutorial.Step;
            var elapsedBefore = LastShiftWakeSequence.Elapsed;

            LastShiftWakeSequence.Tick(1f);
            Assume.That(LastShiftWakeSequence.Elapsed, Is.GreaterThan(elapsedBefore));
            var advanced = LastShiftWakeSequence.Elapsed;

            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);

            Assert.That(LastShiftWakeSequence.Elapsed, Is.EqualTo(advanced).Within(0.001f),
                "구간 판정이 도입부를 처음부터 다시 돌렸다");
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(stepAfterOpen),
                "튜토리얼 단계가 되감겼다");
        }

        /// <summary>
        /// 출항하면 닫힌다. <b>잠금이 구간까지 따라 나가면 안 된다</b> — 도입부는 정박 중에만
        /// 도는 연출이다.
        /// </summary>
        [Test]
        public void LeavingPortClosesWhatTheFreshVoyageOpened()
        {
            LastShiftVoyage.BeginVoyage();
            Assume.That(LastShiftWakeSequence.IsRunning, Is.True);

            LastShiftTutorial.LeavePort();

            Assert.That(LastShiftWakeSequence.IsRunning, Is.False, "출항했는데 도입부가 남아 있다");
        }
    }
}
