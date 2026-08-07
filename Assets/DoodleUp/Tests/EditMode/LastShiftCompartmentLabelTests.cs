using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 구획 이름표가 문 위에 안 걸치는가. 아트 정본
    /// <c>docs/art/last-shift-bow-chain-dressing-v1.md</c> §7-5 가 씬 빌더 몫으로 넘긴 자리다.
    ///
    /// 실제로 걸린 것은 화물칸 하나가 아니었다 — 라벨이 붙는 벽(<c>MinZ</c>)은 여러 구획에서
    /// 동시에 문이 뚫리는 벽이고, 그 문 <c>x</c> 가 방 중심과 같은 구획이 다섯이다. 좌표는
    /// 전부 선체 전장에서 파생하므로 전장이 움직이면 어느 방이 걸리는지도 바뀐다. 그래서
    /// 번호를 박지 않고 <b>열한 개 전부</b>를 같은 규칙으로 잰다.
    /// </summary>
    public sealed class LastShiftCompartmentLabelTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>
        /// 이 카드가 답해야 하는 원래 증상 — 화물칸 라벨이 관측 회랑 문 인방을 가로지른다.
        /// 전제(방 중심 = 문 중심)까지 같이 못박는다. 이 전제가 깨지면 이 검사는 아무것도
        /// 확인하지 않으면서 통과한다.
        /// </summary>
        [Test]
        public void TheCargoBayLabelIsOffTheObservationGalleryDoor()
        {
            var spec = LastShiftCompartments.Of(LastShiftCompartment.CargoBay);

            Assert.That(LastShiftObservationGallery.CargoLandingCenterX,
                Is.EqualTo(spec.CenterX).Within(Tolerance),
                "관측 회랑 문이 더는 화물칸 중심에 없다 — 이 검사가 재는 증상이 사라졌다.");

            var x = LastShiftCompartmentLabels.ResolveX(spec);
            var half = LastShiftCompartmentLabels.HalfWidthOf(LastShiftCompartment.CargoBay);
            var gap = Mathf.Abs(x - LastShiftObservationGallery.CargoLandingCenterX) - half;

            Assert.That(gap, Is.GreaterThanOrEqualTo(LastShiftZoneDoor.OpeningWidth * 0.5f),
                "화물칸 라벨이 여전히 관측 회랑 문 폭 안에 있다.");
            Assert.That(LastShiftCompartmentLabels.ResolveY(spec),
                Is.EqualTo(LastShiftCompartmentLabels.WallLabelY).Within(Tolerance),
                "화물칸은 비켜 놓을 벽이 넉넉한데 라벨이 인방 위로 올라갔다.");
        }

        /// <summary>
        /// 열한 개 전부 — 글자가 문 구멍을 가로지르지 않는다. 좁아서 옆으로 못 비키는 방은
        /// 문 <b>위</b>로 올라가는 것이 허용이고, 그때는 글자 아랫단이 문 구멍 윗단보다
        /// 높아야 한다. 둘 다 아니면 씬에서 글자가 문틀에 잘린다.
        /// </summary>
        [Test]
        public void NoLabelCrossesADoorway()
        {
            foreach (var spec in LastShiftCompartments.Specs)
            {
                var doorways = LastShiftCompartmentLabels.DoorwaysOnLabelWall(spec);
                if (doorways.Length == 0) continue;

                var x = LastShiftCompartmentLabels.ResolveX(spec);
                var y = LastShiftCompartmentLabels.ResolveY(spec);
                var half = LastShiftCompartmentLabels.HalfWidthOf(spec.Compartment);
                var bottom = y - LastShiftCompartmentLabels.LineHeight * 0.5f;

                foreach (var doorway in doorways)
                {
                    var clearInX = Mathf.Abs(x - doorway) >= half + LastShiftZoneDoor.OpeningWidth * 0.5f;
                    var aboveTheHead = bottom >= LastShiftZoneDoor.OpeningHeight;
                    Assert.That(clearInX || aboveTheHead, Is.True,
                        $"{spec.Compartment} 라벨이 x={doorway:0.##} 문에 걸린다 " +
                        $"(라벨 x={x:0.##} 반폭={half:0.##} 아랫단={bottom:0.##}).");
                }
            }
        }

        /// <summary>
        /// 라벨이 붙는 벽 안에 있는가. 비키다가 방 밖으로 나가면 글자가 벽 없는 허공에 뜬다 —
        /// 이 규칙이 없으면 "가장 넓은 구간" 이 방 모서리를 넘어도 통과한다.
        /// </summary>
        [Test]
        public void EveryLabelStaysOnItsOwnWall()
        {
            foreach (var spec in LastShiftCompartments.Specs)
            {
                var x = LastShiftCompartmentLabels.ResolveX(spec);
                var half = LastShiftCompartmentLabels.HalfWidthOf(spec.Compartment);

                Assert.That(x - half, Is.GreaterThanOrEqualTo(spec.MinX - Tolerance),
                    $"{spec.Compartment} 라벨 왼쪽 끝이 방 밖이다.");
                Assert.That(x + half, Is.LessThanOrEqualTo(spec.MaxX + Tolerance),
                    $"{spec.Compartment} 라벨 오른쪽 끝이 방 밖이다.");

                var y = LastShiftCompartmentLabels.ResolveY(spec);
                Assert.That(y + LastShiftCompartmentLabels.LineHeight * 0.5f,
                    Is.LessThanOrEqualTo(LastShiftCompartments.InteriorHeight + Tolerance),
                    $"{spec.Compartment} 라벨 윗단이 천장을 넘는다.");
            }
        }

        /// <summary>
        /// <b>겹칠 때만 움직인다.</b> 라벨 벽에 문이 없는 구획은 예전 좌표 그대로여야 한다 —
        /// 안 그러면 이 카드가 안 건드려도 되는 방 여섯의 프리팹까지 매번 diff 를 낸다.
        /// </summary>
        [Test]
        public void LabelsWithNoDoorOnTheirWallDoNotMove()
        {
            var moved = 0;
            foreach (var spec in LastShiftCompartments.Specs)
            {
                if (LastShiftCompartmentLabels.DoorwaysOnLabelWall(spec).Length == 0)
                {
                    Assert.That(LastShiftCompartmentLabels.ResolveX(spec),
                        Is.EqualTo(spec.CenterX).Within(Tolerance),
                        $"{spec.Compartment} 벽에 문이 없는데 라벨이 옮겨졌다.");
                    Assert.That(LastShiftCompartmentLabels.ResolveY(spec),
                        Is.EqualTo(LastShiftCompartmentLabels.WallLabelY).Within(Tolerance),
                        $"{spec.Compartment} 벽에 문이 없는데 라벨이 올라갔다.");
                    continue;
                }

                if (Mathf.Abs(LastShiftCompartmentLabels.ResolveX(spec) - spec.CenterX) > Tolerance ||
                    Mathf.Abs(LastShiftCompartmentLabels.ResolveY(spec) -
                              LastShiftCompartmentLabels.WallLabelY) > Tolerance)
                    moved++;
            }

            Assert.That(moved, Is.GreaterThan(0),
                "아무 라벨도 안 움직였다 — 회피 규칙이 통째로 안 돌고 있다.");
            Assert.That(moved, Is.LessThan(LastShiftCompartments.Count),
                "라벨 열한 개가 전부 움직였다 — '겹칠 때만' 이 안 지켜진다.");
        }
    }
}
