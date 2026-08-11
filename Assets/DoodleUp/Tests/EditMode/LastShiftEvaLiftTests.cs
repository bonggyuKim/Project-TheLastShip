using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 승강과 감압이 <b>실제로 겹쳐 도는가</b>.
    ///
    /// game-balance 최종 검증: 순차면 첫 EVA 왕복이 <c>39.70/40</c>초로 여유가 <c>0.3</c>초뿐이고,
    /// 겹치면 <c>28.70</c>초로 <c>11.30</c>초가 남는다. 즉 겹침은 최적화가 아니라 통과 조건이라
    /// <b>코드를 읽어서 "겹치는 것 같다" 로는 안 되고 재서 증명해야 한다</b>.
    /// </summary>
    public sealed class LastShiftEvaLiftTests
    {
        private const float Step = 1f / 60f;

        [SetUp]
        public void Reset()
        {
            LastShiftAirlock.Clear();
            LastShiftEvaLift.Clear();
            LastShiftVoyage.Clear();
        }

        [TearDown]
        public void Cleanup() => Reset();

        /// <summary>기항에 들여보낸다 — 하단 게이트가 기항 게이트를 보므로 필요하다.</summary>
        private static void EnterPort()
        {
            LastShiftVoyage.EnterSegment(LastShiftVoyage.SegmentOf(LastShiftPreset.HighHeatHighThrust));
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
        }

        /// <summary>올라가는 데 걸린 시간을 잰다. 두 시계를 같은 프레임에 돌린다.</summary>
        private static float RunAscent(float limitSeconds = 30f)
        {
            var elapsed = 0f;
            while (elapsed < limitSeconds)
            {
                LastShiftEvaLift.Tick(Step);
                LastShiftAirlock.Tick(Step);
                elapsed += Step;
                if (LastShiftEvaLift.IsAtHullTop && LastShiftAirlock.IsOuterHatchOpen) return elapsed;
            }
            return float.PositiveInfinity;
        }

        [Test]
        public void AscentAndDepressurizationRunAtTheSameTime()
        {
            EnterPort();
            Assert.That(LastShiftAirlock.TryOpenInner(liftAwayFromDeck: false), Is.True, "하단 게이트가 안 열린다");
            Assert.That(LastShiftEvaLift.TryAscend(), Is.True, "리프트가 안 출발한다");

            // 출발 시점에 이미 사이클이 돌고 있어야 한다. 도착해서 걸면 그 순간 순차가 된다.
            Assert.That(LastShiftAirlock.IsCycling, Is.True,
                "출발했는데 사이클이 안 돈다 — 겹침이 아니라 순차다. balance 기준 미달이다.");
            Assert.That(LastShiftEvaLift.IsMoving, Is.True);

            var seconds = RunAscent();
            var sequential = LastShiftEvaShaft.LiftSeconds + LastShiftAirlock.CycleSeconds;
            var overlapped = Mathf.Max(LastShiftEvaShaft.LiftSeconds, LastShiftAirlock.CycleSeconds);

            Assert.That(seconds, Is.LessThan(sequential - 0.5f),
                $"상승에 {seconds:F2}초 걸렸다 — 순차({sequential:F2}초)와 구분이 안 된다.");
            Assert.That(seconds, Is.EqualTo(overlapped).Within(0.2f),
                $"겹쳤다면 느린 쪽({overlapped:F2}초)에 묶여야 한다. 실측 {seconds:F2}초.");
        }

        /// <summary>
        /// 승강 시간과 사이클 시간이 같게 묶여 있는가. <see cref="LastShiftEvaShaft.LiftSpeed"/> 를
        /// 그렇게 잡았으므로, 한쪽만 바뀌면 리프트가 기다리거나 사이클이 기다린다.
        /// </summary>
        [Test]
        public void TheLiftArrivesExactlyWhenTheCycleEnds()
        {
            Assert.That(LastShiftEvaShaft.LiftSeconds,
                Is.EqualTo(LastShiftAirlock.CycleSeconds).Within(0.01f),
                "승강 시간과 사이클 시간이 갈렸다 — 겹쳐도 한쪽이 놀고 있다.");
        }

        /// <summary>
        /// 인터록 (b) — 리프트가 갑판에 없으면 하단 게이트를 못 연다(PM 확정 2026-08-11).
        /// 빈 샤프트로 떨어지는 것을 막는다.
        /// </summary>
        [Test]
        public void TheDeckGateStaysShutWhileTheLiftIsAway()
        {
            EnterPort();
            Assert.That(LastShiftAirlock.CanOpenInner(liftAwayFromDeck: true), Is.False,
                "리프트가 위에 있는데 하단 게이트가 열린다 — 빈 샤프트로 떨어진다.");
            Assert.That(LastShiftAirlock.TryOpenInner(liftAwayFromDeck: true), Is.False);
            Assert.That(LastShiftAirlock.CanOpenInner(liftAwayFromDeck: false), Is.True);
        }

        [Test]
        public void TheLiftOnlyDepartsFromAStandingStop()
        {
            EnterPort();
            Assert.That(LastShiftEvaLift.TryDescend(), Is.False, "갑판에 있는데 내려간다");
            Assert.That(LastShiftAirlock.TryOpenInner(liftAwayFromDeck: false), Is.True);
            Assert.That(LastShiftEvaLift.TryAscend(), Is.True);
            Assert.That(LastShiftEvaLift.TryAscend(), Is.False, "이미 움직이는데 또 출발한다");
        }
    }
}
