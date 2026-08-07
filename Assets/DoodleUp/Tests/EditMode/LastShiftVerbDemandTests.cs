using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <c>CT-08</c> 동사 수요 회복 — <c>docs/interaction-verb-diversification-v1.md</c> §4.1·§4.2·§4.3.
    ///
    /// <b>이 파일이 고정하는 것은 수치가 아니라 판정 조건이다.</b> §4.1 이 명시적으로 갈라
    /// 둔 경계가 그것이다 — <c>0.018</c> 이라는 값은 <c>game-balance</c> 가 언제든 다시 잡을 수
    /// 있고(§7-1), 기획이 고정한 것은 두 문장이다:
    ///
    /// <list type="number">
    ///   <item>문을 전부 열어 둔 채 방치하면 <b>P0 타이머 안에</b> 산소 성공선을 잃는다</item>
    ///   <item>격리하면 파공 구역이 <b>예비 산소 지속시간 안에</b> 진공이 된다</item>
    /// </list>
    ///
    /// 그래서 아래 검사는 <c>285</c>초·<c>69</c>초를 직접 비교하지 않고 <c>300</c>초·<c>80</c>초
    /// 예산과 비교한다. 상수를 다시 잡아도 이 성질이 유지되면 통과이고, 깨지면 그때가
    /// 격리·해치·덕트 네 동사가 다시 죽는 순간이다(§2).
    /// </summary>
    public sealed class LastShiftVerbDemandTests
    {
        /// <summary>적분 걸음. 압력 평준화가 지수 접근이라 걸음이 크면 결과가 밀린다.</summary>
        private const float Step = 0.25f;

        /// <summary>
        /// §2·§4.1 실측이 쓴 선체 무결성. <b>프리셋 값이 아니라 문서 산수의 기준선이다</b> —
        /// 실제 운석 후 선체는 <c>0.786</c>/<c>0.626</c>/<c>0.396</c> 이고, 상수를 구속한 것도
        /// 그쪽이다(<see cref="EveryPresetPaysForIsolationInsideTheSuitOxygenBudget"/>).
        /// </summary>
        private const float ReferenceHull = 0.60f;

        /// <summary>
        /// 냉각실 밸브 자리에서 냉각통이 굴러간 화물칸까지의 왕복. §4.3 은 <c>14</c>초로 적었지만
        /// <c>rg1-recalc-cargo-procurement-v1.md</c> §5.3 실측은 냉각실 중심 기준 <c>16.34</c>초다.
        /// <b>더 긴 쪽을 쓴다</b> — 유지 동사가 사야 하는 시간의 하한이므로 짧은 쪽을 쓰면
        /// 검사가 통과하고도 실플레이에서 왕복이 안 덮인다.
        /// </summary>
        private const float CoolantRoundTripSeconds = 16.34f;

        private static readonly float SuitBudgetSeconds =
            LastShiftRecoveryTuning.SuitOxygenInitial / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond;

        // ── C-1 산소 시계 ────────────────────────────────────────────────────

        /// <summary>
        /// <b>격리를 안 하면 P0 타이머 안에 진다.</b> §4.1 판정 조건 (1).
        ///
        /// 이 검사가 실패하면 <c>Q</c>(문 격리)는 §2 가 실측한 상태로 돌아간다 — 이득이
        /// 반올림 오차이고 비용만 실재하는 <b>엄격히 나쁜 선택지</b>. 그때 같이 죽는 것이
        /// 갑판 해치 <c>2</c>개와 덕트 전 구간, 에어록이다(§2 마지막 문단).
        ///
        /// <b>선체 <c>0.60</c> 이 여기에 들어온 것이 <c>0.024</c> 확정의 눈에 보이는 결과다</b>
        /// (§7-1, 카드 <c>2245af31</c>). <c>0.018</c> 에서는 <c>316</c>초로 타이머 밖이었다 —
        /// §4.1 표의 <c>285</c>초는 평준화를 "차이가 매초 <c>8%</c> 줄고 각 구역이 <b>그 전부</b>를
        /// 움직인다" 로 푼 값이고, 코드는 §2.2.1 대로 <b>절반씩</b> 움직인다
        /// (<see cref="LastShiftZonePressures.Equalize"/>). <c>0.024</c> 실측은 <c>280</c>초다.
        ///
        /// <b>다만 이 검사는 참고선이다.</b> "선체 <c>0.60</c> · 모든 구역 <c>1.00</c>" 은 어느
        /// 프리셋에서도 나오지 않는 상태이고, 상수를 실제로 구속한 것은 프리셋 쪽이다 —
        /// <see cref="EveryPresetLosesTheOxygenSuccessLineInsideTheDockingTimerIfLeftAlone"/>.
        /// </summary>
        [TestCase(0.60f)]
        [TestCase(0.30f)]
        [TestCase(0.00f)]
        public void LeavingEveryDoorOpenLosesTheOxygenSuccessLineInsideTheDockingTimer(float hull)
        {
            var seconds = SecondsUntilCockpitLosesSuccessLine(LastShiftDoorState.AllOpen, hull);

            Assert.That(seconds, Is.LessThan(LastShiftRecoveryTuning.DockingTimerSeconds),
                "문을 전부 열어 둔 채 방치하면 도킹 타이머 안에 조종석이 산소 성공선을 잃어야 한다 — " +
                "안 그러면 격리할 이유가 없고, 격리가 열어 주는 덕트 우회로도 같이 죽는다(§2).");
        }

        /// <summary>
        /// <b>판정 조건 (1)을 실제로 플레이되는 상태에서 잰다.</b> §7-1 이 <c>game-balance</c> 에
        /// 넘긴 "세 프리셋 전부에서 성립하는지" 가 이 검사다(카드 <c>2245af31</c>).
        ///
        /// 위 <see cref="LeavingEveryDoorOpenLosesTheOxygenSuccessLineInsideTheDockingTimer"/> 가
        /// 쓰는 "선체 <c>0.60</c> · 모든 구역 <c>1.00</c>" 은 <see cref="LastShiftPresetFactory"/> 의
        /// <b>어느 프리셋에서도 나오지 않는다.</b> 실제 시작 압력은 <c>0.64</c>/<c>0.58</c>/<c>0.96</c>
        /// 이고 운석 뒤 선체는 <c>0.786</c>/<c>0.626</c>/<c>0.396</c> 이다. 상수를 다시 잡을 때
        /// 봐야 하는 것은 이쪽이다.
        ///
        /// 실측(<c>0.024</c>): <c>245.0</c>초 · <c>182.5</c>초 · <c>245.0</c>초.
        /// </summary>
        [TestCase(LastShiftPreset.HighHeatHighThrust)]
        [TestCase(LastShiftPreset.PowerOverloadLooseBattery)]
        [TestCase(LastShiftPreset.BadAttitudeHighOxygen)]
        public void EveryPresetLosesTheOxygenSuccessLineInsideTheDockingTimerIfLeftAlone(LastShiftPreset preset)
        {
            var (state, pressures) = AfterMeteor(preset);
            var seconds = SecondsUntilCockpitLosesSuccessLine(LastShiftDoorState.AllOpen, state, pressures);

            Assert.That(seconds, Is.LessThan(LastShiftRecoveryTuning.DockingTimerSeconds),
                $"{preset}(운석 후 선체 {state.HullIntegrity:F3}) 를 방치하면 도킹 타이머 안에 " +
                "조종석이 산소 성공선을 잃어야 한다 — 안 그러면 그 프리셋에서 격리가 다시 죽는다(§2).");
        }

        /// <summary>
        /// <b>판정 조건 (2)를 실제로 플레이되는 상태에서 잰다. 이쪽이 <c>0.024</c> 를 구속했다.</b>
        ///
        /// <c>0.018</c> 이 깨지는 자리가 정확히 여기다 — <c>HighHeatHighThrust</c> 는 운석 뒤
        /// 선체가 <c>0.786</c> 로 셋 중 가장 높아 실효 누출이 작고, 격리해도 파공 구역이
        /// <c>81.8</c>초에야 진공이 되어 예비 산소 예산(<c>80</c>초)을 <c>1.8</c>초 넘겼다.
        /// 격리에 대가가 없으면 §6-4 의 "문 닫고 버티기" 가 비용 <c>0</c> 의 지배 전략이 된다.
        ///
        /// 실측(<c>0.024</c>): <c>61.2</c>초 · <c>31.5</c>초 · <c>32.2</c>초.
        /// </summary>
        [TestCase(LastShiftPreset.HighHeatHighThrust)]
        [TestCase(LastShiftPreset.PowerOverloadLooseBattery)]
        [TestCase(LastShiftPreset.BadAttitudeHighOxygen)]
        public void EveryPresetPaysForIsolationInsideTheSuitOxygenBudget(LastShiftPreset preset)
        {
            var doors = LastShiftDoorState.AllOpen;
            doors[LastShiftZoneAtlas.BoundaryCount - 1] = false;

            var (state, pressures) = AfterMeteor(preset);
            Integrate(doors, SuitBudgetSeconds, state, pressures, out var breachVacuumSeconds);

            Assert.That(breachVacuumSeconds, Is.GreaterThan(0f).And.LessThan(SuitBudgetSeconds),
                $"{preset}(운석 후 선체 {state.HullIntegrity:F3}) 에서 격리한 파공 구역은 예비 산소 " +
                "지속시간 안에 진공이 되어야 한다 — 그게 격리의 대가이고, 없으면 문을 닫고 " +
                "버티는 것이 공짜가 된다(§6-4).");
        }

        /// <summary>
        /// <b>누출률을 올려도 봉합만 하면 이긴다.</b> 이 상수가 겨누는 것은 방치의 대가 하나이고,
        /// 승리 가능성까지 같이 깎으면 §4.1 이 산 것보다 잃은 것이 커진다.
        ///
        /// 봉합이 늦은 쪽(<c>180</c>초)으로 잡는다 — 그때 봉합해도 펌프
        /// (<see cref="LastShiftRecoveryTuning.OxygenPumpRecoveryPerSecond"/>)가 남은 시간에
        /// 조종석 연결 구역을 되채워 타이머 끝 조종석이 성공선 위여야 한다. 실측은 세 프리셋
        /// 전부 <c>0.43</c> 위다.
        /// </summary>
        [TestCase(LastShiftPreset.HighHeatHighThrust)]
        [TestCase(LastShiftPreset.PowerOverloadLooseBattery)]
        [TestCase(LastShiftPreset.BadAttitudeHighOxygen)]
        public void PatchingLateStillClearsTheOxygenSuccessLineAtTheBuzzer(LastShiftPreset preset)
        {
            const float patchAt = 180f;
            var (state, pressures) = AfterMeteor(preset);
            var containment = new LastShiftContainment { CoolingRestored = true, PowerRestored = true };

            for (var elapsed = 0f; elapsed < LastShiftRecoveryTuning.DockingTimerSeconds; elapsed += Step)
            {
                if (elapsed >= patchAt) containment.OxygenRestored = true;
                LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment,
                    LastShiftZone.LifeSupport, LastShiftDoorState.AllOpen, Step);
            }

            Assert.That(pressures[LastShiftZone.Cockpit],
                Is.GreaterThan(LastShiftRecoveryTuning.DockingSuccessOxygen),
                $"{preset} 에서 {patchAt:F0}초에 봉합해도 타이머 끝 조종석은 성공선 위여야 한다 — " +
                $"실측 {pressures[LastShiftZone.Cockpit]:F3}.");
        }

        /// <summary>
        /// <b>격리하면 조종석은 산다.</b> 같은 조건에서 문만 닫는다.
        ///
        /// 격리의 이득이 실재해야 <c>Q</c> 가 선택지가 된다. §2 가 잰 옛 이득은 <c>60</c>초에
        /// <c>0.018</c> 이었고, 그건 성공선까지 여유 <c>0.76</c> 앞에서 반올림 오차였다.
        /// </summary>
        [Test]
        public void IsolatingTheBreachZoneKeepsTheCockpitAboveTheSuccessLineForTheWholeTimer()
        {
            var doors = LastShiftDoorState.AllOpen;
            doors[LastShiftZoneAtlas.BoundaryCount - 1] = false;

            var pressures = Integrate(doors, LastShiftRecoveryTuning.DockingTimerSeconds, ReferenceHull, out _);

            Assert.That(pressures[LastShiftZone.Cockpit],
                Is.GreaterThan(LastShiftRecoveryTuning.DockingSuccessOxygen),
                "파공 구역을 격리하면 조종석 압력 전파가 그 자리에서 멎어야 한다.");
        }

        /// <summary>
        /// <b>격리의 대가도 시계 안에 있어야 한다.</b> §4.1 판정 조건 (2).
        ///
        /// 격리해 놓고 봉합하러 들어가는 사람은 예비 산소를 태운다. 파공 구역이 진공이 되는
        /// 시각이 그 예산(<c>80</c>초)보다 늦으면 격리에 대가가 없고, 그러면 §6-4 가 경고한
        /// "문 닫고 버티기" 가 비용 <c>0</c> 의 지배 전략이 된다.
        /// </summary>
        [Test]
        public void IsolatedBreachZoneReachesVacuumInsideTheSuitOxygenBudget()
        {
            var doors = LastShiftDoorState.AllOpen;
            doors[LastShiftZoneAtlas.BoundaryCount - 1] = false;

            Integrate(doors, SuitBudgetSeconds, ReferenceHull, out var breachVacuumSeconds);

            Assert.That(breachVacuumSeconds, Is.GreaterThan(0f).And.LessThan(SuitBudgetSeconds),
                "격리한 파공 구역은 예비 산소 지속시간 안에 진공이 되어야 한다 — " +
                "그게 격리의 대가이고, 없으면 문을 닫고 버티는 것이 공짜가 된다(§6-4).");
        }

        /// <summary>
        /// 격리는 이득이 <b>있어야</b> 하지만 공짜여서는 안 된다. 두 검사를 한 문장으로 묶는
        /// 비교이며, 상수를 다시 잡을 때 어느 쪽으로 기울었는지가 여기서 먼저 보인다.
        /// </summary>
        [Test]
        public void IsolationBuysCockpitTimeAndSpendsBreachZoneAir()
        {
            var isolated = LastShiftDoorState.AllOpen;
            isolated[LastShiftZoneAtlas.BoundaryCount - 1] = false;

            var openPressures = Integrate(LastShiftDoorState.AllOpen, 90f, ReferenceHull, out var openBreachVacuum);
            var isolatedPressures = Integrate(isolated, 90f, ReferenceHull, out var isolatedBreachVacuum);

            Assert.That(isolatedPressures[LastShiftZone.Cockpit],
                Is.GreaterThan(openPressures[LastShiftZone.Cockpit]),
                "격리한 쪽 조종석이 더 높아야 한다.");
            Assert.That(isolatedBreachVacuum, Is.LessThan(openBreachVacuum),
                "격리한 쪽 파공 구역이 더 빨리 진공이 되어야 한다 — 그게 지불하는 비용이다.");
        }

        // ── C-3 유지 동사 ────────────────────────────────────────────────────

        /// <summary>
        /// <b>유지 동사는 냉각 복구를 대체하면 안 된다.</b> §4.3 이 "설계의 핵심" 이라고 적은 성질.
        ///
        /// 이 부등호가 뒤집히면 "밸브만 잡고 버티기" 가 최적해가 되고, 동사를 늘리려던 카드가
        /// 오히려 동사를 하나로 수렴시킨다.
        /// </summary>
        [Test]
        public void SustainedCoolingStaysWeakerThanRestoringTheCoolant()
        {
            Assert.That(LastShiftRecoveryTuning.SustainedCoolingPerSecond,
                Is.LessThan(LastShiftRecoveryTuning.HeatRecoveryPerSecond),
                "유지 동사의 하강률은 냉각 복구보다 낮아야 한다 — 대체 가능해지면 복구가 죽는다.");
        }

        /// <summary>
        /// <b>붙잡고 있으면 열이 오르지만 훨씬 느리게 오른다.</b> §4.3 이 든 산수 그대로다:
        /// 상승 <c>0.020/s</c> 에 유지 <c>0.015/s</c> 가 걸려 순 <c>0.005/s</c> 다.
        ///
        /// 부호까지 보는 것이 요점이다 — 유지가 상승을 <b>뒤집으면</b> 그건 복구이지 유지가 아니다.
        /// </summary>
        [Test]
        public void HoldingTheValveSlowsTheHeatRiseWithoutReversingIt()
        {
            var withoutValve = TickHeat(0.60f, 0.65f, held: false, seconds: 10f);
            var withValve = TickHeat(0.60f, 0.65f, held: true, seconds: 10f);

            Assert.That(withoutValve, Is.GreaterThan(0.60f), "밸브가 없으면 열이 오른다.");
            Assert.That(withValve, Is.GreaterThan(0.60f), "밸브를 잡아도 상승 자체는 멈추지 않는다.");
            Assert.That(withValve, Is.LessThan(withoutValve), "잡은 쪽이 덜 올라야 한다.");

            var expected = 0.60f + (LastShiftRecoveryTuning.HeatRisePerSecond -
                                    LastShiftRecoveryTuning.SustainedCoolingPerSecond) * 10f;
            Assert.That(withValve, Is.EqualTo(expected).Within(0.002f));
        }

        /// <summary>
        /// §4.3 의 플레이 장면을 그대로 적분한다 — <c>HighHeatHighThrust</c> 프리셋, 열 <c>0.86</c>,
        /// 추력 <c>0.65</c>, 냉각통 왕복 <c>14</c>초.
        ///
        /// <b>§4.3 의 산수는 <c>S-H2</c> 열 폭주를 안 세었다.</b> 그 문서는 상승률을 <c>0.020/s</c>
        /// 고정으로 두고 <c>0.86 + 0.005 x 14 = 0.93</c> 이라 적었지만, <c>CT-06 N5</c> 가 열
        /// <c>0.90</c> 이상 · 냉각 미연결에서 상승률을 <c>0.035/s</c> 로 갈아탄다
        /// (<see cref="LastShiftRecoveryTuning.HeatRunawayTrigger"/>). 밸브를 잡아도 <c>0.90</c> 은
        /// 지나가므로, 폭주 증분을 그대로 두면 그 뒤로 순 <c>0.020/s</c> 가 되어 <c>13.2</c>초에
        /// 잠겼다 — <c>14</c>초 왕복이 안 덮였다.
        ///
        /// <b>§7-1b 결정(카드 <c>2245af31</c>): 붙잡고 있는 동안 폭주 증분이 안 실린다.</b>
        /// 하강 항(<c>0.015</c>)은 그대로다 — 그쪽을 올려 <c>14</c>초를 사려면 <c>0.0155</c> 가
        /// 필요하고, 그 값은 상승률과의 차가 <c>0.0045</c> 라 밸브가 냉각통의 대체재가 되기
        /// 직전이다(§7-3). 증분을 걷어내면 유지 중 순 상승이 구간 내내 <c>0.005/s</c> 로 고정되어
        /// <c>28.2</c>초다.
        /// </summary>
        [Test]
        public void HoldingTheValveCoversTheCoolantRoundTrip()
        {
            var abandoned = SecondsUntilProtectionLock(0.86f, 0.65f, held: false);
            var sustained = SecondsUntilProtectionLock(0.86f, 0.65f, held: true);

            Assert.That(abandoned, Is.LessThan(6f),
                "아무도 밸브를 안 잡으면 몇 초 만에 엔진 보호 잠금이 걸려야 한다.");
            Assert.That(sustained, Is.GreaterThan(CoolantRoundTripSeconds),
                $"한 사람이 자리를 지키면 냉각통 왕복({CoolantRoundTripSeconds:F1}초) 안에 " +
                $"잠기지 않아야 한다 — 실측 {sustained:F1}초. 안 그러면 밸브를 잡는 선택이 " +
                "'잠금을 조금 늦춘다' 이상을 못 사고, §4.3 이 만들려던 거래가 성립하지 않는다.");
        }

        /// <summary>
        /// <b>폭주 증분을 걷어내도 유지는 복구가 아니다.</b> §7-1b 결정이 §4.3 의 "설계의 핵심"
        /// (유지가 냉각통 연결을 대체하면 안 된다)을 깨지 않는지 직접 본다.
        ///
        /// 잡고 있어도 열은 계속 오르고(<c>0.005/s</c>) 결국 잠긴다. 열을 실제로 <b>내리는</b>
        /// 것은 냉각 복구뿐이다.
        /// </summary>
        [Test]
        public void SuppressingTheRunawaySurchargeStillLeavesTheHeatRising()
        {
            var sustained = SecondsUntilProtectionLock(0.86f, 0.65f, held: true);

            Assert.That(sustained, Is.Not.EqualTo(float.PositiveInfinity),
                "밸브를 잡고 버티는 것만으로 잠금을 영원히 피할 수 있으면 그건 복구다.");

            var heldPastTheTrigger = TickHeat(
                LastShiftRecoveryTuning.HeatRunawayTrigger, 0.65f, held: true, seconds: 10f);
            var expected = LastShiftRecoveryTuning.HeatRunawayTrigger +
                           (LastShiftRecoveryTuning.HeatRisePerSecond -
                            LastShiftRecoveryTuning.SustainedCoolingPerSecond) * 10f;

            Assert.That(heldPastTheTrigger, Is.EqualTo(expected).Within(0.002f),
                "폭주선 위에서 밸브를 잡으면 기본 상승률에서 유지 항만 뺀 값으로 올라야 한다 — " +
                "폭주 증분이 걷힌 것이지 상승이 멈춘 것이 아니다.");
        }

        /// <summary>
        /// <b>손을 떼면 즉시 폭주로 돌아간다.</b> 억제는 붙잡고 있는 동안만이라는 §4.3 의
        /// 해제 규칙("손을 떼거나 밸브에서 벗어나면 즉시 0")이 폭주 증분에도 그대로 걸린다.
        /// </summary>
        [Test]
        public void ReleasingTheValveRestoresTheRunawaySurchargeImmediately()
        {
            var released = TickHeat(
                LastShiftRecoveryTuning.HeatRunawayTrigger, 0.65f, held: false, seconds: 1f);

            Assert.That(released - LastShiftRecoveryTuning.HeatRunawayTrigger,
                Is.EqualTo(LastShiftRecoveryTuning.HeatRiseRunawayPerSecond).Within(0.0005f),
                "밸브에서 손을 뗀 tick 은 폭주 상승률 그대로여야 한다.");
        }

        /// <summary>
        /// 유지는 <b>봉쇄가 아니다.</b> 밸브를 잡고 있다고 <c>CoolingContained</c> 가 참이 되면
        /// 열 상승 분기가 통째로 꺼져 냉각통 연결과 구분이 사라진다(§4.3 전제).
        /// </summary>
        [Test]
        public void HoldingTheValveIsNotContainment()
        {
            var containment = new LastShiftContainment { CoolingValveHeld = true };

            Assert.That(containment.CoolingContained, Is.False);
            Assert.That(containment.CoolingRestored, Is.False);
        }

        // ── C-3 밸브 좌표 ────────────────────────────────────────────────────

        /// <summary>
        /// 밸브는 냉각실 안에 있어야 한다. 구역이 다르면 §4.3 의 거리 계산 전체가 무의미해진다.
        /// </summary>
        [Test]
        public void ValveSitsInsideTheCoolingRoom()
        {
            var valve = LastShiftCoolingValve.Position;

            Assert.That(LastShiftCoolingValve.Zone, Is.EqualTo(LastShiftZone.Cooling));
            Assert.That(valve.x, Is.GreaterThan(LastShiftShipDimensions.RoomMinX(LastShiftZone.Cooling)));
            Assert.That(valve.x, Is.LessThan(LastShiftShipDimensions.RoomMaxX(LastShiftZone.Cooling)));
            Assert.That(Mathf.Abs(valve.z), Is.LessThan(LastShiftShipDimensions.HalfWidth));
        }

        /// <summary>
        /// <b>밸브 사거리와 문 사거리가 겹치지 않는다.</b>
        ///
        /// 냉각실은 <c>5m</c> 인데 양 끝 문이 각각 <c>1.8m</c> 를 차지해 x 만 보면 자리가 없다.
        /// 겹치지 않는 근거는 문 사거리의 z 창이고(<see cref="LastShiftCoolingValve.SternStandoffX"/>),
        /// 그 논증이 좌표 하나만 움직여도 무너지므로 여기서 격자로 직접 확인한다.
        ///
        /// 겹치면 프롬프트 우선순위가 문을 가린다 — 문은 그 자리를 떠나는 동사라 잘못 가려지면
        /// 승무원이 격리된 구역에 갇힌다.
        /// </summary>
        [Test]
        public void ValveReachDoesNotOverlapAnyZoneDoorReach()
        {
            var valve = LastShiftCoolingValve.Position;
            const float reach = LastShiftCoolingValve.ReachDistance;
            const float zWindow = LastShiftZoneDoor.OpeningWidth * 0.5f + 1.0f;

            for (var xStep = -8; xStep <= 8; xStep++)
            for (var zStep = -8; zStep <= 8; zStep++)
            {
                var probe = new Vector3(
                    valve.x + reach * xStep / 8f,
                    0f,
                    valve.z + reach * zStep / 8f);
                if (!LastShiftCoolingValve.IsWithinReach(probe)) continue;

                for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                {
                    var boundaryX = LastShiftZoneAtlas.BoundaryX(boundary);
                    var doorZ = LastShiftZoneDoor.CenterZOf(boundary);
                    var inDoorReach =
                        Mathf.Abs(probe.x - boundaryX) <= LastShiftZoneDoor.ReachDistance &&
                        Mathf.Abs(probe.z - doorZ) <= zWindow;

                    Assert.That(inDoorReach, Is.False,
                        $"밸브 사거리 안의 ({probe.x:F2}, {probe.z:F2}) 가 경계 {boundary} 문 사거리와 겹친다.");
                }
            }
        }

        /// <summary>
        /// 밸브 사거리 안에 냉각통 정위치가 들어오면 안 된다 — 들어오면 §4.3 이 요구한
        /// "밸브를 잡는 대신 냉각통을 포기한다" 는 거래가 좌표에서 사라진다.
        /// </summary>
        [Test]
        public void ValveReachExcludesTheCoolantNominal()
        {
            Assert.That(LastShiftCoolingValve.IsWithinReach(LastShiftShipDimensions.CoolingNominal), Is.False);
        }

        // ── 조립 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 프리셋을 만들고 표준 운석(<see cref="LastShiftMeteorStimulus.Canonical"/>)을 때린
        /// 직후 상태. <b>여기가 실제로 플레이가 시작되는 지점이다</b> —
        /// <c>LastShiftSandboxController.Meteor</c> 가 <c>Canonical</c> 고정이라 severity 는
        /// <c>0.9924</c> 하나뿐이고, 프리셋별 시작 압력·선체가 이 함수에서 결정된다.
        ///
        /// 부품은 넘기지 않는다(<c>items = null</c>). 부품이 굴러가면 <c>patchTravel</c> 이
        /// 파공 구역 압력을 <b>더</b> 깎아 두 판정 조건이 함께 쉬워지므로, 부품이 제자리인
        /// 쪽이 상수에 가장 가혹한 경우다.
        /// </summary>
        private static (LastShiftShipState, LastShiftZonePressures) AfterMeteor(LastShiftPreset preset)
        {
            var state = LastShiftPresetFactory.Create(preset);
            var pressures = LastShiftZonePressures.Uniform(state.OxygenPressure);
            state = LastShiftMeteorApplication.Apply(
                LastShiftMeteorStimulus.Canonical, state, ref pressures, LastShiftZone.LifeSupport, null);
            return (state, pressures);
        }

        /// <summary>
        /// 손상 하나(산소)만 남은 배를 <paramref name="seconds"/> 만큼 민다. 파공은 §2·§4.1 실측과
        /// 같은 자리 — 생명유지실이고 조종석에서 경계 <c>3</c>개 거리다.
        /// </summary>
        private static LastShiftZonePressures Integrate(
            LastShiftDoorState doors, float seconds, float hull, out float breachVacuumSeconds)
        {
            return Integrate(doors, seconds, ReferenceState(hull), LastShiftZonePressures.Uniform(1f),
                out breachVacuumSeconds);
        }

        private static LastShiftZonePressures Integrate(
            LastShiftDoorState doors, float seconds, LastShiftShipState state,
            LastShiftZonePressures pressures, out float breachVacuumSeconds)
        {
            // 열·전력은 봉쇄해 둔다. 이 검사가 겨누는 것은 산소 시계 하나이고, 다른 시계가
            // 같이 돌면 추력 상한이 움직여 압력과 무관한 이유로 결과가 흔들린다.
            var containment = new LastShiftContainment
            {
                CoolingRestored = true,
                PowerRestored = true
            };

            breachVacuumSeconds = float.PositiveInfinity;
            for (var elapsed = 0f; elapsed < seconds; elapsed += Step)
            {
                LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, LastShiftZone.LifeSupport, doors, Step);

                if (float.IsPositiveInfinity(breachVacuumSeconds) &&
                    LastShiftVerdictResolver.IsZoneVacuum(pressures[LastShiftZone.LifeSupport], false))
                    breachVacuumSeconds = elapsed + Step;
            }
            return pressures;
        }

        /// <summary>조종석이 성공선 아래로 내려가는 시각. 안 내려가면 양의 무한대다.</summary>
        private static float SecondsUntilCockpitLosesSuccessLine(LastShiftDoorState doors, float hull)
        {
            return SecondsUntilCockpitLosesSuccessLine(
                doors, ReferenceState(hull), LastShiftZonePressures.Uniform(1f));
        }

        private static float SecondsUntilCockpitLosesSuccessLine(
            LastShiftDoorState doors, LastShiftShipState state, LastShiftZonePressures pressures)
        {
            var containment = new LastShiftContainment
            {
                CoolingRestored = true,
                PowerRestored = true
            };

            for (var elapsed = 0f; elapsed < LastShiftRecoveryTuning.DockingTimerSeconds; elapsed += Step)
            {
                LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, LastShiftZone.LifeSupport, doors, Step);
                if (pressures[LastShiftZone.Cockpit] < LastShiftRecoveryTuning.DockingSuccessOxygen)
                    return elapsed + Step;
            }
            return float.PositiveInfinity;
        }

        /// <summary>§2·§4.1 문서가 쓴 참고 상태. 프리셋이 아니라 문서 산수의 기준선이다.</summary>
        private static LastShiftShipState ReferenceState(float hull)
        {
            return new LastShiftShipState
            {
                ThrustDemand = 0.50f,
                BusPower = 1f,
                OxygenPressure = 1f,
                HullIntegrity = hull,
                EngineHeat = 0.20f,
                FuelReserve = LastShiftRecoveryTuning.FuelReserveInitial
            };
        }

        /// <summary>
        /// 엔진 보호 잠금이 걸리는 시각. 안 걸리면 양의 무한대다.
        ///
        /// 최종 열이 아니라 <b>첫 도달 시각</b>을 재는 것이 요점이다 — 잠금이 걸리면 추력 상한이
        /// <c>0.25</c> 로 눌려 상승 분기(<c>추력 &gt; 0.60</c>)가 스스로 꺼지고 열이 도로 내려간다.
        /// 끝 값만 보면 "잠긴 적이 있다" 는 사실 자체가 안 보인다.
        /// </summary>
        private static float SecondsUntilProtectionLock(float initialHeat, float thrust, bool held)
        {
            var state = NewHeatState(initialHeat, thrust);
            var pressures = LastShiftZonePressures.Uniform(1f);
            var containment = new LastShiftContainment
            {
                PowerRestored = true,
                OxygenRestored = true,
                CoolingValveHeld = held
            };

            for (var elapsed = 0f; elapsed < 60f; elapsed += Step)
            {
                var report = LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, LastShiftZone.LifeSupport,
                    LastShiftDoorState.AllOpen, Step);
                if (report.HeatProtectionEngaged) return elapsed + Step;
            }
            return float.PositiveInfinity;
        }

        /// <summary>열 시계만 돌린다. 냉각은 미복구, 나머지 둘은 봉쇄다.</summary>
        private static float TickHeat(float initialHeat, float thrust, bool held, float seconds)
        {
            var state = NewHeatState(initialHeat, thrust);
            var pressures = LastShiftZonePressures.Uniform(1f);
            var containment = new LastShiftContainment
            {
                PowerRestored = true,
                OxygenRestored = true,
                CoolingValveHeld = held
            };

            for (var elapsed = 0f; elapsed < seconds; elapsed += Step)
                LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, LastShiftZone.LifeSupport,
                    LastShiftDoorState.AllOpen, Step);
            return state.EngineHeat;
        }

        private static LastShiftShipState NewHeatState(float initialHeat, float thrust)
        {
            return new LastShiftShipState
            {
                ThrustDemand = thrust,
                BusPower = 1f,
                OxygenPressure = 1f,
                HullIntegrity = 1f,
                EngineHeat = initialHeat,
                FuelReserve = LastShiftRecoveryTuning.FuelReserveInitial
            };
        }
    }
}
