using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 원반 외피(§26·§27.2)와 상부 회랑(§25.4(B)·§27.4)의 기하 조건을 고정한다.
    /// §27.7-2("상부 회랑 정밀 곡선·분기문 좌표, 겹침 재검증 — <c>game-tech-director</c>")가
    /// 이 파일이 답하는 자리다.
    ///
    /// 씬이 아니라 좌표표를 검사하는 이유는 <see cref="LastShiftCompartmentLayoutTests"/> 와
    /// 같다 — 회랑 좌표는 구획 표에서, 구획 표는 선체 전장에서 파생하므로 전장이 움직이면
    /// 전부 따라 움직인다. 그때 무엇이 깨졌는지는 씬을 다시 굽기 전에 알아야 한다.
    /// </summary>
    public sealed class LastShiftDiscHullTests
    {
        private const float Tolerance = 0.0001f;

        // ── 외피 타원 ────────────────────────────────────────────────────────

        [Test]
        public void ShellMatchesTheApprovedEllipse()
        {
            // §27.1 이 사용자 승인 완료로 확정한 값이다. 여기가 흔들리면 그 승인이 무효다.
            Assert.That(LastShiftHullShell.SemiMajorX, Is.EqualTo(42f).Within(Tolerance));
            Assert.That(LastShiftHullShell.SemiMinorZ, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(LastShiftHullShell.OverallLength, Is.EqualTo(84f).Within(Tolerance));
            Assert.That(LastShiftHullShell.OverallWidth, Is.EqualTo(40f).Within(Tolerance));

            // §26.4 가 정원을 기각했다. 종횡비가 1 로 수렴하면 그 결정이 조용히 뒤집힌 것이다.
            Assert.That(LastShiftHullShell.AspectRatio, Is.EqualTo(2.1f).Within(0.001f));
        }

        [Test]
        public void EveryCompartmentFitsInsideTheShell()
        {
            foreach (var spec in LastShiftCompartments.Specs)
            {
                Assert.That(LastShiftHullShell.ContainsFootprint(spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ),
                    Is.True, $"{spec.Compartment} 의 모서리가 타원 밖이다 — 방이 껍질을 뚫는다.");

                // 이상적인 타원만으로는 부족하다. 씬에 서는 것은 내접 다각형이라 경계에
                // 아슬아슬하게 붙은 발자국은 이상 검사를 통과하고도 실제 판에 잘린다.
                Assert.That(LastShiftHullShell.InscribedContainsFootprint(
                        spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ), Is.True,
                    $"{spec.Compartment} 가 내접 다각형 밖이다 — 타원은 통과해도 판이 파고든다.");
            }
        }

        [Test]
        public void TheHangarIsTheTightestCornerAndStillHasMargin()
        {
            // §27.2 가 타원 치수를 이 한 점에서 뽑았다. 다른 구획이 더 빡빡해지면 그 산출
            // 근거가 낡은 것이고, 타원을 다시 잡아야 하는지 판단해야 한다.
            var worst = LastShiftCompartments.Specs
                .OrderBy(spec => LastShiftHullShell.FootprintMargin(spec))
                .First();
            Assert.That(worst.Compartment, Is.EqualTo(LastShiftCompartment.Hangar),
                "§27.2 는 격납고 모서리(-27, +14)를 최빡빡 지점으로 잡고 타원을 산출했다.");

            // §27.2 실측 0.903 → 여유 약 10%.
            Assert.That(LastShiftHullShell.FootprintMargin(worst), Is.EqualTo(0.097f).Within(0.005f));
        }

        [Test]
        public void ChordApproximationReadsAsACurve()
        {
            // 판 하나의 새그가 판 두께보다 크면 테두리가 곡선이 아니라 다각형으로 보인다.
            Assert.That(LastShiftHullShell.MaxChordSag,
                Is.LessThan(LastShiftHullShell.PanelThickness),
                $"세그먼트 {LastShiftHullShell.SegmentCount} 장으로는 테두리가 각져 보인다.");
        }

        [Test]
        public void ShellDoesNotTouchThePressureZones()
        {
            // §26.5: 원반은 껍질이고 내부 4구역을 안 바꾼다. CT-09(38m x 6.0m)도 그대로다.
            Assert.That(LastShiftShipDimensions.InteriorLength, Is.EqualTo(38f).Within(Tolerance));
            Assert.That(LastShiftShipDimensions.InteriorWidth, Is.EqualTo(6f).Within(Tolerance));
            Assert.That(LastShiftZoneAtlas.ZoneCount, Is.EqualTo(4));

            // §27.5: 스파인 1회 꺾기는 채택하지 않았다. 채택하면 Resolve() 가 x 하나로
            // 못 정하게 되고 경계도 셋이 아니게 된다 — 그 결정이 코드에 들어왔는지는
            // 경계 수와 선체 폭이 먼저 말한다.
            Assert.That(LastShiftZoneAtlas.BoundaryCount, Is.EqualTo(3));
        }

        // ── 상부 회랑 ────────────────────────────────────────────────────────

        [Test]
        public void GalleryMatchesTheApprovedRoute()
        {
            Assert.That(LastShiftUpperGallery.Width, Is.EqualTo(2f).Within(Tolerance), "§27.4 폭 2m");
            Assert.That(LastShiftUpperGallery.NearZ, Is.EqualTo(10f).Within(Tolerance), "§27.4 z +10");
            Assert.That(LastShiftUpperGallery.FarZ, Is.EqualTo(12f).Within(Tolerance), "§27.4 z +12");

            // §27.4 도해의 분기 x 셋. 표는 -15 / +13 / +23.5 라고 적었고, 셋 다 그 구획의
            // 중심이라 리터럴 없이 파생해도 같은 값이 나온다 — 그 등식이 여기서 고정된다.
            AssertBranchDoorX(LastShiftCompartment.ServerRoom, -15f);
            AssertBranchDoorX(LastShiftCompartment.Hydroponics, 13f);
            AssertBranchDoorX(LastShiftCompartment.MedBay, 23.5f);

            // 양 끝. 격납고 분기문은 §27.4 의 x=-19 이고, 강하는 구명정 우현 z=+2 에 붙는다.
            var hangarBranch = BranchOf(LastShiftCompartment.Hangar);
            Assert.That(hangarBranch.DoorPlane, Is.EqualTo(LastShiftDoorPlane.AlongX));
            Assert.That(hangarBranch.DoorPlaneCoordinate,
                Is.EqualTo(-LastShiftShipDimensions.HalfLength).Within(Tolerance), "§27.4 분기문 x=-19");
            Assert.That(LastShiftUpperGallery.DescentEndZ, Is.EqualTo(2f).Within(Tolerance), "§27.4 강하 도착 z=+2");
        }

        [Test]
        public void GalleryHasFiveBranchesAndThreeOfThemAreSideDoors()
        {
            Assert.That(LastShiftUpperGallery.Branches.Length, Is.EqualTo(LastShiftUpperGallery.BranchCount));
            Assert.That(LastShiftUpperGallery.Legs.Length, Is.EqualTo(LastShiftUpperGallery.LegCount));

            // §27.4 "분기 3곳". 종점 둘(격납고·구명정)은 분기가 아니라 회랑의 양 끝이다.
            var sideDoors = LastShiftUpperGallery.Branches
                .Where(branch => branch.Compartment != LastShiftCompartment.Hangar &&
                                 branch.Compartment != LastShiftCompartment.EscapePod)
                .Select(branch => branch.Compartment)
                .ToArray();
            Assert.That(sideDoors, Is.EquivalentTo(new[]
            {
                LastShiftCompartment.ServerRoom,
                LastShiftCompartment.Hydroponics,
                LastShiftCompartment.MedBay
            }));
        }

        [Test]
        public void NoGalleryLegOverlapsACompartmentOrTheHullInterior()
        {
            // §27.7-2 의 "겹침 재검증". §21.1 이 구획 55 쌍에 대해 한 것과 같은 검사를
            // 회랑 다리 다섯에 대해 한다 — 겹치면 씬에서 두 공간이 한 벽을 공유한 것처럼
            // 보이다가 승무원이 벽을 통과한다.
            foreach (var leg in LastShiftUpperGallery.Legs)
            {
                Assert.That(LastShiftUpperGallery.LegOverlapsHullInterior(leg), Is.False,
                    $"회랑 {leg.Name} 이 선체 내부를 침범한다.");

                foreach (var spec in LastShiftCompartments.Specs)
                    Assert.That(LastShiftUpperGallery.LegOverlapsCompartment(leg, spec), Is.False,
                        $"회랑 {leg.Name} 이 {spec.Compartment} 와 겹친다.");
            }
        }

        [Test]
        public void TheDiagonalDescentWouldHaveOverlapped()
        {
            // §27.4 도해가 적은 사선(x +25~+29 에서 z 를 +10 → +2 로 축소)이 실제로 무엇을
            // 뚫는지를 수치로 남긴다. 이 검사가 FAIL 하면 사선을 기각한 근거가 사라진 것이므로
            // 축 정렬 강하(LastShiftUpperGallery.DescentCenterX 주석)를 다시 봐야 한다.
            var medBay = LastShiftCompartments.Of(LastShiftCompartment.MedBay);
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            const float rampMinX = 25f;
            const float rampMaxX = 29f;
            const float rampStartZ = 10f;
            const float rampEndZ = 2f;
            var half = LastShiftUpperGallery.Width * 0.5f;

            Assert.That(CentreZAt(medBay.MaxX, rampMinX, rampMaxX, rampStartZ, rampEndZ) - half,
                Is.LessThan(medBay.MaxZ),
                "사선이 의무실 바깥 모서리를 안 파고든다면 축 정렬로 바꾼 근거가 약해진다.");
            Assert.That(CentreZAt(rampMaxX, rampMinX, rampMaxX, rampStartZ, rampEndZ) - half,
                Is.LessThan(lounge.MaxZ),
                "사선 끝이 휴게실 z 범위 밖이면 사선을 그대로 써도 됐다는 뜻이다.");
            Assert.That(lounge.MaxX, Is.EqualTo(rampMaxX).Within(Tolerance),
                "사선이 z=+2 에 닿는 자리가 구명정이 아니라 휴게실이라는 것이 기각 사유다.");
        }

        [Test]
        public void EveryGalleryDoorSitsOnItsCompartmentFaceAndInsideItsLeg()
        {
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            foreach (var branch in LastShiftUpperGallery.Branches)
            {
                var spec = LastShiftCompartments.Of(branch.Compartment);
                var leg = LastShiftUpperGallery.Legs[branch.LegIndex];

                // 문 평면이 그 구획의 경계면 위인가.
                var (faceMin, faceMax) = branch.DoorPlane == LastShiftDoorPlane.AlongX
                    ? (spec.MinX, spec.MaxX)
                    : (spec.MinZ, spec.MaxZ);
                Assert.That(
                    Mathf.Abs(branch.DoorPlaneCoordinate - faceMin) < Tolerance ||
                    Mathf.Abs(branch.DoorPlaneCoordinate - faceMax) < Tolerance, Is.True,
                    $"{branch.Compartment} 의 회랑 문이 자기 경계면 위에 없다.");

                // 구멍이 구획 면 안에 다 들어가는가.
                var (freeMin, freeMax) = branch.DoorPlane == LastShiftDoorPlane.AlongX
                    ? (spec.MinZ, spec.MaxZ)
                    : (spec.MinX, spec.MaxX);
                Assert.That(branch.DoorCenter - half, Is.GreaterThanOrEqualTo(freeMin - Tolerance),
                    $"{branch.Compartment} 의 회랑 문이 구획 면 밖으로 넘친다.");
                Assert.That(branch.DoorCenter + half, Is.LessThanOrEqualTo(freeMax + Tolerance),
                    $"{branch.Compartment} 의 회랑 문이 구획 면 밖으로 넘친다.");

                // 그리고 회랑 쪽 단면 안에도 들어가야 한다. 넘치면 문 옆이 회랑 벽이라
                // 씬에서는 문틀 절반이 벽에 먹힌다.
                var (legMin, legMax) = branch.DoorPlane == LastShiftDoorPlane.AlongX
                    ? (leg.MinZ, leg.MaxZ)
                    : (leg.MinX, leg.MaxX);
                Assert.That(branch.DoorCenter - half, Is.GreaterThanOrEqualTo(legMin - Tolerance),
                    $"{branch.Compartment} 의 회랑 문이 회랑 단면 밖으로 넘친다.");
                Assert.That(branch.DoorCenter + half, Is.LessThanOrEqualTo(legMax + Tolerance),
                    $"{branch.Compartment} 의 회랑 문이 회랑 단면 밖으로 넘친다.");

                // 다리가 그 면에서 시작하는가. 안 닿으면 문과 회랑 사이에 솔리드가 남는다.
                var (legNear, legFar) = branch.DoorPlane == LastShiftDoorPlane.AlongX
                    ? (leg.MinX, leg.MaxX)
                    : (leg.MinZ, leg.MaxZ);
                Assert.That(
                    Mathf.Abs(branch.DoorPlaneCoordinate - legNear) < Tolerance ||
                    Mathf.Abs(branch.DoorPlaneCoordinate - legFar) < Tolerance, Is.True,
                    $"{branch.Compartment} 의 회랑 다리가 문 평면에서 시작하지 않는다.");
            }
        }

        [Test]
        public void GalleryStaysInsideTheShell()
        {
            foreach (var leg in LastShiftUpperGallery.Legs)
                Assert.That(LastShiftHullShell.InscribedContainsFootprint(
                        leg.MinX, leg.MaxX, leg.MinZ, leg.MaxZ), Is.True,
                    $"회랑 {leg.Name} 이 원반 테두리 밖으로 나간다.");
        }

        [Test]
        public void GalleryIsNotAShortcut()
        {
            // 회랑이 스파인보다 짧으면 배의 주 동선이 방이 아니라 회랑이 된다 —
            // §25.4(B) 가 원한 것은 이면 동선이지 지름길이 아니다.
            var spine = LastShiftUpperGallery.RunMaxX - LastShiftUpperGallery.RunMinX;
            Assert.That(LastShiftUpperGallery.TravelDistance, Is.GreaterThan(spine),
                "회랑이 같은 두 끝을 잇는 직선보다 짧거나 같다 — 강하 구간이 사라졌다.");
        }

        [Test]
        public void LockedCompartmentsDoNotOpenTheRingYet()
        {
            // §15.2 언락 대상은 그레이박스에서 구멍이 아니라 메운 판이다. 회랑도 같은
            // 규칙을 따라야 언락 하나로 방과 고리가 같이 열린다 — 여기가 어긋나면
            // 잠긴 방인데 회랑 쪽 문만 뚫려 뒷문으로 들어가진다.
            foreach (var branch in LastShiftUpperGallery.Branches)
                Assert.That(LastShiftUpperGallery.IsPassable(branch),
                    Is.EqualTo(LastShiftCompartments.Of(branch.Compartment).IsPassable),
                    $"{branch.Compartment} 의 회랑 문 통행 여부가 구획 언락 상태와 다르다.");

            // 지금 실제로 뚫리는 것은 구명정 하나뿐이다(공간은 열려 있고 기능만 잠긴다, §15.4).
            var open = LastShiftUpperGallery.Branches
                .Where(branch => LastShiftUpperGallery.IsPassable(branch))
                .Select(branch => branch.Compartment)
                .ToArray();
            Assert.That(open, Is.EquivalentTo(new[] { LastShiftCompartment.EscapePod }));
        }

        // ── 자투리 구조체(격벽 프레임) ───────────────────────────────────────

        [Test]
        public void FramesActuallyFillTheGap()
        {
            // §27.3 이 요구한 "자투리를 비-게임플레이 구조체로 채운다". 하나도 안 서면
            // 여유(Clearance)나 최소 길이가 너무 커서 조건이 조용히 전부 걸러진 것이다.
            Assert.That(LastShiftHullFrames.BuildableRibCount, Is.GreaterThanOrEqualTo(8),
                "격벽 프레임이 거의 안 선다 — 자투리가 안 채워진 채로 통과한다.");
            Assert.That(LastShiftHullFrames.BuildableRingSegmentCount, Is.GreaterThanOrEqualTo(8),
                "거들 링이 거의 안 선다.");

            // 전부 서는 것도 이상하다. 방이 외피에 가까운 각(격납고 어깨)과 창 앞(좌현)에서는
            // 안 서는 것이 정상이라, 24/24 면 걸러내는 조건 자체가 안 도는 것이다.
            Assert.That(LastShiftHullFrames.BuildableRibCount,
                Is.LessThan(LastShiftHullFrames.RibCount),
                "모든 각에 프레임이 선다 — 방·창 회피가 안 돌고 있다.");
        }

        [Test]
        public void NoFrameTouchesARoomACorridorOrTheHull()
        {
            for (var rib = 0; rib < LastShiftHullFrames.RibCount; rib++)
            {
                if (!LastShiftHullFrames.RibIsBuildable(rib)) continue;
                AssertMemberIsFree($"Rib_{rib:00}",
                    LastShiftHullFrames.RibInner(rib), LastShiftHullFrames.RibOuter(rib));
            }

            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
            {
                if (!LastShiftHullFrames.RingSegmentIsBuildable(segment)) continue;
                AssertMemberIsFree($"Girth_{segment:00}",
                    LastShiftHullFrames.RingSegmentStart(segment),
                    LastShiftHullFrames.RingSegmentStart((segment + 1) % LastShiftHullShell.SegmentCount));
            }
        }

        [Test]
        public void NoFrameStandsInFrontOfThePortWindows()
        {
            // 이 배의 창 너머는 진짜 우주가 아니라 z=-9.1 의 배경막이다. 그 앞에 부재를
            // 세우면 창에서 회색 보가 우주에 떠 있는 것으로 보인다 — 원반 헐에서 창을
            // 어떻게 낼지는 §27.7-4 가 art 로 남긴 미결이라, 그 전에 형상을 못 박지 않는다.
            Assert.That(LastShiftHullFrames.WindowBackdropZ, Is.EqualTo(-9.1f).Within(0.001f),
                "배경막 z 가 씬 빌더의 SpaceVoid 와 어긋났다 — 두 값이 갈리면 회피가 헛돈다.");

            for (var rib = 0; rib < LastShiftHullFrames.RibCount; rib++)
            {
                if (!LastShiftHullFrames.RibIsBuildable(rib)) continue;
                foreach (var point in Samples(LastShiftHullFrames.RibInner(rib), LastShiftHullFrames.RibOuter(rib)))
                    Assert.That(LastShiftHullFrames.IsWindowKeepOut(point.x, point.y), Is.False,
                        $"Rib_{rib:00} 이 좌현 창 앞을 지난다.");
            }
        }

        private static void AssertMemberIsFree(string name, Vector2 from, Vector2 to)
        {
            foreach (var point in Samples(from, to))
                Assert.That(LastShiftHullFrames.IsFree(point.x, point.y), Is.True,
                    $"{name} 이 ({point.x:0.##}, {point.y:0.##}) 에서 방·회랑·선체와 겹친다.");
        }

        /// <summary>부재 위를 촘촘히 훑는다. 양 끝만 보면 방을 가로지르는 부재가 통과한다.</summary>
        private static Vector2[] Samples(Vector2 from, Vector2 to)
        {
            var count = Mathf.Max(8, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.25f));
            var result = new Vector2[count + 1];
            for (var index = 0; index <= count; index++)
                result[index] = Vector2.Lerp(from, to, (float)index / count);
            return result;
        }

        private static float CentreZAt(float x, float minX, float maxX, float startZ, float endZ) =>
            Mathf.Lerp(startZ, endZ, (x - minX) / (maxX - minX));

        private static LastShiftGalleryBranch BranchOf(LastShiftCompartment compartment) =>
            LastShiftUpperGallery.Branches.First(branch => branch.Compartment == compartment);

        private static void AssertBranchDoorX(LastShiftCompartment compartment, float expected)
        {
            var branch = BranchOf(compartment);
            Assert.That(branch.DoorPlane, Is.EqualTo(LastShiftDoorPlane.AlongZ));
            Assert.That(branch.DoorCenter, Is.EqualTo(expected).Within(Tolerance),
                $"{compartment} 분기문 x 가 §27.4 표와 다르다.");
        }
    }
}
