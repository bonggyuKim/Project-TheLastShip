using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 순회 판정. 좌표를 발자국에서 유도하므로 <b>그 유도가 정본 값과 맞는지</b>가 첫 검사이고,
    /// 나머지는 방문 추적이 순서를 안 보는가(조항 <c>N-3</c>)와 시야 판정 둘이다.
    /// </summary>
    public sealed class LastShiftPatrolTests
    {
        [SetUp]
        public void SetUp() => LastShiftPatrol.Clear();

        [TearDown]
        public void TearDown() => LastShiftPatrol.Clear();

        /// <summary>
        /// <b>유도한 자리가 정본 좌표와 같은가.</b> <c>central-plaza-hub-layout-v1.md</c> §4.1 이
        /// 게이지 셋을 <c>(0,−14)</c>·<c>(+16,0)</c>·<c>(0,+14)</c> 로 적는다. 값을 베껴 두지
        /// 않고 발자국에서 뽑았으므로, 이 검사가 그 둘을 묶는 유일한 자리다.
        ///
        /// 방 깊이가 바뀌면 정본 숫자도 같이 바뀐다 — 실제로 전력실이 <c>5→8m</c> 로 깊어지며
        /// <c>(0,−11) → (0,−14)</c> 로 밀렸다. 그때 이 검사가 먼저 깨지는 것이 맞다.
        /// </summary>
        [Test]
        public void TheFixturesSitWhereTheLayoutDocSaysTheyDo()
        {
            AssertFarWall(LastShiftPlazaSpace.PowerRoom, 0f, -14f);
            AssertFarWall(LastShiftPlazaSpace.LifeSupportRoom, 16f, 0f);
            AssertFarWall(LastShiftPlazaSpace.CoolingRoom, 0f, 14f);
            // 조종석 전면 스크린. 같은 규칙이라 같은 자리에서 나온다.
            AssertFarWall(LastShiftPlazaSpace.CockpitRoom, -16f, 0f);
        }

        private static void AssertFarWall(LastShiftPlazaSpace space, float x, float z)
        {
            var point = LastShiftPlazaLayout.FarWallCenter(space);
            Assert.That(point.x, Is.EqualTo(x).Within(0.001f), $"{space} 끝벽 x");
            Assert.That(point.y, Is.EqualTo(z).Within(0.001f), $"{space} 끝벽 z");
        }

        [Test]
        public void StandingAtTheFarWallCountsAsReachingTheFixture()
        {
            var wall = LastShiftPlazaLayout.FarWallCenter(LastShiftPlazaSpace.PowerRoom);

            Assert.That(LastShiftPatrol.IsAtFixture(LastShiftPlazaSpace.PowerRoom,
                new Vector3(wall.x, 0f, wall.y)), Is.True);
            // 사거리 바로 밖은 아니다. 방이 8m 깊으므로 문간은 확실히 밖이다.
            Assert.That(LastShiftPatrol.IsAtFixture(LastShiftPlazaSpace.PowerRoom,
                new Vector3(wall.x, 0f, wall.y + LastShiftPatrol.FixtureReach + 0.5f)), Is.False);
            Assert.That(LastShiftPatrol.IsAtFixture(LastShiftPlazaSpace.PowerRoom,
                new Vector3(0f, 0f, -6f)), Is.False, "문간에서 배전반이 잡혔다");
        }

        /// <summary>
        /// 시야는 <b>거리와 각도를 같이</b> 본다. 하나만 보면 문간에서도 걸리거나(거리만)
        /// 등지고 걸어가면서도 걸린다(각도만).
        /// </summary>
        [Test]
        public void TheScreenNeedsBothDistanceAndFacing()
        {
            var screen = LastShiftPlazaLayout.FarWallCenter(LastShiftPlazaSpace.CockpitRoom);
            // 스크린 앞 2m. 조종석은 x 가 작아지는 쪽이 안쪽이라 스크린을 보려면 -x 를 본다.
            var infront = new Vector3(screen.x + 2f, 0f, screen.y);

            Assert.That(LastShiftPatrol.IsFacingCockpitScreen(infront, Vector3.left), Is.True);
            Assert.That(LastShiftPatrol.IsFacingCockpitScreen(infront, Vector3.right), Is.False,
                "등지고 서 있는데 시야로 잡혔다");

            // 각도는 맞는데 너무 멀다 — 조종석 입구 쪽이다.
            var farAway = new Vector3(screen.x + LastShiftPatrol.ScreenViewDistance + 1f, 0f, screen.y);
            Assert.That(LastShiftPatrol.IsFacingCockpitScreen(farAway, Vector3.left), Is.False);
        }

        /// <summary>
        /// 조항 <c>N-3</c> — <b>순서를 안 본다.</b> 뒤에서부터 돌아도 다섯이 차면 열린다.
        /// </summary>
        [Test]
        public void VisitTrackingIgnoresOrder()
        {
            Assert.That(LastShiftPatrol.AllVisited, Is.False);
            Assert.That(LastShiftPatrol.RemainingCount, Is.EqualTo(LastShiftPatrol.Rooms.Length));

            for (var i = LastShiftPatrol.Rooms.Length - 1; i >= 0; i--)
            {
                Assert.That(LastShiftPatrol.AllVisited, Is.False, "다 안 찼는데 열렸다");
                LastShiftPatrol.Observe(LastShiftPatrol.Rooms[i]);
            }

            Assert.That(LastShiftPatrol.AllVisited, Is.True);
            Assert.That(LastShiftPatrol.RemainingCount, Is.Zero);
        }

        /// <summary>광장은 세는 쪽에 안 든다 — 들면 마지막 방을 나오기 전에 이미 다 찬다.</summary>
        [Test]
        public void ThePlazaIsNotOneOfTheRooms()
        {
            Assert.That(LastShiftPatrol.Rooms, Has.No.Member(LastShiftPlazaSpace.Plaza));

            LastShiftPatrol.Observe(LastShiftPlazaSpace.Plaza);
            Assert.That(LastShiftPatrol.RemainingCount, Is.EqualTo(LastShiftPatrol.Rooms.Length));
            Assert.That(LastShiftPatrol.HasVisited(LastShiftPlazaSpace.Plaza), Is.False);
        }

        /// <summary>같은 방을 여러 프레임 밟아도 한 번이다.</summary>
        [Test]
        public void ObservingTheSameRoomTwiceIsIdempotent()
        {
            LastShiftPatrol.Observe(LastShiftPlazaSpace.CockpitRoom);
            var afterFirst = LastShiftPatrol.RemainingCount;
            LastShiftPatrol.Observe(LastShiftPlazaSpace.CockpitRoom);

            Assert.That(LastShiftPatrol.RemainingCount, Is.EqualTo(afterFirst));
            Assert.That(LastShiftPatrol.HasVisited(LastShiftPlazaSpace.CockpitRoom), Is.True);
        }
    }
}
