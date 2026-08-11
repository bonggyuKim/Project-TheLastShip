using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 상시 라인 넷. 지키는 것은 셋이다 — <b>판당 한 번인가</b>, <b>진행과 무관하게 뜨는가</b>,
    /// <b>회수 두 줄이 순서대로 오는가</b>.
    /// </summary>
    public sealed class LastShiftStandingNarrationTests
    {
        [SetUp]
        public void SetUp() => LastShiftStandingNarration.Clear();

        [TearDown]
        public void TearDown() => LastShiftStandingNarration.Clear();

        [Test]
        public void NothingShowsWhileTheSuitIsFull()
        {
            LastShiftStandingNarration.Observe(false, false, 0.016f);

            Assert.That(LastShiftStandingNarration.HasLine, Is.False);
        }

        [Test]
        public void CrossingTheWarningLineRaisesTheWarningLine()
        {
            LastShiftStandingNarration.Observe(true, false, 0.016f);

            Assert.That(LastShiftStandingNarration.HasLine, Is.True);
            Assert.That(LastShiftStandingNarration.Current.Id, Is.EqualTo("AI_F_W1"));
            Assert.That(LastShiftStandingNarration.Current.Sfx,
                Is.EqualTo(LastShiftNarrationSfx.ChimeAlert), "경고에 경보음이 안 붙었다");
        }

        /// <summary>
        /// <b>판당 한 번이다.</b> 산소는 한 상태에서 단조로워 경계를 되넘지 않으므로, 다시
        /// 뜬다면 그건 같은 말을 반복하는 것이다.
        /// </summary>
        [Test]
        public void EachLineSpeaksOnlyOncePerRun()
        {
            LastShiftStandingNarration.Observe(true, false, 0.016f);
            Assume.That(LastShiftStandingNarration.Current.Id, Is.EqualTo("AI_F_W1"));

            // 머무는 시간이 지나면 내려간다.
            LastShiftStandingNarration.Observe(true, false, LastShiftStandingNarration.DwellSeconds);
            Assert.That(LastShiftStandingNarration.HasLine, Is.False);

            // 경고가 계속 참이어도 다시 안 뜬다.
            LastShiftStandingNarration.Observe(true, false, 1f);
            Assert.That(LastShiftStandingNarration.HasLine, Is.False, "경고가 두 번 떴다");
            Assert.That(LastShiftStandingNarration.HasSpent("AI_F_W1"), Is.True);
        }

        /// <summary>임계는 경고와 <b>별개로</b> 한 번 더 뜬다 — 더 좁은 선이다.</summary>
        [Test]
        public void TheCriticalLineStillComesAfterTheWarningOne()
        {
            LastShiftStandingNarration.Observe(true, false, 0.016f);
            LastShiftStandingNarration.Observe(true, false, LastShiftStandingNarration.DwellSeconds);

            LastShiftStandingNarration.Observe(true, true, 0.016f);

            Assert.That(LastShiftStandingNarration.Current.Id, Is.EqualTo("AI_F_W2"));
        }

        /// <summary>
        /// 같은 프레임에 둘 다 넘어가면(프레임이 길 때) <b>임계가 남는다</b> — 화면에 최신
        /// 상태가 떠야 한다.
        /// </summary>
        [Test]
        public void WhenBothCrossAtOnceTheCriticalOneWins()
        {
            LastShiftStandingNarration.Observe(true, true, 0.016f);

            Assert.That(LastShiftStandingNarration.Current.Id, Is.EqualTo("AI_F_W2"));
            Assert.That(LastShiftStandingNarration.HasSpent("AI_F_W1"), Is.False,
                "경고를 안 띄우고 소모 처리했다");
        }

        /// <summary>
        /// 회수 두 줄. <c>W4</c> 는 <c>W3</c> 뒤 한 박자에 따라온다(정본) — 그 간격이
        /// 머무는 시간과 같은 값이라 <c>W3</c> 이 내려가는 자리에서 바로 이어진다.
        /// </summary>
        [Test]
        public void TheRescuePairArrivesInOrder()
        {
            LastShiftStandingNarration.NotifyAutoReturn();
            Assert.That(LastShiftStandingNarration.Current.Id, Is.EqualTo("AI_F_W3"));
            Assert.That(LastShiftStandingNarration.Current.Sfx,
                Is.EqualTo(LastShiftNarrationSfx.ChimeAlert));

            LastShiftStandingNarration.Observe(false, false, LastShiftStandingNarration.DwellSeconds);

            Assert.That(LastShiftStandingNarration.Current.Id, Is.EqualTo("AI_F_W4"));
            Assert.That(LastShiftStandingNarration.Current.Sfx,
                Is.EqualTo(LastShiftNarrationSfx.None), "이어지는 줄에 소리가 또 붙었다");
        }

        /// <summary>회수도 판당 한 번이다.</summary>
        [Test]
        public void TheRescuePairAlsoSpeaksOnlyOnce()
        {
            LastShiftStandingNarration.NotifyAutoReturn();
            LastShiftStandingNarration.Observe(false, false, LastShiftStandingNarration.DwellSeconds);
            LastShiftStandingNarration.Observe(false, false, LastShiftStandingNarration.DwellSeconds);
            Assume.That(LastShiftStandingNarration.HasLine, Is.False);

            LastShiftStandingNarration.NotifyAutoReturn();

            Assert.That(LastShiftStandingNarration.HasLine, Is.False, "회수 통지가 두 번 떴다");
        }

        /// <summary>
        /// <b>진행 순서 밖이다.</b> 상시 넷은 디렉터가 미는 줄에 하나도 안 들어간다 —
        /// 들어가면 산소가 안 마른 판에서 안내가 그 자리에 선다.
        /// </summary>
        [Test]
        public void TheStandingLinesAreNotPartOfTheDirectedOrder()
        {
            foreach (var standing in LastShiftNarrationScript.Standing)
                foreach (var directed in LastShiftNarrationScript.Directed)
                    Assert.That(directed.Id, Is.Not.EqualTo(standing.Id));
        }
    }
}
