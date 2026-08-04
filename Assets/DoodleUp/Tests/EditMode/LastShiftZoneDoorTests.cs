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
            var pressures = new LastShiftZonePressures(1f, 1f, 0.2f);
            var doors = LastShiftDoorState.AllOpen;
            doors[1] = false;

            pressures.Equalize(doors, 60f);

            Assert.That(pressures.LifeSupport, Is.EqualTo(0.2f).Within(0.0001f),
                "닫힌 경계 너머로는 공기가 넘어오지 않아야 한다(수용 기준 9).");
            Assert.That(pressures.Cockpit, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(pressures.Utility, Is.EqualTo(1f).Within(0.0001f));

            // 같은 상태에서 문만 열면 그 자리에서 다시 평준화가 시작된다.
            doors[1] = true;
            pressures.Equalize(doors, 60f);
            Assert.That(pressures.LifeSupport, Is.GreaterThan(0.2f),
                "문을 열면 평준화가 재개돼야 한다.");
            Assert.That(pressures.Utility, Is.LessThan(1f));
        }

        [Test]
        public void OpenDoorEqualizesAtTheDocumentedRate()
        {
            // 차 0.5 인 두 구역이 약 28초에 만난다(§2.2.1). 정확한 수렴 시점 대신 "절반 지나기"
            // 로 확인한다 — 지수 접근이라 완전히 같아지는 시점은 정의되지 않는다.
            var pressures = new LastShiftZonePressures(1f, 0.5f, 0.5f);
            pressures.Equalize(LastShiftDoorState.AllOpen, 28f);

            var gap = pressures.Cockpit - pressures.Utility;
            Assert.That(gap, Is.LessThan(0.5f * 0.25f),
                "28초면 초기 차이의 4분의 1 미만으로 좁혀져 있어야 한다(수용 기준 8).");
            Assert.That(pressures.Cockpit, Is.GreaterThan(pressures.Utility),
                "평준화는 순서를 뒤집지 않는다.");
        }

        [Test]
        public void DoorIsOperableFromBothSidesOfTheBoundary()
        {
            var setup = CreateDoorSetup(boundary: 1);
            var boundaryX = LastShiftZoneAtlas.BoundaryX(1);

            // 엔진실 쪽(경계보다 낮은 x)에서 닫는다.
            setup.player.transform.position = new Vector3(boundaryX - 1f, 0.1f, 0f);
            Assert.That(setup.door.TryOperate(setup.player), Is.True);
            Assert.That(setup.sandbox.IsDoorOpen(1), Is.False);

            // 산소실 쪽(경계보다 높은 x)에서 다시 연다. 갇힌 쪽에서 열 수 없으면 격리가
            // 아니라 사형이고, 문서가 격리를 "되돌리기 가능" 으로 둔 이유가 사라진다.
            setup.player.transform.position = new Vector3(boundaryX + 1f, 0.1f, 0f);
            Assert.That(setup.door.TryOperate(setup.player), Is.True);
            Assert.That(setup.sandbox.IsDoorOpen(1), Is.True, "수용 기준 11: 문은 안팎 양쪽에서 열린다.");

            Teardown(setup);
        }

        [Test]
        public void DoorRejectsOperationOutOfReachAndFromDeadCrew()
        {
            var setup = CreateDoorSetup(boundary: 0);
            var boundaryX = LastShiftZoneAtlas.BoundaryX(0);

            setup.player.transform.position = new Vector3(boundaryX - 4f, 0.1f, 0f);
            Assert.That(setup.door.TryOperate(setup.player), Is.False, "사거리 밖에서는 조작되지 않는다.");
            Assert.That(setup.sandbox.IsDoorOpen(0), Is.True);

            setup.player.transform.position = new Vector3(boundaryX, 0.1f, 0f);
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
            var found = LastShiftZoneDoor.FindOperable(new Vector3(LastShiftZoneAtlas.BoundaryX(0) + 0.5f, 0.1f, 0f));
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Boundary, Is.EqualTo(0));

            found = LastShiftZoneDoor.FindOperable(new Vector3(LastShiftZoneAtlas.BoundaryX(1) - 0.5f, 0.1f, 0f));
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
            var setup = CreateDoorSetup(boundary: 1);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, new Vector3(4.5f, 0.65f, -1.6f));
            setup.sandbox.Configure(setup.player, new[] { patch });
            setup.sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(setup.sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(setup.sandbox.BreachZone, Is.EqualTo(LastShiftZone.LifeSupport));

            // 격리하지 않으면 나머지 두 구역이 계속 공기를 밀어 넣어 배 전체가 함께 내려간다.
            // 300초(도킹 타이머) 안에 진공에 닿지 않는 것이 이 경로의 성질이다.
            for (var i = 0; i < 300; i++) setup.sandbox.AdvanceMission(1f);
            Assert.That(setup.sandbox.PressureOf(LastShiftZone.LifeSupport),
                Is.GreaterThan(LastShiftRecoveryTuning.VacuumOxygenPressure),
                "문이 열려 있으면 300초 안에 어느 구역도 진공이 되지 않는다.");

            // 격리하면 파공 구역이 자기 공기만으로 빠지므로 훨씬 빨리 진공에 닿는다.
            setup.sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(setup.sandbox.ApplyMeteorImpact(), Is.True);
            setup.player.transform.position = new Vector3(LastShiftZoneAtlas.BoundaryX(1) - 1f, 0.1f, 0f);
            Assert.That(setup.door.TryOperate(setup.player), Is.True);

            var seconds = 0;
            while (seconds < 300 &&
                   setup.sandbox.PressureOf(LastShiftZone.LifeSupport) > LastShiftRecoveryTuning.VacuumOxygenPressure)
            {
                setup.sandbox.AdvanceMission(1f);
                seconds++;
            }
            Assert.That(seconds, Is.LessThan(300),
                "격리하면 파공 구역이 도킹 타이머 안에 진공에 닿는다(수용 기준 9).");
            Assert.That(setup.sandbox.PressureOf(LastShiftZone.Cockpit), Is.GreaterThan(0.5f),
                "격리한 구역 너머의 조종석은 공기를 지킨다 — 이것이 격리를 누르는 이유다.");

            Object.DestroyImmediate(patch.gameObject);
            Teardown(setup);
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

        private static LastShiftZoneDoor CreateDoor(int boundary)
        {
            var doorObject = new GameObject($"ZoneDoor_{boundary}");
            doorObject.transform.position = new Vector3(LastShiftZoneAtlas.BoundaryX(boundary), 0f, 0f);
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
