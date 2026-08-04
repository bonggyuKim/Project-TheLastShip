using System.Collections;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// CT-05 수용 기준 1~6. 3단 구조(사이렌 0.15 / 진공 0.00 / 사망 예비 0.00)가 실제로
    /// 분리되어 있는지, 그리고 1명 사망으로 게임이 끝나지 않는지를 검증한다.
    ///
    /// 시간은 <see cref="LastShiftSandboxController.AdvanceMission"/> 으로 직접 민다.
    /// 프레임 시간에 맡기면 80초 소모를 실시간으로 기다려야 하고 assertion 과 경쟁한다.
    /// </summary>
    public sealed class LastShiftCrewOxygenPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP01.unity";

        /// <summary>예비 산소 1.00 을 다 태우는 시간. 상수에서 유도해 튜닝이 바뀌어도 따라온다.</summary>
        private static float SuitOxygenLifetime =>
            LastShiftRecoveryTuning.SuitOxygenInitial / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond;

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController player;

        [UnitySetUp]
        public IEnumerator LoadSoloScene()
        {
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            player = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftPlayerController>(true)).Single();
            sandbox.enabled = false;
        }

        /// <summary>수용 기준 1: 압력 0.15 에서 사이렌은 울리되 예비 산소는 줄지 않는다.</summary>
        [UnityTest]
        public IEnumerator SirenSoundsAtLowPressureWithoutTouchingSuitOxygen()
        {
            var crew = ArmLeakPreset();
            Assert.That(sandbox.SirenActive, Is.False, "운석 직후 압력 0.96 에서는 사이렌이 울리면 안 된다.");

            AdvanceUntilPressureAtOrBelow(LastShiftRecoveryTuning.OxygenSirenTrigger);

            Assert.That(sandbox.SirenActive, Is.True, "압력 0.15 이하에서 전선 사이렌이 켜져야 한다.");
            Assert.That(sandbox.CurrentState.OxygenPressure, Is.GreaterThan(LastShiftRecoveryTuning.VacuumOxygenPressure),
                "사이렌 시점은 아직 진공이 아니어야 한다 — 두 임계가 겹치면 대응 창이 사라진다.");
            Assert.That(crew.SuitOxygen, Is.EqualTo(LastShiftRecoveryTuning.SuitOxygenInitial).Within(0.0001f),
                "사이렌만으로는 예비 산소가 줄지 않아야 한다.");
            Assert.That(crew.IsDraining, Is.False);
            Assert.That(crew.ShowsSuitGauge, Is.False, "소모 전에는 예비 막대가 뜨지 않아야 한다(N8).");
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.Pending));

            yield break;
        }

        /// <summary>수용 기준 2 + 6: 압력 0.00 에서 소모가 시작되고 80초 뒤 사망한다. 그 사이 막대가 뜬다.</summary>
        [UnityTest]
        public IEnumerator VacuumDrainsSuitOxygenAndKillsCrewAfterEightySeconds()
        {
            var crew = ArmLeakPreset();
            AdvanceUntilPressureAtOrBelow(LastShiftRecoveryTuning.VacuumOxygenPressure);
            Assert.That(crew.SuitOxygen, Is.EqualTo(LastShiftRecoveryTuning.SuitOxygenInitial).Within(0.02f),
                "진공 도달 직후에는 예비가 거의 온전해야 한다.");

            sandbox.AdvanceMission(SuitOxygenLifetime * 0.5f);
            Assert.That(crew.IsDraining, Is.True, "진공 구역에서는 소모가 돌아야 한다.");
            Assert.That(crew.ShowsSuitGauge, Is.True, "소모가 시작된 승무원에게 막대가 떠야 한다(N8).");
            Assert.That(crew.SuitOxygen, Is.LessThan(0.6f).And.GreaterThan(0.4f),
                "절반 시점의 잔량이 소모율과 맞아야 한다.");
            Assert.That(crew.IsCritical, Is.False, "0.5 는 아직 임계 점멸 구간이 아니다.");
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.Pending), "예비가 남아 있으면 실패가 아니다.");

            // 0.25 이하 = 적색 점멸 + 호흡음 증폭 구간(N8).
            sandbox.AdvanceMission(SuitOxygenLifetime * 0.3f);
            Assert.That(crew.SuitOxygen, Is.LessThanOrEqualTo(LastShiftRecoveryTuning.SuitOxygenCriticalThreshold));
            Assert.That(crew.IsCritical, Is.True, "0.25 이하에서 임계 표시가 켜져야 한다.");

            sandbox.AdvanceMission(SuitOxygenLifetime * 0.3f);
            Assert.That(crew.SuitOxygen, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(crew.IsDead, Is.True, "예비 0.00 에서 해당 승무원이 사망해야 한다.");
            Assert.That(crew.IsDraining, Is.False, "사망 후에는 소모가 멈춘다.");
            Assert.That(crew.ShowsSuitGauge, Is.True, "사망 상태도 막대로 보여야 한다.");
            Assert.That(player.enabled, Is.False, "사망한 승무원은 조작할 수 없어야 한다.");

            yield break;
        }

        /// <summary>수용 기준 3: 압력이 0.00 위로 회복되면 소모가 멈춘다(회복은 없다).</summary>
        [UnityTest]
        public IEnumerator RecoveringPressureStopsDrainWithoutRefillingReserve()
        {
            var crew = ArmLeakPreset();
            AdvanceUntilPressureAtOrBelow(LastShiftRecoveryTuning.VacuumOxygenPressure);
            sandbox.AdvanceMission(SuitOxygenLifetime * 0.25f);
            var drained = crew.SuitOxygen;
            Assert.That(crew.IsDraining, Is.True);
            Assert.That(drained, Is.LessThan(LastShiftRecoveryTuning.SuitOxygenInitial));

            // 압력만 0.00 위로 되돌린다. 여기서 검증하려는 것은 "소모 정지" 하나다.
            var restored = sandbox.CurrentState;
            restored.OxygenPressure = 0.30f;
            sandbox.OverrideStateForProbe(restored);
            sandbox.AdvanceMission(SuitOxygenLifetime * 0.25f);

            Assert.That(crew.IsDraining, Is.False, "압력이 0.00 위면 소모가 멈춰야 한다.");
            Assert.That(crew.SuitOxygen, Is.EqualTo(drained).Within(0.0001f),
                "예비 산소는 항해 1회 예산이라 회복되지 않는다.");
            Assert.That(crew.IsDead, Is.False);
            Assert.That(crew.ShowsSuitGauge, Is.True, "이미 깎인 예비는 계속 보여야 한다.");

            yield break;
        }

        /// <summary>수용 기준 4 + 5: 2인 중 1명 사망은 게임을 끝내지 않고, 전원 사망만 실패다.</summary>
        [UnityTest]
        public IEnumerator OneDeathContinuesMissionAndOnlyFullWipeFails()
        {
            var secondPlayer = ClonePlayerForSecondCrew();
            sandbox.Configure(new[] { player, secondPlayer }, sandbox.Items);
            var first = ArmLeakPreset();
            var second = sandbox.CrewOxygenOf(secondPlayer);

            AdvanceUntilPressureAtOrBelow(LastShiftRecoveryTuning.VacuumOxygenPressure);
            // 압력은 선체 전체 값이라 두 승무원이 같은 진공을 공유한다. 먼저 전원 사망 경로를
            // 확인하고(수용 기준 5), 1명만 죽은 상태는 아래에서 따로 조립한다.
            sandbox.AdvanceMission(SuitOxygenLifetime + 1f);
            Assert.That(first.IsDead, Is.True);
            Assert.That(second.IsDead, Is.True, "같은 진공에 있었다면 두 승무원 모두 죽는다.");
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.FailureAsphyxiation),
                "전원 사망일 때만 질식 실패가 난다(수용 기준 5).");

            // 1명만 죽은 상태를 다시 만든다: 리셋 후 2번 승무원의 예비만 손대지 않고
            // 1번만 진공에 노출시킨 것과 동등하게, 사망 직후 시점을 직접 조립한다.
            sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(first.IsDead, Is.False, "리셋은 예비 산소를 되돌린다.");
            Assert.That(second.IsDead, Is.False);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);

            first.KillForProbe();
            // 스폰 지점이 이미 도킹 트리거 반경 안이라 판정은 "진입 엣지" 다. 두 승무원을 모두
            // 반경 밖으로 빼서 엣지 기준을 초기화하지 않으면 이후 이동이 진입으로 잡히지 않는다.
            player.ResetPlayer(Vector3.zero);
            secondPlayer.ResetPlayer(Vector3.zero);
            sandbox.AdvanceMission(0.01f);
            Assert.That(sandbox.LivingCrewCount, Is.EqualTo(1));
            Assert.That(sandbox.AnyCrewAlive, Is.True);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.Pending),
                "1명 사망으로 게임이 끝나면 안 된다(수용 기준 4).");

            // 남은 1명으로 도킹 성공이 가능해야 한다. 사망자를 먼저 트리거에 넣어
            // "시신은 도킹을 성립시키지 못한다" 를 확인한 뒤 생존자를 들여보낸다.
            var docking = sandbox.CurrentState;
            docking.ThrustDemand = LastShiftRecoveryTuning.DockingSuccessThrust + 0.05f;
            docking.OxygenPressure = LastShiftRecoveryTuning.DockingSuccessOxygen + 0.05f;
            sandbox.OverrideStateForProbe(docking);
            player.ResetPlayer(LastShiftSandboxController.DockingTriggerPosition);
            sandbox.AdvanceMission(0.01f);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.Pending),
                "사망한 승무원이 트리거에 있어도 도킹은 성립하지 않아야 한다.");

            sandbox.OverrideStateForProbe(docking);
            secondPlayer.ResetPlayer(LastShiftSandboxController.DockingTriggerPosition);
            sandbox.AdvanceMission(0.01f);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.SuccessNominalDocking),
                "남은 1명으로 도킹 성공이 가능해야 한다(수용 기준 4).");

            Object.Destroy(secondPlayer.gameObject);
            yield break;
        }

        /// <summary>
        /// 산소 계통 성능 포기(구역 차단)는 선체 압력과 무관하게 그 구역만 진공으로 만든다.
        /// 이게 "구역별 진공" 의 유일한 실제 경로라 별도로 못박아 둔다.
        /// </summary>
        [UnityTest]
        public IEnumerator SealedZoneIsVacuumRegardlessOfHullPressure()
        {
            ArmLeakPreset();
            Assert.That(sandbox.CurrentState.OxygenPressure, Is.GreaterThan(LastShiftRecoveryTuning.VacuumOxygenPressure));

            var patch = sandbox.Items.Single(item => item.Role == LastShiftItemRole.PatchPlate);
            player.HoldSocket.position = patch.NominalPosition;
            Assert.That(player.TryGrabForProbe(patch), Is.True);
            player.ResetPlayer(patch.NominalPosition);
            Assert.That(sandbox.TryBeginRepair(LastShiftRepairMode.PerformanceSacrifice), Is.True);
            sandbox.AdvanceMission(LastShiftRecoveryTuning.PerformanceSacrificeSeconds + 0.01f);
            Assert.That(sandbox.Repairs.IsSacrificed(LastShiftShipSystem.Oxygen), Is.True);

            // 밀폐한 생명유지 구역(x >= 2)만 진공이고, 조종석(x <= -2)은 아니다.
            Assert.That(sandbox.IsZoneVacuum(new Vector3(4.5f, 0.6f, -1.6f)), Is.True);
            Assert.That(sandbox.IsZoneVacuum(new Vector3(-4f, 0.6f, 0f)), Is.False);

            yield break;
        }

        /// <summary>운석까지 적용해 산소가 실제로 새기 시작하는 상태를 만든다.</summary>
        private LastShiftCrewOxygen ArmLeakPreset()
        {
            sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            var crew = sandbox.CrewOxygenOf(player);
            Assert.That(crew, Is.Not.Null);
            Assert.That(crew.SuitOxygen, Is.EqualTo(LastShiftRecoveryTuning.SuitOxygenInitial).Within(0.0001f));
            return crew;
        }

        /// <summary>
        /// 목표 압력까지 1초씩 민다. 한 번에 크게 밀면 사이렌선과 진공선을 같은 tick 에 지나쳐
        /// "사이렌만 울리는 구간" 자체를 관측할 수 없다.
        /// </summary>
        private void AdvanceUntilPressureAtOrBelow(float pressure)
        {
            for (var i = 0; i < 2000 && sandbox.CurrentState.OxygenPressure > pressure; i++)
                sandbox.AdvanceMission(1f);
            Assert.That(sandbox.CurrentState.OxygenPressure, Is.LessThanOrEqualTo(pressure + 0.0001f),
                $"압력이 {pressure:F2} 까지 내려가지 않았다.");
        }

        /// <summary>
        /// 2인 검증용 승무원. 씬에는 솔로 승무원 하나뿐이라 같은 구성으로 하나 더 만든다.
        /// </summary>
        private LastShiftPlayerController ClonePlayerForSecondCrew()
        {
            var playerObject = new GameObject("Crew2");
            playerObject.transform.position = LastShiftSandboxController.PlayerSpawn;
            playerObject.AddComponent<CharacterController>();
            var cameraObject = new GameObject("Crew2Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            var socket = new GameObject("Crew2HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            var controller = playerObject.AddComponent<LastShiftPlayerController>();
            // 슬롯을 나눠야 HUD 막대 두 줄이 서로 다른 승무원으로 읽힌다.
            controller.Configure(camera, socket, LastShiftPlayerSlot.PlayerTwo, new Color(1f, 0.6f, 0.2f));
            return controller;
        }
    }
}
