using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 조항 <c>N-3</c> — 순회는 <b>순서가 없다</b>. 여기서 지키는 것은 그 하나이고, 나머지는
    /// 자유로운 것이 방 순서<b>뿐</b>이라는 것(여닫는 줄과 방 안의 두 줄은 고정)이다.
    /// </summary>
    public sealed class LastShiftPatrolNarrationTests
    {
        [SetUp]
        public void SetUp() => LastShiftPatrolNarration.Begin();

        [TearDown]
        public void TearDown() => LastShiftPatrolNarration.Clear();

        [Test]
        public void ThePlazaOpensTheBlock()
        {
            Assert.That(LastShiftPatrolNarration.HasLine, Is.False);

            LastShiftPatrolNarration.NotifyInPlaza();

            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_01"));
            Assert.That(LastShiftPatrolNarration.Current.Sfx,
                Is.EqualTo(LastShiftNarrationSfx.ChimeLong), "블록 첫 줄에 긴 신호음이 없다");
        }

        /// <summary>여는 줄 전에는 방에 들어가도 아무 말도 안 한다 — 아직 안내가 시작 전이다.</summary>
        [Test]
        public void RoomsStaySilentBeforeTheBlockOpens()
        {
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CockpitRoom);

            Assert.That(LastShiftPatrolNarration.HasLine, Is.False);
            Assert.That(LastShiftPatrolNarration.RoomsLeft, Is.EqualTo(4));
        }

        /// <summary>
        /// <b>이 검사가 이 파일의 전부다.</b> 대본이 적은 순서(조종석 → 전력실 → 산소실 →
        /// 냉각실)를 거꾸로 돌아도 각 방 줄이 <b>그 자리에서</b> 나온다.
        /// </summary>
        [Test]
        public void AnyRoomOrderPlaysItsOwnLinesRightThere()
        {
            Open();

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CoolingRoom);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_09"),
                "냉각실부터 들어갔는데 조종석 줄을 기다린다");
            LastShiftPatrolNarration.NotifyAtFixture(LastShiftPlazaSpace.CoolingRoom);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_10"));

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.LifeSupportRoom);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_07"));

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.PowerRoom);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_05"));

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CockpitRoom);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_03"));

            Assert.That(LastShiftPatrolNarration.RoomsLeft, Is.Zero);
        }

        /// <summary>방 안에서는 순서가 있다. 설비 줄은 <b>그 방에 들어온 뒤</b>에만 나온다.</summary>
        [Test]
        public void TheFixtureLineNeedsTheRoomFirst()
        {
            Open();

            LastShiftPatrolNarration.NotifyAtFixture(LastShiftPlazaSpace.PowerRoom);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_02"),
                "안 들어간 방의 설비 줄이 먼저 나왔다");

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.PowerRoom);
            LastShiftPatrolNarration.NotifyAtFixture(LastShiftPlazaSpace.PowerRoom);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_06"));
        }

        /// <summary>같은 방을 다시 밟아도 다시 안 말한다.</summary>
        [Test]
        public void ReenteringARoomSaysNothingNew()
        {
            Open();
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.PowerRoom);
            LastShiftPatrolNarration.NotifyAtFixture(LastShiftPlazaSpace.PowerRoom);

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.PowerRoom);

            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_06"),
                "다시 들어갔더니 진입 줄이 또 나왔다");
        }

        /// <summary>
        /// <b>설비 앞까지 안 가도 안내가 닫힌다.</b> 들어왔다가 그냥 나온 방 때문에 순회가
        /// 영영 안 끝나면, 그 판은 다음 블록으로 못 넘어간다.
        /// </summary>
        [Test]
        public void EnteringIsEnoughToCloseTheBlock()
        {
            Open();
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CockpitRoom);
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.PowerRoom);
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.LifeSupportRoom);
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CoolingRoom);
            Assert.That(LastShiftPatrolNarration.IsComplete, Is.False, "광장에 안 돌아왔는데 닫혔다");

            LastShiftPatrolNarration.NotifyInPlaza();

            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_11"));
            Assert.That(LastShiftPatrolNarration.IsComplete, Is.True);
        }

        /// <summary>방이 남았으면 광장으로 돌아와도 안 닫힌다.</summary>
        [Test]
        public void ComingBackEarlyDoesNotCloseTheBlock()
        {
            Open();
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CockpitRoom);

            LastShiftPatrolNarration.NotifyInPlaza();

            Assert.That(LastShiftPatrolNarration.IsComplete, Is.False);
            Assert.That(LastShiftPatrolNarration.RoomsLeft, Is.EqualTo(3));
        }

        /// <summary>코어는 순서 무관이고 한 번뿐이다.</summary>
        [Test]
        public void TheCoreLineIsFreeAndOnlyOnce()
        {
            Open();
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CoolingRoom);

            LastShiftPatrolNarration.NotifyNearCore();
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_02B"),
                "방을 하나 본 뒤에는 코어 줄이 안 나온다");

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.PowerRoom);
            LastShiftPatrolNarration.NotifyNearCore();
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_05"),
                "코어 줄이 두 번 나왔다");
        }

        /// <summary>
        /// 시간 형 둘(<c>AI_T_02</c>·<c>AI_T_04B</c>)은 <b>바로 앞줄 뒤에만</b> 따라붙는다 —
        /// 방을 건너뛰어도 엉뚱한 줄이 시간으로 안 나온다.
        /// </summary>
        [Test]
        public void AutomaticLinesOnlyFollowTheirOwnLine()
        {
            LastShiftPatrolNarration.NotifyInPlaza();
            LastShiftPatrolNarration.Tick(LastShiftNarrationScript.Of("AI_T_02").AutoAfterSeconds);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_02"));

            // 전력실로 들어가면 그 뒤 줄(AI_T_06)은 시간 형이 아니라 안 따라붙는다.
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.PowerRoom);
            LastShiftPatrolNarration.Tick(600f);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_05"));

            // 조종석 스크린 뒤에는 따라붙는다.
            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CockpitRoom);
            LastShiftPatrolNarration.NotifyAtFixture(LastShiftPlazaSpace.CockpitRoom);
            Assume.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_04"));
            LastShiftPatrolNarration.Tick(LastShiftNarrationScript.Of("AI_T_04B").AutoAfterSeconds);
            Assert.That(LastShiftPatrolNarration.Current.Id, Is.EqualTo("AI_T_04B"));
        }

        /// <summary>
        /// 줄이 바뀌면 재촉 시계가 다시 선다. 안 그러면 앞줄의 재촉("선수 쪽 개구부로 갈 것")이
        /// 방금 들어간 방의 줄 위에 그대로 남는다.
        /// </summary>
        [Test]
        public void TheNudgeClockRestartsWhenARoomInterrupts()
        {
            Open();
            LastShiftPatrolNarration.Tick(LastShiftNarrationScript.Of("AI_T_02").NudgeAfterSeconds);
            Assume.That(LastShiftPatrolNarration.LineElapsedSeconds, Is.GreaterThan(0f));

            LastShiftPatrolNarration.NotifyRoomEntered(LastShiftPlazaSpace.CoolingRoom);

            Assert.That(LastShiftPatrolNarration.LineElapsedSeconds, Is.Zero,
                "새 줄이 떴는데 앞줄의 재촉 시계가 이어졌다");
        }

        /// <summary>
        /// 설비 도달률. <b>순회를 빨리 끝내면 교육 내용의 절반을 안 듣는다</b> — 판정선은
        /// 늘어짐만 잡아서 이 축을 못 본다(game-balance 지적). 진행은 안 막고 수만 센다.
        /// </summary>
        [Test]
        public void TheFixtureCountSeesARushedTour()
        {
            Open();
            foreach (var space in new[]
                     {
                         LastShiftPlazaSpace.CockpitRoom, LastShiftPlazaSpace.PowerRoom,
                         LastShiftPlazaSpace.LifeSupportRoom, LastShiftPlazaSpace.CoolingRoom
                     })
                LastShiftPatrolNarration.NotifyRoomEntered(space);

            Assert.That(LastShiftPatrolNarration.RoomsLeft, Is.Zero);
            Assert.That(LastShiftPatrolNarration.FixturesReached, Is.Zero,
                "설비 앞에 한 번도 안 갔는데 도달로 세었다");

            // 그래도 안내는 닫힌다 — 지표이지 관문이 아니다.
            LastShiftPatrolNarration.NotifyInPlaza();
            Assert.That(LastShiftPatrolNarration.IsComplete, Is.True);
        }

        /// <summary>다 들렀으면 <c>4/4</c> 다.</summary>
        [Test]
        public void TheFixtureCountReachesFourOnAFullTour()
        {
            Open();
            foreach (var space in new[]
                     {
                         LastShiftPlazaSpace.CockpitRoom, LastShiftPlazaSpace.PowerRoom,
                         LastShiftPlazaSpace.LifeSupportRoom, LastShiftPlazaSpace.CoolingRoom
                     })
            {
                LastShiftPatrolNarration.NotifyRoomEntered(space);
                LastShiftPatrolNarration.NotifyAtFixture(space);
            }

            Assert.That(LastShiftPatrolNarration.FixturesReached,
                Is.EqualTo(LastShiftPatrolNarration.RoomCount));
        }

        /// <summary>여는 두 줄까지 밀어 둔다 — 방 검사들의 공통 준비다.</summary>
        private static void Open()
        {
            LastShiftPatrolNarration.NotifyInPlaza();
            LastShiftPatrolNarration.Tick(LastShiftNarrationScript.Of("AI_T_02").AutoAfterSeconds);
        }
    }
}
