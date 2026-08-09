using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CT-05 N0b 수용 기준 8·9·11·12. 문이 압력 평준화를 실제로 끊는지, 안팎 양쪽에서
    /// 열리는지, 사망한 승무원은 조작할 수 없는지를 본다.
    ///
    /// 개폐 애니메이션(0.8초)은 <see cref="LastShiftZoneDoor.Update"/> 가 도는 PlayMode 소관이고,
    /// 여기서는 "지금 열려 있는가" 라는 판정값과 그 값이 시뮬레이션에 미치는 영향만 다룬다.
    /// 열림 플래그는 조작 순간 뒤집히므로(즉시 효과) 판이 다 움직이기를 기다릴 필요가 없다.
    /// </summary>
    public sealed class LastShiftZoneDoorTests
    {
        [Test]
        public void ClosedDoorStopsPressureEqualizationAcrossThatBoundaryOnly()
        {
            // 산소실만 낮은 상태에서 시작한다. 파공 직후의 모양이다.
            var pressures = new LastShiftZonePressures(1f, 1f, 1f, 0.2f);
            var doors = LastShiftDoorState.AllOpen;
            // 산소실을 끊는 경계는 언제나 마지막 경계다. 번호를 적어 두면 구역이 셋에서 넷이
            // 되며 경계가 하나 늘었을 때(§2.1) 엉뚱한 경계를 닫고도 "닫았다" 고 믿게 된다 —
            // 실제로 이 검사가 그렇게 통과 못 하고 0.60 을 봤다.
            var lastBoundary = LastShiftZoneAtlas.BoundaryCount - 1;
            doors[lastBoundary] = false;

            pressures.Equalize(doors, 60f);

            Assert.That(pressures.LifeSupport, Is.EqualTo(0.2f).Within(0.0001f),
                "닫힌 경계 너머로는 공기가 넘어오지 않아야 한다(수용 기준 9).");
            Assert.That(pressures.Cockpit, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(pressures.Power, Is.EqualTo(1f).Within(0.0001f));

            // 같은 상태에서 문만 열면 그 자리에서 다시 평준화가 시작된다.
            doors[lastBoundary] = true;
            pressures.Equalize(doors, 60f);
            Assert.That(pressures.LifeSupport, Is.GreaterThan(0.2f),
                "문을 열면 평준화가 재개돼야 한다.");
            Assert.That(pressures.Power, Is.LessThan(1f));
        }

        [Test]
        public void OpenDoorEqualizesAtTheDocumentedRate()
        {
            // 차 0.5 인 두 구역이 약 28초에 만난다(§2.2.1). 정확한 수렴 시점 대신 "절반 지나기"
            // 로 확인한다 — 지수 접근이라 완전히 같아지는 시점은 정의되지 않는다.
            var pressures = new LastShiftZonePressures(1f, 0.5f, 0.5f, 0.5f);
            pressures.Equalize(LastShiftDoorState.AllOpen, 28f);

            var gap = pressures.Cockpit - pressures.Power;
            Assert.That(gap, Is.LessThan(0.5f * 0.25f),
                "28초면 초기 차이의 4분의 1 미만으로 좁혀져 있어야 한다(수용 기준 8).");
            Assert.That(pressures.Cockpit, Is.GreaterThan(pressures.Power),
                "평준화는 순서를 뒤집지 않는다.");
        }

        [Test]
        public void DoorIsOperableFromBothSidesOfTheBoundary()
        {
            var setup = CreateDoorSetup(boundary: 1);

            // 광장 쪽에서 닫는다. 문 앞 좌표를 문에서 뽑는 것이 요점이다 — 압력문 셋 중
            // 둘이 z 평면에 서므로 x 로 민 좌표는 "문 앞" 이 아니라 같은 평면 위의 벽 앞이다.
            setup.player.transform.position = NearDoor(1, towardRoom: false, distance: 1f);
            Assert.That(setup.door.TryOperate(setup.player), Is.True);
            Assert.That(setup.sandbox.IsDoorOpen(1), Is.False);

            // 산소실 쪽(경계보다 높은 x)에서 다시 연다. 갇힌 쪽에서 열 수 없으면 격리가
            // 아니라 사형이고, 문서가 격리를 "되돌리기 가능" 으로 둔 이유가 사라진다.
            setup.player.transform.position = NearDoor(1, towardRoom: true, distance: 1f);
            Assert.That(setup.door.TryOperate(setup.player), Is.True);
            Assert.That(setup.sandbox.IsDoorOpen(1), Is.True, "수용 기준 11: 문은 안팎 양쪽에서 열린다.");

            Teardown(setup);
        }

        [Test]
        public void DoorRejectsOperationOutOfReachAndFromDeadCrew()
        {
            var setup = CreateDoorSetup(boundary: 0);

            setup.player.transform.position = NearDoor(0, towardRoom: false, distance: 4f);
            Assert.That(setup.door.TryOperate(setup.player), Is.False, "사거리 밖에서는 조작되지 않는다.");
            Assert.That(setup.sandbox.IsDoorOpen(0), Is.True);

            // 평면 위에서 옆으로 벗어난 경우도 사거리 밖이다. 광장 한 변에 문이 둘 있으므로
            // (좌현: 전력실 + 에어록 홀) 경계면 위에 서 있다는 것만으로는 문 앞이 아니다 —
            // 옆 문 앞이거나 그 사이 벽 앞일 수 있다.
            setup.player.transform.position = BesideDoor(0, LastShiftZoneDoor.OpeningWidth * 0.5f + 1.5f);
            Assert.That(setup.door.TryOperate(setup.player), Is.False, "문 구멍에서 옆으로 벗어나면 조작되지 않는다.");
            Assert.That(setup.sandbox.IsDoorOpen(0), Is.True);

            setup.player.transform.position = BesideDoor(0, 0f);
            var crew = LastShiftCrewOxygen.Ensure(setup.player);
            crew.KillForProbe();
            Assert.That(setup.door.TryOperate(setup.player), Is.False,
                "사망한 승무원은 배를 만질 수 없다(기획 §4.4).");
            Assert.That(setup.sandbox.IsDoorOpen(0), Is.True);

            crew.ResetCrewOxygen();
            Assert.That(setup.door.TryOperate(setup.player), Is.True, "되살아나면 다시 조작된다.");
            Assert.That(setup.sandbox.IsDoorOpen(0), Is.False);

            Teardown(setup);
        }

        [Test]
        public void FindOperablePicksTheNearestBoundaryDoor()
        {
            var first = CreateDoorSetup(boundary: 0);
            var second = CreateDoor(boundary: 1);

            // 조종석↔엔진실 경계(-2) 바로 앞. 반대쪽 경계(+2)는 4m 떨어져 있다.
            var found = LastShiftZoneDoor.FindOperable(NearDoor(0, towardRoom: false, distance: 0.5f));
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Boundary, Is.EqualTo(0));

            found = LastShiftZoneDoor.FindOperable(NearDoor(1, towardRoom: false, distance: 0.5f));
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Boundary, Is.EqualTo(1));

            // 구역 한가운데는 어느 문에도 닿지 않는다. 여기서 문이 잡히면 프롬프트가 상시로
            // 떠서 "문 앞" 이라는 신호 자체가 사라진다.
            Assert.That(LastShiftZoneDoor.FindOperable(new Vector3(0f, 0.1f, 0f)), Is.Null);

            Object.DestroyImmediate(second.gameObject);
            Teardown(first);
        }

        [Test]
        public void ClosingDoorIsolatesBreachZoneInsideTheMissionTimer()
        {
            // 파공 구역(산소실)을 가르는 경계를 번호로 안 적는다. 방사형에서 경계는
            // 사슬이 아니라 별이라 "하나 낮은 번호" 같은 산수가 안 통한다 — 그대로 두면
            // 이 검사가 냉각실 문을 닫고 산소실이 격리되기를 기다린다.
            var breachBoundary = 0;
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                if (LastShiftZoneAtlas.HighZoneOf(boundary) == LastShiftZone.LifeSupport)
                    breachBoundary = boundary;

            var setup = CreateDoorSetup(breachBoundary);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            setup.sandbox.Configure(setup.player, new[] { patch });
            setup.sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(setup.sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(setup.sandbox.BreachZone, Is.EqualTo(LastShiftZone.LifeSupport));

            // 격리하지 않으면 나머지 구역이 계속 공기를 밀어 넣어 배 전체가 함께 내려간다.
            // 파공 구역은 결국 진공에 닿지만 <b>배 전체를 끌고</b> 내려간 뒤다.
            //
            // 예전에는 여기서 "300초 안에 어느 구역도 진공이 되지 않는다" 를 고정하고 있었다.
            // 그 성질이 곧 격리를 죽인 것이었다 — 방치의 대가가 타이머 안에 안 오면 문을 닫을
            // 이유가 없다(interaction-verb-diversification-v1.md §2). C-1 이 그 전제를 뒤집었다.
            var openSeconds = AdvanceUntilBreachVacuum(setup.sandbox, 300);
            Assert.That(openSeconds, Is.LessThan(300),
                "방치해도 파공 구역은 타이머 안에 진공이 된다 — 그 시계가 있어야 격리가 판단이 된다.");

            // 격리하면 파공 구역이 자기 공기만으로 빠지므로 훨씬 빨리 진공에 닿는다.
            setup.sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(setup.sandbox.ApplyMeteorImpact(), Is.True);
            setup.player.transform.position = NearDoor(breachBoundary, towardRoom: false, distance: 1f);
            Assert.That(setup.door.TryOperate(setup.player), Is.True);

            var seconds = AdvanceUntilBreachVacuum(setup.sandbox, 300);
            Assert.That(seconds, Is.LessThan(300),
                "격리하면 파공 구역이 도킹 타이머 안에 진공에 닿는다(수용 기준 9).");
            Assert.That(seconds, Is.LessThan(openSeconds),
                "격리한 쪽이 더 빨라야 한다 — 파공 구역이 이웃에게서 공기를 못 받기 때문이다.");
            Assert.That(setup.sandbox.PressureOf(LastShiftZone.Cockpit), Is.GreaterThan(0.5f),
                "격리한 구역 너머의 조종석은 공기를 지킨다 — 이것이 격리를 누르는 이유다.");

            Object.DestroyImmediate(patch.gameObject);
            Teardown(setup);
        }

        /// <summary>파공 구역이 진공에 닿기까지 몇 초인가. 예산 안에 안 닿으면 예산을 돌려준다.</summary>
        private static int AdvanceUntilBreachVacuum(LastShiftSandboxController sandbox, int budgetSeconds)
        {
            var seconds = 0;
            while (seconds < budgetSeconds &&
                   sandbox.PressureOf(LastShiftZone.LifeSupport) > LastShiftRecoveryTuning.VacuumOxygenPressure)
            {
                sandbox.AdvanceMission(1f);
                seconds++;
            }
            return seconds;
        }

        private struct DoorSetup
        {
            public LastShiftSandboxController sandbox;
            public LastShiftPlayerController player;
            public LastShiftZoneDoor door;
            public GameObject runtimeObject;
        }

        /// <summary>
        /// 문 하나 + sandbox + 승무원. sandbox 를 먼저 만들어야 문이 Awake 에서 찾는다 —
        /// 문은 자기 상태를 들고 있지 않고 sandbox 를 따라가기만 하기 때문이다.
        /// </summary>
        private static DoorSetup CreateDoorSetup(int boundary)
        {
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            var player = CreatePlayer();
            sandbox.Configure(player, System.Array.Empty<LastShiftGrabbable>());
            return new DoorSetup
            {
                sandbox = sandbox,
                player = player,
                door = CreateDoor(boundary),
                runtimeObject = runtimeObject
            };
        }

        /// <summary>
        /// 문 앞 좌표. <b>축을 문에서 뽑는다</b> — 압력문 셋 중 둘(전력실 <c>z=-6</c>,
        /// 냉각실 <c>z=+6</c>)이 <c>z</c> 평면에 서므로 <c>x</c> 로 민 좌표는 "문 앞" 이 아니라
        /// 같은 평면 위의 벽 앞이다. 그 자리에서 조작을 시도하면 검사가 "사거리 밖" 을
        /// 당연히 통과시키고도 이유를 모른다.
        /// </summary>
        private static Vector3 NearDoor(int boundary, bool towardRoom, float distance, float y = 0.1f)
        {
            var door = LastShiftZoneAtlas.BoundaryDoor(boundary);
            var room = LastShiftPlazaLayout.Of(door.Space);
            var roomThrough = door.PlaneIsX
                ? (room.MinX + room.MaxX) * 0.5f
                : (room.MinZ + room.MaxZ) * 0.5f;
            var outward = Mathf.Sign(roomThrough - door.Plane) * (towardRoom ? 1f : -1f);
            var through = door.Plane + outward * distance;
            return door.PlaneIsX
                ? new Vector3(through, y, door.Center)
                : new Vector3(door.Center, y, through);
        }

        /// <summary>문 평면 위에서 구멍 중심으로부터 <paramref name="offset"/> 만큼 옆으로 민 자리.</summary>
        private static Vector3 BesideDoor(int boundary, float offset, float y = 0.1f)
        {
            var door = LastShiftZoneAtlas.BoundaryDoor(boundary);
            return door.PlaneIsX
                ? new Vector3(door.Plane, y, door.Center + offset)
                : new Vector3(door.Center + offset, y, door.Plane);
        }

        private static LastShiftZoneDoor CreateDoor(int boundary)
        {
            var doorObject = new GameObject($"ZoneDoor_{boundary}");
            var waypoint = LastShiftZoneAtlas.BoundaryWaypoint(boundary);
            doorObject.transform.position = new Vector3(waypoint.x, 0f, waypoint.y);
            var door = doorObject.AddComponent<LastShiftZoneDoor>();
            door.Configure(boundary, null, null, null);
            return door;
        }

        private static void Teardown(DoorSetup setup)
        {
            Object.DestroyImmediate(setup.door.gameObject);
            Object.DestroyImmediate(setup.player.gameObject);
            Object.DestroyImmediate(setup.runtimeObject);
        }

        private static LastShiftPlayerController CreatePlayer()
        {
            var playerObject = new GameObject("Player");
            playerObject.transform.position = LastShiftSandboxController.PlayerSpawn;
            playerObject.AddComponent<CharacterController>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            var socket = new GameObject("HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            var player = playerObject.AddComponent<LastShiftPlayerController>();
            player.Configure(camera, socket);
            return player;
        }

        private static LastShiftGrabbable CreateItem(LastShiftItemRole role, Vector3 position)
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.transform.position = position;
            itemObject.AddComponent<Rigidbody>();
            var item = itemObject.AddComponent<LastShiftGrabbable>();
            item.Configure(role, true);
            return item;
        }
    }
}
