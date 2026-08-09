using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 구역 위상의 불변식. <b>구역 수에 아무 숫자도 박지 않는 것</b>이 이 파일의 목적이다.
    ///
    /// 통로 재설계 v6(<c>docs/corridor-4p-redesign-v1.md</c> §4)이 엔진실을 전력실·냉각실로
    /// 쪼개면 <see cref="LastShiftZoneAtlas.ZoneCount"/> 가 <c>3 → 4</c> 가 된다. 그 변경이
    /// 안전하려면 구역 수를 따로 들고 있는 자리가 하나도 없어야 하는데, 그런 자리는 대개
    /// 컴파일 오류로 안 드러난다 — <see cref="LastShiftZonePressures.Lowest"/> 가 세 구역만
    /// 보고 있으면 네 번째 구역이 0.1 이어도 사이렌이 안 울릴 뿐, 빌드는 멀쩡히 통과한다.
    ///
    /// 그래서 여기서는 값이 아니라 <b>관계</b>만 검사한다. 구역이 넷이 되는 날 이 파일은
    /// 손대지 않고 그대로 통과해야 하고, 통과하지 않는다면 그게 빠뜨린 자리다.
    /// </summary>
    public sealed class LastShiftZoneTopologyTests
    {
        [Test]
        public void EveryBoundaryIsAPressureDoorOnThePlaza()
        {
            // <b>"경계 = 구역 수 - 1" 은 이제 우연이다.</b> 그 등식은 구역이 일렬일 때 나오는
            // 것이었고, 방사형에서는 경계가 광장 변의 압력문 셋이라 값만 같다. 그래서 관계를
            // 구역 수가 아니라 문 표에서 잰다 — 방이 하나 늘어도 그 방이 압력 경계를 갖는지는
            // 별개 결정이고, 옛 등식을 남겨 두면 그 결정이 산수로 강제된다.
            Assert.That(LastShiftZoneAtlas.BoundaryCount,
                Is.EqualTo(LastShiftPlazaLayout.PressureBoundaryCount));
            Assert.That(LastShiftZoneAtlas.ZoneCount, Is.GreaterThanOrEqualTo(2));

            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                Assert.That(LastShiftZoneAtlas.BoundaryDoor(boundary).Kind,
                    Is.EqualTo(LastShiftPlazaDoorKind.PressureDoor),
                    $"경계 {boundary} 에 압력문이 아닌 구멍이 달려 있다.");
        }

        [Test]
        public void EveryBoundaryHangsOffTheCockpitZone()
        {
            // 별 위상의 정의다(조항 S-1). 경계 하나가 조종석 구역을 안 물면 그 경계 너머는
            // 광장을 안 거치고 닿는 방이라는 뜻이고, 그러면 "경유 방이 없다" 가 깨진다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                Assert.That(LastShiftZoneAtlas.LowZoneOf(boundary), Is.EqualTo(LastShiftZone.Cockpit));
                Assert.That(LastShiftZoneAtlas.HighZoneOf(boundary),
                    Is.Not.EqualTo(LastShiftZone.Cockpit),
                    $"경계 {boundary} 가 조종석 구역을 자기 자신과 가른다.");
            }
        }

        [Test]
        public void BoundaryNumbersCoverEveryNonCockpitZone()
        {
            // 경계 번호와 구역 번호의 관계가 <c>boundary + 1</c> 이라는 것을 여기 한 곳에서
            // 고정한다. 문 상태 스냅샷과 세이브 파일이 경계 번호로 실려 있어 이 매핑이
            // 흔들리면 옛 판이 <b>다른 문을 닫은 채</b> 복원된다 — 값이 그럴듯해서 안 보인다.
            var seen = new bool[LastShiftZoneAtlas.ZoneCount];
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                seen[(int)LastShiftZoneAtlas.HighZoneOf(boundary)] = true;

            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                if ((LastShiftZone)zone == LastShiftZone.Cockpit) continue;
                Assert.That(seen[zone], Is.True,
                    $"구역 {zone}({LastShiftZoneAtlas.ShortLabelOf((LastShiftZone)zone)}) 에 압력 경계가 없다 " +
                    "— 그 방은 격리할 수 없다.");
            }
        }

        [Test]
        public void EveryZoneIsReachableSomewhereInsideTheShip()
        {
            // 선내를 격자로 훑으면 구역 넷이 전부 나와야 한다. 하나가 안 나오면 그 구역은
            // 좌표를 하나도 안 갖는 유령 구역이고, 그래도 컴파일은 된다.
            //
            // <b>단조 증가는 이제 요구하지 않는다.</b> 그것은 구역이 x 축 위에 일렬로
            // 늘어서 있을 때의 성질이었고, 전력실·냉각실이 같은 x 를 z 좌우로 나눠 가지면서
            // 성립하지 않는다 — 그게 §6.2 가 밴드 훑기를 폐기한 이유 그 자체다.
            var seen = new bool[LastShiftZoneAtlas.ZoneCount];
            const float step = 0.25f;

            for (var x = LastShiftPlazaLayout.MinX; x <= LastShiftPlazaLayout.MaxX; x += step)
            for (var z = LastShiftPlazaLayout.MinZ; z <= LastShiftPlazaLayout.MaxZ; z += step)
            {
                if (!LastShiftPlazaLayout.TryResolveSpace(x, z, out _)) continue;
                var zone = (int)LastShiftZoneAtlas.Resolve(new Vector3(x, 0f, z));
                Assert.That(zone, Is.InRange(0, LastShiftZoneAtlas.ZoneCount - 1),
                    $"({x:F2}, {z:F2}) 가 범위 밖 구역 {zone} 으로 판정됐다.");
                seen[zone] = true;
            }

            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                Assert.That(seen[zone], Is.True,
                    $"구역 {zone}({LastShiftZoneAtlas.ShortLabelOf((LastShiftZone)zone)}) 에 해당하는 좌표가 선내에 없다.");
        }

        [Test]
        public void TheDoorPlaneItselfBelongsToThePlazaSide()
        {
            // 동점 규칙을 못박는다. 문 평면 여섯이 전부 광장 변과 <b>같은 값</b>이라 이 규칙이
            // 실제로 관측된다. 정해 두지 않으면 배열 순서 같은 우연이 답을 정하고, 방이 늘 때
            // 조용히 뒤집힌다 — 그때 증상은 "게이지가 반대편 구역을 가리킨다" 이고, 값이
            // 그럴듯해서 눈에 안 띈다.
            //
            // 광장이 발자국표의 첫 줄인 것이 그 규칙의 구현이다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var waypoint = LastShiftZoneAtlas.BoundaryWaypoint(boundary);
                Assert.That(LastShiftZoneAtlas.Resolve(new Vector3(waypoint.x, 0f, waypoint.y)),
                    Is.EqualTo(LastShiftZoneAtlas.LowZoneOf(boundary)),
                    $"경계 {boundary}({waypoint.x:F2}, {waypoint.y:F2}) 평면 위의 점이 광장 쪽에 안 속한다.");
            }
        }

        [Test]
        public void EachBoundaryDoorSitsOnBothItsRoomAndThePlaza()
        {
            // "경유 방이 없다"(§2.3)의 좌표 형태다. 문 평면이 광장 변이면서 동시에 자기 방
            // 경계여야 그 방이 광장에 직결이고, 그 조건이 §6.1 의 사슬 깊이 1 을 만든다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var door = LastShiftZoneAtlas.BoundaryDoor(boundary);
                var room = LastShiftPlazaLayout.Of(door.Space);
                var plaza = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Plaza);

                var onRoom = door.PlaneIsX
                    ? Mathf.Abs(door.Plane - room.MinX) < 0.001f || Mathf.Abs(door.Plane - room.MaxX) < 0.001f
                    : Mathf.Abs(door.Plane - room.MinZ) < 0.001f || Mathf.Abs(door.Plane - room.MaxZ) < 0.001f;
                var onPlaza = door.PlaneIsX
                    ? Mathf.Abs(door.Plane - plaza.MinX) < 0.001f || Mathf.Abs(door.Plane - plaza.MaxX) < 0.001f
                    : Mathf.Abs(door.Plane - plaza.MinZ) < 0.001f || Mathf.Abs(door.Plane - plaza.MaxZ) < 0.001f;

                Assert.That(onRoom, Is.True, $"경계 {boundary} 문이 자기 방 경계 위에 없다.");
                Assert.That(onPlaza, Is.True, $"경계 {boundary} 문이 광장 변 위에 없다.");

                // 구멍 폭이 두 발자국 안에 다 들어가야 문틀이 허공에 안 걸친다.
                var lo = door.PlaneIsX ? Mathf.Max(room.MinZ, plaza.MinZ) : Mathf.Max(room.MinX, plaza.MinX);
                var hi = door.PlaneIsX ? Mathf.Min(room.MaxZ, plaza.MaxZ) : Mathf.Min(room.MaxX, plaza.MaxX);
                Assert.That(door.MinSpan, Is.GreaterThanOrEqualTo(lo - 0.001f));
                Assert.That(door.MaxSpan, Is.LessThanOrEqualTo(hi + 0.001f));
            }
        }

        [Test]
        public void NearestBoundaryPicksTheClosestDoor()
        {
            // <b>평면 거리가 아니라 문 중심까지의 거리로 고른다.</b> 평면으로 재면 광장
            // 어디에 서 있어도 전력실 문과 냉각실 문이 z 하나로만 갈려, 광장 선수 구석에서
            // 산소실 문(x = +6)이 더 가까운데도 안 잡힌다.
            const int samples = 24;
            for (var i = 0; i <= samples; i++)
            for (var j = 0; j <= samples; j++)
            {
                var x = Mathf.Lerp(LastShiftPlazaLayout.PlazaMinX, LastShiftPlazaLayout.PlazaMaxX, i / (float)samples);
                var z = Mathf.Lerp(LastShiftPlazaLayout.PlazaMinZ, LastShiftPlazaLayout.PlazaMaxZ, j / (float)samples);
                var at = new Vector3(x, 0f, z);
                var point = new Vector2(x, z);

                var picked = LastShiftZoneAtlas.NearestBoundary(at);
                var pickedDistance = Vector2.Distance(point, LastShiftZoneAtlas.BoundaryWaypoint(picked));

                for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                    Assert.That(pickedDistance,
                        Is.LessThanOrEqualTo(
                            Vector2.Distance(point, LastShiftZoneAtlas.BoundaryWaypoint(boundary)) + 0.0001f),
                        $"({x:F2}, {z:F2}) 에서 경계 {picked} 를 골랐지만 경계 {boundary} 가 더 가깝다.");
            }

        }
        [Test]
        public void LowestPressureSeesEveryZone()
        {
            // 구역 하나씩 낮춰 보고 Lowest 가 따라 내려가는지 본다. 이름을 나열한 구현이
            // 남아 있으면 새로 늘어난 구역에서만 이 검사가 깨진다.
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var pressures = LastShiftZonePressures.Uniform(1f);
                pressures[(LastShiftZone)zone] = 0.11f;
                Assert.That(pressures.Lowest, Is.EqualTo(0.11f).Within(0.0001f),
                    $"구역 {zone}({LastShiftZoneAtlas.ShortLabelOf((LastShiftZone)zone)}) 이 Lowest 계산에서 빠져 있다 " +
                    "— 이 구역이 진공이어도 사이렌이 안 울린다.");
            }
        }

        [Test]
        public void SetAllAndUniformFillEveryZone()
        {
            var assigned = new LastShiftZonePressures();
            assigned.SetAll(0.42f);
            var uniform = LastShiftZonePressures.Uniform(0.42f);

            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                Assert.That(assigned[(LastShiftZone)zone], Is.EqualTo(0.42f).Within(0.0001f),
                    $"SetAll 이 구역 {zone} 을 안 채운다.");
                Assert.That(uniform[(LastShiftZone)zone], Is.EqualTo(0.42f).Within(0.0001f),
                    $"Uniform 이 구역 {zone} 을 안 채운다.");
            }
        }

        [Test]
        public void PressureEqualityComparesEveryZone()
        {
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var baseline = LastShiftZonePressures.Uniform(1f);
                var altered = LastShiftZonePressures.Uniform(1f);
                altered[(LastShiftZone)zone] = 0.5f;
                Assert.That(baseline.Equals(altered), Is.False,
                    $"구역 {zone} 만 다른 두 압력을 같다고 본다 — 그 구역이 비교에서 빠져 있다.");
            }
        }

        [Test]
        public void AllOpenOpensEveryBoundary()
        {
            var doors = LastShiftDoorState.AllOpen;
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                Assert.That(doors[boundary], Is.True,
                    $"AllOpen 이 경계 {boundary} 의 문을 안 연다 — 그 경계만 압력 평준화가 멈춘다.");
        }

        [Test]
        public void DoorEqualityComparesEveryBoundary()
        {
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var baseline = LastShiftDoorState.AllOpen;
                var altered = LastShiftDoorState.AllOpen;
                altered[boundary] = false;
                Assert.That(baseline.Equals(altered), Is.False,
                    $"경계 {boundary} 의 문만 다른 두 상태를 같다고 본다 — 그 경계가 비교에서 빠져 있다.");
            }
        }

        [Test]
        public void EveryZoneHasDistinctNamesAndLabels()
        {
            // 씬 오브젝트 이름과 HUD 라벨은 구역이 늘 때 같이 늘려야 하는데, 안 늘리면 두 구역이
            // 같은 문자열을 쓴다. TryResolveName 이 그 순간부터 한쪽만 돌려주고, 씬 검증기는
            // "구역 오브젝트가 없다" 가 아니라 "엉뚱한 구역을 찾았다" 로 실패한다.
            var names = new string[LastShiftZoneAtlas.ZoneCount];
            var labels = new string[LastShiftZoneAtlas.ZoneCount];
            var keys = new string[LastShiftZoneAtlas.ZoneCount];

            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var id = (LastShiftZone)zone;
                names[zone] = LastShiftZoneAtlas.NameOf(id);
                labels[zone] = LastShiftZoneAtlas.ShortLabelOf(id);
                keys[zone] = LastShiftZoneAtlas.KeyOf(id);

                Assert.That(LastShiftZoneAtlas.TryResolveName(names[zone], out var round), Is.True,
                    $"구역 {zone} 의 이름 \"{names[zone]}\" 이 되짚어지지 않는다.");
                Assert.That(round, Is.EqualTo(id));
            }

            Assert.That(names, Is.Unique, "구역 오브젝트 이름이 겹친다.");
            Assert.That(labels, Is.Unique, "HUD 라벨이 겹친다.");
            Assert.That(keys, Is.Unique, "로그 키가 겹친다.");
        }
    }
}
