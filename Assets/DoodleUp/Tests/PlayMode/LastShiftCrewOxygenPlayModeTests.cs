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
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        /// <summary>예비 산소 1.00 을 다 태우는 시간. 상수에서 유도해 튜닝이 바뀌어도 따라온다.</summary>
        private static float SuitOxygenLifetime =>
            LastShiftRecoveryTuning.SuitOxygenInitial / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond;

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController player;

        [UnitySetUp]
        public IEnumerator LoadSoloScene()
        {
            // 이 파일은 네트워크가 아니라 시계를 잰다. 씬이 하나가 되면서 이 씬을 열기만 해도
            // host 가 자동으로 뜨는데, 테스트마다 같은 UDP 포트를 잡으면 앞 테스트의 host 가
            // 내려가기 전에 다음이 떠서 SetUp 부터 죽는다. 로드 전에 꺼 둔다.
            LastShiftNetworkSession.AutoStartHostInEditor = false;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            var items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();

            // 씬에는 승무원이 없다 — 레벨이 하나가 되면서 플레이어는 접속 시 NGO 가 프리팹에서
            // 스폰한다. 이 파일이 재는 것은 산소 시계이지 네트워크가 아니므로 host 를 띄우지 않고
            // 같은 프리팹으로 승무원 하나를 직접 세운다.
            // 아이템은 이제 NetworkObject 다. host 없이 도는 이 파일에서 잡기(재부모화)를 하면
            // NGO 자동 부모 동기화가 "listening 이 아니다" 며 에러를 찍는다 — 검사 대상이 아니라
            // 소음이므로 끈다. 실제 잡기 복제는 LastShiftNetworkGrabbable 이 따로 한다.
            foreach (var grabbable in roots.SelectMany(root => root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true)))
                grabbable.AutoObjectParentSync = false;

            player = SpawnCrewFromPrefab();
            sandbox.Configure(new[] { player }, items);
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
            Assert.That(sandbox.PressureOf(sandbox.BreachZone), Is.GreaterThan(LastShiftRecoveryTuning.VacuumOxygenPressure),
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
            // CT-08 N11: 사망은 조작을 뺏되 이동은 남긴다. 컨트롤러를 통째로 끄면 유령이
            // 성립하지 않으므로, "조작 불가" 의 판정 대상이 enabled 에서 IsGhost 로 옮겨졌다.
            Assert.That(player.IsGhost, Is.True, "사망한 승무원은 유령이 되어야 한다(기획 §4.4).");
            Assert.That(player.enabled, Is.True, "유령은 이동 제약만 잃는다 — 컨트롤러는 살아 있다.");
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.False,
                "몸이 없으므로 콜라이더는 꺼져야 한다.");

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
            // 승무원이 선 곳은 파공 구역이므로 되돌릴 대상도 그 구역 압력이다. 세 구역을 함께
            // 올려 두어야 다음 tick 의 평준화가 도로 0 으로 끌어내리지 않는다.
            sandbox.OverrideZonePressuresForProbe(LastShiftZonePressures.Uniform(0.30f));
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

            // 두 승무원을 같은 파공 구역에 세운다. N0 이후 진공은 구역별이므로 "같은 진공을
            // 공유한다" 는 전제가 자동으로 성립하지 않는다 — 같은 구역에 있어야 성립한다.
            secondPlayer.ResetPlayer(BreachZoneStandingPosition);
            AdvanceUntilPressureAtOrBelow(LastShiftRecoveryTuning.VacuumOxygenPressure);
            // 먼저 전원 사망 경로를 확인하고(수용 기준 5), 1명만 죽은 상태는 아래에서 따로 조립한다.
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
            // CT-06 N4: 순간 조건 둘에 더해 누적 진행도 채워야 도킹이 성립한다. 이 테스트가
            // 겨누는 것은 "시신은 도킹을 성립시키지 못한다" 이므로 누적은 조건에서 빼 준다.
            docking.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds;
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

            // 밀폐한 생명유지 구역만 진공이고, 조종석은 아니다. 좌표는 구역 중심에서 뽑는다 —
            // 리터럴을 두면 경계가 옮겨갈 때 두 표본이 같은 구역으로 무너져 검사가 사라진다.
            Assert.That(sandbox.IsZoneVacuum(new Vector3(LastShiftShipDimensions.LifeSupportCenterX, 0.6f, -1.6f)), Is.True);
            Assert.That(sandbox.IsZoneVacuum(new Vector3(LastShiftShipDimensions.CockpitCenterX, 0.6f, 0f)), Is.False);

            yield break;
        }

        /// <summary>
        /// 운석까지 적용해 산소가 실제로 새기 시작하는 상태를 만들고, 승무원을 <b>파공 구역 안</b>에
        /// 세운다.
        ///
        /// N0 이전에는 압력이 배 전체 단일 값이라 승무원이 어디에 서 있든 같은 진공을 겪었고,
        /// 스폰 지점(조종석)에 둔 채로 검증할 수 있었다. 구역이 분리된 뒤로는 그 전제가 사라진다 —
        /// 파공은 산소실이고 조종석은 두 경계 건너편이라, 조종석 승무원을 죽이려면 배 전체가
        /// 진공이 될 때까지 기다려야 한다. 그건 공기 보존상 300초 타이머 안에 일어나지 않는다
        /// (전체 공기 2.86 / 누출 0.00726 = 394초. 평준화를 아무리 올려도 공기를 옮길 뿐 없애지
        /// 못하므로 이 하한은 내려가지 않는다).
        ///
        /// 그래서 승무원을 파공 구역에 세운다. 검증 기준을 낮춘 것이 아니라 <b>판정 대상을
        /// 구역으로 옮긴 것</b>이다. 기획 §2.2 A-2 의 사망 규칙 자체가 "구역 0.00 도달 시
        /// <b>그 구역에 있는</b> 승무원의 예비 산소 소모 시작" 이므로, 파공 구역에 선 승무원이
        /// 정확히 그 규칙의 대상이다.
        /// </summary>
        private LastShiftCrewOxygen ArmLeakPreset()
        {
            sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            player.ResetPlayer(BreachZoneStandingPosition);
            Assert.That(LastShiftZoneAtlas.Resolve(player.transform.position), Is.EqualTo(sandbox.BreachZone),
                "승무원이 파공 구역 안에 서 있어야 이 묶음의 사망 경로가 성립한다.");
            IsolateBreachZone();
            var crew = sandbox.CrewOxygenOf(player);
            Assert.That(crew, Is.Not.Null);
            Assert.That(crew.SuitOxygen, Is.EqualTo(LastShiftRecoveryTuning.SuitOxygenInitial).Within(0.0001f));
            return crew;
        }

        /// <summary>파공 구역(산소실) 안의 서 있을 자리. PatchPlate 정위치와 같은 구역이다.</summary>
        private static Vector3 BreachZoneStandingPosition =>
            new(LastShiftShipDimensions.LifeSupportCenterX, 0.1f, -1.6f);

        /// <summary>
        /// 파공 구역을 격리한다(엔진실↔산소실 문을 닫는다). 이게 있어야 진공이 도달 가능해진다.
        ///
        /// 문이 열린 채로는 평준화가 나머지 두 구역의 공기를 파공 구역으로 계속 밀어 넣어,
        /// 배 전체가 함께 천천히 내려갈 뿐 어느 구역도 300초 안에 0.00 에 닿지 않는다
        /// (타이머 만료 시점 파공 구역 0.13). 문을 닫으면 파공 구역이 자기 공기만으로
        /// 빠지므로 129초에 진공, 사망은 209초 — 타이머 안이다.
        ///
        /// 이것은 검증을 쉽게 하려는 우회가 아니라 기획 §2.2.2 의 격리 그 자체다. 격리의
        /// 대가가 "그 안의 승무원 고립" 이므로, 파공 구역에 갇힌 승무원이 예비 산소를 태우다
        /// 죽는 경로는 격리를 켰을 때 성립하는 정규 경로다.
        /// </summary>
        private void IsolateBreachZone()
        {
            // 산소실을 끊는 경계는 언제나 <b>마지막</b> 경계다. 번호 1 은 구역이 셋일 때만
            // 엔진실-산소실이었고, 넷이 된 뒤로는 전력실-냉각실이다(§2.1) — 그대로 두면
            // 엉뚱한 문을 닫고도 "격리했다" 고 믿게 되어 진공이 영영 도달하지 않는다.
            var sternBoundary = LastShiftZoneAtlas.BoundaryCount - 1;
            sandbox.SetDoorOpen(sternBoundary, false);
            Assert.That(sandbox.IsDoorOpen(sternBoundary), Is.False, "산소실 격리가 걸려야 진공이 도달 가능하다.");
        }

        /// <summary>
        /// 목표 압력까지 1초씩 민다. 한 번에 크게 밀면 사이렌선과 진공선을 같은 tick 에 지나쳐
        /// "사이렌만 울리는 구간" 자체를 관측할 수 없다.
        ///
        /// 보는 값은 <b>파공 구역</b> 압력이다. 조종석 파생값(<c>CurrentState.OxygenPressure</c>)을
        /// 보면 위 <see cref="ArmLeakPreset"/> 주석의 394초 하한에 걸려 타이머가 먼저 끝난다.
        /// 임계값(0.15 / 0.00)은 그대로다 — 기준이 아니라 대상만 구역으로 옮겼다.
        /// </summary>
        private void AdvanceUntilPressureAtOrBelow(float pressure)
        {
            for (var i = 0; i < 2000 && sandbox.PressureOf(sandbox.BreachZone) > pressure; i++)
                sandbox.AdvanceMission(1f);
            Assert.That(sandbox.PressureOf(sandbox.BreachZone), Is.LessThanOrEqualTo(pressure + 0.0001f),
                $"파공 구역 압력이 {pressure:F2} 까지 내려가지 않았다.");
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

        /// <summary>
        /// 세션이 들고 있는 플레이어 프리팹으로 승무원 하나를 세운다. 경로를 테스트가 따로 적으면
        /// 빌더가 프리팹을 옮겼을 때 여기만 조용히 뒤처진다.
        /// </summary>
        private static LastShiftPlayerController SpawnCrewFromPrefab()
        {
            var session = Object.FindAnyObjectByType<LastShiftNetworkSession>();
            Assert.That(session, Is.Not.Null, "network session missing from the scene");
            var prefab = session.PlayerPrefab;
            Assert.That(prefab, Is.Not.Null, "session is not wired to a player prefab");
            var crew = Object.Instantiate(prefab.gameObject);
            crew.name = "PlayerOne";
            var controller = crew.GetComponent<LastShiftPlayerController>();
            Assert.That(controller, Is.Not.Null, "player prefab must carry LastShiftPlayerController");
            controller.transform.position = LastShiftShipDimensions.SpawnPoint;
            return controller;
        }

        [OneTimeTearDown]
        public void RestoreAutoHost()
        {
            // 껐던 것을 되돌린다. 정적 값이라 안 되돌리면 같은 Play 세션의 네트워크 테스트가
            // host 없이 돌아 원인을 알 수 없는 실패가 된다.
            LastShiftNetworkSession.AutoStartHostInEditor = true;
        }

        [UnityTearDown]
        public IEnumerator ShutDownSessionBetweenTests()
        {
            // 레벨이 하나가 되면서 이 파일도 네트워크 씬을 연다. 세션을 안 내리고 나가면
            // NetworkManager 싱글턴과 직접 세운 승무원이 다음 픽스처까지 살아남아, 네트워크
            // 테스트에서 슬롯 할당이 어긋나고 sandbox.Players 가 빈 채로 남는다 — 격리로
            // 돌리면 통과하고 전체로 돌리면 실패하는, 원인 찾기 가장 나쁜 형태가 된다.
            if (player != null) Object.Destroy(player.gameObject);

            // Shutdown 만으로는 부족하다. NGO 는 host 기동 때 NetworkManager 를
            // DontDestroyOnLoad 로 올리고 Shutdown 은 오브젝트를 지우지 않으므로, 씬을 다시
            // 로드하면 옛 세션과 새 세션이 함께 존재한다. 그러면
            // LastShiftNetworkPlayer.OnNetworkSpawn 의 FindFirstObjectByType 가 옛 세션을
            // 집을 수 있고, 등록이 이미 파괴된 sandbox 로 가서 새 씬의 sandbox.Players 는
            // 빈 채로 남는다 — 격리로는 통과하고 전체로는 실패하는 형태가 된다.
            var manager = Object.FindFirstObjectByType<Unity.Netcode.NetworkManager>(FindObjectsInactive.Include);
            if (manager != null)
            {
                if (manager.IsListening) manager.Shutdown();
                Object.Destroy(manager.gameObject);
            }

            yield return null;
        }
    }
}
