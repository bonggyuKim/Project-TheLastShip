using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CT-10 T5(판독 가능성) + T4 의 <c>SIMUL_ZONES ≤ 2</c>. 둘 다
    /// <see cref="LastShiftSightlineProbe"/> 의 선분 대 AABB 로만 재고 좌표를 박지 않는다.
    ///
    /// <b>이 검사가 못 보는 것.</b> 여기서 재는 것은 차폐뿐이다 — 게이지가 시야에 들어오는가.
    /// 각크기(너무 비스듬해서 칸이 안 갈리는 경우)는 재지 않으므로, 개구부 0·3 을 목표로 넣으면
    /// 기하학적으로는 PASS 가 나온다(0.28m 앞에서 판을 옆에서 보는 상태다). 개구부 0·3 을
    /// 기각한 근거는 각크기이고 그것을 자동으로 잡는 검사는 아직 없다 — CT-14(종횡비/화각)와
    /// 같은 종류의 구멍이다. 그래서 목표 개구부를 <see cref="LastShiftSightlineProbe.GaugeOpenings"/>
    /// 로 파라미터화해 두고, 여기서 0·3 의 실패를 주장하지 않는다.
    /// </summary>
    public sealed class LastShiftGaugeReadabilityTests
    {
        /// <summary>관찰자 z 를 쓰는 간격. 통로 폭 3.6m 을 36칸으로 훑는다.</summary>
        private const float SweepStep = 0.1f;

        [Test]
        public void EveryStandableSpotInTheWalkLaneReadsTheGaugeFullWidth()
        {
            for (var passage = 0; passage < 2; passage++)
            {
                var near = LastShiftShipDimensions.BaffleNearOpening(passage);
                var gaugeOpening = LastShiftShipDimensions.BaffleFarOpening(passage);

                // 가장 불리한 자리에서 잰다 — 배플 반대쪽 끝, 벽에서 몸 반지름만큼 떨어진 곳.
                var inward = Mathf.Sign(
                    LastShiftShipDimensions.PassageCenterX(passage) - LastShiftShipDimensions.OpeningX(near));
                var eyeX = LastShiftShipDimensions.OpeningX(near) + inward * LastShiftShipPhysics.CrewRadius;

                var minZ = LastShiftShipDimensions.PassageMinZ(passage) + LastShiftShipPhysics.CrewRadius;
                var maxZ = LastShiftShipDimensions.PassageMaxZ(passage) - LastShiftShipPhysics.CrewRadius;

                var bandMin = float.NaN;
                var bandMax = float.NaN;
                var readable = 0;
                for (var z = minZ; z <= maxZ + 0.0001f; z += SweepStep)
                {
                    if (!LastShiftSightlineProbe.GaugeReadableFrom(new Vector2(eyeX, z), gaugeOpening)) continue;
                    readable++;
                    if (float.IsNaN(bandMin)) bandMin = z;
                    bandMax = z;
                }

                Assert.That(readable, Is.GreaterThan(0),
                    $"통로 {passage} 어디에 서도 개구부 {gaugeOpening} 게이지가 전폭으로 안 읽힌다.");

                // 그리고 <b>통행 차선 전체</b>에서 읽혀야 한다. 한 점에서만 읽히면 그 자리를
                // 찾는 것이 플레이가 되고, 통로에 서는 이유가 정보가 아니라 위치 맞추기가 된다.
                var laneMin = LastShiftShipDimensions.OpeningMinZ(gaugeOpening) + LastShiftShipPhysics.CrewRadius;
                var laneMax = LastShiftShipDimensions.OpeningMaxZ(gaugeOpening) - LastShiftShipPhysics.CrewRadius;
                Assert.That(bandMin, Is.LessThanOrEqualTo(laneMin + SweepStep),
                    $"통로 {passage} 판독 띠가 차선 하단을 덮지 않는다.");
                Assert.That(bandMax, Is.GreaterThanOrEqualTo(laneMax - SweepStep),
                    $"통로 {passage} 판독 띠가 차선 상단을 덮지 않는다.");

                Debug.Log($"[LAST_SHIFT_GAUGE_READ] passage={passage} opening={gaugeOpening} " +
                          $"eyeX={eyeX:F2} band=[{bandMin:F2},{bandMax:F2}] bandWidth={bandMax - bandMin:F2} " +
                          $"laneWidth={laneMax - laneMin:F2} samples={readable} result=PASS");
            }
        }

        [Test]
        public void BaffleBlocksTheGaugeFromTheDeadStripSideOfThePassage()
        {
            // 위 검사가 "차선에서 읽힌다" 만 보면 배플을 지워도 통과한다. 반대 방향도 고정한다 —
            // 죽은 틈 쪽에 서면 배플이 게이지를 가려야 한다. 이것이 A3 격리의 관측 가능한 형태다.
            for (var passage = 0; passage < 2; passage++)
            {
                var gaugeOpening = LastShiftShipDimensions.BaffleFarOpening(passage);
                var near = LastShiftShipDimensions.BaffleNearOpening(passage);
                var inward = Mathf.Sign(
                    LastShiftShipDimensions.PassageCenterX(passage) - LastShiftShipDimensions.OpeningX(near));
                var eyeX = LastShiftShipDimensions.OpeningX(near) + inward * LastShiftShipPhysics.CrewRadius;

                // 배플 z 구간의 중앙에 서면 게이지까지의 선분이 배플을 정면으로 통과한다.
                var behind = new Vector2(eyeX, LastShiftShipDimensions.BaffleCenterZ(passage));
                Assert.That(LastShiftSightlineProbe.GaugeReadableFrom(behind, gaugeOpening), Is.False,
                    $"통로 {passage} 배플 뒤에서 게이지가 읽힌다 — 배플이 시선을 안 막고 있다.");
            }
        }

        [Test]
        public void NoPointInTheShipReadsMoreThanTwoZones()
        {
            // 기획 정본 SIMUL_ZONES ≤ 2. 선내를 격자로 훑는다 — 대표 지점 몇 개만 보면
            // 하필 그 사이에 있는 자리를 놓친다.
            var worst = 0;
            var worstAt = Vector2.zero;
            var samples = 0;
            for (var x = -LastShiftShipDimensions.HalfLength; x <= LastShiftShipDimensions.HalfLength; x += 0.5f)
            for (var z = -LastShiftShipDimensions.HalfWidth; z <= LastShiftShipDimensions.HalfWidth; z += 0.5f)
            {
                var at = new Vector2(x, z);
                if (!IsStandable(at)) continue;
                samples++;
                var count = LastShiftSightlineProbe.SimultaneousZones(at, out _);
                if (count <= worst) continue;
                worst = count;
                worstAt = at;
            }

            Assert.That(samples, Is.GreaterThan(0), "격자가 방·통로를 하나도 못 잡았다.");
            Assert.That(worst, Is.LessThanOrEqualTo(2),
                $"({worstAt.x:F1}, {worstAt.y:F1}) 에서 {worst} 구역이 동시에 읽힌다 — SIMUL_ZONES 위반이다.");
            Debug.Log($"[LAST_SHIFT_SIMUL_ZONES] samples={samples} worst={worst} " +
                      $"worstAt=({worstAt.x:F1},{worstAt.y:F1}) result=PASS");
        }

        [Test]
        public void UtilityRoomCentreReadsOnlyItsOwnZone()
        {
            // 기획이 짚은 핵심 검사 지점. 게이지를 양면에 달면 여기서 3구역이 되고, 배전반 앞에
            // 자리 잡고 뒤만 돌아보는 것이 최적 플레이가 된다 — 166 이 겨냥한 그림이 하필 일이
            // 가장 많은 방에서 성립한다. 단면 배치가 그것을 막는 조건이다.
            var centre = new Vector2(LastShiftShipDimensions.UtilityCenterX, 0f);
            var count = LastShiftSightlineProbe.SimultaneousZones(centre, out var zones);
            Assert.That(count, Is.EqualTo(1), $"엔진실 방 중앙에서 읽히는 구역: {string.Join(",", zones)}");
            Assert.That(zones[0], Is.EqualTo(LastShiftZone.Utility));

            foreach (var opening in LastShiftSightlineProbe.GaugeOpenings)
                Assert.That(LastShiftSightlineProbe.GaugeReadableFrom(centre, opening), Is.False,
                    $"엔진실 방에서 개구부 {opening} 게이지 앞면이 보인다 — 게이지가 양면에 달렸다.");
        }

        [Test]
        public void EachPassageReadsExactlyTwoZonesAndEachRoomOne()
        {
            // 기획 정본 §3.1.2 의 표를 그대로 고정한다. 통로가 2 여야 소거법이 성립하고,
            // 방이 1 이어야 "감시하기 좋은 자리가 곧 사각이 가장 위험한 자리" 가 유지된다.
            var expected = new List<(string name, Vector2 at, int zones)>
            {
                ("통로 A", new Vector2(LastShiftShipDimensions.PassageCenterX(0),
                    LastShiftShipDimensions.PassageCenterZ(0)), 2),
                ("통로 B", new Vector2(LastShiftShipDimensions.PassageCenterX(1),
                    LastShiftShipDimensions.PassageCenterZ(1)), 2),
                ("조종석 방", new Vector2(LastShiftShipDimensions.CockpitCenterX, 0f), 1),
                ("엔진실 방", new Vector2(LastShiftShipDimensions.UtilityCenterX, 0f), 1),
                ("산소실 방", new Vector2(LastShiftShipDimensions.LifeSupportCenterX, 0f), 1)
            };

            foreach (var (name, at, zones) in expected)
                Assert.That(LastShiftSightlineProbe.SimultaneousZones(at, out var seen), Is.EqualTo(zones),
                    $"{name} 에서 읽히는 구역 수가 정본과 다르다: {string.Join(",", seen)}");
        }

        [Test]
        public void GaugesPointAtTheZoneTheirFaceLooksAwayFrom()
        {
            // 게이지 전용 접근자가 방향을 스스로 정하지 않는다는 것을 고정한다. 개구부 1·2 의
            // 게이지는 둘 다 엔진실을 가리킨다 — 통로 쪽 단면이라 너머가 언제나 엔진실이다.
            var host = new GameObject("Sandbox");
            try
            {
                var sandbox = host.AddComponent<LastShiftSandboxController>();
                sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
                // 세 구역 압력을 벌려 둔다. 같은 값이면 잘못된 구역을 가리켜도 스칼라가 같다.
                sandbox.OverrideZonePressuresForProbe(new LastShiftZonePressures(1f, 0.29f, 0.11f));
                foreach (var opening in LastShiftSightlineProbe.GaugeOpenings)
                {
                    Assert.That(sandbox.GaugeReading(opening).Zone, Is.EqualTo(LastShiftZone.Utility),
                        $"개구부 {opening} 게이지가 엔진실이 아닌 구역을 가리킨다.");

                    // 그리고 관찰자 인자를 남겨 둔 이유 — 범용 함수는 여전히 양쪽을 구분한다.
                    var front = LastShiftShipDimensions.GaugeViewerX(opening);
                    var back = LastShiftShipDimensions.OpeningX(opening)
                               - (front - LastShiftShipDimensions.OpeningX(opening));
                    Assert.That(sandbox.DistressBeyondOpening(opening, back).Zone,
                        Is.Not.EqualTo(sandbox.GaugeReading(opening).Zone),
                        $"개구부 {opening} 이 양쪽에서 같은 값을 낸다 — 단면 결정과 기하 사실이 섞였다.");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>방 또는 통로 안이고 벽에서 몸 반지름만큼 떨어져 있는가.</summary>
        private static bool IsStandable(Vector2 at)
        {
            var r = LastShiftShipPhysics.CrewRadius;
            foreach (var zone in new[] { LastShiftZone.Cockpit, LastShiftZone.Utility, LastShiftZone.LifeSupport })
                if (at.x >= LastShiftShipDimensions.RoomMinX(zone) + r &&
                    at.x <= LastShiftShipDimensions.RoomMaxX(zone) - r &&
                    Mathf.Abs(at.y) <= LastShiftShipDimensions.HalfWidth - r)
                    return true;
            for (var passage = 0; passage < 2; passage++)
                if (at.x >= LastShiftShipDimensions.PassageMinX(passage) + r &&
                    at.x <= LastShiftShipDimensions.PassageMaxX(passage) - r &&
                    at.y >= LastShiftShipDimensions.PassageMinZ(passage) + r &&
                    at.y <= LastShiftShipDimensions.PassageMaxZ(passage) - r)
                    return true;
            return false;
        }
    }
}
