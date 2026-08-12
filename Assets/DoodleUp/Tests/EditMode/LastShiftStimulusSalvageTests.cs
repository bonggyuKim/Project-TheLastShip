using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 피격 방이 남기는 잔해와 방 추첨 가중치(<c>game-balance</c> 확정 · PM 핸드오프 2026-08-12).
    ///
    /// 재는 것 셋 — <b>선체분은 항상 나오는가</b>, <b>계통분은 계통이 있는 방에서만 나오는가</b>,
    /// <b>안 맞은 방이 결국 맞는가</b>.
    /// </summary>
    public sealed class LastShiftStimulusSalvageTests
    {
        [SetUp]
        public void SetUp() => LastShiftExternalStimulus.Clear();

        [TearDown]
        public void TearDown() => LastShiftExternalStimulus.Clear();

        /// <summary>
        /// <b>선체분은 다섯 방 전부에서 나온다.</b> 맞으면 뚫리는 것은 방을 안 가린다 —
        /// 어느 방을 맞아도 빈손인 기항이 없어야 한다.
        /// </summary>
        [Test]
        public void EveryRoomLeavesHullSalvage()
        {
            foreach (LastShiftStimulusRoom room in System.Enum.GetValues(typeof(LastShiftStimulusRoom)))
            {
                var yielded = LastShiftStimulusSalvage.Of(room, 1f);
                Assert.That(yielded.Hull, Is.GreaterThan(0), $"{room} 이 선체분을 안 남긴다");
            }
        }

        /// <summary>PM 표 그대로. 심각도 <c>1.0</c> 기준값이다.</summary>
        [Test]
        public void TheYieldTableMatchesTheBalanceSheet()
        {
            void Check(LastShiftStimulusRoom room, int hull, int system, LastShiftSalvageComponent kind)
            {
                var y = LastShiftStimulusSalvage.Of(room, 1f);
                Assert.That(y.Hull, Is.EqualTo(hull), $"{room} 선체분");
                Assert.That(y.System, Is.EqualTo(system), $"{room} 계통분");
                Assert.That(y.Component, Is.EqualTo(kind), $"{room} 계열");
            }

            Check(LastShiftStimulusRoom.Cockpit, 60, 60, LastShiftSalvageComponent.Propulsion);
            Check(LastShiftStimulusRoom.Power, 30, 60, LastShiftSalvageComponent.Power);
            Check(LastShiftStimulusRoom.Cooling, 30, 60, LastShiftSalvageComponent.Heat);
            Check(LastShiftStimulusRoom.LifeSupport, 30, 60, LastShiftSalvageComponent.Oxygen);
            Check(LastShiftStimulusRoom.Quarters, 60, 0, LastShiftSalvageComponent.None);
        }

        /// <summary>
        /// <b>숙소만 계통분이 없다.</b> 그 방에 계통이 없다는 사실을 그대로 반영한 것이고,
        /// 대신 선체분이 커서 빈손이 되지 않는다.
        /// </summary>
        [Test]
        public void OnlyTheQuartersLeaveNoSystemSalvage()
        {
            foreach (LastShiftStimulusRoom room in System.Enum.GetValues(typeof(LastShiftStimulusRoom)))
            {
                var y = LastShiftStimulusSalvage.Of(room, 1f);
                var expected = room != LastShiftStimulusRoom.Quarters;
                Assert.That(y.HasSystem, Is.EqualTo(expected), $"{room} 의 계통분 유무가 표와 다르다");
            }

            var quarters = LastShiftStimulusSalvage.Of(LastShiftStimulusRoom.Quarters, 1f);
            Assert.That(quarters.Total, Is.GreaterThan(0), "숙소를 맞으면 아무것도 안 나온다");
            Assert.That(quarters.Hull,
                Is.EqualTo(LastShiftStimulusSalvage.Of(LastShiftStimulusRoom.Power, 1f).Hull * 2),
                "계통 없는 방의 선체분이 단독 구역 방과 같으면 그 판이 손해가 된다");
        }

        /// <summary>
        /// <b>총량이 심각도에 비례한다.</b> 안 그러면 강도 랜덤화가 파밍에는 아무 의미가 없다.
        /// </summary>
        [Test]
        public void WeakerHitsLeaveLess()
        {
            var strong = LastShiftStimulusSalvage.Of(LastShiftStimulusRoom.Power, 1f);
            var weak = LastShiftStimulusSalvage.Of(LastShiftStimulusRoom.Power, 0.7f);

            Assert.That(weak.Total, Is.LessThan(strong.Total), "약하게 맞았는데 같은 양이 나온다");
            Assert.That(weak.Hull, Is.EqualTo(Mathf.RoundToInt(30 * 0.7f)));
            Assert.That(weak.System, Is.EqualTo(Mathf.RoundToInt(60 * 0.7f)));
        }

        // ── 방 추첨 ──────────────────────────────────────────────────────────

        /// <summary>가중치 식이 <c>w = 1 + 0.4k</c> 다.</summary>
        [Test]
        public void TheWeightGrowsWithEveryPortMissed()
        {
            Assert.That(LastShiftExternalStimulus.PityWeightPerPort, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(LastShiftExternalStimulus.WeightOf(LastShiftStimulusRoom.Power),
                Is.EqualTo(1f).Within(0.001f), "한 번도 안 지났는데 가중치가 1 이 아니다");
        }

        /// <summary>
        /// <b>하한이 8 이다.</b> 6 이하로 조이면 순서가 눈에 보여 "다음은 저 방" 을 외우게
        /// 되고, 그러면 랜덤화가 만들려던 것이 사라진다.
        /// </summary>
        [Test]
        public void TheHardCapDoesNotDropBelowEight()
        {
            Assert.That(LastShiftExternalStimulus.HardCapPorts, Is.GreaterThanOrEqualTo(8),
                "하드캡을 8 밑으로 내리면 피격 순서를 외우게 된다");
        }

        /// <summary>
        /// <b>어떤 방도 하드캡을 넘겨 굶지 않는다.</b> 이 검사가 이 기능의 존재 이유다 —
        /// 한 계열 자재가 안 나오면 그 계열이 필요한 확장이 통째로 막힌다.
        /// </summary>
        [Test]
        public void NoRoomStarvesPastTheHardCap()
        {
            var worst = 0;
            for (var port = 0; port < 400; port++)
            {
                LastShiftExternalStimulus.BeginSegment(port);
                foreach (LastShiftStimulusRoom room in System.Enum.GetValues(typeof(LastShiftStimulusRoom)))
                    worst = Mathf.Max(worst, LastShiftExternalStimulus.PortsSince(room));
            }

            // <b>정확히 8 에서 멈출 수는 없다.</b> 기항 하나가 때리는 방은 하나인데 캡에
            // 동시에 닿는 방은 여럿일 수 있어서, 둘이 같이 8 이 되면 한 쪽은 그 기항을
            // 더 기다린다. 그래서 실제 상한은 캡 + (방 수 - 1) 이고, 굶은 방이 여럿일 때
            // 가장 오래 굶은 쪽부터 빼는 것이 그 초과를 최소로 만드는 방법이다.
            var bound = LastShiftExternalStimulus.HardCapPorts + LastShiftExternalStimulus.RoomCount - 1;
            Assert.That(worst, Is.LessThanOrEqualTo(bound),
                $"어떤 방이 {worst} 기항 동안 안 맞았다 — 캡 " +
                $"{LastShiftExternalStimulus.HardCapPorts} 에 동시 적체 여유를 더한 {bound} 도 넘겼다");
        }

        /// <summary>
        /// 굶은 방은 <b>확정으로</b> 맞는다. 하드캡에 닿은 방을 하나 만들어 두고 다음 구간을
        /// 열면 그 방이 나와야 한다.
        /// </summary>
        [Test]
        public void AStarvedRoomIsGuaranteedTheNextHit()
        {
            // 굶은 방이 하나라도 생길 때까지 돌린다. 어느 방이 굶을지는 추첨이 정하므로
            // 방을 미리 고르지 않는다 — 고르면 그 방이 안 굶는 씨앗에서 검사가 흔들린다.
            LastShiftStimulusRoom Starved()
            {
                var worst = LastShiftStimulusRoom.Cockpit;
                foreach (LastShiftStimulusRoom room in System.Enum.GetValues(typeof(LastShiftStimulusRoom)))
                    if (LastShiftExternalStimulus.PortsSince(room)
                        > LastShiftExternalStimulus.PortsSince(worst)) worst = room;
                return worst;
            }

            var guard = 0;
            while (LastShiftExternalStimulus.PortsSince(Starved())
                   < LastShiftExternalStimulus.HardCapPorts && guard++ < 400)
                LastShiftExternalStimulus.BeginSegment(guard);

            var target = Starved();
            Assume.That(LastShiftExternalStimulus.PortsSince(target),
                Is.GreaterThanOrEqualTo(LastShiftExternalStimulus.HardCapPorts),
                "굶은 방을 만들지 못했다");

            LastShiftExternalStimulus.BeginSegment(9999);

            Assert.That(LastShiftExternalStimulus.Room, Is.EqualTo(target),
                "가장 오래 굶은 방이 안 나왔다 — 씨앗이 하드캡을 이겼다는 뜻이다");
        }

        /// <summary>맞은 방의 기록은 그 자리에서 <c>0</c> 으로 돌아간다.</summary>
        [Test]
        public void BeingHitResetsThatRoomsCounter()
        {
            LastShiftExternalStimulus.BeginSegment(4);
            var hit = LastShiftExternalStimulus.Room;

            Assert.That(LastShiftExternalStimulus.PortsSince(hit), Is.Zero,
                "맞은 방의 결핍 기록이 안 지워졌다");

            var others = new List<LastShiftStimulusRoom>();
            foreach (LastShiftStimulusRoom room in System.Enum.GetValues(typeof(LastShiftStimulusRoom)))
                if (room != hit) others.Add(room);
            foreach (var room in others)
                Assert.That(LastShiftExternalStimulus.PortsSince(room), Is.EqualTo(1),
                    $"{room} 의 기항 카운터가 안 올라갔다");
        }

        /// <summary>새 항해는 빚 없이 시작한다.</summary>
        [Test]
        public void ClearingWipesTheDroughtRecord()
        {
            for (var port = 0; port < 12; port++) LastShiftExternalStimulus.BeginSegment(port);
            LastShiftExternalStimulus.Clear();

            foreach (LastShiftStimulusRoom room in System.Enum.GetValues(typeof(LastShiftStimulusRoom)))
                Assert.That(LastShiftExternalStimulus.PortsSince(room), Is.Zero,
                    $"{room} 의 결핍 기록이 새 항해로 넘어왔다");
        }
    }
}
