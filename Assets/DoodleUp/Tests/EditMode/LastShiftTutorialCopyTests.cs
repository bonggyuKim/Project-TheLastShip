using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 단계 안내 문안 — 미결 §8-<c>5</c> 의 문안 몫. 정본은
    /// <c>docs/tutorial-o3-free-placement-farming-deposit-v1.md</c> §2 표다.
    ///
    /// <b>문안은 옳고 그름을 기계가 못 잰다.</b> 그래서 재는 것은 <b>표에 구멍이 없는지</b>와
    /// <b>부르는 자리가 문안을 하나만 보는지</b> 둘이다 — 단계를 하나 더 넣고 문안을 안 쓴 채
    /// 지나가는 것이 이 표에서 가장 나기 쉬운 사고이고, 그때 화면에 빈 줄이 뜬다.
    /// </summary>
    public sealed class LastShiftTutorialCopyTests
    {
        [SetUp]
        public void ClearBefore() => LastShiftVoyage.Clear();

        [TearDown]
        public void ClearAfter() => LastShiftVoyage.Clear();

        private static IEnumerable<LastShiftTutorialStep> AllSteps =>
            System.Enum.GetValues(typeof(LastShiftTutorialStep))
                .Cast<LastShiftTutorialStep>()
                .Where(step => step != LastShiftTutorialStep.None);

        /// <summary>
        /// <b>표에 구멍이 없다.</b> 단계 열 개가 모두 제목·안내·재촉을 갖고, 행 수가 단계 수와 같다.
        /// </summary>
        [Test]
        public void EveryStepHasCopy()
        {
            Assert.That(LastShiftTutorialCopy.LineCount, Is.EqualTo((int)LastShiftTutorialStep.HandsOff),
                "표의 행 수가 단계 수와 다르다 — 단계를 넣고 문안을 안 적었다");

            foreach (var step in AllSteps)
            {
                var line = LastShiftTutorialCopy.Of(step);
                Assert.That(line.Step, Is.EqualTo(step), $"{step} 행이 제 번호 자리에 없다");
                Assert.That(line.Title, Is.Not.Empty, $"{step} 제목이 비었다");
                Assert.That(line.Guide, Is.Not.Empty, $"{step} 안내가 비었다");
                Assert.That(line.Nudge, Is.Not.Empty, $"{step} 재촉이 비었다");
                Assert.That(line.NudgeAfterSeconds, Is.GreaterThan(0f), $"{step} 재촉 시점이 0 이면 안내를 못 읽는다");
            }
        }

        /// <summary>
        /// <b>안내와 재촉이 다른 말이다.</b> 같으면 시간이 흘러도 화면이 안 바뀌어, 헤매는
        /// 사람에게 방금 읽은 줄을 다시 읽히는 것이 된다.
        /// </summary>
        [Test]
        public void NudgeDiffersFromGuide()
        {
            foreach (var step in AllSteps)
            {
                var line = LastShiftTutorialCopy.Of(step);
                Assert.That(line.Nudge, Is.Not.EqualTo(line.Guide), $"{step} 재촉이 안내와 같다");
            }
        }

        /// <summary><b>단계마다 다른 제목이다.</b> 같은 제목이 둘이면 진행이 멈춘 것으로 읽힌다.</summary>
        [Test]
        public void TitlesAreDistinct()
        {
            var titles = AllSteps.Select(step => LastShiftTutorialCopy.Of(step).Title).ToArray();
            Assert.That(titles.Distinct().Count(), Is.EqualTo(titles.Length), "제목이 겹치는 단계가 있다");
        }

        /// <summary>
        /// <b>머문 시간이 안내를 갈아 끼운다.</b> 들어온 직후에는 안내, 재촉 시점을 넘기면 재촉이다.
        /// </summary>
        [Test]
        public void GuideSwapsToNudgeAfterThreshold()
        {
            var line = LastShiftTutorialCopy.Of(LastShiftTutorialStep.Harvest);

            Assert.That(LastShiftTutorialCopy.Guide(LastShiftTutorialStep.Harvest, 0f), Is.EqualTo(line.Guide));
            Assert.That(LastShiftTutorialCopy.Guide(LastShiftTutorialStep.Harvest, line.NudgeAfterSeconds - 0.1f),
                Is.EqualTo(line.Guide));
            Assert.That(LastShiftTutorialCopy.Guide(LastShiftTutorialStep.Harvest, line.NudgeAfterSeconds),
                Is.EqualTo(line.Nudge));
        }

        /// <summary>
        /// <b>머리줄에 단계 번호가 붙는다.</b> 그 번호는 조항 <c>T-9</c> 로그의 번호와 같은
        /// 번호라, QA 가 로그와 화면을 같은 눈금으로 맞춘다.
        /// </summary>
        [Test]
        public void HeadingCarriesStepNumber()
        {
            var heading = LastShiftTutorialCopy.Heading(LastShiftTutorialStep.Deposit);

            Assert.That(heading, Does.Contain($"{(int)LastShiftTutorialStep.Deposit}/{(int)LastShiftTutorialStep.HandsOff}"));
            Assert.That(heading, Does.Contain(LastShiftTutorialCopy.Of(LastShiftTutorialStep.Deposit).Title));
        }

        /// <summary>
        /// <b>튜토리얼이 안 돌 때는 한 글자도 없다.</b> 띠는 <c>IsRunning</c> 으로 막지만,
        /// 표가 빈 행을 안 주면 그 막이 뚫린 자리에서 <c>None</c> 이 문안을 얻는다.
        /// </summary>
        [Test]
        public void NoneStepHasNoCopy()
        {
            Assert.That(LastShiftTutorialCopy.Heading(LastShiftTutorialStep.None), Is.Empty);
            Assert.That(LastShiftTutorialCopy.Guide(LastShiftTutorialStep.None, 999f), Is.Empty);
            Assert.That(LastShiftTutorialCopy.Of(LastShiftTutorialStep.None).HasPrompt, Is.False);
        }

        /// <summary>
        /// <b>조작 프롬프트는 <c>8</c>단계 하나다</b> — 조항 <c>T-3</c>. 다른 단계가 프롬프트를
        /// 갖는 순간 <c>LastShiftPlacementUi</c> 의 빨간 사유가 그 단계에서도 통째로 가려진다.
        /// </summary>
        [Test]
        public void OnlyRotateStepCarriesPrompt()
        {
            foreach (var step in AllSteps)
                Assert.That(LastShiftTutorialCopy.Of(step).HasPrompt,
                    Is.EqualTo(step == LastShiftTutorialStep.RotateFrame), $"{step} 의 프롬프트 유무가 T-3 과 다르다");
        }

        /// <summary>
        /// <b>띠 한 줄에 들어가는 길이다.</b> 안내줄 폭이 <c>660</c>px · 글자 <c>14</c>px 라
        /// 한글 <c>45</c>자쯤에서 잘린다 — 여유를 두고 <c>40</c>자로 잡는다. 넘길 문장이 생기면
        /// 줄을 늘리기 전에 문장을 줄인다(계기줄이 아래에 붙어 있다).
        /// </summary>
        [Test]
        public void LinesFitTheBanner()
        {
            foreach (var step in AllSteps)
            {
                var line = LastShiftTutorialCopy.Of(step);
                Assert.That(line.Title.Length, Is.LessThanOrEqualTo(12), $"{step} 제목이 머리줄보다 길다");
                Assert.That(line.Guide.Length, Is.LessThanOrEqualTo(40), $"{step} 안내가 띠에서 잘린다");
                Assert.That(line.Nudge.Length, Is.LessThanOrEqualTo(40), $"{step} 재촉이 띠에서 잘린다");
            }
        }

        /// <summary>
        /// <b>단계가 오르면 문안도 오른다.</b> 상태기를 실제로 굴려, 화면이 읽는 경로
        /// (<c>Step</c> → 표)가 단계마다 다른 줄을 내는지 확인한다.
        /// </summary>
        [Test]
        public void RunningTutorialYieldsDistinctLinesPerStep()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 1);

            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.SightSalvage));
            var first = LastShiftTutorialCopy.Guide(LastShiftTutorial.Step, LastShiftTutorial.StepElapsedSeconds);

            LastShiftTutorial.AdvanceTo(LastShiftTutorialStep.CrossPlaza);
            var second = LastShiftTutorialCopy.Guide(LastShiftTutorial.Step, LastShiftTutorial.StepElapsedSeconds);

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(LastShiftTutorialCopy.Heading(LastShiftTutorial.Step), Does.StartWith("튜토리얼 2/10"));
        }
    }
}
