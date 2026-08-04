using System.Collections;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    public sealed class LastShiftSandboxPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP01.unity";

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController player;
        private LastShiftGrabbable[] items;

        [UnitySetUp]
        public IEnumerator LoadSoloScene()
        {
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.path, Is.EqualTo(ScenePath));
            var roots = scene.GetRootGameObjects();
            sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            player = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftPlayerController>(true)).Single();
            items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();

            // 이후 시간은 AdvanceMission 으로 정확히 밀어 프레임 시간과 assertion 사이의 경쟁을 없앤다.
            sandbox.enabled = false;
        }

        [UnityTest]
        public IEnumerator SavedSoloSceneLoadsAndRunsOneShotLifecycle()
        {
            var battery = Item(LastShiftItemRole.Battery);

            Assert.That(items.Length, Is.EqualTo(4));
            Assert.That(sandbox.HasAppliedImpact, Is.False);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.None));

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            var nominal = battery.NominalPosition;
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(sandbox.HasAppliedImpact, Is.True);
            Assert.That(sandbox.ImpactApplicationCount, Is.EqualTo(1));
            Assert.That(battery.transform.position, Is.Not.EqualTo(nominal));
            Assert.That(battery.DisplacementFromNominal, Is.GreaterThan(0f));
            Assert.That(sandbox.ApplyMeteorImpact(), Is.False);

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.HasAppliedImpact, Is.False);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.None));
            Assert.That(battery.transform.position, Is.EqualTo(nominal));
        }

        [UnityTest]
        public IEnumerator MeteorStartsThreeIndependentDeteriorationClocks()
        {
            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            var heatBefore = sandbox.CurrentState.EngineHeat;
            sandbox.AdvanceMission(1f);
            Assert.That(sandbox.CurrentState.EngineHeat, Is.GreaterThan(heatBefore), "냉각 미복구 시 열 시계가 움직여야 한다.");

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            var busBefore = sandbox.CurrentState.BusPower;
            sandbox.AdvanceMission(1f);
            Assert.That(sandbox.CurrentState.BusPower, Is.LessThan(busBefore), "bus 미연결 시 전력 시계가 움직여야 한다.");
            Assert.That(sandbox.SteeringDelayed, Is.True);

            sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            var oxygenBefore = sandbox.CurrentState.OxygenPressure;
            sandbox.AdvanceMission(1f);
            Assert.That(sandbox.CurrentState.OxygenPressure, Is.LessThan(oxygenBefore), "파공 미봉합 시 산소 시계가 움직여야 한다.");

            yield break;
        }

        [UnityTest]
        public IEnumerator ThreeItemsRestoreTheirSystemsAndRepairModesHaveDistinctConsequences()
        {
            AssertSafeRestore(
                LastShiftPreset.HighHeatHighThrust,
                LastShiftItemRole.CoolingCanister,
                LastShiftShipSystem.Cooling);
            AssertSafeRestore(
                LastShiftPreset.PowerOverloadLooseBattery,
                LastShiftItemRole.Battery,
                LastShiftShipSystem.Power);
            AssertSafeRestore(
                LastShiftPreset.BadAttitudeHighOxygen,
                LastShiftItemRole.PatchPlate,
                LastShiftShipSystem.Oxygen);

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            PlaceHeldItemAtNominal(Item(LastShiftItemRole.Battery));
            Assert.That(sandbox.TryBeginRepair(LastShiftRepairMode.QuickBypass), Is.True);
            sandbox.AdvanceMission(LastShiftRecoveryTuning.QuickBypassSeconds);
            Assert.That(sandbox.IsSystemRestored(LastShiftShipSystem.Power), Is.True);
            Assert.That(
                sandbox.Repairs.BypassRemaining(LastShiftShipSystem.Power),
                Is.EqualTo(LastShiftRecoveryTuning.QuickBypassLifetimeSeconds).Within(0.001f));

            sandbox.AdvanceMission(LastShiftRecoveryTuning.QuickBypassLifetimeSeconds + 0.01f);
            Assert.That(sandbox.BypassLapseCount, Is.EqualTo(1));
            Assert.That(sandbox.IsSystemRestored(LastShiftShipSystem.Power), Is.False);
            Assert.That(Item(LastShiftItemRole.Battery).Secured, Is.False);

            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            var cooling = Item(LastShiftItemRole.CoolingCanister);
            player.ResetPlayer(cooling.NominalPosition);
            var heatAtSacrifice = sandbox.CurrentState.EngineHeat;
            Assert.That(sandbox.TryBeginRepair(LastShiftRepairMode.PerformanceSacrifice), Is.True);
            Assert.That(sandbox.Repairs.IsSacrificed(LastShiftShipSystem.Cooling), Is.True);
            Assert.That(sandbox.IsSystemRestored(LastShiftShipSystem.Cooling), Is.False);
            Assert.That(sandbox.SacrificeCount, Is.EqualTo(1));
            Assert.That(sandbox.ThrustCeiling, Is.EqualTo(LastShiftRecoveryTuning.SacrificedThrustCeiling).Within(0.001f));

            sandbox.AdvanceMission(5f);
            Assert.That(sandbox.CurrentState.EngineHeat, Is.EqualTo(heatAtSacrifice).Within(0.001f),
                "성능 포기는 악화를 멈추지만 상태를 회복하지 않아야 한다.");
            Assert.That(sandbox.CurrentState.ThrustDemand, Is.EqualTo(LastShiftRecoveryTuning.SacrificedThrustCeiling).Within(0.001f));

            player.ResetPlayer(LastShiftSandboxController.DockingTriggerPosition);
            sandbox.AdvanceMission(0.01f);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.SuccessCompromised));

            yield break;
        }

        [UnityTest]
        public IEnumerator DockingOxygenAndTimeoutSettleAllMissionVerdictPaths()
        {
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            player.ResetPlayer(Vector3.zero);
            sandbox.AdvanceMission(0.01f);
            player.ResetPlayer(LastShiftSandboxController.DockingTriggerPosition);
            sandbox.AdvanceMission(0.01f);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.SuccessNominalDocking));

            // CT-05 N2: 질식 실패는 이제 선체 압력이 아니라 전원의 개인 예비 산소가 바닥나야 난다.
            // 고정 시간으로 재면 안 된다 — 진공 도달은 프리셋 누출률에서 나오는 파생 시점이라
            // 튜닝이 바뀌면 "압력 0 이지만 예비는 남은" 창을 통째로 지나쳐 버린다. 실제 도달
            // 시점까지 민 뒤 그 자리에서 확인한다.
            sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            // N0 이후 압력은 구역별이다. 파공은 산소실이고 승무원 스폰은 조종석이라, 승무원을
            // 옮기지 않으면 질식 경로가 성립하지 않는다. 조종석까지 진공이 되기를 기다리는 것은
            // 답이 아니다 — 전체 공기(2.86)가 파공 하나로만 빠지므로 최소 394초가 필요하고,
            // 300초 타이머가 먼저 끝난다. 평준화는 공기를 옮길 뿐 없애지 않아 이 하한은 못 내린다.
            // 그리고 그 구역을 격리한다(§2.2.2). 문이 열려 있으면 평준화가 나머지 두 구역의
            // 공기를 계속 밀어 넣어 배 전체가 함께 내려갈 뿐, 300초 안에 어느 구역도 진공에
            // 닿지 않는다. 문을 닫아야 파공 구역이 자기 공기만으로 빠져 129초에 진공이 된다.
            player.ResetPlayer(new Vector3(4.5f, 0.1f, -1.6f));
            sandbox.SetDoorOpen(1, false);
            for (var i = 0; i < 2000 && sandbox.PressureOf(sandbox.BreachZone) > LastShiftRecoveryTuning.VacuumOxygenPressure; i++)
                sandbox.AdvanceMission(1f);
            Assert.That(sandbox.PressureOf(sandbox.BreachZone), Is.EqualTo(0f).Within(0.0001f),
                "누출이 계속되면 파공 구역 압력은 진공까지 내려가야 한다.");
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.Pending),
                "압력이 0 이어도 예비 산소가 남아 있는 동안은 실패가 아니다.");
            sandbox.AdvanceMission(LastShiftRecoveryTuning.SuitOxygenInitial / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond + 1f);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.FailureAsphyxiation));

            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            sandbox.AdvanceMission(LastShiftRecoveryTuning.DockingTimerSeconds);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.FailureInsufficientThrust));
            Assert.That(sandbox.DockingSecondsRemaining, Is.Zero);

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            sandbox.AdvanceMission(LastShiftRecoveryTuning.DockingTimerSeconds);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.FailureAdrift));
            Assert.That(sandbox.DockingSecondsRemaining, Is.Zero);

            yield break;
        }

        [UnityTest]
        public IEnumerator HeatProtectionLocksThrustAtQuarterEvenAgainstNewInput()
        {
            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);

            sandbox.AdvanceMission(5f);

            Assert.That(sandbox.CurrentState.EngineHeat, Is.EqualTo(LastShiftRecoveryTuning.HeatProtectionTrigger).Within(0.001f));
            Assert.That(sandbox.HeatProtectionEngaged, Is.True);
            Assert.That(sandbox.ThrustCeiling, Is.EqualTo(LastShiftRecoveryTuning.ProtectedThrustCeiling).Within(0.001f));
            Assert.That(sandbox.CurrentState.ThrustDemand, Is.EqualTo(LastShiftRecoveryTuning.ProtectedThrustCeiling).Within(0.001f));

            sandbox.ApplyControl(0.9f, sandbox.CurrentState.ShipAttitudeDegrees);
            Assert.That(sandbox.CurrentState.ThrustDemand, Is.EqualTo(LastShiftRecoveryTuning.ProtectedThrustCeiling).Within(0.001f));

            yield break;
        }

        private void AssertSafeRestore(
            LastShiftPreset preset,
            LastShiftItemRole role,
            LastShiftShipSystem system)
        {
            sandbox.ResetPreset(preset);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            PlaceHeldItemAtNominal(Item(role));

            Assert.That(sandbox.TryBeginRepair(LastShiftRepairMode.SafeRestore), Is.True);
            Assert.That(sandbox.Repairs.IsChanneling(system), Is.True);
            sandbox.AdvanceMission(LastShiftRecoveryTuning.SafeRestoreSeconds);

            Assert.That(sandbox.Repairs.ModeOf(system), Is.EqualTo(LastShiftRepairMode.SafeRestore));
            Assert.That(sandbox.IsSystemRestored(system), Is.True);
            Assert.That(Item(role).Secured, Is.True);
            Assert.That(player.HeldItem, Is.Null, "완료 후 holder 참조도 함께 비워져야 한다.");
        }

        private void PlaceHeldItemAtNominal(LastShiftGrabbable item)
        {
            player.HoldSocket.position = item.NominalPosition;
            Assert.That(player.TryGrabForProbe(item), Is.True);
            Assert.That(Vector3.Distance(item.transform.position, item.NominalPosition), Is.LessThan(0.0001f));
        }

        private LastShiftGrabbable Item(LastShiftItemRole role)
        {
            return items.Single(item => item.Role == role);
        }
    }
}
