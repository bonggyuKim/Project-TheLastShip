using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CT-10 T2 판독 스칼라. 이 파일이 고정하는 것은 <b>값과 등급</b>뿐이다 — 얼마나 떨어져서
    /// 읽히는가(글자 크기·발광)는 표현 요건이라 아트 소관이고, 지금 그 거리 자체가 기획
    /// 판정을 기다리는 중이라 여기에도 상수로 적지 않는다.
    ///
    /// 고정하는 성질은 다섯이다.
    ///   1. 문을 열지 않아도 같은 값이 읽힌다(기획 §3.1.2 제약).
    ///   2. 사이렌 발동선이 정확히 위험 경계다 — 두 채널이 어긋날 수 없다.
    ///   3. 여러 상태는 합산이 아니라 최댓값 하나다.
    ///   4. 갓 터진 계통도 최소 "이상" 이다.
    ///   5. 개구부 너머 공간 판정이 양쪽에서 각각 맞다.
    /// </summary>
    public sealed class LastShiftDoorDistressTests
    {
        // ── 순수 스칼라 ──────────────────────────────────────────────────────

        [Test]
        public void SirenTriggerLandsExactlyOnTheCriticalBoundary()
        {
            // 사이렌(N9)은 전 구역에서 들리고 이 게이지는 개구부마다 다르게 읽힌다. 둘이 다른
            // 선을 쓰면 "사이렌은 우는데 게이지는 이상" 이 되어 어느 쪽을 믿을지가 사라진다.
            var atTrigger = LastShiftDoorDistress.PressureDistress(LastShiftRecoveryTuning.OxygenSirenTrigger);
            Assert.That(atTrigger, Is.EqualTo(LastShiftDoorDistress.CriticalScalar).Within(0.0001f));
            Assert.That(LastShiftDoorDistress.Quantize(atTrigger), Is.EqualTo(LastShiftDistressGrade.Critical));

            // 발동선 바로 위는 아직 위험이 아니다. 경계가 한쪽으로만 열려 있어야 두 채널이
            // 같은 순간에 넘어간다.
            Assert.That(
                LastShiftDoorDistress.Quantize(
                    LastShiftDoorDistress.PressureDistress(LastShiftRecoveryTuning.OxygenSirenTrigger + 0.01f)),
                Is.EqualTo(LastShiftDistressGrade.Abnormal));
        }

        [Test]
        public void PressureDistressIsMonotonicAndPinnedAtBothEnds()
        {
            Assert.That(LastShiftDoorDistress.PressureDistress(1f), Is.EqualTo(0f).Within(0.0001f),
                "만압은 정상이다.");
            Assert.That(LastShiftDoorDistress.PressureDistress(0f), Is.EqualTo(1f).Within(0.0001f),
                "압력 0 은 최대다.");
            Assert.That(LastShiftDoorDistress.Quantize(LastShiftDoorDistress.PressureDistress(1f)),
                Is.EqualTo(LastShiftDistressGrade.Nominal));

            var previous = LastShiftDoorDistress.PressureDistress(0f);
            for (var pressure = 0.02f; pressure <= 1.0001f; pressure += 0.02f)
            {
                var current = LastShiftDoorDistress.PressureDistress(pressure);
                Assert.That(current, Is.LessThanOrEqualTo(previous + 0.0001f),
                    $"압력이 오르는데 이상도가 올라가면 통로에서의 비교가 뒤집힌다(pressure={pressure:F2}).");
                previous = current;
            }
        }

        [Test]
        public void PlanningDialogueGradesReproduceExactly()
        {
            // 기획 §3.1.2 의 대화 그대로다 — B 가 읽은 산소실 0.11 과 A 가 아는 조종석 0.29.
            // 둘 다 정상이 아니고, 그중 어느 쪽이 급한지는 등급으로 갈린다. 이 두 줄이
            // 어긋나면 그 장면이 게임 안에서 성립하지 않는다.
            var lifeSupport = LastShiftDoorDistress.PressureDistress(0.11f);
            var cockpit = LastShiftDoorDistress.PressureDistress(0.29f);

            Assert.That(LastShiftDoorDistress.Quantize(lifeSupport), Is.EqualTo(LastShiftDistressGrade.Critical));
            Assert.That(LastShiftDoorDistress.Quantize(cockpit), Is.EqualTo(LastShiftDistressGrade.Abnormal));
            Assert.That(lifeSupport, Is.GreaterThan(cockpit), "더 낮은 압력이 더 급해야 한다.");
        }

        [Test]
        public void FreshlyBrokenSystemAlreadyReadsAbnormal()
        {
            // 계통이 막 터진 직후에는 어떤 수치도 아직 안 움직였다. 그때 정상으로 읽히면
            // 통로에서 "저쪽은 괜찮다" 는 잘못된 비교가 성립한다.
            Assert.That(LastShiftDoorDistress.SystemDistress(0f),
                Is.EqualTo(LastShiftDoorDistress.AbnormalScalar).Within(0.0001f));
            Assert.That(LastShiftDoorDistress.Quantize(LastShiftDoorDistress.SystemDistress(0f)),
                Is.EqualTo(LastShiftDistressGrade.Abnormal));
            Assert.That(LastShiftDoorDistress.Quantize(LastShiftDoorDistress.SystemDistress(1f)),
                Is.EqualTo(LastShiftDistressGrade.Critical));

            var reading = LastShiftDoorDistress.Evaluate(
                LastShiftZone.Power, pressure: 1f, vacuum: false, worstSystemProgress: 0f);
            Assert.That(reading.Grade, Is.EqualTo(LastShiftDistressGrade.Abnormal),
                "압력이 멀쩡해도 계통이 터졌으면 정상으로 읽히면 안 된다.");
        }

        [Test]
        public void EvaluateKeepsTheWorstStateInsteadOfSummingThem()
        {
            // 합산하면 가벼운 이상 둘이 심각한 이상 하나를 앞질러 비교가 뒤집힌다.
            const float pressure = 0.29f;   // -> 0.557 (이상)
            const float progress = 0.25f;   // -> 0.500 (이상)
            var expected = Mathf.Max(
                LastShiftDoorDistress.PressureDistress(pressure),
                LastShiftDoorDistress.SystemDistress(progress));

            var reading = LastShiftDoorDistress.Evaluate(
                LastShiftZone.Cockpit, pressure, vacuum: false, worstSystemProgress: progress);

            Assert.That(reading.Scalar, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(reading.Grade, Is.EqualTo(LastShiftDistressGrade.Abnormal),
                "이상 둘을 더하면 위험이 되지만, 최댓값이면 이상 그대로다.");
        }

        [Test]
        public void SystemIdentityIsNotRecoverableFromTheReading()
        {
            // 통로에서 계통이 읽히면 그 구역에 들어갈 이유가 없어진다(CT-01 §3.3).
            // 세 계통이 같은 진행도면 판독도 같은 값이어야 한다.
            var state = new LastShiftShipState
            {
                EngineHeat = LastShiftRecoveryTuning.HeatProtectionTrigger * 0.5f,
                BusPower = 1f - (1f - LastShiftRecoveryTuning.UnpoweredBusCeiling) * 0.5f,
                HullIntegrity = 1f - LastShiftRecoveryTuning.OxygenLeakHullReference * 0.5f
            };

            var cooling = LastShiftDoorDistress.ClockProgress(state, LastShiftShipSystem.Cooling);
            var power = LastShiftDoorDistress.ClockProgress(state, LastShiftShipSystem.Power);
            var oxygen = LastShiftDoorDistress.ClockProgress(state, LastShiftShipSystem.Oxygen);

            Assert.That(cooling, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(power, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(oxygen, Is.EqualTo(0.5f).Within(0.0001f));

            var a = LastShiftDoorDistress.Evaluate(LastShiftZone.Power, 1f, false, cooling);
            var b = LastShiftDoorDistress.Evaluate(LastShiftZone.Power, 1f, false, oxygen);
            Assert.That(a.Scalar, Is.EqualTo(b.Scalar).Within(0.0001f),
                "판독값에서 계통을 되짚을 수 있으면 국소 정보 규칙이 깨진다.");
        }

        [Test]
        public void VacuumIsAlwaysMaximumRegardlessOfPressure()
        {
            // 성능 포기로 밀폐한 구역은 압력이 남아 있어도 진공이다. 그 구역을 압력만으로
            // 읽으면 "정상" 으로 표시되고, 들어간 승무원은 예비 산소를 태우기 시작한다.
            var reading = LastShiftDoorDistress.Evaluate(
                LastShiftZone.LifeSupport, pressure: 1f, vacuum: true, worstSystemProgress: -1f);
            Assert.That(reading.Scalar, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(reading.Grade, Is.EqualTo(LastShiftDistressGrade.Critical));
        }

        [Test]
        public void GradeBoundariesFollowTheGradeCountRatherThanLiterals()
        {
            // 등급을 넷으로 늘리면 경계도 함께 따라와야 한다. 리터럴 0.33/0.67 을 박아 두면
            // 등급 수만 바꿨을 때 경계가 그대로 남아 한 칸이 죽는다.
            Assert.That(LastShiftDoorDistress.GradeCount, Is.EqualTo(3));
            Assert.That(LastShiftDoorDistress.AbnormalScalar,
                Is.EqualTo(1f / LastShiftDoorDistress.GradeCount).Within(0.0001f));
            Assert.That(LastShiftDoorDistress.CriticalScalar,
                Is.EqualTo(2f / LastShiftDoorDistress.GradeCount).Within(0.0001f));

            Assert.That(LastShiftDoorDistress.Quantize(LastShiftDoorDistress.AbnormalScalar - 0.001f),
                Is.EqualTo(LastShiftDistressGrade.Nominal));
            Assert.That(LastShiftDoorDistress.Quantize(LastShiftDoorDistress.AbnormalScalar),
                Is.EqualTo(LastShiftDistressGrade.Abnormal));
            Assert.That(LastShiftDoorDistress.Quantize(LastShiftDoorDistress.CriticalScalar - 0.001f),
                Is.EqualTo(LastShiftDistressGrade.Abnormal));
            Assert.That(LastShiftDoorDistress.Quantize(LastShiftDoorDistress.CriticalScalar),
                Is.EqualTo(LastShiftDistressGrade.Critical));
        }

        // ── 샌드박스 경유 ────────────────────────────────────────────────────

        [Test]
        public void ReadingIsIdenticalWithTheDoorOpenAndClosed()
        {
            // 기획 §3.1.2 의 유일한 제약이다. 열어야 수치가 보이면 "확인" 과 "압력 혼합" 이
            // 같은 동작이 되어 판단 자체가 사라진다.
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            sandbox.OverrideZonePressuresForProbe(new LastShiftZonePressures(0.29f, 0.7f, 0.7f, 0.11f));

            var openLifeSupport = sandbox.DistressOf(LastShiftZone.LifeSupport);
            var openCockpit = sandbox.DistressOf(LastShiftZone.Cockpit);
            Assert.That(sandbox.IsDoorOpen(0), Is.True);
            Assert.That(sandbox.IsDoorOpen(1), Is.True);

            sandbox.SetDoorOpen(0, false);
            sandbox.SetDoorOpen(1, false);

            var closedLifeSupport = sandbox.DistressOf(LastShiftZone.LifeSupport);
            var closedCockpit = sandbox.DistressOf(LastShiftZone.Cockpit);

            Assert.That(closedLifeSupport.Scalar, Is.EqualTo(openLifeSupport.Scalar).Within(0.0001f),
                "닫힌 문 앞에서도 같은 값이 읽혀야 한다.");
            Assert.That(closedLifeSupport.Grade, Is.EqualTo(openLifeSupport.Grade));
            Assert.That(closedCockpit.Scalar, Is.EqualTo(openCockpit.Scalar).Within(0.0001f));
            Assert.That(closedCockpit.Grade, Is.EqualTo(openCockpit.Grade));

            // 그리고 그 값이 실제로 기획 대화의 두 등급이다.
            Assert.That(closedLifeSupport.Grade, Is.EqualTo(LastShiftDistressGrade.Critical));
            Assert.That(closedCockpit.Grade, Is.EqualTo(LastShiftDistressGrade.Abnormal));

            Object.DestroyImmediate(runtimeObject);
        }

        [Test]
        public void BeyondDoorResolvesTheSpaceOnTheFarSideFromEitherApproach()
        {
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            // 네 구역을 서로 다른 압력으로 벌려 둔다. 같은 값이면 잘못된 구역을 가리켜도
            // 스칼라가 같아서 이 검사가 통과한다 — 조용히 틀리는 바로 그 모양이다.
            sandbox.OverrideZonePressuresForProbe(new LastShiftZonePressures(1f, 0.29f, 0.53f, 0.11f));

            var scalars = new[]
            {
                sandbox.DistressOf(LastShiftZone.Cockpit).Scalar,
                sandbox.DistressOf(LastShiftZone.Power).Scalar,
                sandbox.DistressOf(LastShiftZone.Cooling).Scalar,
                sandbox.DistressOf(LastShiftZone.LifeSupport).Scalar
            };
            Assert.That(scalars, Is.Unique);

            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var door = LastShiftZoneAtlas.BoundaryDoor(boundary);
                var room = LastShiftPlazaLayout.Of(door.Space);
                var roomCentre = new Vector3(
                    (room.MinX + room.MaxX) * 0.5f, 0f, (room.MinZ + room.MaxZ) * 0.5f);

                // 광장에서 보면 너머는 방, 방에서 보면 너머는 광장(조종석 구역)이다.
                Assert.That(sandbox.DistressBeyondDoor(boundary, Vector3.zero).Zone,
                    Is.EqualTo(LastShiftZoneAtlas.HighZoneOf(boundary)),
                    $"경계 {boundary}: 광장에서 보면 문 너머 방이 너머다.");
                Assert.That(sandbox.DistressBeyondDoor(boundary, roomCentre).Zone,
                    Is.EqualTo(LastShiftZone.Cockpit),
                    $"경계 {boundary}: 방에서 보면 광장이 너머다.");
            }

            // <b>문 평면 위 좌표를 쓰면 안 되는 이유가 여기 있다.</b> 문 평면은 방 경계와
            // 같은 값이라, 평면에서 ε 만큼 민 좌표로 구역을 정하면 부호를 한 번 잘못 잡았을
            // 때 판독이 통째로 반대편을 가리키고도 값이 그럴듯하다. 그래서 방과 광장의
            // <b>중심</b>으로 잰다 — 그쪽은 그런 여지가 없다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var waypoint = LastShiftZoneAtlas.BoundaryWaypoint(boundary);
                var room = LastShiftPlazaLayout.Of(LastShiftZoneAtlas.BoundaryDoor(boundary).Space);
                Assert.That(room.Contains(waypoint.x, waypoint.y), Is.True,
                    $"경계 {boundary} 문 중심이 자기 방 경계 위가 아니다 — 동점이 관측되지 않는다.");
            }

            // 게이지는 방향과 무관하게 언제나 문 너머 구역을 표시한다(§4.1 이설의 결과).
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                Assert.That(sandbox.GaugeReading(boundary).Zone,
                    Is.EqualTo(LastShiftZoneAtlas.HighZoneOf(boundary)));

            Object.DestroyImmediate(runtimeObject);
        }

        [Test]
        public void TheCockpitOpeningReadsTheSameZoneFromBothSides()
        {
            // 조종석↔광장은 문짝 없는 개구부라 양쪽이 <b>같은 구역</b>이다. 이건 결함이 아니라
            // 성질이고(§3.2), 여기가 조용히 뒤집히면 압력 구역이 넷에서 다섯이 된다.
            Assert.That(LastShiftZoneAtlas.Resolve(Vector3.zero), Is.EqualTo(LastShiftZone.Cockpit));
            Assert.That(
                LastShiftZoneAtlas.Resolve(new Vector3(LastShiftShipDimensions.CockpitCenterX, 0f, 0f)),
                Is.EqualTo(LastShiftZone.Cockpit));

            // 광장에 일반문으로 붙은 부속 둘도 같은 구역을 따라온다.
            foreach (var space in new[] { LastShiftPlazaSpace.AirlockHall, LastShiftPlazaSpace.Quarters })
            {
                var room = LastShiftPlazaLayout.Of(space);
                Assert.That(
                    LastShiftZoneAtlas.Resolve(new Vector3(
                        (room.MinX + room.MaxX) * 0.5f, 0f, (room.MinZ + room.MaxZ) * 0.5f)),
                    Is.EqualTo(LastShiftZone.Cockpit),
                    $"{space} 가 조종석 구역이 아니다 — 일반문으로 붙은 부속은 구역을 안 가른다.");
            }
        }

        [Test]
        public void UncontainedMaskIsEmptyBeforeImpactAndCarriesTheBrokenSystemAfter()
        {
            var player = CreatePlayer();
            var battery = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var cooling = CreateItem(LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(player, new[] { battery, cooling, patch });
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);

            Assert.That(sandbox.UncontainedSystemMask, Is.Zero,
                "충격 전에는 손상 계통이 없다 — 여기가 0 이 아니면 시작부터 게이지가 켜진다.");

            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            var mask = sandbox.UncontainedSystemMask;
            Assert.That(mask & (1 << (int)LastShiftShipSystem.Power), Is.Not.Zero,
                "배터리가 풀린 프리셋이므로 전력 계통이 미억제로 잡혀야 한다.");

            // 배터리 정위치는 엔진실이므로 판독도 엔진실에 실린다. 게이지가 가리키는 구역과
            // 실제로 고치러 가야 하는 구역이 어긋나면 판독이 사람을 엉뚱한 데로 보낸다.
            Assert.That(LastShiftZoneAtlas.Resolve(LastShiftShipDimensions.BatteryNominal),
                Is.EqualTo(LastShiftZone.Power));
            Assert.That(sandbox.DistressOf(LastShiftZone.Power).Grade,
                Is.Not.EqualTo(LastShiftDistressGrade.Nominal));

            Object.DestroyImmediate(runtimeObject);
            Object.DestroyImmediate(patch.gameObject);
            Object.DestroyImmediate(cooling.gameObject);
            Object.DestroyImmediate(battery.gameObject);
            Object.DestroyImmediate(player.gameObject);
        }

        [Test]
        public void ClientReadsTheReplicatedMaskInsteadOfRecomputingIt()
        {
            // 클라이언트에는 손상 판정도 수리 장부의 완료 플래그도 없다(sandbox.enabled = IsServer).
            // 같은 식을 다시 계산하면 "고쳤는데 게이지는 계속 위험" 이 되는데, 그건 화면이
            // 조용히 틀리는 형태라 눈에 안 띈다. 서버가 접은 마스크를 그대로 읽어야 한다.
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);

            var snapshot = new LastShiftNetworkSnapshot
            {
                Preset = LastShiftPreset.HighHeatHighThrust,
                ShipState = new LastShiftShipState
                {
                    OxygenPressure = 1f,
                    HullIntegrity = 1f,
                    BusPower = 1f,
                    EngineHeat = 0f
                },
                PowerPressure = 1f,
                CoolingPressure = 1f,
                LifeSupportPressure = 1f,
                Boundary0DoorOpen = true,
                Boundary1DoorOpen = true,
                Boundary2DoorOpen = true,
                UncontainedSystemMask = 1 << (int)LastShiftShipSystem.Oxygen
            };
            sandbox.ApplyNetworkSnapshot(snapshot);

            Assert.That(sandbox.UncontainedSystemMask,
                Is.EqualTo((byte)(1 << (int)LastShiftShipSystem.Oxygen)));
            // 파공 부품(PatchPlate)은 산소실에 있으므로 판독도 산소실에만 실린다.
            Assert.That(sandbox.DistressOf(LastShiftZone.LifeSupport).Grade,
                Is.EqualTo(LastShiftDistressGrade.Abnormal),
                "압력은 만압이지만 미억제 계통이 있으므로 최소 '이상' 이다.");
            Assert.That(sandbox.DistressOf(LastShiftZone.Cockpit).Grade,
                Is.EqualTo(LastShiftDistressGrade.Nominal),
                "손상이 없는 구역까지 올라가면 어느 쪽으로 갈지가 안 갈린다.");

            // 서버가 마스크를 내려 보내면 게이지도 같이 내려간다.
            snapshot.UncontainedSystemMask = 0;
            sandbox.ApplyNetworkSnapshot(snapshot);
            Assert.That(sandbox.DistressOf(LastShiftZone.LifeSupport).Grade,
                Is.EqualTo(LastShiftDistressGrade.Nominal),
                "고쳤는데 게이지가 안 내려가면 수리가 화면에서 안 보인다.");

            Object.DestroyImmediate(runtimeObject);
        }

        [Test]
        public void SnapshotRoundTripPreservesTheUncontainedMask()
        {
            // 스냅샷 필드를 늘렸을 때 Equals 에만 안 넣으면 값이 바뀌어도 NetworkVariable 이
            // 변경으로 안 보고 넘어간다 — 클라이언트 게이지가 영영 안 움직인다.
            var a = new LastShiftNetworkSnapshot { UncontainedSystemMask = 0 };
            var b = new LastShiftNetworkSnapshot { UncontainedSystemMask = 1 };
            Assert.That(a.Equals(b), Is.False, "마스크 차이는 스냅샷 차이여야 한다.");
            b.UncontainedSystemMask = 0;
            Assert.That(a.Equals(b), Is.True);
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
