using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 순회 안내가 끝난 뒤 <b>화면 자리를 놓는가</b>.
    ///
    /// 마지막 줄(<c>AI_T_11</c>)이 뜨면 <c>showing</c> 이 그 인덱스에 고정되는데, 그 뒤로
    /// 되돌리는 코드가 없어서 <c>HasLine</c> 이 영원히 참으로 남았다. 배너 분기에서 이 블록이
    /// 맨 위를 계속 차지하므로, 줄의 표시·페이드가 끝나 배너가 투명해진 뒤에도 분기가 아래로
    /// 못 내려가 <b>화면이 완전히 빈 채로 영구 고정</b>됐다 — 사용자가 방을 만들고 순회 안내
    /// 뒤 아무것도 안 뜬다고 지적한 상태다.
    ///
    /// <b>블록을 닫는 것과 자리를 놓는 것은 다르다.</b> 코어 유도 대사(<c>AI_B_01</c>)가
    /// <c>IsComplete</c> 를 기다리고 있으므로 블록 자체는 열린 채로 남아야 한다.
    /// </summary>
    public sealed class LastShiftPatrolNarrationReleaseTests
    {
        private const float Step = 1f / 60f;

        [SetUp]
        public void SetUp() => LastShiftPatrolNarration.Clear();

        [TearDown]
        public void TearDown() => LastShiftPatrolNarration.Clear();

        /// <summary>줄이 다 흐를 때까지 시계를 돌린다.</summary>
        private static void Settle(float seconds)
        {
            var left = seconds;
            while (left > 0f)
            {
                var dt = Mathf.Min(Step, left);
                LastShiftPatrolNarration.Tick(dt);
                left -= dt;
            }
        }

        /// <summary>
        /// 블록을 열고 네 방을 다 돌아 마지막 줄까지 몰아붙인다. <b>실제 이벤트만 쓴다</b> —
        /// 줄 이름으로 직접 재생시키면 게임에 없는 경로로 상태를 만들게 된다.
        /// </summary>
        private static void PlayToTheLastLine()
        {
            var hold = LastShiftPatrolNarration.MinimumDisplaySeconds + Step;

            LastShiftPatrolNarration.Begin();
            LastShiftPatrolNarration.NotifyInPlaza();
            Settle(hold);

            foreach (var room in new[]
                     {
                         LastShiftPlazaSpace.CockpitRoom,
                         LastShiftPlazaSpace.PowerRoom,
                         LastShiftPlazaSpace.CoolingRoom,
                         LastShiftPlazaSpace.LifeSupportRoom
                     })
            {
                LastShiftPatrolNarration.NotifyRoomEntered(room);
                Settle(hold);
                LastShiftPatrolNarration.NotifyAtFixture(room);
                Settle(hold);
            }

            // 마지막 방에서 광장으로 나오는 순간 닫는 줄이 뜬다.
            LastShiftPatrolNarration.NotifyInPlaza();
            Settle(hold);
        }

        /// <summary>
        /// <b>PM 수용기준.</b> 마지막 줄이 제 시간을 채우면 자리를 놓는다 — 그래야 배너 분기가
        /// 아래로 내려가 튜토리얼 안내가 보인다.
        /// </summary>
        [Test]
        public void TheLastLineReleasesTheBannerWhenItIsDone()
        {
            PlayToTheLastLine();
            Assume.That(LastShiftPatrolNarration.IsComplete, Is.True, "마지막 줄까지 못 갔다");

            Settle(LastShiftPatrolNarration.MinimumDisplaySeconds * 2f);

            Assert.That(LastShiftPatrolNarration.HasLine, Is.False,
                "순회 안내가 끝났는데 배너 자리를 계속 잡고 있다 — 그 아래 안내가 영영 못 뜬다");
        }

        /// <summary>
        /// <b>블록은 닫지 않는다.</b> 코어 유도 대사가 <c>IsComplete</c> 를 기다리므로, 자리를
        /// 놓았다고 블록까지 끄면 그 줄이 영영 안 뜬다.
        /// </summary>
        [Test]
        public void ReleasingTheBannerDoesNotCloseTheBlock()
        {
            PlayToTheLastLine();
            Settle(LastShiftPatrolNarration.MinimumDisplaySeconds * 2f);

            Assert.That(LastShiftPatrolNarration.IsRunning, Is.True,
                "자리를 놓으면서 블록까지 껐다 — AI_B_01 이 영영 안 뜬다");
            Assert.That(LastShiftPatrolNarration.IsComplete, Is.True,
                "완료 판정이 사라졌다 — 코어 유도 조건이 깨진다");
        }

        /// <summary>
        /// <b>마지막 줄을 너무 빨리 걷지 않는다.</b> 자리를 놓는 것이 최소 표시보다 앞서면
        /// 그 줄을 읽을 시간이 없다.
        /// </summary>
        [Test]
        public void TheLastLineStaysForItsMinimumTime()
        {
            LastShiftPatrolNarration.Begin();
            LastShiftPatrolNarration.NotifyInPlaza();

            // 첫 줄이 뜬 직후, 최소 표시의 절반만 흘린다.
            Settle(LastShiftPatrolNarration.MinimumDisplaySeconds * 0.5f);

            Assert.That(LastShiftPatrolNarration.HasLine, Is.True,
                "줄이 최소 표시 시간도 못 채우고 사라졌다");
        }
    }
}
