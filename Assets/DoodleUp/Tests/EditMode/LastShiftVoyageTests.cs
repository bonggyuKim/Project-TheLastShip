using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 구간 → 기항 자동 전이 — <c>docs/voyage-run-structure-v1.md</c> §3·§4.1·§5.
    ///
    /// <b>여기서 재는 것은 넷이다.</b>
    /// <list type="number">
    /// <item><b>구간 판정이 실제로 기항을 연다.</b> 이 경로가 없으면 정비 여력은 배치 화면을
    /// 처음 열 때 임시로 생기는 값이고, 그 상태에서는 이월도 견인도 플레이로 확인이 안 된다.</item>
    /// <item><b>§5 표의 세 갈래가 갈린다</b>: 성공은 기항, 표류·추력 부족은 견인(수입 <c>0</c>,
    /// 잔액 보존), 질식만 항해를 끝낸다.</item>
    /// <item><b>수입이 두 번 안 들어온다.</b> 같은 구간을 다시 판정하는 경로(디버그 재시작,
    /// 판정 재도착)가 하나라도 이중 지급이면 그건 무한 자원이다.</item>
    /// <item><b>이월이 항해를 안 넘는다</b>(§0-3 세이브 불필요). 새 항해는 여력 <c>0</c> 이다.</item>
    /// </list>
    ///
    /// 정적 상태를 만지므로 항해·원장을 앞뒤로 다 비운다.
    /// </summary>
    public sealed class LastShiftVoyageTests
    {
        [SetUp]
        public void ClearBefore() => LastShiftVoyage.Clear();

        [TearDown]
        public void ClearAfter() => LastShiftVoyage.Clear();

        // ── 전이 (§5) ───────────────────────────────────────────────────────

        /// <summary>
        /// <b>이 카드가 존재하는 이유다.</b> 구간 하나가 성공으로 끝나면 아무도 안 눌러도
        /// 기항이 열리고 래치 수만큼 여력이 들어온다.
        /// </summary>
        [Test]
        public void SettlingASegmentOpensThePortAndPaysTheLatches()
        {
            LastShiftVoyage.BeginVoyage();

            var transition = LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 3);

            Assert.That(transition, Is.EqualTo(LastShiftSegmentTransition.ToPort));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(1));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(3), "래치 3 = 여력 3 (조항 B-1 개정)");
            Assert.That(LastShiftMaintenance.IsAtPort, Is.True, "기항이 아니면 아무것도 못 산다");
        }

        /// <summary>
        /// 절충 생환도 기항으로 간다(§5 표). <b>포기하고 붙인 배가 정비 지점을 못 들르면</b>
        /// 포기가 곧 항해 종료라 §4.4 의 "포기 없이 클리어" 가 다시 최적해가 된다.
        /// </summary>
        [Test]
        public void CompromisedDockingStillGoesToPort()
        {
            LastShiftVoyage.BeginVoyage();

            Assert.That(LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessCompromised, 1),
                Is.EqualTo(LastShiftSegmentTransition.ToPort));
            // 래치 1 은 조항 B-1 개정에서 래치 0 과 같은 1 이다 — 최소 보장이 하한이지
            // 덧셈이 아니기 때문이다.
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(1));
        }

        /// <summary>
        /// 견인(§5). <b>기항에는 가지만 수입이 <c>0</c> 이고 모아 둔 잔액은 안 건드린다</b> —
        /// 여기서 잔액까지 날리면 아끼는 선택이 언제나 손해가 된다(조항 M-3).
        /// </summary>
        [TestCase(LastShiftVerdict.FailureAdrift)]
        [TestCase(LastShiftVerdict.FailureInsufficientThrust)]
        public void FailingToDockIsTowedToPortWithoutIncome(LastShiftVerdict verdict)
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 4);
            var saved = LastShiftMaintenance.Balance;
            LastShiftVoyage.Advance();

            var transition = LastShiftVoyage.SettleSegment(verdict, 4);

            Assert.That(transition, Is.EqualTo(LastShiftSegmentTransition.TowedToPort));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(2), "견인이어도 기항 회차는 올라간다");
            Assert.That(LastShiftMaintenance.LastPortIncome, Is.EqualTo(0));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(saved), "모아 둔 것은 안 날아간다");
        }

        /// <summary>
        /// 질식만 항해를 끝낸다(§5). 기항이 없으므로 회차도 안 오른다 — <b>배가 못 간 것과
        /// 사람이 죽은 것은 다르다</b>는 그 절의 주장이 여기서 값으로 갈린다.
        /// </summary>
        [Test]
        public void AsphyxiationEndsTheVoyageWithoutAPort()
        {
            LastShiftVoyage.BeginVoyage();

            var transition = LastShiftVoyage.SettleSegment(LastShiftVerdict.FailureAsphyxiation, 4);

            Assert.That(transition, Is.EqualTo(LastShiftSegmentTransition.VoyageLost));
            Assert.That(LastShiftVoyage.IsVoyageOver, Is.True);
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(0));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(0));
        }

        /// <summary>
        /// 종착 구간에는 기항이 없다(§4.1) — 거기서 래치는 정거장 모듈이 된다.
        /// 기항이 <c>2</c> 회인 것(§3.1)이 이 조건 하나로 성립한다.
        /// </summary>
        [Test]
        public void TheFinalSegmentHasNoPort()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 0);
            LastShiftVoyage.Advance();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 0);
            LastShiftVoyage.Advance();

            Assert.That(LastShiftVoyage.SegmentIndex, Is.EqualTo(3));
            var transition = LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 4);

            Assert.That(transition, Is.EqualTo(LastShiftSegmentTransition.VoyageComplete));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(2), "기항은 두 번뿐이다");
            Assert.That(LastShiftVoyage.IsVoyageOver, Is.True);
        }

        /// <summary>종착 구간에서 도킹을 놓치면 견인될 다음 구간이 없다 — 항해가 끝난다.</summary>
        [Test]
        public void FailingTheFinalSegmentEndsTheVoyage()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.EnterSegment(LastShiftVoyage.SegmentCount);

            Assert.That(LastShiftVoyage.SettleSegment(LastShiftVerdict.FailureAdrift, 2),
                Is.EqualTo(LastShiftSegmentTransition.VoyageLost));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(0));
        }

        // ── 이중 지급 ───────────────────────────────────────────────────────

        /// <summary>
        /// 같은 구간을 다시 판정해도 수입이 한 번뿐이다. <b>회차 조건 하나가 이걸 막는다</b> —
        /// 기항 회차는 구간 회차보다 앞설 수 없다.
        /// </summary>
        [Test]
        public void SettlingTheSameSegmentTwicePaysOnce()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);

            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);

            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(1));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(2));
        }

        /// <summary>디버그로 구간을 다시 밟아 판정해도 마찬가지다(§8-2 단일 구간 진입).</summary>
        [Test]
        public void ReplayingASegmentDoesNotPayAgain()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 4);
            var balance = LastShiftMaintenance.Balance;

            LastShiftVoyage.EnterSegment(1);
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 4);

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(balance));
        }

        /// <summary>판정 전에는 아무 일도 안 일어난다.</summary>
        [Test]
        public void PendingVerdictSettlesNothing()
        {
            LastShiftVoyage.BeginVoyage();

            Assert.That(LastShiftVoyage.SettleSegment(LastShiftVerdict.Pending, 4),
                Is.EqualTo(LastShiftSegmentTransition.Pending));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(0));
        }

        // ── 이월과 리셋 (§0-3 · §9-3) ───────────────────────────────────────

        /// <summary>
        /// <b>§9-3 이 플레이로 물어볼 그 장면이다.</b> 첫 기항에서 다 쓰고 둘째 기항을 견인으로
        /// 들어오면 여력이 <c>0</c> 이라 아무것도 못 산다 — 이월을 안 남긴 대가가 여기서 온다.
        /// </summary>
        [Test]
        public void SpendingEverythingLeavesTheTowedPortEmpty()
        {
            LastShiftVoyage.BeginVoyage();
            // 래치 2 — 조항 B-1 개정에서 개방(2)을 한 기항에 무는 최소 성적이다.
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
            Assert.That(LastShiftMaintenance.TrySpend(LastShiftMaintenanceItem.CompartmentUnlock), Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(0));

            LastShiftVoyage.Advance();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.FailureAdrift, 4);

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(0));
            Assert.That(LastShiftMaintenance.CanAfford(1), Is.False);
        }

        /// <summary>반대로 아낀 항해는 둘째 기항에서 이월분을 그대로 얹어 쓴다(조항 M-1).</summary>
        [Test]
        public void SavingCarriesIntoTheSecondPort()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
            LastShiftVoyage.Advance();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);

            Assert.That(LastShiftMaintenance.LastCarriedOver, Is.EqualTo(2));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(4));
        }

        /// <summary>
        /// 새 항해는 여력 <c>0</c> 이다(§0-3 · §6 사례 D "배가 완전히 새것이다").
        /// 항해가 끝난 뒤의 <see cref="LastShiftVoyage.Advance"/> 가 그 문이다.
        /// </summary>
        [Test]
        public void AdvancingPastAFinishedVoyageStartsAnEmptyOne()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 4);
            LastShiftVoyage.Advance();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.FailureAsphyxiation, 4);

            LastShiftVoyage.Advance();

            Assert.That(LastShiftVoyage.SegmentIndex, Is.EqualTo(1));
            Assert.That(LastShiftVoyage.IsVoyageOver, Is.False);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(0));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(0));
        }

        // ── 고정 순서 (§3.3) ────────────────────────────────────────────────

        /// <summary>구간 순서는 고정 <c>1 → 2 → 3</c> 이다. 섞으면 P1 재미 판정에 순서 운이 섞인다.</summary>
        [Test]
        public void SegmentsRunTheFixedPresetOrder()
        {
            Assert.That(LastShiftVoyage.PresetOf(1), Is.EqualTo(LastShiftPreset.HighHeatHighThrust));
            Assert.That(LastShiftVoyage.PresetOf(2), Is.EqualTo(LastShiftPreset.PowerOverloadLooseBattery));
            Assert.That(LastShiftVoyage.PresetOf(3), Is.EqualTo(LastShiftPreset.BadAttitudeHighOxygen));
            Assert.That(LastShiftVoyage.SegmentOf(LastShiftPreset.BadAttitudeHighOxygen), Is.EqualTo(3));
        }

        /// <summary>종착 구간의 "다음" 은 새 항해의 구간 <c>1</c> 이다 — 결과 화면이 적는 값이다.</summary>
        [Test]
        public void NextPresetWrapsToTheFirstSegmentAtTheEnd()
        {
            LastShiftVoyage.BeginVoyage();
            Assert.That(LastShiftVoyage.NextPreset, Is.EqualTo(LastShiftPreset.PowerOverloadLooseBattery));

            LastShiftVoyage.EnterSegment(LastShiftVoyage.SegmentCount);
            Assert.That(LastShiftVoyage.NextPreset, Is.EqualTo(LastShiftPreset.HighHeatHighThrust));
        }

        // ── 래치 판정 (modular-docking §2.1) ────────────────────────────────

        /// <summary>
        /// 래치는 <b>압력이 판정선 위이고 봉인되지 않은</b> 구역에 물린다. 봉인 조건이 빠지면
        /// 산소 계통을 포기해 밀폐한 구역이 진공인 채로 여력을 벌어 준다.
        /// </summary>
        [Test]
        public void LatchNeedsPressureAndAnUnsealedZone()
        {
            Assert.That(LastShiftVerdictResolver.IsLatched(LastShiftVerdictResolver.LatchPressure, false), Is.True);
            Assert.That(LastShiftVerdictResolver.IsLatched(LastShiftVerdictResolver.LatchPressure - 0.01f, false), Is.False);
            Assert.That(LastShiftVerdictResolver.IsLatched(1f, true), Is.False, "봉인된 구역은 압력과 무관하게 안 물린다");
        }

        /// <summary>
        /// 불변식 L3 — 래치는 도킹 성립 조건에 안 들어간다. 래치 <c>0</c> 개로 끝난 구간도
        /// 기항으로 가고 여력 <c>1</c> 을 받는다(§4.1-(나)).
        /// </summary>
        [Test]
        public void ZeroLatchesStillReachesThePortWithTheMinimum()
        {
            LastShiftVoyage.BeginVoyage();

            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 0);

            Assert.That(LastShiftMaintenance.IsAtPort, Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MinimumIncome));
        }
    }
}
