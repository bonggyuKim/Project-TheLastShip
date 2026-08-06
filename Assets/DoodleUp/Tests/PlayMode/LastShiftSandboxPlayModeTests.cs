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
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController player;
        private LastShiftGrabbable[] items;

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

            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.path, Is.EqualTo(ScenePath));
            var roots = scene.GetRootGameObjects();
            sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();

            // 씬에는 승무원이 없다. 레벨이 하나가 되면서 플레이어는 접속 시 NGO 가 프리팹에서
            // 스폰하기 때문이다. 이 파일이 재는 것은 임무·악화 시계이지 네트워크가 아니므로
            // host 를 띄우지 않고 같은 프리팹을 직접 하나 세워 쓴다 — 시뮬레이션 검사 전부가
            // NGO 기동 비용과 접속 타이밍에 얹히면 그때부터 실패가 무엇 때문인지 안 갈린다.
            // 아이템은 이제 NetworkObject 다. host 없이 도는 이 파일에서 잡기(재부모화)를 하면
            // NGO 자동 부모 동기화가 "listening 이 아니다" 며 에러를 찍는다 — 검사 대상이 아니라
            // 소음이므로 끈다. 실제 잡기 복제는 LastShiftNetworkGrabbable 이 따로 한다.
            foreach (var grabbable in roots.SelectMany(root => root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true)))
                grabbable.AutoObjectParentSync = false;

            player = SpawnCrewFromPrefab();
            sandbox.Configure(new[] { player }, items);

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

            // 전력 시계는 <b>미연결 상한(0.40) 위에서만</b> 돈다 — 그 상한까지 끌어내리는 것이
            // 시계의 일이고, 이미 그 아래면 더 내릴 것이 없다(LastShiftRecovery.cs:472).
            //
            // 그래서 프리셋 초기값에 기대어 이 시계를 재지 않는다. balance 가 이 프리셋의
            // BusPower 를 0.40 으로 확정하면서(§2.2 A-3, t=0 에 S-P2 를 켜기 위해) 운석 하강분
            // 0.1034 까지 겹쳐 시작부터 상한 아래가 됐다. 그 값은 밸런스 판단이고 이 검사가
            // 지키려는 것은 "미연결이면 상한까지 내려간다" 는 규칙이지 특정 프리셋이 아니므로,
            // 상한 위 상태를 직접 만들어 재고 프리셋 값과의 결합을 끊는다.
            sandbox.SetBusPowerForTest(LastShiftRecoveryTuning.UnpoweredBusCeiling + 0.20f);
            var busBefore = sandbox.CurrentState.BusPower;
            sandbox.AdvanceMission(1f);
            Assert.That(sandbox.CurrentState.BusPower, Is.LessThan(busBefore), "bus 미연결 시 전력 시계가 움직여야 한다.");
            Assert.That(sandbox.CurrentState.BusPower,
                Is.GreaterThanOrEqualTo(LastShiftRecoveryTuning.UnpoweredBusCeiling),
                "전력 시계는 미연결 상한 아래로는 내려가지 않는다.");
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

            // 성능 포기는 <b>악화</b>를 멈춘다. 예전에는 "회복도 없다(그 자리에 얼어붙는다)" 로
            // 검사했는데, 그 동작은 자연 냉각(§0.2 HEAT_COOL)이 코드에 없어서 생긴 것이었고
            // S-H3 영구 잠금(RG-3 위반)의 원인이기도 했다. CT-07 에서 자연 냉각을 넣으면서
            // 포기 상태에서도 열은 내려간다 — 포기가 막는 것은 악화이지 물리적 방열이 아니고,
            // 포기했을 때만 잠금이 안 풀리면 RG-3 에 구멍이 남는다.
            //
            // 포기가 회복을 주지 않는다는 성질 자체는 여전히 검사한다: 능동 냉각률(0.030/s)이
            // 아니라 자연 냉각률(0.008/s)로만 내려가야 한다. 이 구분이 사라지면 "고쳤다" 와
            // "포기했다" 가 같은 결과가 되어 수리 3택의 비용 차이가 없어진다.
            var cooled = heatAtSacrifice - sandbox.CurrentState.EngineHeat;
            Assert.That(cooled, Is.EqualTo(LastShiftRecoveryTuning.HeatNaturalCoolPerSecond * 5f).Within(0.001f),
                "성능 포기 상태의 열은 자연 냉각률로만 내려가야 한다 — 능동 냉각과 같아지면 안 된다.");
            Assert.That(sandbox.CurrentState.ThrustDemand, Is.EqualTo(LastShiftRecoveryTuning.SacrificedThrustCeiling).Within(0.001f));

            GrantDockProgress();
            player.ResetPlayer(LastShiftSandboxController.DockingTriggerPosition);
            sandbox.AdvanceMission(0.01f);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.SuccessCompromised));

            yield break;
        }

        /// <summary>
        /// 도킹 누적 진행(CT-06 N4)을 목표까지 채운다.
        ///
        /// 이 테스트들이 겨누는 축은 누적이 아니라 수리 결과·질식·성공 판정이다. 실제로
        /// <c>150 thrust·s</c> 를 벌려면 추력 <c>0.50</c> 을 <c>300초</c> 유지해야 하는데, 그 시간을
        /// 밀면 이 테스트가 재는 <b>다른 시계</b>(예비 산소, 우회 수명, 도킹 타이머)가 함께 흘러
        /// 검사 대상 자체가 바뀐다. 누적 규칙은 EditMode <c>LastShiftFuelBudgetTests</c> 가 따로 본다.
        /// </summary>
        private void GrantDockProgress()
        {
            var state = sandbox.CurrentState;
            state.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds;
            sandbox.OverrideStateForProbe(state);
        }

        [UnityTest]
        public IEnumerator DockingOxygenAndTimeoutSettleAllMissionVerdictPaths()
        {
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            player.ResetPlayer(Vector3.zero);
            sandbox.AdvanceMission(0.01f);

            // CT-06 N4 이후 도킹은 순간 조건만으로 성립하지 않는다. 누적이 모자란 채 트리거에
            // 들어가면 성공이 아니라 Pending 이어야 한다 — 실패가 아니라 "아직 아니다" 다.
            player.ResetPlayer(LastShiftSandboxController.DockingTriggerPosition);
            sandbox.AdvanceMission(0.01f);
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.Pending),
                "추력·산소 성공선을 넘겨도 누적 진행이 모자라면 도킹이 성립하면 안 된다.");

            // 누적을 채우고 다시 들어온다. 트리거는 상주가 아니라 진입으로 판정하므로 한 번 나갔다 온다.
            GrantDockProgress();
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
            player.ResetPlayer(new Vector3(LastShiftShipDimensions.LifeSupportCenterX, 0.1f, -1.6f));
            // 산소실을 끊는 경계는 언제나 <b>마지막</b> 경계다. 번호 1 은 구역이 셋일 때만
            // 엔진실-산소실이었고, 넷이 된 뒤로는 전력실-냉각실이다(§2.1) — 그대로 두면
            // 엉뚱한 문을 닫고도 "격리했다" 고 믿게 되어 진공이 영영 도달하지 않는다.
            var sternBoundary = LastShiftZoneAtlas.BoundaryCount - 1;
            sandbox.SetDoorOpen(sternBoundary, false);
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

        /// <summary>
        /// CT-06 N3 — 연료가 바닥나고 도킹 진행이 모자라면 <b>타이머를 기다리지 않고</b> 표류로
        /// 끝난다(기획 §2.3 B-2). 아무것도 할 수 없는 채로 남은 시간을 지켜보게 두는 것이
        /// <c>RG-3</c> 이 금지한 영구 잠금이라 즉시 판정한다.
        /// </summary>
        [UnityTest]
        public IEnumerator EmptyFuelTankStrandsTheShipBeforeTheTimerRunsOut()
        {
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(sandbox.CurrentState.FuelReserve,
                Is.EqualTo(LastShiftRecoveryTuning.FuelReserveInitial).Within(0.0001f));

            // 연료만 마지막 한 방울로 줄인다. 진행도는 목표에 한참 못 미친 채로 둔다.
            var nearlyDry = sandbox.CurrentState;
            nearlyDry.FuelReserve = LastShiftRecoveryTuning.FuelDrainPerThrustSecond * 0.1f;
            nearlyDry.DockProgress = 0f;
            sandbox.OverrideStateForProbe(nearlyDry);

            var timerBefore = sandbox.DockingSecondsRemaining;
            sandbox.AdvanceMission(1f);

            Assert.That(sandbox.CurrentState.FuelReserve, Is.EqualTo(0f).Within(0.0001f),
                "추력을 유지했는데 연료가 안 말랐다.");
            Assert.That(sandbox.Verdict, Is.EqualTo(LastShiftVerdict.FailureAdrift),
                "연료가 0 이고 도킹도 못 채웠는데 판정이 안 났다.");
            Assert.That(sandbox.DockingSecondsRemaining, Is.GreaterThan(0f),
                "제한시간이 아직 남은 채로 끝나야 '기다리지 않고 즉시 판정' 이다.");
            Assert.That(sandbox.DockingSecondsRemaining, Is.LessThan(timerBefore),
                "제한시간이 계속 흐르고 있어야 한다 — N4 가 기존 타이머를 대체하면 안 된다.");

            yield break;
        }

        /// <summary>
        /// CT-06 N4 — 도킹 누적이 실제 항해 시간으로 쌓이는지 씬 위에서 확인한다.
        /// EditMode 는 순수 tick 을 보고, 여기서는 샌드박스가 그 tick 을 실제로 돌리는지를 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator DockProgressAccumulatesWhileTheMissionRuns()
        {
            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(sandbox.CurrentState.DockProgress, Is.EqualTo(0f).Within(0.0001f));

            var thrust = sandbox.CurrentState.ThrustDemand;
            sandbox.AdvanceMission(10f);

            Assert.That(sandbox.CurrentState.DockProgress, Is.EqualTo(thrust * 10f).Within(0.01f),
                "샌드박스 tick 이 도킹 진행을 쌓지 않는다.");
            Assert.That(sandbox.CurrentState.DockProgress,
                Is.LessThan(LastShiftRecoveryTuning.DockTargetThrustSeconds),
                "10초 만에 도킹 목표에 닿았다 — 누적이 사건이 되지 않는 수치다.");

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
