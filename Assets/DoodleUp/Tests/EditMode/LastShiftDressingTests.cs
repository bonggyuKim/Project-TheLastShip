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
        private const float Tolerance = 0.0001f;

        private static LastShiftCompartment[] AllCompartments =>
            Enum.GetValues(typeof(LastShiftCompartment)).Cast<LastShiftCompartment>().ToArray();

        private static LastShiftZone[] AllZones =>
            Enum.GetValues(typeof(LastShiftZone)).Cast<LastShiftZone>().ToArray();

        [Test]
        public void StateCuesStayOutOfTheDoorwayWedge()
        {
            // <b>절대 z 밴드가 문 쐐기로 바뀌었다.</b> 옛 상한(StateCueSafeMaxZ)은 전력실↔냉각실
            // 방-방 개구부의 원뿔에서 나온 값인데 그 개구부가 §3.4 에서 폐지됐다. 지금 남은
            // 요건은 "광장에서 문 구멍을 지나는 직선이 이 단서에 닿는가" 이고, 두 방의 문은
            // z 평면이라 자유축이 x 다.
            //
            // 중심이 아니라 단서가 차지하는 x 구간으로 본다 — 중심이 쐐기 밖이어도 폭이 크면
            // 모서리가 넘어온다.
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            foreach (var cue in LastShiftDressing.StateCues)
            {
                var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaLayout.RoomOf(cue.Room));
                Assert.That(door.PlaneIsX, Is.False,
                    $"{cue.Room} 문이 x 평면으로 옮겨갔다 — 이 검사의 자유축이 x 라는 전제가 깨진다.");

                Assert.That(
                    cue.MaxX <= door.Center - half + Tolerance ||
                    cue.MinX >= door.Center + half - Tolerance, Is.True,
                    $"{cue.Name} 이 {cue.Room} 문 구멍 정면" +
                    $"([{door.Center - half:0.##}, {door.Center + half:0.##}])에 걸친다 — " +
                    "광장에서 읽히면 게이지가 없어도 세 번째 게이지가 된다(§4).");
            }
        }

        [Test]
        public void StateCuesStayInsideTheirOwnRoom()
        {
            // 방 밖으로 새면 서리가 전력실에서, 그을음이 냉각실에서 보인다 — 상태가 엉뚱한
            // 방의 정보로 읽히는 것이 쐐기 위반보다 나쁘다.
            //
            // <b>z 도 같이 본다.</b> 일자 스파인에서는 방 넷이 전폭을 다 써서 z 를 볼 것이
            // 없었는데, 전력실·냉각실이 z 로 갈라지면서 z 가 방을 가르는 축이 됐다.
            foreach (var cue in LastShiftDressing.StateCues)
            {
                Assert.That(cue.MinX, Is.GreaterThanOrEqualTo(LastShiftShipDimensions.RoomMinX(cue.Room)),
                    $"{cue.Name} 이 {cue.Room} 선수 쪽 벽을 넘는다.");
                Assert.That(cue.MaxX, Is.LessThanOrEqualTo(LastShiftShipDimensions.RoomMaxX(cue.Room)),
                    $"{cue.Name} 이 {cue.Room} 선미 쪽 벽을 넘는다.");
                // <b>z 는 중심만 본다</b> — 검증기(<c>R1_Bounds</c>)와 같은 규약이다. 문턱에
                // 걸치는 갑판 데칼과 벽에 박히는 판은 상자가 경계를 넘는 것이 정상이라,
                // 상자 전체를 재면 정상 단서가 전부 걸린다.
                Assert.That(cue.Center.z, Is.GreaterThanOrEqualTo(LastShiftShipDimensions.RoomMinZ(cue.Room) - Tolerance),
                    $"{cue.Name} 중심이 {cue.Room} 좌현 벽 밖이다.");
                Assert.That(cue.Center.z, Is.LessThanOrEqualTo(LastShiftShipDimensions.RoomMaxZ(cue.Room) + Tolerance),
                    $"{cue.Name} 중심이 {cue.Room} 우현 벽 밖이다.");
            }
        }

        [Test]
        public void StateCuesStayInsideTheHull()
        {
            foreach (var cue in LastShiftDressing.StateCues)
            {
                Assert.That(LastShiftHullShell.InscribedContains(cue.MinX, cue.MinZ), Is.True,
                    $"{cue.Name} 의 선수·좌현 모서리가 원반 밖이다.");
                Assert.That(LastShiftHullShell.InscribedContains(cue.MaxX, cue.MaxZ), Is.True,
                    $"{cue.Name} 의 선미·우현 모서리가 원반 밖이다.");
                Assert.That(cue.CenterY - cue.Size.y * 0.5f, Is.GreaterThanOrEqualTo(-Tolerance),
                    $"{cue.Name} 이 갑판 아래로 내려간다.");
                Assert.That(cue.CenterY + cue.Size.y * 0.5f,
                    Is.LessThanOrEqualTo(LastShiftShipDimensions.CeilingInnerHeight + Tolerance),
                    $"{cue.Name} 이 천장을 뚫는다.");
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
        public void NothingIsRedAnyMoreBecauseTheEscapePodIsGone()
        {
            // 예전에는 구명정 하나만 적색이었고, 배 어디에도 그 색이 없다는 것이 "복도 끝의
            // 적색 = 마지막 수단" 을 성립시켰다. 구명정이 제거되면서(맵 개편 §6.2-6) 그 색을
            // 쓰는 방이 <c>0</c> 이 됐다.
            //
            // <b>적색을 비워 두는 것이 이 검사의 요지다.</b> 그 강조는 에어록이 물려받기를
            // 권고했고(outboard-outpost-and-map-final-v1.md §7-7), 그때까지 아무 방에도 적색이
            // 새로 붙으면 안 된다 — 붙는 순간 "마지막 수단" 이라는 뜻이 조용히 재배정된다.
            bool IsRed(Color c) => c.r > 0.6f && c.r - Mathf.Max(c.g, c.b) > 0.25f;

            var reds = AllCompartments.Where(c => IsRed(LastShiftDressing.TintOf(c))).ToArray();
            Assert.That(reds, Is.Empty,
                "적색을 쓰는 방이 생겼다 — 이 색은 에어록이 물려받을 때까지 비워 둔다.");
            Assert.That(IsRed(LastShiftDressing.ModuleTint), Is.False,
                "자유 배치 모듈 띠가 적색 대역이다 — 항해 중에 붙인 방이 비상 경로로 읽힌다.");
            foreach (var zone in AllZones)
                Assert.That(IsRed(LastShiftDressing.TintOf(zone)), Is.False,
                    $"구역 {zone} 색이 적색 대역에 들어왔다.");
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
