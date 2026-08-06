using System;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 그레이박스 비주얼 드레싱의 제약을 고정한다. 드레싱은 씬 빌더가 세우지만 <b>제약은
    /// 좌표와 색에 걸려 있고</b>, 씬을 다시 구워야만 보이는 위반은 사실상 아무도 못 본다 —
    /// 그래서 값 정본이 <see cref="LastShiftDressing"/> 에 있고 검사가 여기 있다.
    ///
    /// 두 가지를 본다.
    ///   1. 상태 단서가 개구부 노출 원뿔 밖에 있는가(§19.4/§19.7). 원뿔 안에 든 시각 단서는
    ///      게이지가 없어도 사실상 세 번째 게이지가 된다.
    ///   2. 색 스물다섯 개가 서로 구분되는가. 구역 색 넷이 1차 인지 앵커이므로 구획색이
    ///      그 옆에 붙으면 위계가 무너진다.
    /// </summary>
    public sealed class LastShiftDressingTests
    {
        private static LastShiftCompartment[] AllCompartments =>
            Enum.GetValues(typeof(LastShiftCompartment)).Cast<LastShiftCompartment>().ToArray();

        private static LastShiftZone[] AllZones =>
            Enum.GetValues(typeof(LastShiftZone)).Cast<LastShiftZone>().ToArray();

        [Test]
        public void StateCuesStayOutOfTheOpeningExposureCone()
        {
            // 중심이 아니라 단서가 차지하는 가장 큰 z 로 본다 — 중심이 안전대 안이어도 폭이
            // 크면 모서리가 원뿔로 넘어간다.
            foreach (var cue in LastShiftDressing.StateCues)
                Assert.That(cue.MaxZ, Is.LessThanOrEqualTo(LastShiftDressing.StateCueSafeMaxZ),
                    $"{cue.Name} 이 개구부 노출 원뿔로 넘어간다 — 상태에 반응하는 단서는 " +
                    $"z ≤ {LastShiftDressing.StateCueSafeMaxZ} 안에만 둘 수 있다(§19.4/§19.7).");
        }

        [Test]
        public void StateCuesStayInsideTheirOwnRoom()
        {
            // 방 밖으로 새면 서리가 전력실에서, 그을음이 냉각실에서 보인다 — 상태가 엉뚱한
            // 방의 정보로 읽히는 것이 원뿔 위반보다 나쁘다.
            foreach (var cue in LastShiftDressing.StateCues)
            {
                Assert.That(cue.MinX, Is.GreaterThanOrEqualTo(LastShiftShipDimensions.RoomMinX(cue.Room)),
                    $"{cue.Name} 이 {cue.Room} 선수 쪽 벽을 넘는다.");
                Assert.That(cue.MaxX, Is.LessThanOrEqualTo(LastShiftShipDimensions.RoomMaxX(cue.Room)),
                    $"{cue.Name} 이 {cue.Room} 선미 쪽 벽을 넘는다.");
            }
        }

        [Test]
        public void StateCuesStayInsideTheHull()
        {
            const float halfWidth = LastShiftShipDimensions.HalfWidth;
            foreach (var cue in LastShiftDressing.StateCues)
            {
                var minZ = cue.CenterZ - cue.Size.z * 0.5f;
                Assert.That(minZ, Is.GreaterThanOrEqualTo(-halfWidth - 0.0001f),
                    $"{cue.Name} 이 좌현 벽 밖으로 나간다.");
                Assert.That(cue.CenterY - cue.Size.y * 0.5f, Is.GreaterThanOrEqualTo(-0.0001f),
                    $"{cue.Name} 이 갑판 아래로 내려간다.");
            }
        }

        [Test]
        public void BothStateCueKindsAreRepresented()
        {
            // 냉각실만, 또는 전력실만 단서를 가지면 둘 중 하나가 상태 없는 방으로 읽힌다.
            foreach (var kind in Enum.GetValues(typeof(LastShiftStateCue)).Cast<LastShiftStateCue>())
                Assert.That(LastShiftDressing.StateCues.Any(cue => cue.Kind == kind), Is.True,
                    $"{kind} 단서가 하나도 없다.");
        }

        [Test]
        public void EveryCompartmentHasItsOwnTint()
        {
            var tints = AllCompartments.Select(LastShiftDressing.TintOf).ToArray();
            for (var a = 0; a < tints.Length; a++)
            for (var b = a + 1; b < tints.Length; b++)
                Assert.That(LastShiftDressing.TintDistance(tints[a], tints[b]),
                    Is.GreaterThanOrEqualTo(LastShiftDressing.MinimumTintSeparation),
                    $"{AllCompartments[a]} 와 {AllCompartments[b]} 가 같은 조명 아래 같은 색으로 보인다.");
        }

        [Test]
        public void EveryZoneHasItsOwnTint()
        {
            // 구역 색 넷은 1차 인지 앵커다. 개구부 너머로 보이는 색이 어느 방인지 안 갈리면
            // 나머지 위계가 전부 그 위에 얹혀 있으므로 같이 무너진다.
            var zones = AllZones;
            for (var a = 0; a < zones.Length; a++)
            for (var b = a + 1; b < zones.Length; b++)
                Assert.That(
                    LastShiftDressing.TintDistance(LastShiftDressing.TintOf(zones[a]), LastShiftDressing.TintOf(zones[b])),
                    Is.GreaterThanOrEqualTo(LastShiftDressing.MinimumTintSeparation),
                    $"구역 {zones[a]} 와 {zones[b]} 가 같은 색으로 보인다.");
        }

        [Test]
        public void CompartmentTintsStayClearOfZoneTints()
        {
            // 구역 색이 1차 앵커다. 구획색이 그 옆에 붙으면 복도 끝 색이 어느 위계의
            // 정보인지 읽히지 않는다.
            foreach (var compartment in AllCompartments)
            foreach (var zone in AllZones)
                Assert.That(
                    LastShiftDressing.TintDistance(LastShiftDressing.TintOf(compartment), LastShiftDressing.TintOf(zone)),
                    Is.GreaterThanOrEqualTo(LastShiftDressing.MinimumTintSeparation),
                    $"구획 {compartment} 색이 구역 {zone} 색과 붙어 있다 — 색 위계가 무너진다.");
        }

        [Test]
        public void OnlyTheEscapePodIsRed()
        {
            // 배 전체에서 적색이 하나뿐이어야 "복도 끝의 적색 = 마지막 수단" 이 성립한다.
            // 언락 상태를 말하지 않으면서 방의 역할을 색 하나로 전달하는 유일한 자리다.
            bool IsRed(Color c) => c.r > 0.6f && c.r - Mathf.Max(c.g, c.b) > 0.25f;

            var reds = AllCompartments.Where(c => IsRed(LastShiftDressing.TintOf(c))).ToArray();
            Assert.That(reds, Is.EqualTo(new[] { LastShiftCompartment.EscapePod }),
                "적색은 구명정 전용이다.");
            foreach (var zone in AllZones)
                Assert.That(IsRed(LastShiftDressing.TintOf(zone)), Is.False,
                    $"구역 {zone} 색이 적색 대역에 들어왔다 — 구명정의 단독성이 깨진다.");
        }

        [Test]
        public void TintsStayWithinTheGrayboxValueBand()
        {
            // 그레이박스는 조명이 임시라 값이 양 끝으로 몰리면 검거나 흰 덩어리가 된다.
            // 상한·하한을 걸어 두면 새 색을 넣을 때 그 밖으로 나가는 것을 바로 안다.
            foreach (var compartment in AllCompartments)
            {
                var tint = LastShiftDressing.TintOf(compartment);
                var value = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
                Assert.That(value, Is.InRange(0.30f, 0.92f),
                    $"{compartment} 색의 명도가 그레이박스 대역을 벗어난다.");
            }
        }
    }
}
