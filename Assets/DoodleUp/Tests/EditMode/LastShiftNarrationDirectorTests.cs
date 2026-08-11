using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 디렉터가 지키는 것은 하나다 — <b>정본 순서 그대로 흐르는가</b>. 사건 신호는 여러 곳에서
    /// 오고 그중 몇은 여러 번 오므로, 순서 판정이 한 곳에 있어야 부르는 쪽이 조건을 안 센다.
    /// </summary>
    public sealed class LastShiftNarrationDirectorTests
    {
        [SetUp]
        public void SetUp() => LastShiftNarrationDirector.Clear();

        [TearDown]
        public void TearDown() => LastShiftNarrationDirector.Clear();

        /// <summary>안 도는 동안은 아무 신호도 안 받는다 — 튜토리얼을 끝낸 판이다.</summary>
        [Test]
        public void NothingFiresBeforeBegin()
        {
            Assert.That(LastShiftNarrationDirector.Notify("AI_B_01"), Is.False);
            Assert.That(LastShiftNarrationDirector.HasLine, Is.False);
            Assert.That(LastShiftNarrationDirector.NextId, Is.Null);
        }

        [Test]
        public void TheFirstLineIsTheFirstOfTheDirectedOrder()
        {
            LastShiftNarrationDirector.Begin();

            Assert.That(LastShiftNarrationDirector.HasLine, Is.False, "아직 아무 사건도 안 났다");
            Assert.That(LastShiftNarrationDirector.NextId, Is.EqualTo("AI_B_01"));

            Assert.That(LastShiftNarrationDirector.Notify("AI_B_01"), Is.True);
            Assert.That(LastShiftNarrationDirector.Current.Id, Is.EqualTo("AI_B_01"));
        }

        /// <summary>
        /// <b>순서가 어긋난 신호를 안 받는다.</b> 사거리 판정은 드나들 때마다 오고 잔해는
        /// 덩이마다 오므로, 이 걸러내기가 없으면 부르는 쪽마다 "이미 떴는가" 를 따로 센다.
        /// </summary>
        [Test]
        public void OutOfOrderSignalsAreDropped()
        {
            LastShiftNarrationDirector.Begin();

            Assert.That(LastShiftNarrationDirector.Notify("AI_F_06"), Is.False,
                "한참 뒤 줄이 먼저 떴다");
            Assert.That(LastShiftNarrationDirector.HasLine, Is.False);

            LastShiftNarrationDirector.Notify("AI_B_01");
            Assert.That(LastShiftNarrationDirector.Notify("AI_B_01"), Is.False,
                "같은 신호가 두 번 먹었다");
            Assert.That(LastShiftNarrationDirector.Current.Id, Is.EqualTo("AI_B_01"));
        }

        /// <summary>조건 형 호출은 거짓이면 아무 일도 안 한다.</summary>
        [Test]
        public void TheConditionalFormOnlyFiresWhenTrue()
        {
            LastShiftNarrationDirector.Begin();

            Assert.That(LastShiftNarrationDirector.Notify("AI_B_01", false), Is.False);
            Assert.That(LastShiftNarrationDirector.NextId, Is.EqualTo("AI_B_01"));
            Assert.That(LastShiftNarrationDirector.Notify("AI_B_01", true), Is.True);
        }

        /// <summary>
        /// "앞줄 후 <c>N</c>초" 형은 시간이 민다. <c>AI_B_02</c> 가 그 첫 자리이고, 그때까지는
        /// 앞줄이 떠 있어야 한다.
        /// </summary>
        [Test]
        public void AutomaticLinesArriveOnTheirOwn()
        {
            LastShiftNarrationDirector.Begin();
            LastShiftNarrationDirector.Notify("AI_B_01");

            var wait = LastShiftNarrationScript.Of("AI_B_02").AutoAfterSeconds;
            Assert.That(wait, Is.GreaterThan(0f), "AI_B_02 가 시간 형으로 안 잡혀 있다");

            LastShiftNarrationDirector.Tick(wait * 0.5f);
            Assert.That(LastShiftNarrationDirector.Current.Id, Is.EqualTo("AI_B_01"),
                "예정 시각 전에 넘어갔다");

            LastShiftNarrationDirector.Tick(wait);
            Assert.That(LastShiftNarrationDirector.Current.Id, Is.EqualTo("AI_B_02"));
        }

        /// <summary>
        /// 시간 형이 아닌 줄에서는 <b>아무리 기다려도 안 넘어간다</b>. 사건이 안 왔는데
        /// 흘러가면 안내가 플레이어를 앞질러 끝난다.
        /// </summary>
        [Test]
        public void ManualLinesWaitForever()
        {
            LastShiftNarrationDirector.Begin();
            LastShiftNarrationDirector.Notify("AI_B_01");
            LastShiftNarrationDirector.Tick(LastShiftNarrationScript.Of("AI_B_02").AutoAfterSeconds);

            LastShiftNarrationDirector.Tick(600f);

            Assert.That(LastShiftNarrationDirector.Current.Id, Is.EqualTo("AI_B_02"));
            Assert.That(LastShiftNarrationDirector.NextId, Is.EqualTo("AI_F_01"));
        }

        /// <summary>
        /// 나가는 길 아홉 줄을 실제 순서대로 밟는다. <b>지금 배선된 블록의 끝이 여기다</b> —
        /// 파밍·도면 신호는 아직 안 걸려서 디렉터가 <c>AI_F_06</c> 에서 선다.
        /// </summary>
        [Test]
        public void TheExitBlockRunsEndToEnd()
        {
            LastShiftNarrationDirector.Begin();

            foreach (var line in LastShiftNarrationScript.Exit)
            {
                if (line.IsAutomatic) LastShiftNarrationDirector.Tick(line.AutoAfterSeconds);
                else Assert.That(LastShiftNarrationDirector.Notify(line.Id), Is.True,
                    $"{line.Id} 에서 막혔다");
                Assert.That(LastShiftNarrationDirector.Current.Id, Is.EqualTo(line.Id));
            }

            Assert.That(LastShiftNarrationDirector.FiredCount,
                Is.EqualTo(LastShiftNarrationScript.Exit.Length));
            Assert.That(LastShiftNarrationDirector.NextId, Is.EqualTo("AI_F_07"),
                "나가는 길 다음이 파밍 첫 줄이 아니다");
        }

        /// <summary>
        /// 조항 <c>N-6</c> — 마무리 두 줄이 도면 뒤다. 배열 순서가 곧 진행 순서라
        /// 따로 검사할 것이 없다는 것을 여기서 못박는다.
        /// </summary>
        [Test]
        public void TheDirectedOrderPutsTheWrapUpLast()
        {
            var ids = LastShiftNarrationScript.Directed.Select(line => line.Id).ToList();

            Assert.That(ids.First(), Is.EqualTo("AI_B_01"));
            Assert.That(ids.Last(), Is.EqualTo("AI_F_16"));
            Assert.That(ids.IndexOf("AI_F_15"), Is.GreaterThan(ids.IndexOf("AI_B_17")));
            Assert.That(ids.Count, Is.EqualTo(LastShiftNarrationScript.Count
                                              - LastShiftNarrationScript.Wake.Length));
        }
    }
}
