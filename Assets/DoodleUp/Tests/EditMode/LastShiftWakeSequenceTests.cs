using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 기상 도입부(정본 §4-1). 여기서 지키는 것은 셋이다 — <b>안 돌 때 조작이 안 잠기는가</b>,
    /// <b>해금이 두 단으로 오는가</b>, <b>행동이 미는 두 줄이 제 순서에만 걸리는가</b>.
    /// </summary>
    public sealed class LastShiftWakeSequenceTests
    {
        [TearDown]
        public void TearDown() => LastShiftWakeSequence.Clear();

        /// <summary>
        /// <b>가장 중요한 검사다.</b> 이 상태기가 안 도는 동안 게이트가 잠기면, 튜토리얼을
        /// 이미 끝낸 판이나 이 코드를 안 부르는 씬에서 <b>아무도 움직일 수 없다.</b>
        /// 잠금이 아니라 해제가 기본값이어야 하는 이유가 그것이다.
        /// </summary>
        [Test]
        public void NothingIsLockedWhileTheSequenceIsAsleep()
        {
            Assert.That(LastShiftWakeSequence.IsRunning, Is.False);
            Assert.That(LastShiftWakeSequence.Gate, Is.EqualTo(LastShiftWakeGate.Free));
            Assert.That(LastShiftWakeSequence.CanLook, Is.True);
            Assert.That(LastShiftWakeSequence.CanMove, Is.True);
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(0f));
            Assert.That(LastShiftWakeSequence.HasLine, Is.False);
        }

        /// <summary><see cref="LastShiftWakeSequence.Clear"/> 뒤에도 같아야 한다 — 출항 경로다.</summary>
        [Test]
        public void ClearingReleasesTheGate()
        {
            LastShiftWakeSequence.Begin();
            Assume.That(LastShiftWakeSequence.CanMove, Is.False);

            LastShiftWakeSequence.Clear();

            Assert.That(LastShiftWakeSequence.Gate, Is.EqualTo(LastShiftWakeGate.Free));
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(0f));
        }

        /// <summary>
        /// <c>AI_W_01</c> 은 "씬 로드 직후" 다. 첫 Tick 을 기다리면 아무 말도 없는 검은 화면이
        /// 한 프레임 지나간다.
        /// </summary>
        [Test]
        public void TheFirstLineIsUpBeforeAnyTick()
        {
            LastShiftWakeSequence.Begin();

            Assert.That(LastShiftWakeSequence.HasLine, Is.True);
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_01"));
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(1f));
            Assert.That(LastShiftWakeSequence.Gate, Is.EqualTo(LastShiftWakeGate.Locked));
        }

        /// <summary>암전은 유지 구간 동안 <c>1</c> 이고, 페이드 구간에서만 내려간다.</summary>
        [Test]
        public void TheBlackoutHoldsThenLifts()
        {
            LastShiftWakeSequence.Begin();

            TickUntil(LastShiftWakeSequence.BlackoutSeconds);
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(1f).Within(0.001f),
                "유지 구간이 끝나기 전에 이미 걷히고 있다");

            TickUntil(LastShiftWakeSequence.BlackoutSeconds + LastShiftWakeSequence.FadeSeconds * 0.5f);
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(0.5f).Within(0.01f));

            TickUntil(LastShiftWakeSequence.LookSeconds);
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(0f));
        }

        /// <summary>
        /// 해금이 <b>두 단</b>이다 — 시점이 먼저 풀리고 이동이 나중이다. 한 번에 풀면
        /// "누운 채로 깼다" 는 그림이 안 나온다.
        /// </summary>
        [Test]
        public void LookUnlocksBeforeMove()
        {
            LastShiftWakeSequence.Begin();
            Assert.That(LastShiftWakeSequence.CanLook, Is.False);

            TickUntil(LastShiftWakeSequence.LookSeconds);
            Assert.That(LastShiftWakeSequence.Gate, Is.EqualTo(LastShiftWakeGate.LookOnly));
            Assert.That(LastShiftWakeSequence.CanLook, Is.True);
            Assert.That(LastShiftWakeSequence.CanMove, Is.False, "시점과 이동이 같이 풀렸다");

            TickUntil(LastShiftWakeSequence.StandSeconds);
            Assert.That(LastShiftWakeSequence.Gate, Is.EqualTo(LastShiftWakeGate.Free));
        }

        /// <summary>시간이 미는 다섯 줄이 정본 순서대로 온다.</summary>
        [Test]
        public void TheTimedLinesArriveInOrder()
        {
            LastShiftWakeSequence.Begin();
            var expected = new[] { "AI_W_01", "AI_W_02", "AI_W_03", "AI_W_04", "AI_W_05" };

            for (var i = 0; i < expected.Length; i++)
            {
                TickUntil(LastShiftWakeSequence.ScheduledAt(i));
                Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo(expected[i]),
                    $"{LastShiftWakeSequence.ScheduledAt(i)}초에 뜬 줄이 다르다");
            }
        }

        /// <summary>
        /// 한 프레임이 길어도(로딩 직후 첫 프레임이 그렇다) <b>마지막 시간 줄에서 멈춘다</b> —
        /// 행동이 미는 둘까지 시간으로 넘어가면 안 걷고도 안내가 끝난다.
        /// </summary>
        [Test]
        public void OneHugeFrameStopsAtTheLastTimedLine()
        {
            LastShiftWakeSequence.Begin();

            LastShiftWakeSequence.Tick(60f);

            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_05"));
            Assert.That(LastShiftWakeSequence.IsComplete, Is.False);
            Assert.That(LastShiftWakeSequence.Gate, Is.EqualTo(LastShiftWakeGate.Free));
        }

        /// <summary>
        /// <c>AI_W_06</c> 은 이동이 풀린 뒤에만 받는다. 잠긴 동안 들어온 신호를 받으면
        /// 도입부가 한 줄 건너뛴 채로 흐른다.
        /// </summary>
        [Test]
        public void TheFirstMoveOnlyCountsAfterMoveUnlocks()
        {
            LastShiftWakeSequence.Begin();

            LastShiftWakeSequence.NotifyFirstMove();
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_01"),
                "이동이 잠긴 동안 들어온 신호가 줄을 밀었다");

            TickUntil(LastShiftWakeSequence.StandSeconds);
            LastShiftWakeSequence.NotifyFirstMove();
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_06"));

            // 두 번째 이동은 "첫" 이동이 아니다.
            LastShiftWakeSequence.NotifyFirstMove();
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_06"));
        }

        /// <summary>문 사거리는 걷기 시작한 뒤에만 센다. 그 전에는 조회조차 안 돈다.</summary>
        [Test]
        public void TheDoorLineWaitsForTheMoveLine()
        {
            LastShiftWakeSequence.Begin();
            TickUntil(LastShiftWakeSequence.StandSeconds);

            Assert.That(LastShiftWakeSequence.IsAwaitingQuartersDoor, Is.False);
            LastShiftWakeSequence.NotifyQuartersDoorInRange();
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_05"));

            LastShiftWakeSequence.NotifyFirstMove();
            Assert.That(LastShiftWakeSequence.IsAwaitingQuartersDoor, Is.True);

            LastShiftWakeSequence.NotifyQuartersDoorInRange();
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_07"));
            Assert.That(LastShiftWakeSequence.IsComplete, Is.True);
            Assert.That(LastShiftWakeSequence.IsAwaitingQuartersDoor, Is.False);
        }

        /// <summary>재촉 시각을 재는 시계가 <b>줄마다</b> 다시 선다.</summary>
        [Test]
        public void TheNudgeClockRestartsOnEveryLine()
        {
            LastShiftWakeSequence.Begin();
            TickUntil(LastShiftWakeSequence.BlackoutSeconds * 0.5f);
            Assert.That(LastShiftWakeSequence.LineElapsedSeconds,
                Is.EqualTo(LastShiftWakeSequence.BlackoutSeconds * 0.5f).Within(0.001f));

            TickUntil(LastShiftWakeSequence.BlackoutSeconds);
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_02"));
            Assert.That(LastShiftWakeSequence.LineElapsedSeconds, Is.EqualTo(0f).Within(0.001f),
                "줄이 바뀌었는데 앞줄의 시계가 그대로 이어졌다");
        }

        /// <summary>
        /// 상태기가 <see cref="LastShiftNarrationScript.Wake"/> 를 그대로 읽으므로, 그 배열의
        /// 순서가 곧 도입부의 순서다. 문안이 재정렬되면 여기서 먼저 걸린다.
        /// </summary>
        [Test]
        public void TheSequenceReadsTheScriptBlockInOrder()
        {
            var wake = LastShiftNarrationScript.Wake;
            Assert.That(wake.Length, Is.EqualTo(LastShiftWakeSequence.TimedLineCount + 2),
                "시간이 미는 다섯 + 행동이 미는 둘이 아니다");
            for (var i = 0; i < wake.Length; i++)
                Assert.That(wake[i].Id, Is.EqualTo($"AI_W_{i + 1:00}"));
        }

        /// <summary>
        /// <b>절대 시각으로 몬다.</b> <c>1/60</c> 을 더해 나가면 <c>2</c>초가 <c>1.9999993</c> 이
        /// 되어 예정 시각을 안 넘는다 — 처음에 상대 시간으로 짰다가 세 검사가 그 오차로
        /// 걸렸다. 실제 프레임도 경계에 정확히 안 떨어지므로, 재는 쪽이 절대값을 보는 것이 맞다.
        /// 고정 프레임으로 나눠 돌리는 이유는 한 번에 몰아 주면 while 루프를 안 밟기 때문이다.
        /// </summary>
        private static void TickUntil(float targetElapsed)
        {
            const float step = 1f / 60f;
            var guard = 0;
            while (LastShiftWakeSequence.Elapsed < targetElapsed && guard++ < 100000)
                LastShiftWakeSequence.Tick(
                    Mathf.Min(step, targetElapsed - LastShiftWakeSequence.Elapsed));
        }
    }
}
