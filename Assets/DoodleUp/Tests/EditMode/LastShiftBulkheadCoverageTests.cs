using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 광장 벽이 문 구멍 말고는 남김없이 서 있는가.
    ///
    /// <b>이 파일은 벌크헤드·배플·개구부 다섯 체계 위에 있었다.</b> 그 체계가 §3.4 에서
    /// 폐지되면서 재던 것들(통로 안 두 개구부의 <c>GAP_Z</c>, 배플 위치 <c>t</c>, 통행 차선
    /// 폭)이 전부 없어졌다. 남은 질문은 하나이고 그것이 원래 이 파일의 이름이 뜻하던 것이다 —
    /// <b>문 옆으로 걸어서 지나갈 틈이 있는가.</b>
    ///
    /// 틈이 있으면 격리가 "압력만 끊고 사람은 안 막는" 반쪽이 된다. 그레이박스에서 그 틈은
    /// 좌표 실수 하나로 생기고, 눈으로는 벽이 이어져 보인다.
    /// </summary>
    public sealed class LastShiftBulkheadCoverageTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>광장 네 변. 문이 얹히는 평면 넷이고, 벽 빌더가 이 순서로 판을 세운다.</summary>
        private static readonly (bool PlaneIsX, float Plane)[] Sides =
        {
            (true, LastShiftPlazaLayout.PlazaMinX),
            (true, LastShiftPlazaLayout.PlazaMaxX),
            (false, LastShiftPlazaLayout.PlazaMinZ),
            (false, LastShiftPlazaLayout.PlazaMaxZ)
        };

        [Test]
        public void EveryDoorSitsOnAPlazaSide()
        {
            // §2.3 의 "경유 방이 없다" 가 좌표에서 뜻하는 것이 이것이다 — 문 평면이 광장 변과
            // 같은 값이어야 그 방이 광장에 <b>직결</b>이다. 하나라도 어긋나면 그 방은 광장에서
            // 한 칸 떨어져 있고, 사슬 깊이가 1 이라는 §6.1 의 전제가 거기서 깨진다.
            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                var onSide = Sides.Any(side =>
                    side.PlaneIsX == door.PlaneIsX && Mathf.Abs(side.Plane - door.Plane) < Tolerance);
                Assert.That(onSide, Is.True, $"{door.Space} 문이 광장 변 위에 없다 — 경유 방이 생겼다.");
            }
        }

        [Test]
        public void DoorsOnTheSameSideDoNotOverlap()
        {
            // 좌현·우현 변은 문을 둘씩 문다(압력문 + 부속 생활문). 두 구멍이 겹치면 벽 빌더가
            // 자르는 구간 목록이 뒤엉켜 판이 겹치거나 빠지는데, 씬에서는 z-파이팅으로만 보인다.
            foreach (var side in Sides)
            {
                var doors = DoorsOn(side);
                for (var a = 0; a < doors.Length; a++)
                for (var b = a + 1; b < doors.Length; b++)
                    Assert.That(doors[a].MaxSpan, Is.LessThanOrEqualTo(doors[b].MinSpan + Tolerance),
                        $"{doors[a].Space} 와 {doors[b].Space} 의 구멍이 같은 변에서 겹친다.");
            }
        }

        [Test]
        public void EveryDoorFitsWithinItsPlazaSide()
        {
            // 구멍이 변을 넘치면 넘친 쪽 벽 조각의 길이가 음수가 되고, 그건 씬에서 안쪽이
            // 뒤집힌 판으로 선다.
            foreach (var side in Sides)
            {
                var lo = side.PlaneIsX ? LastShiftPlazaLayout.PlazaMinZ : LastShiftPlazaLayout.PlazaMinX;
                var hi = side.PlaneIsX ? LastShiftPlazaLayout.PlazaMaxZ : LastShiftPlazaLayout.PlazaMaxX;
                foreach (var door in DoorsOn(side))
                {
                    Assert.That(door.MinSpan, Is.GreaterThanOrEqualTo(lo - Tolerance),
                        $"{door.Space} 구멍이 광장 변 아래로 넘친다.");
                    Assert.That(door.MaxSpan, Is.LessThanOrEqualTo(hi + Tolerance),
                        $"{door.Space} 구멍이 광장 변 위로 넘친다.");
                }
            }
        }

        [Test]
        public void WallSegmentsCoverEverythingButTheOpenings()
        {
            // 벽 빌더와 같은 계산을 여기서 다시 한 번 돌린다. 씬 오브젝트를 세지 않는 이유는
            // 이 검사가 <b>좌표가 성립하는가</b>를 묻기 때문이고, 실제로 선 판을 세는 것은
            // LastShiftSceneVerifier 가 씬에서 한다 — 둘 다 있어야 "표는 맞는데 안 세웠다" 와
            // "세웠는데 표가 틀렸다" 가 갈린다.
            foreach (var side in Sides)
            {
                var lo = side.PlaneIsX ? LastShiftPlazaLayout.PlazaMinZ : LastShiftPlazaLayout.PlazaMinX;
                var hi = side.PlaneIsX ? LastShiftPlazaLayout.PlazaMaxZ : LastShiftPlazaLayout.PlazaMaxX;

                var covered = 0f;
                var cursor = lo;
                foreach (var door in DoorsOn(side))
                {
                    covered += Mathf.Max(0f, door.MinSpan - cursor);
                    cursor = door.MaxSpan;
                }
                covered += Mathf.Max(0f, hi - cursor);

                var openings = DoorsOn(side).Length * LastShiftZoneDoor.OpeningWidth;
                Assert.That(covered, Is.EqualTo(hi - lo - openings).Within(Tolerance),
                    $"광장 변(평면 {side.Plane:F1})에서 벽이 덮는 길이가 구멍을 뺀 값과 다르다.");
            }
        }

        [Test]
        public void PressureDoorsAreExactlyTheZoneBoundaries()
        {
            // 압력문 셋이 곧 경계 셋이다(조항 S-1). 이 등식이 깨지면 벽에는 문이 있는데
            // 압력은 안 끊기거나 그 반대가 된다.
            var pressureDoors = LastShiftPlazaLayout.Doors
                .Count(door => door.Kind == LastShiftPlazaDoorKind.PressureDoor);
            Assert.That(pressureDoors, Is.EqualTo(LastShiftZoneAtlas.BoundaryCount));

            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var door = LastShiftZoneAtlas.BoundaryDoor(boundary);
                Assert.That(door.Kind, Is.EqualTo(LastShiftPlazaDoorKind.PressureDoor));
                Assert.That(LastShiftZoneAtlas.LowZoneOf(boundary), Is.EqualTo(LastShiftZone.Cockpit),
                    "경계 이쪽이 조종석 구역이 아니다 — 광장이 모든 압력문을 물고 있다는 전제가 깨졌다.");
                Assert.That(LastShiftPlazaLayout.Of(door.Space).Zone,
                    Is.EqualTo(LastShiftZoneAtlas.HighZoneOf(boundary)));
            }
        }

        [Test]
        public void OnlyTheCockpitOpeningHasNoDoorPanel()
        {
            // 문짝 없는 개구부가 하나뿐이어야 한다. 둘이 되면 그 두 구역이 정의상 하나가 되고,
            // 압력 구역 넷이 셋으로 조용히 줄어든다.
            var openings = LastShiftPlazaLayout.Doors
                .Where(door => door.Kind == LastShiftPlazaDoorKind.Opening)
                .ToArray();
            Assert.That(openings.Length, Is.EqualTo(1));
            Assert.That(openings[0].Space, Is.EqualTo(LastShiftPlazaSpace.CockpitRoom));
            Assert.That(LastShiftPlazaLayout.Of(openings[0].Space).Zone, Is.EqualTo(LastShiftZone.Cockpit));
        }

        private static LastShiftPlazaDoor[] DoorsOn((bool PlaneIsX, float Plane) side) =>
            LastShiftPlazaLayout.Doors
                .Where(door => door.PlaneIsX == side.PlaneIsX && Mathf.Abs(door.Plane - side.Plane) < Tolerance)
                .OrderBy(door => door.Center)
                .ToArray();
    }
}
