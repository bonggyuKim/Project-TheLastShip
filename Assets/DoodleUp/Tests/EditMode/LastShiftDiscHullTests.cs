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
        public void GalleryDoorsFollowCompartmentAccessAndTheRunIsNowAThroughRoute()
        {
            // §15.2 언락 대상은 그레이박스에서 구멍이 아니라 메운 판이다. 회랑도 같은
            // 규칙을 따라야 언락 하나로 방과 고리가 같이 열린다 — 여기가 어긋나면
            // 잠긴 방인데 회랑 쪽 문만 뚫려 뒷문으로 들어가진다.
            foreach (var branch in LastShiftUpperGallery.Branches)
                Assert.That(LastShiftUpperGallery.IsPassable(branch),
                    Is.EqualTo(LastShiftCompartments.Of(branch.Compartment).IsPassable),
                    $"{branch.Compartment} 의 회랑 문 통행 여부가 구획 언락 상태와 다르다.");

            // 뚫리는 것은 양 끝 둘이다. 격납고가 P0 상시 개방(확장 검토 §2)이 되면서
            // 회랑이 막다른 관에서 <b>격납고 ↔ 구명정 관통로</b>가 됐다. 옆구리 분기 셋은
            // §2.2 대로 여전히 메운 판이다.
            var open = LastShiftUpperGallery.Branches
                .Where(branch => LastShiftUpperGallery.IsPassable(branch))
                .Select(branch => branch.Compartment)
                .ToArray();
            Assert.That(open, Is.EquivalentTo(new[]
            {
                LastShiftCompartment.Hangar,
                LastShiftCompartment.EscapePod
            }));

            // 입구가 둘이라는 것이 이 카드가 실제로 고친 것이다 — 하나뿐이면 그 하나가
            // 구명정이라, 회랑에 가려면 §15.4 가 "최후 수단" 이라고 정의한 탈출포드를
            // 계단실로 쓰게 된다(확장 검토 §1.2).
            Assert.That(open.Length, Is.GreaterThanOrEqualTo(2),
                "상부 회랑 출입구가 하나뿐이다 — 구명정이 다시 계단실이 된다.");
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
            // 이 배의 창 너머는 진짜 우주가 아니라 배경막이다. 그 앞에 부재를 세우면
            // 창에서 회색 보가 우주에 떠 있는 것으로 보인다.
            //
            // 배경막은 <b>원반 외피 바깥</b>에 서야 한다(§28.6 art 결정 (a)). 안쪽에 두면
            // 껍질에 갇혀서 창에 우주가 아니라 테두리 판이 보인다 — 예전 -9.1 이 그 상태였다.
            Assert.That(LastShiftHullFrames.WindowBackdropZ,
                Is.LessThan(-LastShiftHullShell.SemiMinorZ),
                "배경막이 원반 단축 반지름 안쪽에 있다 — 외피에 가려 창에서 안 보인다.");
            Assert.That(LastShiftHullFrames.WindowBackdropZ, Is.EqualTo(-22f).Within(0.001f),
                "배경막 z 가 씬 빌더의 SpaceVoid 와 어긋났다 — 두 값이 갈리면 회피가 헛돈다.");

            // 별 판도 원반 밖이어야 한다. 배경막과 달리 별은 배경막 <b>앞</b>으로 흩뿌려지므로
            // 상한을 따로 건다 — 이게 없으면 좌현 테두리 유리(§29.4-(1)) 앞에 별이 뜬다.
            Assert.That(LastShiftHullFrames.WindowStarNearestZ,
                Is.LessThan(-LastShiftHullShell.SemiMinorZ),
                "별 판 상한이 원반 안쪽이다 — 테두리 창 앞에 별이 떠 있는 것으로 보인다.");
            Assert.That(LastShiftHullFrames.WindowStarNearestZ,
                Is.GreaterThan(LastShiftHullFrames.WindowBackdropZ),
                "별 판 상한이 배경막보다 뒤다 — 별이 배경막에 가려 하나도 안 보인다.");

            for (var rib = 0; rib < LastShiftHullFrames.RibCount; rib++)
            {
                if (!LastShiftHullFrames.RibIsBuildable(rib)) continue;
                foreach (var point in Samples(LastShiftHullFrames.RibInner(rib), LastShiftHullFrames.RibOuter(rib)))
                    Assert.That(LastShiftHullFrames.IsWindowKeepOut(point.x, point.y), Is.False,
                        $"Rib_{rib:00} 이 좌현 창 앞을 지난다.");
            }
        }

        /// <summary>
        /// §29.6 판정기준 1 — <b>테두리 판이 전부 선다.</b> 예전에는 좌현 창 구간
        /// <c>10</c>장을 통째로 비웠고(그래서 §29.3 이 잰 실루엣은 <c>38/48</c>), 그 자리가
        /// 원반 전장 <c>84m</c> 중 <c>50m</c> 짜리 노치였다.
        ///
        /// 이 검사가 세그먼트 번호를 안 박는 이유는 <c>SegmentCount</c> 가 바뀌면 번호가
        /// 통째로 밀리기 때문이다 — 세는 것은 "판이 서는가/창 판인가" 둘뿐이다.
        /// </summary>
        [Test]
        public void DiscRimStandsAllTheWayAround()
        {
            var bays = LastShiftHullFrames.WindowBaySegmentCount;

            Assert.That(bays, Is.GreaterThan(0),
                "창 판이 하나도 없다 — 테두리가 닫히기만 하고 창이 사라졌다.");
            Assert.That(bays, Is.LessThan(LastShiftHullShell.SegmentCount / 2),
                "창 판이 좌현 절반을 넘게 먹었다 — 테두리가 유리 띠로 읽힌다.");

            // 나머지는 전부 불투명 판이다. 둘을 더해 SegmentCount 가 되어야 "48장 전부 선다".
            var opaque = 0;
            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
                if (!LastShiftHullFrames.SegmentIsWindowBay(segment)) opaque++;

            Assert.That(opaque + bays, Is.EqualTo(LastShiftHullShell.SegmentCount),
                "테두리 판 수가 세그먼트 수와 다르다 — 실루엣에 구멍이 남았다.");
        }

        /// <summary>
        /// 창 판이 <b>끊기지 않은 호 하나</b>이고, 멀리언이 그 호의 이음매마다 선다.
        /// 창 판이 두 덩어리로 갈리면 그 사이에 불투명 판이 한 장 끼어 조종석에서 보는
        /// 별 띠가 중간에 잘린다. 멀리언은 세그먼트가 아니라 이음매라 <c>n+1</c> 개다 —
        /// 양 끝 멀리언이 유리와 불투명 판의 경계를 마감한다(아트 정본 §3.3).
        /// </summary>
        [Test]
        public void WindowBaysFormOneArcWithAMullionAtEverySeam()
        {
            var count = LastShiftHullShell.SegmentCount;
            var runs = 0;
            for (var segment = 0; segment < count; segment++)
            {
                var previous = LastShiftHullFrames.SegmentIsWindowBay((segment + count - 1) % count);
                if (LastShiftHullFrames.SegmentIsWindowBay(segment) && !previous) runs++;
            }

            Assert.That(runs, Is.EqualTo(1), "창 판이 여러 덩어리로 갈렸다 — 별 띠가 중간에 잘린다.");

            var seams = LastShiftHullFrames.WindowMullionSeams();
            Assert.That(seams.Length, Is.EqualTo(LastShiftHullFrames.WindowBaySegmentCount + 1),
                "멀리언 수가 이음매 수와 다르다 — 유리와 불투명 판의 경계가 안 마감된다.");
            Assert.That(seams.Distinct().Count(), Is.EqualTo(seams.Length),
                "같은 이음매에 멀리언이 두 번 선다.");

            foreach (var seam in seams)
            {
                var point = LastShiftHullShell.SegmentStart(seam);
                Assert.That(LastShiftHullShell.NormalizedRadiusSquared(point.x, point.y),
                    Is.EqualTo(1f).Within(0.001f),
                    $"Mullion_{seam:00} 이 테두리 타원 위가 아니다 — 이음매에서 벗어났다.");
            }
        }

        /// <summary>
        /// §29.6 판정기준 2 의 전제 — 창을 테두리로 옮겨도 <b>발자국은 안 변한다.</b>
        /// 창 판·멀리언은 테두리 위에 서고 방·회랑 발자국에 관여하지 않는다. 좌현 점유율을
        /// 실제로 올리는 것은 §29.4-(2) 관측 회랑이고, (1)은 그 숫자를 안 건드려야 한다 —
        /// 여기서 점유율이 흔들리면 (1)이 발자국을 건드린 것이고 그건 이 카드 범위 밖이다.
        /// </summary>
        [Test]
        public void MovingTheWindowsToTheRimDoesNotChangeAnyFootprint()
        {
            foreach (var segment in Enumerable.Range(0, LastShiftHullShell.SegmentCount))
            {
                if (!LastShiftHullFrames.SegmentIsWindowBay(segment)) continue;
                var start = LastShiftHullShell.SegmentStart(segment);
                var end = LastShiftHullShell.SegmentStart((segment + 1) % LastShiftHullShell.SegmentCount);
                var middle = (start + end) * 0.5f;

                foreach (var spec in LastShiftCompartments.Specs)
                    Assert.That(
                        middle.x >= spec.MinX && middle.x <= spec.MaxX &&
                        middle.y >= spec.MinZ && middle.y <= spec.MaxZ, Is.False,
                        $"창 판 {segment:00} 이 구획 {spec.Compartment} 발자국 안에 있다.");

                foreach (var leg in LastShiftUpperGallery.Legs)
                    Assert.That(
                        middle.x >= leg.MinX && middle.x <= leg.MaxX &&
                        middle.y >= leg.MinZ && middle.y <= leg.MaxZ, Is.False,
                        $"창 판 {segment:00} 이 회랑 다리 {leg.Name} 발자국 안에 있다.");
            }
        }

        // ── 좌현 관측 회랑(§29.4-(2)) ────────────────────────────────────────

        /// <summary>
        /// 경로가 §29.4-(2) 도해 그대로인가 — 조종석 좌현 ↔ 화물칸 좌현이고, 양 끝이 둘 다
        /// <b>선수 클러스터 안</b>이다. 접속점을 관측실로 옮기면 정비창·화물칸 둘을 건너뛰는
        /// 우회로가 되므로 그 선택은 여기서 걸려야 한다.
        /// </summary>
        [Test]
        public void ObservationGalleryRunsFromTheCockpitToTheCargoBay()
        {
            Assert.That(LastShiftObservationGallery.CockpitLandingCenterX,
                Is.EqualTo(LastShiftShipDimensions.CockpitCenterX).Within(Tolerance),
                "조종석 쪽 끝이 조종석 방 중심이 아니다 — 문이 벽 한가운데에 안 온다.");

            var cargo = LastShiftCompartments.Of(LastShiftCompartment.CargoBay);
            Assert.That(LastShiftObservationGallery.CargoLandingCenterX,
                Is.EqualTo(cargo.CenterX).Within(Tolerance));

            // 회랑 문은 화물칸에만 요구한다. 다른 구획에 하나라도 나면 국소 고리가 아니다.
            foreach (var spec in LastShiftCompartments.Specs)
            {
                if (spec.Compartment == LastShiftCompartment.CargoBay) continue;
                foreach (var plane in new[] { LastShiftDoorPlane.AlongX, LastShiftDoorPlane.AlongZ })
                foreach (var face in new[] { spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ })
                    Assert.That(LastShiftObservationGallery.DoorwaysOn(spec.Compartment, plane, face), Is.Empty,
                        $"관측 회랑이 {spec.Compartment} 에도 문을 요구한다 — 선수 클러스터 밖으로 새어 나갔다.");
            }

            // 화물칸이 P0 상시 개방(확장 검토 §2)이 되면서 §29.4-(2) 가 "언락되면 그때"
            // 라고 미뤄 둔 고리가 지금 닫힌다 — 화물칸 쪽 문이 실제로 뚫려 회랑이
            // 조종석 ↔ 화물칸 고리가 되고, 막다른 관측 통로가 아니게 된다.
            Assert.That(cargo.IsPassable, Is.True);
            var cargoDoorways = LastShiftObservationGallery.DoorwaysOn(LastShiftCompartment.CargoBay,
                LastShiftDoorPlane.AlongZ, LastShiftObservationGallery.CargoDoorwayFaceZ);
            Assert.That(cargoDoorways.Length, Is.EqualTo(1),
                "화물칸이 열렸는데 관측 회랑 쪽 문이 안 뚫린다 — 고리가 안 닫혀 회랑이 그대로 막다른 관이다.");
            Assert.That(cargoDoorways[0],
                Is.EqualTo(LastShiftObservationGallery.CargoLandingCenterX).Within(Tolerance));
            Assert.That(LastShiftObservationGallery.CockpitDoorwayIsOpen, Is.True,
                "조종석 쪽 문까지 닫히면 회랑 전체가 승무원이 못 가는 자리다 — §29.6-4 가 거짓이 된다.");
        }

        /// <summary>
        /// 칸들이 <b>빈틈 없이 이어지는가</b>. 계단이 한 칸이라도 어긋나면 그 자리에 바닥이
        /// 없는 구간이 생기는데, 판이 아니라 좌표라 씬을 굽기 전에는 안 보인다.
        /// </summary>
        [Test]
        public void ObservationBandsTileTheWholeRouteWithoutAGap()
        {
            var bands = LastShiftObservationGallery.Bands;
            Assert.That(bands.Length, Is.GreaterThan(LastShiftObservationGallery.RunCount),
                "구간 수만큼밖에 안 쪼개졌다 — 계단이 아니라 상자 셋이다.");

            Assert.That(bands[0].MinX, Is.EqualTo(LastShiftObservationGallery.MinX).Within(Tolerance));
            Assert.That(bands[^1].MaxX, Is.EqualTo(LastShiftObservationGallery.MaxX).Within(Tolerance));

            for (var index = 1; index < bands.Length; index++)
                Assert.That(bands[index].MinX, Is.EqualTo(bands[index - 1].MaxX).Within(Tolerance),
                    $"{bands[index].Name} 이 앞 칸과 안 붙는다.");

            // 구간마다 한 칸 이상. 착륙 구간이 0 칸이면 회랑이 테두리에서 끝나 버린다.
            for (var run = 0; run < LastShiftObservationGallery.RunCount; run++)
                Assert.That(bands.Count(band => band.Run == run), Is.GreaterThan(0),
                    $"구간 {run} 에 칸이 하나도 없다.");
        }

        /// <summary>
        /// <b>계단 한 칸의 단차가 판 두께를 안 넘는다.</b> 바닥·천장 슬래브는 칸 안에서 가장
        /// 깊은 테두리 위치까지 나가므로, 단차가 판 두께를 넘는 순간 슬래브가 테두리 판
        /// 바깥면을 뚫고 나와 원반 실루엣에 혹이 생긴다 — §29.4-(1) 이 방금 닫은 그 실루엣이다.
        /// </summary>
        [Test]
        public void ObservationStepsStayInsideTheRimPanel()
        {
            foreach (var band in LastShiftObservationGallery.Bands)
            {
                Assert.That(band.OuterZ - band.SlabOuterZ,
                    Is.InRange(0f, LastShiftObservationGallery.MaxRimStep + Tolerance),
                    $"{band.Name} 의 단차가 판 두께를 넘는다 — 바닥이 테두리 밖으로 나온다.");

                Assert.That(LastShiftHullShell.InscribedContainsFootprint(
                        band.MinX, band.MaxX, band.OuterZ, band.InnerZ), Is.True,
                    $"{band.Name} 의 발자국이 실제로 서는 테두리 다각형 밖이다.");
            }
        }

        /// <summary>
        /// <b>회랑 바깥면이 통째로 창면인가</b>(§29.4-(2) 둘째 항목). 이 성질이 접속점을
        /// 관측실이 아니라 화물칸으로 정한 근거다 — 창 호는 <c>|x| ≤ 25</c> 구간에만 있고,
        /// 관측실(<c>x -35~-32</c>)까지 끌면 회랑 절반이 불투명 판을 보고 걷는다.
        /// </summary>
        [Test]
        public void ObservationGalleryLooksThroughRimGlassAllTheWay()
        {
            foreach (var band in LastShiftObservationGallery.Bands)
            foreach (var x in new[] { band.MinX, band.CenterX, band.MaxX })
            {
                var segment = PortRimSegmentAt(x);
                Assert.That(segment, Is.GreaterThanOrEqualTo(0), $"{band.Name} 이 테두리 밖 x 다.");
                Assert.That(LastShiftHullFrames.SegmentIsWindowBay(segment), Is.True,
                    $"{band.Name} 앞 테두리({segment:00})가 불투명 판이다 — 별도 창 구조가 필요해진다.");
            }
        }

        /// <summary>
        /// §29.6 판정기준 4 — <b>축 정렬이 아닌 통로가 하나 생겼다.</b> 칸 하나하나는 축
        /// 정렬이지만(§28.2 의 <c>AABB</c> 제약) 이어 붙인 동선은 아니고, 그 "아님" 의 크기를
        /// §29.4-(3) 이 상부 회랑에 제안한 곡률(<c>32m</c> 에 <c>5m</c>)과 견준다. 그보다
        /// 완만하면 이 회랑은 테두리를 따라간 것이 아니라 그냥 비스듬한 복도다.
        /// </summary>
        [Test]
        public void ObservationGalleryIsTheFirstCurvedRoute()
        {
            var arc = LastShiftObservationGallery.Bands
                .Where(band => band.Run == LastShiftObservationGallery.ArcRun)
                .ToArray();

            Assert.That(arc.Length, Is.GreaterThan(1),
                "호 구간이 한 칸이다 — 계단이 없으면 곡선도 없다.");

            // 칸마다 실제로 꺾인다. 같은 z 가 이어지면 그만큼은 직선 복도다.
            for (var index = 1; index < arc.Length; index++)
                Assert.That(arc[index].OuterZ, Is.LessThan(arc[index - 1].OuterZ - Tolerance),
                    $"{arc[index].Name} 이 앞 칸과 같은 z 다 — 그 구간은 안 꺾인다.");

            const float proposedCurvature = 5f / 32f;   // §29.4-(3) 의 상부 회랑 호 전환 제안
            var curvature = LastShiftObservationGallery.ArcCenterlineDrift /
                            LastShiftObservationGallery.ArcLength;
            Assert.That(curvature, Is.GreaterThan(proposedCurvature),
                $"호가 §29.4-(3) 제안보다 완만하다({curvature:0.###} ≤ {proposedCurvature:0.###}).");
        }

        /// <summary>
        /// 회랑이 방·선체를 파고들지 않는가. 상부 회랑에 건 것과 같은 검사이고, 계단으로
        /// 쪼갠 덕에 <c>AABB</c> 비교가 형상과 같은 상자를 본다(§28.2·§29.4-(2)).
        /// </summary>
        [Test]
        public void NoObservationBandOverlapsACompartmentOrTheHullInterior()
        {
            foreach (var band in LastShiftObservationGallery.Bands)
            {
                foreach (var spec in LastShiftCompartments.Specs)
                    Assert.That(LastShiftObservationGallery.BandOverlapsCompartment(band, spec), Is.False,
                        $"{band.Name} 이 구획 {spec.Compartment} 을 파고든다.");

                Assert.That(LastShiftUpperGallery.LegOverlapsHullInterior(band.Footprint), Is.False,
                    $"{band.Name} 이 선체 내부를 파고든다.");

                foreach (var leg in LastShiftUpperGallery.Legs)
                    Assert.That(
                        band.MinX < leg.MaxX - Tolerance && leg.MinX < band.MaxX - Tolerance &&
                        band.OuterZ < leg.MaxZ - Tolerance && leg.MinZ < band.InnerZ - Tolerance, Is.False,
                        $"{band.Name} 이 상부 회랑 다리 {leg.Name} 과 겹친다.");
            }
        }

        /// <summary>좌현 반쪽에서 이 x 를 덮는 테두리 세그먼트. 없으면 <c>-1</c>.</summary>
        private static int PortRimSegmentAt(float x)
        {
            for (var index = LastShiftHullShell.SegmentCount / 2;
                 index < LastShiftHullShell.SegmentCount; index++)
            {
                var start = LastShiftHullShell.SegmentStart(index);
                var end = LastShiftHullShell.SegmentStart((index + 1) % LastShiftHullShell.SegmentCount);
                if (x >= start.x && x <= end.x) return index;
            }

            return -1;
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
