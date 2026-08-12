using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 4인 동시 플레이가 좌표 위에서 성립하는지 재는 검사.
    /// <c>docs/tech/four-crew-clearance-v1.md</c> 의 측정표를 코드로 옮긴 것이고, 문서 숫자를
    /// 재인용하지 않고 <see cref="LastShiftPlazaLayout"/>·<see cref="LastShiftShipPhysics"/>·
    /// <see cref="LastShiftZoneDoor"/> 에서 매번 다시 뽑는다.
    ///
    /// <b>차선 폭이 둘인 것이 이 파일의 요점이다.</b> 물리 하한
    /// (<see cref="CrewLane"/> <c>0.72m</c> = 캡슐 지름 + skinWidth 두 겹)과 실제로 통과되는 폭
    /// (<see cref="LastShiftDoorways.MinClearWidth"/> <c>1.1m</c>)이 다르고, 후자는 <c>2026-08-08</c>
    /// 플레이테스트가 <c>0.95m</c> 문을 "막혀 있다" 로 보고해서 정해진 값이다. 4인 검증에서
    /// 갈리는 항목이 정확히 이 둘 사이에 있다 — 문 구멍 <c>1.6m</c> 는 물리로는 <b>2인 교행</b>이
    /// 되고 플레이 기준으로는 <b>1인 단선</b>이다.
    ///
    /// <b>기존 <see cref="LastShiftPlazaLayoutTests"/> 와 겹치지 않는다.</b> 그쪽은 겹침·문 위치·
    /// 게이지·이탈 거리처럼 <b>1인 기준</b>으로 성립하는 것을 재고, 여기서는 같은 좌표에
    /// <b>승무원 넷</b>을 얹었을 때만 나오는 것(교행·수용·분산·스폰 간격)만 잰다.
    /// </summary>
    public sealed class LastShiftFourCrewClearanceTests
    {
        private const float Tolerance = 0.01f;

        /// <summary>동시 플레이 인원. <see cref="LastShiftNetworkSession.MaxPlayers"/> 가 정본이다.</summary>
        private const int Crew = LastShiftNetworkSession.MaxPlayers;

        /// <summary>
        /// 승무원 하나가 평면에서 실제로 먹는 폭. <b>반지름만으로 재면 안 된다</b> —
        /// PhysX 컨트롤러는 <see cref="LastShiftShipPhysics.CrewSkinWidth"/> 만큼 떨어진 자리에서
        /// 접촉을 만들므로 그 두 겹이 폭에 그대로 들어간다(<c>LastShiftShipPhysics.cs:81</c>).
        /// </summary>
        private const float CrewLane =
            2f * (LastShiftShipPhysics.CrewRadius + LastShiftShipPhysics.CrewSkinWidth);

        /// <summary>
        /// 조작 오차를 포함한 1인 통행 폭. 정본은 드레싱 검사가 쓰는
        /// <see cref="LastShiftDoorways.MinClearWidth"/> 이고, 그 값이 이미 "정중앙으로 밀지
        /// 않아도 지나가진다" 를 뜻한다.
        /// </summary>
        private const float PlayableLane = LastShiftDoorways.MinClearWidth;

        /// <summary>이 폭에 몇 명이 나란히 서는가. 경계에서 흔들리지 않게 눈금 하나를 얹어 내린다.</summary>
        private static int Lanes(float width, float lane) => Mathf.FloorToInt(width / lane + 1e-4f);

        // ── 1. 차선 폭 자체 ──────────────────────────────────────────────────

        [Test]
        public void BothLaneWidthsComeFromTheCanonAndThePlayableOneIsWider()
        {
            Assert.That(CrewLane, Is.EqualTo(0.72f).Within(Tolerance),
                "승무원 차선 폭이 0.72m 가 아니다 — 캡슐 반지름이나 skinWidth 가 움직였다. " +
                "아래 판정 전부가 이 값 위에 서 있으므로 프리팹 CharacterController 부터 다시 본다.");

            Assert.That(PlayableLane, Is.GreaterThan(CrewLane),
                "플레이 통행 폭이 물리 하한보다 좁다 — 그러면 물리로 못 지나가는 폭을 " +
                "통과 가능으로 판정하게 된다.");

            Assert.That(PlayableLane, Is.EqualTo(1.1f).Within(Tolerance));
        }

        // ── 2. 문 구멍 여섯 ──────────────────────────────────────────────────

        [Test]
        public void EveryPlazaDoorTakesTwoCrewAbreastPhysicallyButOnlyOneAtPlayableWidth()
        {
            var width = LastShiftZoneDoor.OpeningWidth;

            Assert.That(Lanes(width, CrewLane), Is.EqualTo(2),
                $"문 구멍 {width}m 의 물리 차선이 2 가 아니다 (필요 {2f * CrewLane:0.##}m). " +
                "2 밑으로 내려가면 4인 플레이에서 마주친 둘이 서로를 밀어내고, 위로 올라가면 " +
                "이 파일이 고정한 나머지 판정도 같이 다시 잰다.");

            Assert.That(Lanes(width, PlayableLane), Is.EqualTo(1),
                $"문 구멍 {width}m 의 플레이 차선이 1 이 아니다 (2인 교행에 필요 " +
                $"{2f * PlayableLane:0.##}m). <b>이 값이 4인 검증의 결론이다</b> — 문은 물리로만 " +
                "2인 교행이고 실제로는 단선이라, 같은 문을 마주 보고 쓰는 둘은 서로를 기다린다.");

            // 여섯이 전부 같은 규격이어야 이 결론이 배 전체에 걸린다. 하나만 다른 폭이면
            // "어느 문은 되고 어느 문은 안 된다" 가 되어 동선 설계가 문마다 갈린다.
            Assert.That(LastShiftPlazaLayout.Doors.Length, Is.EqualTo(6));
            foreach (var door in LastShiftPlazaLayout.Doors)
                Assert.That(door.MaxSpan - door.MinSpan, Is.EqualTo(width).Within(Tolerance),
                    $"{door.Space} 문만 폭이 다르다.");
        }

        // ── 3. 방 여섯 + 광장 ────────────────────────────────────────────────

        [Test]
        public void EveryRoomShortSideStillTakesTwoCrewAbreastAtPlayableWidth()
        {
            var required = 2f * PlayableLane;
            var narrowest = float.MaxValue;
            var narrowestSpace = LastShiftPlazaSpace.Plaza;

            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                var shortSide = Mathf.Min(footprint.LengthX, footprint.WidthZ);
                if (shortSide < narrowest)
                {
                    narrowest = shortSide;
                    narrowestSpace = footprint.Space;
                }

                Assert.That(shortSide, Is.GreaterThanOrEqualTo(required - Tolerance),
                    $"{footprint.Space} 의 짧은 변이 {shortSide:0.##}m 라 2인 교행 폭 " +
                    $"{required:0.##}m 밑이다 — 방 안에서 마주친 둘이 못 비켜난다.");
            }

            Assert.That(narrowestSpace, Is.EqualTo(LastShiftPlazaSpace.Quarters),
                "가장 좁은 변을 가진 공간이 숙소가 아니다 — 발자국표가 움직였다.");
            Assert.That(narrowest, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void EveryRoomHoldsFourCrewWithItsOwnDoorApproachLeftClear()
        {
            // 문 앞 통행 구역은 서 있는 자리로 안 센다. 드레싱 검사가 소품에 요구하는 것과
            // 같은 구역이고(제약 C5), 승무원이 거기 서 있으면 같은 이유로 문이 막힌다.
            var approach = LastShiftZoneDoor.OpeningWidth * LastShiftDoorways.ApproachDepth;
            var standing = Crew * CrewLane * CrewLane;

            var tightest = float.MaxValue;
            var tightestSpace = LastShiftPlazaSpace.Plaza;

            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                var area = footprint.Area;
                if (footprint.Space == LastShiftPlazaSpace.Plaza)
                    area -= LastShiftPlazaLayout.CoreArea;

                var usable = area - approach;
                Assert.That(usable, Is.GreaterThanOrEqualTo(standing),
                    $"{footprint.Space} 는 문 앞을 비우고 나면 {usable:0.##}m² 라 승무원 넷 " +
                    $"({standing:0.##}m²)이 안 들어간다.");

                var perCrew = area / Crew;
                if (perCrew >= tightest) continue;
                tightest = perCrew;
                tightestSpace = footprint.Space;
            }

            Assert.That(tightestSpace, Is.EqualTo(LastShiftPlazaSpace.Quarters));
            Assert.That(tightest, Is.EqualTo(6f).Within(Tolerance),
                "숙소의 1인당 바닥이 6.0m² 가 아니다 — 가장 얇은 값이므로 래칫으로 고정한다.");
        }

        // ── 4. 광장 고리 ─────────────────────────────────────────────────────

        [Test]
        public void ThePlazaRingAroundTheCoreTakesFourCrewAbreastPhysically()
        {
            // 코어가 광장 한가운데를 통째로 먹으므로 광장 통행은 고리다. 고리 폭은 광장 반폭에서
            // 코어 반폭을 뺀 값이고, 네 변이 같다.
            var ring = (LastShiftPlazaLayout.PlazaMaxX - LastShiftPlazaLayout.PlazaMinX) * 0.5f
                       - LastShiftPlazaLayout.CoreHalfExtent;

            Assert.That(ring, Is.EqualTo(4f).Within(Tolerance),
                "광장 고리 폭이 4m 가 아니다 — 광장 변이나 코어 치수가 움직였다.");

            Assert.That(Lanes(ring, CrewLane), Is.GreaterThanOrEqualTo(Crew),
                $"광장 고리 {ring:0.##}m 에 승무원 넷이 물리적으로도 안 나란히 선다 " +
                $"(필요 {Crew * CrewLane:0.##}m).");

            // 플레이 기준으로는 셋이다. 넷을 요구하면 4.4m 가 필요하고 그건 코어를 줄여야
            // 나오는데, 코어 4x4 는 SIMUL_ZONES <= 2 의 성립 조건이라 못 줄인다
            // (LastShiftPlazaLayout.CoreHalfExtent 주석).
            Assert.That(Lanes(ring, PlayableLane), Is.EqualTo(3),
                $"광장 고리의 플레이 차선이 3 이 아니다 (넷이면 {Crew * PlayableLane:0.##}m 필요).");
        }

        // ── 5. 분산 ──────────────────────────────────────────────────────────

        [Test]
        public void FourCrewSplittingAcrossTheFourZonesNeverShareADoor()
        {
            // 방사형 허브의 4인 근거가 이것이다 — 압력 구역 넷이 각자 전용 문으로 광장에
            // 직결하므로, 넷이 서로 다른 구역으로 흩어지면 공유하는 통과 지점이 없다.
            var waypoints = new List<Vector2>();
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var room = LastShiftPlazaLayout.RoomOf((LastShiftZone)zone);
                waypoints.Add(LastShiftPlazaLayout.DoorOf(room).Waypoint);
            }

            Assert.That(waypoints.Count, Is.EqualTo(Crew),
                "압력 구역 수가 동시 플레이 인원과 다르다 — 1인 1구역 분산 전제가 깨진다.");

            for (var a = 0; a < waypoints.Count; a++)
            for (var b = a + 1; b < waypoints.Count; b++)
                Assert.That(Vector2.Distance(waypoints[a], waypoints[b]),
                    Is.GreaterThan(LastShiftZoneDoor.OpeningWidth),
                    $"구역 {(LastShiftZone)a} 와 {(LastShiftZone)b} 의 문이 서로 구멍 폭 안에 " +
                    "붙어 있다 — 문이 둘이어도 접근 동선이 하나로 합쳐진다.");
        }

        [Test]
        public void EveryRoomHasExactlyOneDoorSoLeavingItSerialisesAtTwoAbreast()
        {
            // 광장만 여섯 갈래이고 나머지 여섯은 전부 외문 하나다. 그래서 한 방에 모인 넷이
            // 동시에 나가야 하는 상황에서는 최선이 2열이고, 그 2열조차 물리 하한 기준이다.
            var doorsPerSpace = new Dictionary<LastShiftPlazaSpace, int>();
            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                doorsPerSpace.TryGetValue(door.Space, out var count);
                doorsPerSpace[door.Space] = count + 1;
            }

            Assert.That(doorsPerSpace.ContainsKey(LastShiftPlazaSpace.Plaza), Is.False,
                "광장이 문 표에 자기 항목으로 들어 있다 — 문은 전부 상대 방 쪽에 귀속돼야 " +
                "방마다 몇 갈래인지가 이 표로 읽힌다.");

            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                if (footprint.Space == LastShiftPlazaSpace.Plaza) continue;
                Assert.That(doorsPerSpace[footprint.Space], Is.EqualTo(1),
                    $"{footprint.Space} 의 문이 하나가 아니다.");
            }

            Assert.That(doorsPerSpace.Count, Is.EqualTo(LastShiftPlazaLayout.Footprints.Length - 1));
        }

        // ── 6. 스폰 ──────────────────────────────────────────────────────────

        [Test]
        public void FourSpawnSlotsDoNotOverlapAndAllLandInsideTheQuarters()
        {
            // <b>깨어나는 방은 숙소다</b>(사용자 지시 2026-08-12). 이 검사가 머지돼 올 때는
            // 스폰이 조종석이었고, 같은 날 온보딩 1단계("기상(숙소)")에 맞춰 옮겼다.
            // 재려던 것 — 넷이 겹치지 않고 벽을 안 파고든다 — 은 그대로다.
            var room = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Quarters);
            var half = CrewLane * 0.5f;
            var previous = float.MinValue;

            for (var slot = 0; slot < Crew; slot++)
            {
                var position = LastShiftNetworkSession.SpawnForSlot(slot);

                Assert.That(room.Contains(position.x, position.z), Is.True,
                    $"슬롯 {slot} 스폰 ({position.x:0.##}, {position.z:0.##}) 이 숙소 밖이다.");

                Assert.That(position.x - half, Is.GreaterThanOrEqualTo(room.MinX - Tolerance));
                Assert.That(position.x + half, Is.LessThanOrEqualTo(room.MaxX + Tolerance));
                Assert.That(position.z - half, Is.GreaterThanOrEqualTo(room.MinZ - Tolerance),
                    $"슬롯 {slot} 캡슐이 좌현 벽을 파고든다.");
                Assert.That(position.z + half, Is.LessThanOrEqualTo(room.MaxZ + Tolerance),
                    $"슬롯 {slot} 캡슐이 우현 벽을 파고든다.");

                if (slot > 0)
                    Assert.That(position.z - previous, Is.GreaterThan(CrewLane),
                        $"슬롯 {slot - 1} 과 {slot} 의 간격이 차선 폭 {CrewLane:0.##}m 이하다 — " +
                        "스폰 프레임에 캡슐 둘이 서로를 밀어낸다.");
                previous = position.z;
            }

            // 넷이 한 줄로 서고도 방 폭이 남는지. 남는 몫이 0 이면 슬롯 간격을 넓히는 순간
            // 바깥 슬롯이 벽을 판다.
            var occupied = LastShiftNetworkSession.SpawnForSlot(Crew - 1).z
                           - LastShiftNetworkSession.SpawnForSlot(0).z + CrewLane;
            Assert.That(occupied, Is.LessThan(room.WidthZ),
                $"스폰 줄 {occupied:0.##}m 가 조종석 방 폭 {room.WidthZ:0.##}m 를 다 먹는다.");
        }

        // ── 7. 갑판 하부 우회로 ──────────────────────────────────────────────

        [Test]
        public void TheUnderDeckDuctIsSingleFileAndStaysSlowerThanCrossingThePlaza()
        {
            var section = LastShiftBypassDuct.Section;

            Assert.That(Lanes(section, CrewLane), Is.EqualTo(1),
                $"우회 덕트 단면 {section:0.##}m 의 물리 차선이 1 이 아니다 — 이 통로는 " +
                "설계상 단선 비용 경로이고, 2 가 되면 광장을 안 쓰는 4인 동선이 생긴다.");

            Assert.That(section, Is.LessThan(PlayableLane),
                "덕트 단면이 플레이 통행 폭 위로 올라왔다 — 웅크림 비용 경로가 서서 가는 " +
                "지름길이 되지 않는지 다시 본다.");

            // L 자 경로. 선수 다리(z 방향)와 선미로 달리는 긴 구간(x 방향)의 합이다.
            var duct = Mathf.Abs(LastShiftBypassDuct.RunZ - LastShiftBypassDuct.ForeShaftZ)
                       + (LastShiftBypassDuct.AftShaftX - LastShiftBypassDuct.ForeShaftX);
            var ductSeconds = duct / LastShiftShipPhysics.CrouchSpeed;

            // 같은 두 방을 광장으로 가로지르는 길이. 방 중심에서 방 중심이다.
            var plaza = Vector2.Distance(
                LastShiftShipDimensions.RoomCenter(LastShiftZone.Cockpit),
                LastShiftShipDimensions.RoomCenter(LastShiftZone.LifeSupport));
            var plazaSeconds = plaza / LastShiftPlayerController.MoveSpeed;

            Assert.That(ductSeconds, Is.GreaterThan(plazaSeconds * 2f),
                $"덕트 {ductSeconds:0.##}초 가 광장 경로 {plazaSeconds:0.##}초 의 두 배 밑이다 — " +
                "단선 우회로가 주 동선의 지름길이 되면 넷이 전부 덕트로 몰린다.");
        }
    }
}
