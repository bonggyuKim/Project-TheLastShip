using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 개구부가 통로를 따라 한쪽으로 치우친 뒤의 기하 조건을 코드로 고정한다.
    ///
    /// 여기서 확인하는 것은 씬이 아니라 파생식이다. 씬 검증기(LastShiftSceneVerifier)는 빌드된
    /// 씬을 보므로 Editor 어셈블리에 있고 자동 테스트에서 매번 돌지 않는다. 반면 개구부 중심 z 와
    /// 통로 폭은 여섯 자리(판·문틀·인방·차단 콜라이더·벌크헤드 좌우·조작 사거리)가 전부 참조하는
    /// 값이라, 누가 상수 하나를 옮기면 그 여섯이 조용히 어긋난다. 그 순간을 여기서 잡는다.
    /// </summary>
    public sealed class LastShiftBulkheadCoverageTests
    {
        [Test]
        public void PassageWidthDerivesFromTheOpeningGapNotALiteral()
        {
            // 정본 조건은 폭이 아니라 간격이다. 폭을 리터럴로 두면 개구부 폭이 바뀔 때
            // 두 구간이 겹쳐도 폭 검사만 계속 통과한다.
            Assert.That(LastShiftShipDimensions.OpeningGapZ, Is.GreaterThan(0f),
                "간격 0 은 유한 개 레이캐스트로 못 잡는 칼날 틈이 되어 검증이 여유 0 을 여유 있음으로 보고한다.");
            Assert.That(LastShiftShipDimensions.PassageWidth,
                Is.EqualTo(LastShiftZoneDoor.OpeningWidth * 2f + LastShiftShipDimensions.OpeningGapZ).Within(0.0001f));
            Assert.That(LastShiftShipDimensions.PassageWidth,
                Is.LessThanOrEqualTo(LastShiftShipDimensions.InteriorWidth),
                "통로가 선체 폭을 넘으면 꺾을 여유가 없다.");
        }

        [Test]
        public void OpeningsInsideOnePassageDoNotOverlapInZ()
        {
            // 관통 차단의 진짜 조건. 통로 A 는 개구부 0·1, 통로 B 는 3·2 다(z 큰 쪽이 앞).
            AssertGap(upper: 0, lower: 1);
            AssertGap(upper: 3, lower: 2);

            // 그리고 개구부 넷 전부가 선체 안에 들어가야 한다. 하나라도 벽을 뚫고 나가면
            // 벌크헤드 좌우 판 중 한 짝의 폭이 음수가 된다.
            for (var opening = 0; opening < LastShiftShipDimensions.OpeningCount; opening++)
            {
                Assert.That(LastShiftShipDimensions.OpeningMinZ(opening),
                    Is.GreaterThanOrEqualTo(-LastShiftShipDimensions.HalfWidth - 0.0001f));
                Assert.That(LastShiftShipDimensions.OpeningMaxZ(opening),
                    Is.LessThanOrEqualTo(LastShiftShipDimensions.HalfWidth + 0.0001f));
            }
        }

        [Test]
        public void CockpitToLifeSupportHasNoStraightOpeningInZ()
        {
            // 조종석에서 산소실이 한눈에 보이면 국소 정보 규칙이 화면에서 거짓이 된다.
            // 통로 둘을 잇는 축선이 되려면 개구부 0(조종석↔통로A)과 3(통로B↔산소실)의
            // z 구간이 겹쳐야 하므로, 그 둘이 안 겹치는 것을 직접 확인한다.
            var lowMax = LastShiftShipDimensions.OpeningMaxZ(3);
            var highMin = LastShiftShipDimensions.OpeningMinZ(0);
            Assert.That(highMin, Is.GreaterThan(lowMax),
                "개구부 0 과 3 의 z 구간이 겹치면 조종석↔산소실 직선 시선이 생긴다.");
        }

        [Test]
        public void BulkheadSidePanelsCoverEverythingBesideTheOpening()
        {
            // 문 옆으로 걸어서 지나갈 틈이 남지 않는가. 좌우 판의 폭 합이 아니라 각 판이
            // 덮는 구간을 본다 — 합만 보면 판 둘이 같은 쪽으로 몰려 반대편이 통째로 뚫려
            // 있어도 통과한다. 개구부가 치우친 뒤로는 좌우 폭이 서로 달라 더 위험하다.
            var wallHalf = LastShiftShipDimensions.EndWallSpan * 0.5f;
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var centerZ = LastShiftZoneDoor.CenterZOf(boundary);
                var openingMin = centerZ - LastShiftZoneDoor.OpeningWidth * 0.5f;
                var openingMax = centerZ + LastShiftZoneDoor.OpeningWidth * 0.5f;

                var foreWidth = openingMin - -wallHalf;
                var aftWidth = wallHalf - openingMax;
                Assert.That(foreWidth, Is.GreaterThan(0f), $"경계 {boundary} 의 -z 쪽 벌크헤드 폭이 0 이하다.");
                Assert.That(aftWidth, Is.GreaterThan(0f), $"경계 {boundary} 의 +z 쪽 벌크헤드 폭이 0 이하다.");
                Assert.That(foreWidth + aftWidth + LastShiftZoneDoor.OpeningWidth,
                    Is.EqualTo(LastShiftShipDimensions.EndWallSpan).Within(0.0001f),
                    $"경계 {boundary} 의 벌크헤드가 개구부를 뺀 나머지를 남김없이 덮어야 한다.");
            }
        }

        [Test]
        public void DoorReachFollowsTheOpeningCentreNotTheShipAxis()
        {
            // 경계면 위에 서 있다는 것만으로는 문 앞이 아니다. 개구부가 한쪽으로 치우쳤으므로
            // 반대쪽 벽 앞에서 문이 잡히면 벽을 통과해 조작하는 것이 된다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var door = new GameObject($"ReachProbe_{boundary}").AddComponent<LastShiftZoneDoor>();
                door.Configure(boundary, null, null, null);
                var boundaryX = LastShiftZoneAtlas.BoundaryX(boundary);
                var centerZ = LastShiftZoneDoor.CenterZOf(boundary);

                Assert.That(door.IsWithinReach(new Vector3(boundaryX, 0.1f, centerZ)), Is.True);
                Assert.That(
                    door.IsWithinReach(new Vector3(boundaryX, 0.1f, centerZ - LastShiftZoneDoor.OpeningWidth * 0.5f - 1.5f)),
                    Is.False, "개구부에서 z 로 벗어난 자리는 문 앞이 아니다.");

                Object.DestroyImmediate(door.gameObject);
            }
        }

        private static void AssertGap(int upper, int lower)
        {
            var gap = LastShiftShipDimensions.OpeningMinZ(upper) - LastShiftShipDimensions.OpeningMaxZ(lower);
            Assert.That(gap, Is.EqualTo(LastShiftShipDimensions.OpeningGapZ).Within(0.0001f),
                $"개구부 {upper}·{lower} 의 z 간격이 정본 조건과 다르다.");
        }
    }
}
