using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftSandboxTests
    {
        [Test]
        public void GrabAndDropChangesParentAndPhysicsWithoutInventory()
        {
            var player = CreatePlayer();
            var item = CreateItem(LastShiftItemRole.Battery, Vector3.zero);

            Assert.That(player.TryGrabForProbe(item), Is.True);
            Assert.That(player.HeldItem, Is.EqualTo(item));
            Assert.That(item.IsHeld, Is.True);
            Assert.That(item.transform.parent, Is.EqualTo(player.HoldSocket));
            Assert.That(item.Body.isKinematic, Is.True);

            player.DropForProbe();
            Assert.That(player.HeldItem, Is.Null);
            Assert.That(item.IsHeld, Is.False);
            Assert.That(item.Body.isKinematic, Is.False);

            Object.DestroyImmediate(item.gameObject);
            Object.DestroyImmediate(player.gameObject);
        }

        [Test]
        public void TemporaryControlReturnsToPresetAfterEightSeconds()
        {
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);

            sandbox.ApplyControl(0.2f, 65f);
            Assert.That(sandbox.ControlHoldRemaining, Is.EqualTo(8f));
            Assert.That(sandbox.CurrentState.ThrustDemand, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(sandbox.CurrentState.ShipAttitudeDegrees, Is.EqualTo(65f).Within(0.0001f));

            sandbox.AdvanceControlHold(8.1f);

            Assert.That(sandbox.ControlHoldRemaining, Is.Zero);
            Assert.That(sandbox.CurrentState.ThrustDemand, Is.EqualTo(0.92f).Within(0.0001f));
            Assert.That(sandbox.CurrentState.ShipAttitudeDegrees, Is.EqualTo(8f).Within(0.0001f));

            Object.DestroyImmediate(runtimeObject);
        }

        [Test]
        public void MeteorImpactIsOneShotAndUsesActualItemDisplacement()
        {
            var player = CreatePlayer();
            var battery = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var cooling = CreateItem(LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(player, new[] { battery, cooling, patch });
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            var beforePosition = battery.transform.position;

            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            var afterFirstPosition = battery.transform.position;
            var afterFirstState = sandbox.CurrentState;
            Assert.That(sandbox.FirstResult.Problem, Is.EqualTo(LastShiftDominantProblem.BatteryDisplacedBusDisconnected));
            Assert.That(afterFirstPosition, Is.Not.EqualTo(beforePosition));
            Assert.That(battery.DisplacementFromNominal, Is.GreaterThan(0f));
            Assert.That(sandbox.ImpactApplicationCount, Is.EqualTo(1));
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.BatteryDisplacedBusDisconnected));

            Assert.That(sandbox.ApplyMeteorImpact(), Is.False);
            Assert.That(battery.transform.position, Is.EqualTo(afterFirstPosition));
            Assert.That(sandbox.CurrentState.BusPower, Is.EqualTo(afterFirstState.BusPower));
            Assert.That(sandbox.ImpactApplicationCount, Is.EqualTo(1));

            Destroy(runtimeObject, player, battery, cooling, patch);
        }

        [Test]
        public void RecoveryKeepsFirstProblemAndReevaluatesWithAppliedMeteor()
        {
            var player = CreatePlayer();
            var battery = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var cooling = CreateItem(LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            var tether = CreateItem(LastShiftItemRole.Tether, LastShiftShipDimensions.TetherNominal);
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(player, new[] { battery, cooling, patch, tether });
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            var meteor = LastShiftMeteorStimulus.Canonical;
            meteor.ImpactVector = Vector3.forward;

            Assert.That(sandbox.ApplyMeteorImpact(meteor), Is.True);
            var first = sandbox.FirstResult;
            battery.SetSecured(true);
            sandbox.RefreshResultAfterImpact();

            Assert.That(sandbox.FirstResult.Problem, Is.EqualTo(first.Problem));
            Assert.That(sandbox.LastResult.BatteryScore, Is.LessThan(first.BatteryScore));
            Assert.That(sandbox.LastResult.CauseChain, Does.Contain($"vector={meteor.ImpactVector}"));
            Destroy(runtimeObject, player, battery, cooling, patch, tether);
        }

        [Test]
        public void PresetResetRestoresDamagedStateAndItemsThenAllowsNextOneShot()
        {
            var player = CreatePlayer();
            var battery = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var cooling = CreateItem(LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(player, new[] { battery, cooling, patch });
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            var nominal = battery.NominalPosition;
            // 프리셋 초기값은 밸런스가 바꾸는 값이라 리터럴로 박지 않는다. 이 검사가 보는
            // 것은 "충격이 깎고 리셋이 되돌린다" 이지 특정 숫자가 아니다 — 0.84 를 적어
            // 뒀더니 balance 가 0.70 으로 정정한 순간 무관한 검사가 같이 울었다.
            var presetHull = LastShiftPresetFactory.Create(LastShiftPreset.PowerOverloadLooseBattery).HullIntegrity;
            sandbox.ApplyMeteorImpact();
            Assert.That(sandbox.CurrentState.HullIntegrity, Is.LessThan(presetHull));
            Assert.That(battery.DisplacementFromNominal, Is.GreaterThan(0f));

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);

            Assert.That(sandbox.HasAppliedImpact, Is.False);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.None));
            Assert.That(sandbox.CurrentState.HullIntegrity, Is.EqualTo(presetHull).Within(0.0001f));
            Assert.That(battery.transform.position, Is.EqualTo(nominal));
            Assert.That(battery.DisplacementFromNominal, Is.Zero.Within(0.0001f));
            Assert.That(battery.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(sandbox.ImpactApplicationCount, Is.EqualTo(2));

            Destroy(runtimeObject, player, battery, cooling, patch);
        }

        [Test]
        public void NearbyLooseItemCanBeSecuredAndDominantProblemChanges()
        {
            var player = CreatePlayer();
            var battery = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var cooling = CreateItem(LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(player, new[] { battery, cooling, patch });
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            sandbox.ApplyMeteorImpact();
            Assert.That(player.TryGrabForProbe(battery), Is.True);
            player.HoldSocket.position = battery.NominalPosition + Vector3.right * 0.3f;
            sandbox.RefreshResultAfterImpact();
            var before = sandbox.LastResult.Problem;

            Assert.That(sandbox.TrySecureHeldItem(), Is.True);

            Assert.That(before, Is.EqualTo(LastShiftDominantProblem.BatteryDisplacedBusDisconnected));
            Assert.That(player.HeldItem, Is.Null);
            Assert.That(battery.Secured, Is.True);
            Assert.That(battery.transform.position, Is.EqualTo(battery.NominalPosition));
            Assert.That(battery.Body.isKinematic, Is.True);
            Assert.That(sandbox.LastResult.Problem, Is.Not.EqualTo(LastShiftDominantProblem.BatteryDisplacedBusDisconnected));

            Destroy(runtimeObject, player, battery, cooling, patch);
        }

        [Test]
        public void SecureSkipsFarFirstHolderAndSecuresNearbySecondHolder()
        {
            var firstPlayer = CreatePlayer();
            var secondPlayer = CreatePlayer();
            var battery = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var cooling = CreateItem(LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal);
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(new[] { firstPlayer, secondPlayer }, new[] { battery, cooling });
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);

            Assert.That(firstPlayer.TryGrabForProbe(battery), Is.True);
            firstPlayer.HoldSocket.position = battery.NominalPosition + Vector3.right * (LastShiftSandboxController.SecureDistance + 0.1f);
            Assert.That(secondPlayer.TryGrabForProbe(cooling), Is.True);
            secondPlayer.HoldSocket.position = cooling.NominalPosition + Vector3.right * (LastShiftSandboxController.SecureDistance - 0.1f);

            Assert.That(sandbox.TrySecureHeldItem(), Is.True);
            Assert.That(firstPlayer.HeldItem, Is.SameAs(battery));
            Assert.That(secondPlayer.HeldItem, Is.Null);
            Assert.That(battery.Secured, Is.False);
            Assert.That(cooling.Secured, Is.True);

            Object.DestroyImmediate(runtimeObject);
            Object.DestroyImmediate(battery.gameObject);
            Object.DestroyImmediate(cooling.gameObject);
            Object.DestroyImmediate(firstPlayer.gameObject);
            Object.DestroyImmediate(secondPlayer.gameObject);
        }

        [Test]
        public void PresetResetRestoresSoloPlayerAndDistinctPostImpactOutcome()
        {
            var player = CreatePlayer();
            var battery = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var cooling = CreateItem(LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal);
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            var tether = CreateItem(LastShiftItemRole.Tether, LastShiftShipDimensions.TetherNominal);
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(player, new[] { battery, cooling, patch, tether });

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.None));
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.BatteryDisplacedBusDisconnected));
            Assert.That(Vector3.Distance(player.transform.position, LastShiftSandboxController.PlayerSpawn), Is.LessThan(0.0001f));
            // CT-03: 즉사 없는 대신 타이머 360s -> 300s. 값은 LastShiftRecoveryTuning 에서 온다.
            Assert.That(sandbox.DockingSecondsRemaining, Is.EqualTo(LastShiftRecoveryTuning.DockingTimerSeconds));
            Assert.That(LastShiftRecoveryTuning.DockingTimerSeconds, Is.EqualTo(300f));

            sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.None));
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.SideOxygenLeak));
            Assert.That(sandbox.ResetGeneration, Is.EqualTo(2));

            Destroy(runtimeObject, player, battery, cooling, patch, tether);
        }

        /// <summary>
        /// 든 물건이 잡은 사람을 밀지 않는가. 사용자 플레이에서 "물건을 잡으면 튕겨 나간다"
        /// 로 잡힌 건이다 — kinematic 으로 바꿔도 콜라이더는 살아 있고, 물건이 소켓에 붙어
        /// 눈앞에 오므로 CharacterController 가 매 프레임 그것을 밀어냈다.
        ///
        /// 놓을 때 되돌리는 것까지 같이 본다. <c>UnityEngine.Physics.IgnoreCollision</c> 은 쌍에 남는
        /// 상태라 안 풀면 그 물건은 영영 그 사람을 통과한다 — 고치려다 반대 버그를 만드는
        /// 자리이고, 화면에서는 한참 뒤에야 보인다.
        /// </summary>
        [Test]
        public void HeldItemDoesNotPushTheHolderAndCollisionReturnsOnDrop()
        {
            var player = CreatePlayer();
            var item = CreateItem(LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal);
            var holder = player.GetComponent<CharacterController>();
            var itemCollider = item.GetComponent<Collider>();

            Assert.That(UnityEngine.Physics.GetIgnoreCollision(itemCollider, holder), Is.False,
                "잡기 전에는 부딪혀야 한다 — 안 그러면 물건을 통과해 걸어간다.");

            Assert.That(player.TryGrabForProbe(item), Is.True);
            Assert.That(UnityEngine.Physics.GetIgnoreCollision(itemCollider, holder), Is.True,
                "든 물건은 잡은 사람을 밀면 안 된다.");

            player.DropForProbe();
            Assert.That(UnityEngine.Physics.GetIgnoreCollision(itemCollider, holder), Is.False,
                "놓으면 다시 부딪혀야 한다 — 안 풀면 그 물건만 영영 통과된다.");

            Object.DestroyImmediate(item.gameObject);
            Object.DestroyImmediate(player.gameObject);
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

        private static void Destroy(GameObject runtime, LastShiftPlayerController player, params LastShiftGrabbable[] items)
        {
            Object.DestroyImmediate(runtime);
            foreach (var item in items) Object.DestroyImmediate(item.gameObject);
            Object.DestroyImmediate(player.gameObject);
        }
    }
}
