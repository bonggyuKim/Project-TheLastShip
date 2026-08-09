using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 게이지 판독의 두 조건. <b>레이캐스트가 아니라 좌표로 잰다</b> — 유한 개를 쏘면 폭 0 의
    /// 칼날 틈이 확률적으로 안 걸리고, 그때 검사는 "여유 0" 을 "막혔음" 으로 보고한다.
    ///
    /// <b>재는 대상이 통로 개구부에서 광장 압력문으로 옮겨왔다.</b> 예전에는 게이지가 통로 쪽
    /// 한 면에만 붙는다는 <b>배치 결정</b>이 <c>SIMUL_ZONES ≤ 2</c> 를 지켰는데, 통로가
    /// 폐지되면서 그 장치가 §4.1 의 <b>게이지 이설</b>(문틀 → 문 너머 방 안쪽 끝벽)과
    /// §6.4 의 <b>중앙 코어</b> 둘로 바뀌었다.
    ///
    /// 판독 판정 자체는 <see cref="LastShiftPlazaLayout.GaugeVisible"/> 이 갖고 있고, 그것이
    /// <b>보수적인 쪽으로 틀린다</b>(차폐만 재고 각크기는 안 잰다). 그래서 여기서 세는 동시
    /// 판독 수는 실제보다 <b>많거나 같다</b> — 이 검사가 통과하면 실플레이도 통과한다.
    /// </summary>
    public sealed class LastShiftGaugeReadabilityTests
    {
        /// <summary>광장 표본 격자. §4.2 가 실측에 쓴 것과 같은 눈금이다.</summary>
        private const float Step = 0.05f;

        [Test]
        public void GaugesHangOnlyOnPressureDoors()
        {
            foreach (var door in LastShiftPlazaLayout.Doors)
                Assert.That(door.HasGauge, Is.EqualTo(door.Kind == LastShiftPlazaDoorKind.PressureDoor),
                    $"{door.Space} 문의 게이지 유무가 문 종류와 어긋난다.");
        }

        [Test]
        public void EveryGaugeSitsOnTheFarWallOfItsRoom()
        {
            // §4.1 의 이설이 실제로 됐는가. 게이지가 문틀에 남아 있으면 보이는 영역이 쐐기가
            // 아니라 광장 절반이 되고, 코어를 아무리 키워도 SIMUL_ZONES 가 3 으로 남는다.
            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                if (!door.HasGauge) continue;

                var room = LastShiftPlazaLayout.Of(door.Space);
                Assert.That(room.Contains(door.Gauge.x, door.Gauge.y), Is.True,
                    $"{door.Space} 게이지가 자기 방 밖이다.");

                // 문 평면에서 방의 <b>먼 쪽</b> 끝이어야 한다. 가까운 쪽이면 문틀과 같은 값이다.
                var gaugeThrough = door.PlaneIsX ? door.Gauge.x : door.Gauge.y;
                var near = door.PlaneIsX ? room.MinX : room.MinZ;
                var far = door.PlaneIsX ? room.MaxX : room.MaxZ;
                var farthest = Mathf.Abs(near - door.Plane) > Mathf.Abs(far - door.Plane) ? near : far;
                Assert.That(gaugeThrough, Is.EqualTo(farthest).Within(0.0001f),
                    $"{door.Space} 게이지가 방 안쪽 끝벽이 아니다 — §4.1 이설이 안 됐다.");
            }
        }

        [Test]
        public void NoPlazaPointReadsThreeZonesAtOnce()
        {
            // SIMUL_ZONES ≤ 2. 이 배에서 회피 플레이(금지 규칙 166)를 막는 조건이고,
            // 코어 치수가 아트 판단으로 못 줄어드는 근거이기도 하다.
            var worst = 0;
            var worstAt = Vector2.zero;
            var samples = 0;

            for (var x = LastShiftPlazaLayout.PlazaMinX; x <= LastShiftPlazaLayout.PlazaMaxX; x += Step)
            for (var z = LastShiftPlazaLayout.PlazaMinZ; z <= LastShiftPlazaLayout.PlazaMaxZ; z += Step)
            {
                if (LastShiftPlazaLayout.InsideCore(x, z)) continue;
                samples++;
                var readings = LastShiftPlazaLayout.SimultaneousZoneReadings(x, z);
                if (readings <= worst) continue;
                worst = readings;
                worstAt = new Vector2(x, z);
            }

            Assert.That(samples, Is.GreaterThan(40000), "표본이 너무 적다 — 격자가 성기면 위반을 놓친다.");
            Assert.That(worst, Is.LessThanOrEqualTo(2),
                $"({worstAt.x:F2}, {worstAt.y:F2}) 에서 {worst} 구역이 동시에 읽힌다.");
            Assert.That(worst, Is.EqualTo(2),
                "동시 판독 최댓값이 2 가 아니다 — 2 여야 '두 구역 비교' 라는 설계 의도가 산다.");
        }

        [Test]
        public void TheCoreIsWhatKeepsSimulZonesAtTwo()
        {
            // 코어를 빼면 위반이 실제로 돌아오는가. 이 검사가 없으면 코어가 장식으로 읽히고,
            // 그 다음 카드에서 아트가 그것을 줄이거나 없앤다.
            var violations = 0;
            for (var x = -LastShiftPlazaLayout.CoreHalfExtent; x <= LastShiftPlazaLayout.CoreHalfExtent; x += Step)
            for (var z = -LastShiftPlazaLayout.CoreHalfExtent; z <= LastShiftPlazaLayout.CoreHalfExtent; z += Step)
                if (LastShiftPlazaLayout.SimultaneousZoneReadings(x, z) >= 3)
                    violations++;

            Assert.That(violations, Is.GreaterThan(0),
                "코어 자리에 위반이 하나도 없다 — 그렇다면 코어는 SIMUL_ZONES 장치가 아니고, " +
                "§6.4 가 코어 치수를 게임플레이 가드레일로 둔 근거가 사라진다.");
        }

        [Test]
        public void StandingBehindAGaugeReadsNothing()
        {
            // 게이지 뒷면은 아트 소관이고 판독은 안 된다. 방 안(문 너머)에서 광장 게이지를
            // 읽으면 "구역에 가야 진단이 읽힌다" 가 화면에서 거짓이 된다.
            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                if (!door.HasGauge) continue;

                // 게이지와 같은 쪽, 즉 방 안. 문 평면 너머 1m 다.
                var sign = Mathf.Sign((door.PlaneIsX ? door.Gauge.x : door.Gauge.y) - door.Plane);
                var behind = door.PlaneIsX
                    ? new Vector2(door.Plane + sign, door.Center)
                    : new Vector2(door.Center, door.Plane + sign);

                Assert.That(LastShiftPlazaLayout.GaugeVisible(behind.x, behind.y, door), Is.False,
                    $"{door.Space} 게이지가 자기 방 안에서 읽힌다.");
            }
        }

        [Test]
        public void EachGaugeIsReadableFromSomewhereOnThePlaza()
        {
            // 판독이 <b>가능하긴 한가.</b> 위 검사들이 전부 "안 읽혀야 한다" 쪽이라, 게이지를
            // 벽 안에 파묻어도 그것들은 전부 통과한다.
            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                if (!door.HasGauge) continue;

                var readable = false;
                for (var x = LastShiftPlazaLayout.PlazaMinX; x <= LastShiftPlazaLayout.PlazaMaxX && !readable; x += Step)
                for (var z = LastShiftPlazaLayout.PlazaMinZ; z <= LastShiftPlazaLayout.PlazaMaxZ && !readable; z += Step)
                {
                    if (LastShiftPlazaLayout.InsideCore(x, z)) continue;
                    readable = LastShiftPlazaLayout.GaugeVisible(x, z, door);
                }

                Assert.That(readable, Is.True, $"{door.Space} 게이지를 광장 어디에서도 못 읽는다.");
            }
        }

        [Test]
        public void GaugeReadingAlwaysNamesTheZoneBeyondTheDoor()
        {
            // 런타임 접근자와 좌표표가 같은 답을 내는가. 둘이 갈리면 게이지가 가리키는 구역과
            // 실제로 가야 하는 구역이 어긋나고, 그건 화면에서 그럴듯해 보인다.
            var sandbox = new GameObject("Sandbox").AddComponent<LastShiftSandboxController>();
            try
            {
                for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                {
                    var expected = LastShiftZoneAtlas.HighZoneOf(boundary);
                    Assert.That(sandbox.GaugeReading(boundary).Zone, Is.EqualTo(expected));

                    // 광장에 서서 문 너머를 보면 같은 답, 방 안에서 보면 광장(조종석 구역)이다.
                    var door = LastShiftZoneAtlas.BoundaryDoor(boundary);
                    var room = LastShiftPlazaLayout.Of(door.Space);
                    var inRoom = new Vector3((room.MinX + room.MaxX) * 0.5f, 0f, (room.MinZ + room.MaxZ) * 0.5f);

                    Assert.That(sandbox.DistressBeyondDoor(boundary, Vector3.zero).Zone, Is.EqualTo(expected));
                    Assert.That(sandbox.DistressBeyondDoor(boundary, inRoom).Zone, Is.EqualTo(LastShiftZone.Cockpit));
                }
            }
            finally
            {
                Object.DestroyImmediate(sandbox.gameObject);
            }
        }

        [Test]
        public void EveryGaugeIsAccountedForByAPressureBoundary()
        {
            var gauges = LastShiftPlazaLayout.Doors.Count(door => door.HasGauge);
            Assert.That(gauges, Is.EqualTo(LastShiftZoneAtlas.BoundaryCount),
                "게이지 수와 압력 경계 수가 다르다 — 어느 경계는 눈으로 못 읽는다.");
        }
    }
}
