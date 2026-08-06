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
        public void BoundaryCountIsOneLessThanZoneCount()
        {
            // 구역이 일렬로 늘어서 있다는 전제 자체다. 이게 깨지면 아래 검사가 전부 무의미하다.
            Assert.That(LastShiftZoneAtlas.BoundaryCount, Is.EqualTo(LastShiftZoneAtlas.ZoneCount - 1));
            Assert.That(LastShiftZoneAtlas.ZoneCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void BoundaryPlanesAscendFromBowToStern()
        {
            // Resolve 가 경계를 앞에서부터 훑고 처음 걸리는 곳에서 멈추므로, 경계가 오름차순이
            // 아니면 중간 구역이 통째로 도달 불가가 된다 — 그래도 컴파일은 된다.
            for (var boundary = 1; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                Assert.That(LastShiftZoneAtlas.BoundaryX(boundary),
                    Is.GreaterThan(LastShiftZoneAtlas.BoundaryX(boundary - 1)),
                    $"경계 {boundary} 가 경계 {boundary - 1} 보다 선수 쪽에 있다 — 구역 순서가 뒤집혔다.");
        }

        [Test]
        public void EveryZoneIsReachableAndZonesAppearInOrderAlongTheShip()
        {
            // 선내를 선수에서 선미로 훑으면 구역 번호가 0 에서 ZoneCount-1 까지 단조 증가해야
            // 한다. 건너뛰는 번호가 있으면 그 구역은 좌표를 하나도 안 갖는 유령 구역이다.
            var half = LastShiftShipDimensions.HalfLength;
            var seen = new bool[LastShiftZoneAtlas.ZoneCount];
            var previous = -1;
            const int samples = 2000;

            for (var i = 0; i <= samples; i++)
            {
                var x = Mathf.Lerp(-half, half, i / (float)samples);
                var zone = (int)LastShiftZoneAtlas.Resolve(new Vector3(x, 0f, 0f));

                Assert.That(zone, Is.InRange(0, LastShiftZoneAtlas.ZoneCount - 1),
                    $"x={x:F2} 가 범위 밖 구역 {zone} 으로 판정됐다.");
                Assert.That(zone, Is.GreaterThanOrEqualTo(previous),
                    $"x={x:F2} 에서 구역이 {previous} → {zone} 으로 되돌아갔다 — 구역이 일렬이 아니다.");

                seen[zone] = true;
                previous = zone;
            }

            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                Assert.That(seen[zone], Is.True,
                    $"구역 {zone}({LastShiftZoneAtlas.ShortLabelOf((LastShiftZone)zone)}) 에 해당하는 x 가 선내에 없다.");
        }

        [Test]
        public void BoundaryPlaneItselfBelongsToTheLowerZone()
        {
            // 동점 규칙을 못박는다. 개구부 몇 개는 x 가 구역 경계와 같은 값이라 이 규칙이 실제로
            // 관측된다. 규칙을 정해 두지 않으면 분기 순서 같은 우연이 답을 정하고, 구역이 늘 때
            // 조용히 뒤집힌다 — 그때 증상은 "게이지가 반대편 구역을 가리킨다" 이고, 값이
            // 그럴듯해서 눈에 안 띈다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var x = LastShiftZoneAtlas.BoundaryX(boundary);
                Assert.That(LastShiftZoneAtlas.Resolve(new Vector3(x, 0f, 0f)),
                    Is.EqualTo(LastShiftZoneAtlas.LowZoneOf(boundary)),
                    $"경계 {boundary}(x={x:F2}) 평면 위의 점은 낮은 쪽 구역에 속해야 한다.");
            }
        }

        [Test]
        public void EachBoundarySeparatesTwoAdjacentZones()
        {
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var low = (int)LastShiftZoneAtlas.LowZoneOf(boundary);
                var high = (int)LastShiftZoneAtlas.HighZoneOf(boundary);
                Assert.That(high - low, Is.EqualTo(1),
                    $"경계 {boundary} 가 인접하지 않은 구역 {low}·{high} 를 가른다.");
                Assert.That(low, Is.EqualTo(boundary),
                    $"경계 번호와 낮은 쪽 구역 번호가 어긋난다 — 경계 {boundary}, 낮은 구역 {low}.");
            }
        }

        /// <summary>
        /// <b>문 구멍이 실제로 지나갈 수 있는 자리에 뚫리는가.</b> 사용자 플레이에서 "냉각실에서
        /// 산소실로 가는 길이 막혔다" 로 잡힌 건의 회귀 검사다.
        ///
        /// 원인은 <c>OpeningIndexOf</c> 가 3구역 시절 식(<c>boundary &lt;= 0 ? 1 : 2</c>)으로
        /// 남아 있어 경계 2 가 개구부 3 이 아니라 2 를 가리킨 것이었다. 벌크헤드 x 는 맞고
        /// 구멍 z 만 통로 반대편에 뚫려서, 그림상으로는 문이 있는데 통로로 걸어가면 벽이었다.
        ///
        /// 번호 대응(<c>boundary + 1</c>)을 직접 비교하지 않는다 — 그건 구현을 구현으로
        /// 검사하는 것이다. 대신 <b>구멍이 그 경계에 접한 통로의 z 폭 안에 들어오는지</b>를 본다.
        /// 통로가 없는 방-방 경계(전력실|냉각실)는 통로 대신 선체 폭 안이면 된다.
        /// </summary>
        [Test]
        public void EveryDoorOpeningLiesInsideThePassageItConnects()
        {
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var centerZ = LastShiftZoneDoor.CenterZOf(boundary);
                var half = LastShiftZoneDoor.OpeningWidth * 0.5f;

                // <b>어느 통로에 접한 경계인지는 x 로 판정한다.</b> OpeningIndexOf 로 나누면
                // 검사 대상이 스스로 "나는 방-방이라 통로 검사 대상이 아니다" 라고 답할 수 있어,
                // 잘못된 번호가 예외 분기로 빠져나간다 — 처음 쓴 판이 실제로 그래서 통과했다.
                var boundaryX = LastShiftZoneAtlas.BoundaryX(boundary);
                var passage = -1;
                for (var candidate = 0; candidate <= 1; candidate++)
                {
                    if (Mathf.Abs(boundaryX - LastShiftShipDimensions.PassageMinX(candidate)) > 0.001f &&
                        Mathf.Abs(boundaryX - LastShiftShipDimensions.PassageMaxX(candidate)) > 0.001f) continue;
                    passage = candidate;
                    break;
                }

                if (passage < 0)
                {
                    Assert.That(Mathf.Abs(centerZ) + half, Is.LessThanOrEqualTo(LastShiftShipDimensions.HalfWidth + 0.001f),
                        $"방-방 경계 {boundary} 의 구멍이 선체 밖으로 나간다.");
                    continue;
                }

                // 구멍은 그 통로 안에 있어야 한다. 아니면 통로로 걸어가는 승무원 앞에
                // 벌크헤드만 있다.
                var minZ = LastShiftShipDimensions.PassageMinZ(passage);
                var maxZ = LastShiftShipDimensions.PassageMaxZ(passage);

                Assert.That(centerZ - half, Is.GreaterThanOrEqualTo(minZ - 0.001f),
                    $"경계 {boundary} 의 구멍이 통로 {passage}(z {minZ:0.##}~{maxZ:0.##}) 밖이다 — 구멍 z={centerZ:0.##}.");
                Assert.That(centerZ + half, Is.LessThanOrEqualTo(maxZ + 0.001f),
                    $"경계 {boundary} 의 구멍이 통로 {passage}(z {minZ:0.##}~{maxZ:0.##}) 밖이다 — 구멍 z={centerZ:0.##}.");
            }
        }

        [Test]
        public void NearestBoundaryPicksTheClosestPlane()
        {
            var half = LastShiftShipDimensions.HalfLength;
            const int samples = 500;

            for (var i = 0; i <= samples; i++)
            {
                var x = Mathf.Lerp(-half, half, i / (float)samples);
                var picked = LastShiftZoneAtlas.NearestBoundary(new Vector3(x, 0f, 0f));
                var pickedDistance = Mathf.Abs(x - LastShiftZoneAtlas.BoundaryX(picked));

                for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                    Assert.That(pickedDistance,
                        Is.LessThanOrEqualTo(Mathf.Abs(x - LastShiftZoneAtlas.BoundaryX(boundary)) + 0.0001f),
                        $"x={x:F2} 에서 경계 {picked} 를 골랐지만 경계 {boundary} 가 더 가깝다.");
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
