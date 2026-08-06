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
            // 개구부가 다섯이 되며 통로 B 쪽 번호가 밀렸다(§3) — 옛 3·2 가 이제 4·3 이다.
            AssertGap(upper: 0, lower: 1);
            AssertGap(upper: 4, lower: 3);

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

        [Test]
        public void RoomsAndPassagesTileTheHullWithoutGapOrOverlap()
        {
            // 방 넷 + 통로 둘이 전장을 남김없이 채우는가. 하나라도 어긋나면 그 틈에 놓인
            // 좌표가 "어느 형상에도 안 들어감" 이 되어 형상 검사가 통과할 수 없다.
            Assert.That(LastShiftShipDimensions.EndRoomLength * 2f + LastShiftShipDimensions.MidRoomLength * 2f + LastShiftShipDimensions.PassageLength * 2f,
                Is.EqualTo(LastShiftShipDimensions.InteriorLength).Within(0.0001f));
            Assert.That(LastShiftShipDimensions.RoomMinX(LastShiftZone.Cockpit),
                Is.EqualTo(-LastShiftShipDimensions.HalfLength).Within(0.0001f));
            Assert.That(LastShiftShipDimensions.RoomMaxX(LastShiftZone.LifeSupport),
                Is.EqualTo(LastShiftShipDimensions.HalfLength).Within(0.0001f));
            for (var passage = 0; passage < 2; passage++)
            {
                // 통로 B 는 냉각실 뒤에 붙는다 — 가운데가 둘로 갈린 뒤로 앞쪽(전력실)이 아니다.
                var before = passage == 0 ? LastShiftZone.Cockpit : LastShiftZone.Cooling;
                var after = passage == 0 ? LastShiftZone.Power : LastShiftZone.LifeSupport;
                Assert.That(LastShiftShipDimensions.PassageMinX(passage),
                    Is.EqualTo(LastShiftShipDimensions.RoomMaxX(before)).Within(0.0001f));
                Assert.That(LastShiftShipDimensions.PassageMaxX(passage),
                    Is.EqualTo(LastShiftShipDimensions.RoomMinX(after)).Within(0.0001f));
            }

            // 구역 경계는 방 경계와 같은 자리여야 한다. 문이 달리는 자리와 압력
            // 판정 자리가 갈라지면 문을 닫아도 판정면에 차단물이 없어 압력이 안 끊긴다.
            Assert.That(LastShiftShipDimensions.ZoneBoundaryX,
                Is.EqualTo(LastShiftShipDimensions.RoomMaxX(LastShiftZone.Cooling)).Within(0.0001f),
                "ZoneBoundaryX 는 const 라 RoomMaxX 와 따로 계산된다 — 둘이 어긋나면 여기서 잡는다.");
        }

        [Test]
        public void SightlineBaffleBlocksEveryStraightLineThroughAPassage()
        {
            // 배플이 A3 를 성립시키는 근거를 식으로 고정한다. 두 개구부를 모두 지나는 직선은
            // 배플이 선 x 평면(t = BaffleOffsetT)에서 반드시 두 개구부 구간을 같은 비율로
            // 보간한 구간 안에 있다. 그 구간을 막으면 관통 직선이 표본이 아니라 전부 사라진다.
            var t = LastShiftShipDimensions.BaffleOffsetT;
            for (var passage = 0; passage < 2; passage++)
            {
                var near = LastShiftShipDimensions.BaffleNearOpening(passage);
                var far = LastShiftShipDimensions.BaffleFarOpening(passage);
                var cutMin = Mathf.LerpUnclamped(
                    LastShiftShipDimensions.OpeningMinZ(near), LastShiftShipDimensions.OpeningMinZ(far), t);
                var cutMax = Mathf.LerpUnclamped(
                    LastShiftShipDimensions.OpeningMaxZ(near), LastShiftShipDimensions.OpeningMaxZ(far), t);

                Assert.That(LastShiftShipDimensions.BaffleMinZ(passage), Is.LessThanOrEqualTo(cutMin + 0.0001f),
                    $"통로 {passage} 배플이 관통 직선 구간의 하단을 덮지 않는다.");
                Assert.That(LastShiftShipDimensions.BaffleMaxZ(passage), Is.GreaterThanOrEqualTo(cutMax - 0.0001f),
                    $"통로 {passage} 배플이 관통 직선 구간의 상단을 덮지 않는다.");
                Assert.That(LastShiftShipDimensions.BaffleMinZ(passage),
                    Is.GreaterThanOrEqualTo(LastShiftShipDimensions.PassageMinZ(passage) - 0.0001f));
                Assert.That(LastShiftShipDimensions.BaffleMaxZ(passage),
                    Is.LessThanOrEqualTo(LastShiftShipDimensions.PassageMaxZ(passage) + 0.0001f));

                // 배플 판(두께 0.4m)이 개구부 평면을 물면 문·벌크헤드와 겹친다. 통로 안에 있어야 한다.
                var half = LastShiftShipDimensions.BaffleThickness * 0.5f;
                Assert.That(LastShiftShipDimensions.BaffleCenterX(passage) - half,
                    Is.GreaterThan(LastShiftShipDimensions.PassageMinX(passage)),
                    $"통로 {passage} 배플이 -x 쪽 개구부 평면을 넘어간다.");
                Assert.That(LastShiftShipDimensions.BaffleCenterX(passage) + half,
                    Is.LessThan(LastShiftShipDimensions.PassageMaxX(passage)),
                    $"통로 {passage} 배플이 +x 쪽 개구부 평면을 넘어간다.");

                // 통행 차선은 문 쪽 개구부의 z 구간과 같은 자리여야 한다. 어긋나면 판을 든
                // 승무원이 문 앞 1m 안에서 z 를 바꿔야 하고, 그 자리는 문이 닫혀 있을 수 있다.
                Assert.That(LastShiftShipDimensions.BaffleFreeStripCenterZ(passage),
                    Is.EqualTo(LastShiftShipDimensions.OpeningCenterZ(far)).Within(0.0001f));
                var lane = LastShiftShipDimensions.BaffleMinZ(passage) - LastShiftShipDimensions.PassageMinZ(passage)
                           > LastShiftShipDimensions.PassageMaxZ(passage) - LastShiftShipDimensions.BaffleMaxZ(passage)
                    ? (LastShiftShipDimensions.PassageMinZ(passage), LastShiftShipDimensions.BaffleMinZ(passage))
                    : (LastShiftShipDimensions.BaffleMaxZ(passage), LastShiftShipDimensions.PassageMaxZ(passage));
                Assert.That(lane.Item2 - lane.Item1,
                    Is.EqualTo(LastShiftShipDimensions.BaffleFreeStrip).Within(0.0001f),
                    $"통로 {passage} 넓은 쪽 통행 폭이 BaffleFreeStrip 과 다르다.");
                Assert.That(lane.Item1,
                    Is.EqualTo(LastShiftShipDimensions.OpeningMinZ(far)).Within(0.0001f),
                    $"통로 {passage} 통행 차선이 문 쪽 개구부와 z 가 어긋난다.");
            }

            // 그리고 <b>물건을 든</b> 사람이 지나갈 수 있어야 한다. 배플이 통행까지 막으면 격리가
            // 아니라 폐쇄다. 기준은 승무원 지름 0.56m 이 아니라 개구부 폭이다 — 통로는 그것이
            // 잇는 개구부보다 좁아질 수 없고, 개구부 1.6m 를 고정한 이유가 판을 들고 통과다.
            Assert.That(LastShiftShipDimensions.BaffleFreeStrip,
                Is.GreaterThanOrEqualTo(LastShiftShipDimensions.OpeningWidth - 0.0001f),
                "배플 옆 통행 폭이 개구부보다 좁으면 판을 든 승무원이 통로에서 막힌다.");
            Assert.That(LastShiftShipDimensions.BaffleFreeStrip + LastShiftShipDimensions.BaffleDeadStrip,
                Is.EqualTo(LastShiftShipDimensions.PassageWidth - LastShiftShipDimensions.BaffleWidth).Within(0.0001f),
                "차선 + 죽은 틈이 통로에서 배플을 뺀 폭과 맞지 않는다.");
        }

        [Test]
        public void EveryThroughSegmentActuallyIntersectsTheBaffleBox()
        {
            // 위 검사와 <b>같은 사실을 다른 계산으로</b> 잰다. 위쪽은 배플 중심 평면에서 보간
            // 구간을 대수로 덮는 방식이고 — 중심 평면은 판 두께 안의 단면이므로 그것만으로도
            // 증명은 완결이다 — 이쪽은 두 개구부를 잇는 선분을 실제로 만들어 상자와 교차하는지
            // 본다. 목적은 증명 보강이 아니라 <b>보간 대수가 틀렸을 때를 잡는 것</b>이다.
            // 한쪽이 조용히 틀리면 다른 쪽이 남는다.
            for (var passage = 0; passage < 2; passage++)
            {
                var near = LastShiftShipDimensions.BaffleNearOpening(passage);
                var far = LastShiftShipDimensions.BaffleFarOpening(passage);
                var nearX = LastShiftShipDimensions.OpeningX(near);
                var farX = LastShiftShipDimensions.OpeningX(far);
                var step = 0.05f / LastShiftShipDimensions.OpeningWidth;
                var missed = 0;
                var total = 0;

                // 두 개구부 z 구간을 각각 0.05m 로 훑어 모든 조합의 관통 선분을 만든다.
                for (var a = 0f; a <= 1f + 0.0001f; a += step)
                for (var b = 0f; b <= 1f + 0.0001f; b += step)
                {
                    var from = new Vector2(nearX, Mathf.Lerp(
                        LastShiftShipDimensions.OpeningMinZ(near), LastShiftShipDimensions.OpeningMaxZ(near), a));
                    var to = new Vector2(farX, Mathf.Lerp(
                        LastShiftShipDimensions.OpeningMinZ(far), LastShiftShipDimensions.OpeningMaxZ(far), b));
                    total++;
                    if (!LastShiftSightlineProbe.BaffleBlocks(from, to, passage)) missed++;
                }

                Assert.That(total, Is.GreaterThan(0));
                Assert.That(missed, Is.EqualTo(0),
                    $"통로 {passage}: 두 개구부를 잇는 선분 {total}개 중 {missed}개가 배플 상자를 비껴간다.");
            }
        }

        [Test]
        public void FixedPointsSitInsideARoomOrPassageNotJustInsideAZone()
        {
            // PM 이 찾은 두 자리(스폰·Tether)를 코드로 못 박는다. 구역으로 재면 통로 한복판도
            // 통과하므로, 방 또는 통로의 실제 x·z 범위 안인지를 본다.
            var points = new (string name, Vector3 at)[]
            {
                ("spawn0", LastShiftNetworkSession.SpawnForSlot(0)),
                ("spawn1", LastShiftNetworkSession.SpawnForSlot(1)),
                ("spawn2", LastShiftNetworkSession.SpawnForSlot(2)),
                ("spawn3", LastShiftNetworkSession.SpawnForSlot(3)),
                ("Battery", LastShiftShipDimensions.BatteryNominal),
                ("CoolingCanister", LastShiftShipDimensions.CoolingNominal),
                ("PatchPlate", LastShiftShipDimensions.PatchPlateNominal),
                ("Tether", LastShiftShipDimensions.TetherNominal)
            };

            foreach (var point in points)
                Assert.That(Clearance(point.at), Is.GreaterThan(0f),
                    $"{point.name} {point.at} 이 방·통로 어디에도 들어가지 않는다 — 벽 안이거나 통로 옆 솔리드다.");

            // Tether 는 위치에 의미가 있다. 스폰에서 조준해 바로 잡히는 거리여야 하고
            // (씬 빌더 주석 참조), 도킹 트리거와 겹치면 시작하자마자 도킹이 성립한다.
            var eye = LastShiftSandboxController.PlayerSpawn + new Vector3(0f, 1.55f, 0f);
            Assert.That(Vector3.Distance(eye, LastShiftShipDimensions.TetherNominal),
                Is.LessThan(LastShiftPlayerController.GrabDistance),
                "Tether 가 시작 자리 사거리 밖이면 상시 grab 대상이라는 성질이 사라진다.");
            Assert.That(Vector3.Distance(
                    LastShiftSandboxController.PlayerSpawn, LastShiftShipDimensions.TetherRackPosition),
                Is.GreaterThan(LastShiftSandboxController.DockingTriggerRadius),
                "받침대가 도킹 트리거 반경 안이면 시작 자리와 도킹 자리가 구분되지 않는다.");
        }

        private static float Clearance(Vector3 point)
        {
            var best = float.MinValue;
            foreach (LastShiftZone zone in System.Enum.GetValues(typeof(LastShiftZone)))
                best = Mathf.Max(best, Box(point,
                    LastShiftShipDimensions.RoomMinX(zone), LastShiftShipDimensions.RoomMaxX(zone),
                    -LastShiftShipDimensions.HalfWidth, LastShiftShipDimensions.HalfWidth));
            for (var passage = 0; passage < 2; passage++)
                best = Mathf.Max(best, Box(point,
                    LastShiftShipDimensions.PassageMinX(passage), LastShiftShipDimensions.PassageMaxX(passage),
                    LastShiftShipDimensions.PassageMinZ(passage), LastShiftShipDimensions.PassageMaxZ(passage)));
            return best;
        }

        private static float Box(Vector3 point, float minX, float maxX, float minZ, float maxZ) =>
            Mathf.Min(point.x - minX, maxX - point.x, point.z - minZ, maxZ - point.z);

        private static void AssertGap(int upper, int lower)
        {
            var gap = LastShiftShipDimensions.OpeningMinZ(upper) - LastShiftShipDimensions.OpeningMaxZ(lower);
            Assert.That(gap, Is.EqualTo(LastShiftShipDimensions.OpeningGapZ).Within(0.0001f),
                $"개구부 {upper}·{lower} 의 z 간격이 정본 조건과 다르다.");
        }
    }
}
