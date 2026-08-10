using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 확장 모듈 효과 계수 — <c>docs/module-effect-coefficients-v1.md</c>.
    /// <c>docs/port-module-catalog-v1.md</c> §6 의 <c>P-2</c> 이고 §9-2·§9-4 를 닫는다.
    ///
    /// <b><see cref="LastShiftVerbDemandTests"/> 와 같은 규약이다 — 수치가 아니라 판정 조건을
    /// 고정한다.</b> <c>0.15</c>·<c>0.10</c>·<c>0.05</c> 는 <c>game-balance</c> 가 다시 잡을 수
    /// 있는 값이고, 여기가 붙드는 것은 <b>그 값이 무엇에 막혀 그 크기인가</b> 다:
    ///
    /// <list type="number">
    ///   <item>모듈을 세워도 방치의 대가와 격리의 대가가 둘 다 남는다(조항 C-2, 산소)</item>
    ///   <item>모듈 + 밸브가 냉각통을 대체하지 않는다(<c>CT-08</c> §4.3, 열)</item>
    ///   <item>모듈이 배터리 꽂기를 대체하지 않고 전력 시계를 안 죽인다(전력)</item>
    ///   <item>같은 효과를 두 번 사서 위 셋을 뚫을 수 없다(조항 E-3)</item>
    /// </list>
    ///
    /// 정적 오버레이를 만지므로 <see cref="LastShiftPlacedModules.Clear"/> 가 매 테스트 앞뒤에
    /// 붙는다. 하나라도 새면 이 배의 효과가 다음 테스트의 시계로 흘러간다.
    /// </summary>
    public sealed class LastShiftModuleEffectsTests
    {
        /// <summary>적분 걸음. <see cref="LastShiftVerbDemandTests"/> 와 같은 값이어야 비교가 선다.</summary>
        private const float Step = 0.25f;

        /// <summary>파공 구역. 시뮬 전체가 이 구역을 파공으로 잡고 돈다.</summary>
        private const LastShiftZone Breach = LastShiftZone.LifeSupport;

        private static readonly float SuitBudgetSeconds =
            LastShiftRecoveryTuning.SuitOxygenInitial / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond;

        [SetUp]
        public void ClearBefore() => LastShiftPlacedModules.Clear();

        [TearDown]
        public void ClearAfter() => LastShiftPlacedModules.Clear();

        // ── 모듈이 없는 배 ───────────────────────────────────────────────────

        /// <summary>
        /// <b>이 카드가 기존 시계를 하나도 안 건드린다는 주장의 전부가 이것이다.</b> 모듈이 하나도
        /// 안 선 배에서 세 계수가 전부 항등이어야 <see cref="LastShiftVerbDemandTests"/> 를 비롯한
        /// 기존 EditMode 가 예전 값 그대로 돈다.
        /// </summary>
        [Test]
        public void EmptyShipCollectsNoEffectAtAll()
        {
            var effects = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(effects.Any, Is.False, "모듈이 없는데 효과가 걷혔다.");
            Assert.That(effects.HeatRiseMultiplier, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(effects.BusFloor, Is.EqualTo(LastShiftRecoveryTuning.UnpoweredBusCeiling).Within(1e-5f));
            Assert.That(effects.SpareItemCount, Is.Zero);
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                Assert.That(effects.OxygenLeakMultiplierFor((LastShiftZone)zone), Is.EqualTo(1f).Within(1e-5f));
        }

        /// <summary>
        /// 효과가 없는 다섯은 <b>세워도 아무 시계도 안 건드린다.</b> 둘(연결 통로 · 격납고)은
        /// 설계상 영영 없는 것이고, 셋(서버/통신실 · 정비창 · 의무실)은 수치가 아직
        /// <c>game-balance</c> 미결이다(맵 개편 §7-3). <b>미결인 셋이 여기 같이 걸려 있는 것이
        /// 요점이다</b> — 값이 정해지기 전에 무언가 붙으면 그건 아무도 안 고른 효과다.
        /// </summary>
        [TestCase(LastShiftModuleCatalog.Corridor)]
        [TestCase(LastShiftModuleCatalog.Observatory)]
        [TestCase(LastShiftModuleCatalog.ServerRoom)]
        [TestCase(LastShiftModuleCatalog.Workshop)]
        [TestCase(LastShiftModuleCatalog.MedBay)]
        [TestCase(LastShiftModuleCatalog.Hangar)]
        public void GeometryOnlyModulesCollectNoEffect(int catalogIndex)
        {
            Place(catalogIndex, Breach);
            var effects = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(effects.Any, Is.False,
                $"카탈로그 {catalogIndex}({LastShiftModuleCatalog.At(catalogIndex).Name})가 효과를 냈다 — " +
                "효과가 아직 안 붙은 칸이 조용히 값을 갖고 있다.");
        }

        /// <summary>
        /// 카탈로그를 안 거친 등록(표를 직접 쓰는 조립·테스트 경로)은 효과가 없다.
        /// <b>아무도 안 산 효과를 가진 배가 나오면 안 된다.</b>
        /// </summary>
        [Test]
        public void ModuleRegisteredWithoutACatalogIndexCollectsNoEffect()
        {
            LastShiftPlacedModules.Register(0f, 4f, 0f, 4f, Breach);
            Assert.That(LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f)).Any, Is.False);
        }

        // ── 수경재배 ────────────────────────────────────────────────────

        /// <summary>
        /// <b>파공 구역에 붙인 재생기만 그 파공에 말을 한다.</b> 카탈로그 §7-C 가 "위치가 처음으로
        /// 결정이 되는 자리" 라고 부른 것의 실행판이다 — 산소실 옆에 붙인 배와 조종석 옆에 붙인
        /// 배가 여기서 갈린다.
        /// </summary>
        [Test]
        public void HydroponicsSlowsOnlyItsOwnZone()
        {
            Place(LastShiftModuleCatalog.Hydroponics, LastShiftZone.Cockpit);
            var effects = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(effects.OxygenLeakMultiplierFor(LastShiftZone.Cockpit),
                Is.EqualTo(1f - LastShiftModuleEffects.OxygenLeakReduction).Within(1e-5f));
            Assert.That(effects.OxygenLeakMultiplierFor(Breach), Is.EqualTo(1f).Within(1e-5f),
                "조종석에 붙인 재생기가 산소실 파공을 늦췄다 — 그러면 어디 붙일지가 결정이 아니다.");
        }

        /// <summary>
        /// <b>판정 조건 (1) — 재생기를 파공 구역에 붙여도 방치의 대가는 남는다.</b>
        ///
        /// <b>이쪽은 여유가 큰 조건이다</b> — 감속 <c>0.25</c> 에서도 <c>293.0</c>초로 타이머
        /// 안이다. 계수를 실제로 구속한 것은 아래
        /// <see cref="HydroponicsStillPaysForIsolationInsideTheSuitOxygenBudget"/> 이고,
        /// 그래도 이 조건을 같이 거는 것은 <see cref="LastShiftRecoveryTuning.OxygenLeakPerSecond"/>
        /// 가 되살린 "방치의 대가" 가 모듈로 지워지지 않는지를 따로 봐야 하기 때문이다 —
        /// 두 조건은 감속을 반대 방향에서 잡고 있지 않고 <b>같은 방향</b>이라, 하나만 걸면
        /// 나중에 누출률이 재조정될 때 어느 쪽이 먼저 깨질지 알 수 없다.
        ///
        /// 실측(<c>0.15</c>): <c>269.8</c>초 · <c>191.0</c>초 · <c>255.5</c>초.
        /// </summary>
        [TestCase(LastShiftPreset.HighHeatHighThrust)]
        [TestCase(LastShiftPreset.PowerOverloadLooseBattery)]
        [TestCase(LastShiftPreset.BadAttitudeHighOxygen)]
        public void HydroponicsStillLosesTheSuccessLineInsideTheTimerIfLeftAlone(LastShiftPreset preset)
        {
            Place(LastShiftModuleCatalog.Hydroponics, Breach);

            var (state, pressures) = AfterMeteor(preset);
            var seconds = SecondsUntilCockpitLosesSuccessLine(LastShiftDoorState.AllOpen, state, pressures);

            Assert.That(seconds, Is.LessThan(LastShiftRecoveryTuning.DockingTimerSeconds),
                $"{preset} 에 수경재배를 세웠더니 방치해도 도킹 타이머 안에 성공선을 안 잃는다 — " +
                "모듈 하나가 산소 실패 시계를 껐다(조항 C-2 위반).");
        }

        /// <summary>
        /// <b>판정 조건 (2) — 재생기를 세워도 격리의 대가는 남는다.</b> 파공 구역은 여전히 예비
        /// 산소 예산(<c>80</c>초) 안에 진공이 된다. 안 그러면 §6-4 의 "문 닫고 버티기" 가 여력
        /// <c>2</c> 로 다시 공짜가 된다.
        ///
        /// <b>이 검사가 <see cref="LastShiftModuleEffects.OxygenLeakReduction"/> 의 위쪽을 막았다.</b>
        /// 구속하는 프리셋은 <c>0.024</c> 를 구속한 것과 같은 <c>HighHeatHighThrust</c> 하나이고
        /// (운석 후 선체 <c>0.786</c> 로 셋 중 가장 높아 실효 누출이 작다), 감속 <c>0.25</c> 에서
        /// <c>81.8</c>초로 예산을 <c>1.8</c>초 넘긴다 — <c>0.018</c> 이 깨졌던 것과 같은 자리다.
        ///
        /// 실측(<c>0.15</c>): <c>72.3</c>초 · <c>37.0</c>초 · <c>38.0</c>초.
        /// </summary>
        [TestCase(LastShiftPreset.HighHeatHighThrust)]
        [TestCase(LastShiftPreset.PowerOverloadLooseBattery)]
        [TestCase(LastShiftPreset.BadAttitudeHighOxygen)]
        public void HydroponicsStillPaysForIsolationInsideTheSuitOxygenBudget(LastShiftPreset preset)
        {
            Place(LastShiftModuleCatalog.Hydroponics, Breach);

            var doors = LastShiftDoorState.AllOpen;
            doors[LastShiftZoneAtlas.BoundaryCount - 1] = false;

            var (state, pressures) = AfterMeteor(preset);
            Integrate(doors, SuitBudgetSeconds, state, pressures, out var breachVacuumSeconds);

            Assert.That(breachVacuumSeconds, Is.GreaterThan(0f).And.LessThan(SuitBudgetSeconds),
                $"{preset} 에 수경재배를 세웠더니 격리한 파공 구역이 예비 산소 예산 안에 " +
                "진공이 안 된다 — 격리가 다시 공짜가 됐다(§6-4).");
        }

        /// <summary>
        /// 그러면서 <b>이득은 실재해야 한다.</b> 값을 냈는데 시계가 안 늘면 이 항목은 살 이유가 없다.
        /// </summary>
        [Test]
        public void HydroponicsActuallyBuysTimeInTheBreachZone()
        {
            var (bareState, barePressures) = AfterMeteor(LastShiftPreset.HighHeatHighThrust);
            var bare = SecondsUntilCockpitLosesSuccessLine(LastShiftDoorState.AllOpen, bareState, barePressures);

            Place(LastShiftModuleCatalog.Hydroponics, Breach);
            var (state, pressures) = AfterMeteor(LastShiftPreset.HighHeatHighThrust);
            var withModule = SecondsUntilCockpitLosesSuccessLine(LastShiftDoorState.AllOpen, state, pressures);

            Assert.That(withModule, Is.GreaterThan(bare),
                $"재생기를 세웠는데 성공선을 잃는 시각이 안 늦었다 — 실측 {bare:F1}s -> {withModule:F1}s.");
        }

        // ── 방열 라디에이터실 ────────────────────────────────────────────────

        /// <summary>
        /// <b>모듈 + 밸브가 냉각통을 대체하지 않는다.</b>
        /// <see cref="LastShiftModuleEffects.HeatRiseReduction"/> 의 위쪽을 막은 조건이다.
        ///
        /// 밸브 유지(<c>0.015</c>)가 이미 상승률(<c>0.020</c>)의 <c>75%</c> 를 먹고 있어서, 모듈이
        /// <c>0.25</c> 를 넘게 먹으면 순 상승이 <c>0</c> 이하가 되어 <b>밸브만 잡고 있으면 열이
        /// 안 오르는 배</b>가 된다. 그 순간 <c>CT-08</c> §4.3 이 "설계의 핵심" 이라 부른 성질이
        /// 죽고, 동사를 늘리려던 카드가 동사를 도로 하나로 수렴시킨다.
        /// </summary>
        [Test]
        public void RadiatorPlusHeldValveStillLetsHeatRise()
        {
            Place(LastShiftModuleCatalog.Radiator, LastShiftZone.Cooling);

            var netRise = LastShiftRecoveryTuning.HeatRisePerSecond *
                          (1f - LastShiftModuleEffects.HeatRiseReduction) -
                          LastShiftRecoveryTuning.SustainedCoolingPerSecond;

            Assert.That(netRise, Is.GreaterThan(0f),
                "라디에이터 + 밸브에서 열이 안 오른다 — 냉각통을 연결할 이유가 사라졌다.");

            // 밸브 단독 순 상승의 절반은 남아야 한다. 부호만 맞고 크기가 0 에 붙으면
            // "오르기는 한다" 가 화면에서는 "안 오른다" 와 같은 뜻이 된다.
            var valveOnly = LastShiftRecoveryTuning.HeatRisePerSecond -
                            LastShiftRecoveryTuning.SustainedCoolingPerSecond;
            Assert.That(netRise, Is.GreaterThanOrEqualTo(valveOnly * 0.5f),
                $"순 상승이 밸브 단독({valveOnly:F4}/s)의 절반 아래다 — 열이 사실상 멎었다.");

            // 그리고 실제로 잠긴다. 산수가 아니라 시뮬이 답해야 한다.
            var lock1 = SecondsUntilProtectionLock(0.86f, thrust: 0.92f, held: true);
            Assert.That(lock1, Is.LessThan(float.PositiveInfinity),
                "라디에이터 + 밸브로 엔진 보호 잠금이 영원히 안 걸린다.");
        }

        /// <summary>
        /// <b>모듈 단독으로는 냉각통 왕복을 못 산다.</b> 밸브를 안 잡으면 잠금까지가 <c>5.00</c>초
        /// 에서 <c>5.50</c>초로 <c>+0.5</c>초뿐이고, 실측 왕복 <c>16.34</c>초
        /// (<c>rg1-recalc-cargo-procurement-v1.md</c> §5.3) 근처에도 못 간다.
        ///
        /// <b>이 검사가 라디에이터를 "밸브를 잡는 사람이 있을 때만 값을 하는 물건" 으로 못 박는다</b> —
        /// 모듈만 세워 놓고 아무도 안 잡아도 되는 배가 되면 그건 냉각통의 대체재다.
        /// </summary>
        [Test]
        public void RadiatorAloneDoesNotCoverTheCoolantRoundTrip()
        {
            const float roundTripSeconds = 16.34f;

            Place(LastShiftModuleCatalog.Radiator, LastShiftZone.Cooling);
            var lockAt = SecondsUntilProtectionLock(0.86f, thrust: 0.92f, held: false);

            Assert.That(lockAt, Is.LessThan(roundTripSeconds),
                $"라디에이터만 세워도 냉각통 왕복({roundTripSeconds:F1}s)이 덮인다 — " +
                $"실측 잠금 {lockAt:F2}s. 그러면 밸브를 잡을 이유가 없다.");
        }

        /// <summary>
        /// 그러면서 <b>왕복은 밸브와 같이 있을 때 덮인다.</b> <c>CT-08</c> §7-(나)가 남긴 미결이고,
        /// §7-3 이 <b>밸브 값을 올려 푸는 길을 기각</b>했다 — 라디에이터는 상승항 쪽을 건드리므로
        /// 그 위험(두 상수의 차가 얇아 부호가 뒤집힘)이 없다.
        ///
        /// 실측: 밸브만 <c>28.00</c>초 → 밸브 + 라디에이터 <c>46.75</c>초(왕복의 <c>2.9</c>배).
        /// </summary>
        [Test]
        public void RadiatorWithHeldValveCoversTheCoolantRoundTrip()
        {
            const float roundTripSeconds = 16.34f;

            var valveOnly = SecondsUntilProtectionLock(0.86f, thrust: 0.92f, held: true);

            Place(LastShiftModuleCatalog.Radiator, LastShiftZone.Cooling);
            var withModule = SecondsUntilProtectionLock(0.86f, thrust: 0.92f, held: true);

            Assert.That(withModule, Is.GreaterThan(valveOnly),
                "라디에이터를 세웠는데 잠금이 안 늦었다.");
            Assert.That(withModule, Is.GreaterThan(roundTripSeconds),
                $"밸브 + 라디에이터로도 왕복({roundTripSeconds:F1}s)이 안 덮인다 — 실측 {withModule:F2}s.");
        }

        /// <summary>
        /// 라디에이터는 <b>하강항을 안 건드린다.</b> 냉각통을 연결한 배가 더 빨리 식으면 그건
        /// 잃은 기능을 다시 세우는 것이 아니라 증폭기이고, 조항 C-2 가 겨눈 것과 반대 방향이다.
        /// </summary>
        [Test]
        public void RadiatorDoesNotSpeedUpCooling()
        {
            var bare = HeatAfterCooling();
            Place(LastShiftModuleCatalog.Radiator, LastShiftZone.Cooling);
            var withModule = HeatAfterCooling();

            Assert.That(withModule, Is.EqualTo(bare).Within(1e-5f),
                "라디에이터가 냉각 복구 속도를 바꿨다 — 상승항에만 걸려야 한다.");
        }

        // ── 예비 전력실 ──────────────────────────────────────────────────────

        /// <summary>
        /// <b>고치는 것은 하나다 — 미연결 하한(<c>0.40</c>)이 <c>S-P2</c> 발동선과 같은 값이라는 것.</b>
        /// 그래서 전력을 잃은 배가 남은 항해 내내 <c>PowerCascade</c>(Fault)에 눌러앉는다.
        /// </summary>
        [Test]
        public void ReservePowerLiftsTheUnpoweredFloorAboveTheCascadeTrigger()
        {
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Power);
            var effects = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(effects.BusFloor, Is.EqualTo(
                    LastShiftRecoveryTuning.UnpoweredBusCeiling + LastShiftModuleEffects.BusFloorBonus).Within(1e-5f));
            Assert.That(effects.BusFloor, Is.GreaterThan(LastShiftSituationTable.PowerCascadeTrigger),
                "예비 전력실을 세워도 bus 가 S-P2 발동선까지 내려간다 — 이 모듈이 고치려던 것이 그것이다.");
            Assert.That(effects.BusFloor, Is.LessThan(LastShiftSituationTable.BusDetachedTrigger),
                "하한이 S-P1 발동선 위로 올라갔다 — 배터리를 안 꽂아도 되는 배가 된다(조항 C-2 위반).");
        }

        /// <summary>
        /// <b>전력 시계를 죽이면 안 된다.</b> 프리셋 <c>2</c> 의 시작값 <c>0.63</c> 은 "운석 후
        /// <c>0.40</c> 아래면 시계가 한 번도 안 돈다" 를 피해 고른 값이다
        /// (<see cref="LastShiftPresetFactory"/> 주석). 하한을 <c>0.50</c> 까지 올리면 그 프리셋에
        /// 남는 시계가 <c>0.53</c>초라 <b>그 조정이 사 둔 여유를 모듈이 도로 먹는다.</b>
        ///
        /// 실측(<c>+0.05</c>, 배터리 고정): <c>3.10</c>초 · <c>3.30</c>초 · <c>2.30</c>초.
        /// </summary>
        [TestCase(LastShiftPreset.HighHeatHighThrust)]
        [TestCase(LastShiftPreset.PowerOverloadLooseBattery)]
        [TestCase(LastShiftPreset.BadAttitudeHighOxygen)]
        public void ReservePowerLeavesThePowerClockRunning(LastShiftPreset preset)
        {
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Power);

            var (state, _) = AfterMeteor(preset);
            var busAfterMeteor = state.BusPower;
            var floor = LastShiftRecoveryTuning.UnpoweredBusCeiling + LastShiftModuleEffects.BusFloorBonus;

            Assert.That(busAfterMeteor, Is.GreaterThan(floor),
                $"{preset} 은 운석 직후 bus({busAfterMeteor:F4})가 이미 예비 전력실 하한({floor:F2}) " +
                "아래다 — 그 프리셋에서 전력 시계가 한 번도 안 돈다.");
        }

        /// <summary>
        /// <b>가장 짧은 시계는 배터리가 굴러간 프리셋 <c>2</c> 다.</b>
        /// <see cref="AfterMeteor"/> 는 아이템을 안 넘겨 <c>batteryTravel = 0</c> 이므로 위 검사가
        /// 그 경우를 못 본다 — 그런데 그 프리셋의 이름이 <c>LooseBattery</c> 이고, 운석이
        /// <c>batteryTravel × 0.16</c> 을 더 깎는다. <b>하한을 구속한 것이 이 값이므로 따로 건다.</b>
        ///
        /// <c>0.5266</c> 은 <see cref="LastShiftPresetFactory"/> 주석이 실측으로 적어 둔 값이다
        /// (배터리 <c>0.55m</c> 이동). 하한 <c>0.50</c> 이면 남는 시계가 <c>0.53</c>초다.
        /// </summary>
        [Test]
        public void ReservePowerFloorLeavesClockEvenForTheLoosestBattery()
        {
            const float worstBusAfterMeteor = 0.5266f;
            var floor = LastShiftRecoveryTuning.UnpoweredBusCeiling + LastShiftModuleEffects.BusFloorBonus;
            var clockSeconds = (worstBusAfterMeteor - floor) / LastShiftRecoveryTuning.BusDropPerSecond;

            Assert.That(clockSeconds, Is.GreaterThan(1f),
                $"배터리가 굴러간 프리셋 2 에서 전력 시계가 {clockSeconds:F2}초밖에 안 남는다 — " +
                "그 프리셋의 BusPower 재조정(0.98 -> 0.63)이 사 둔 여유를 모듈이 도로 먹었다.");
        }

        /// <summary>
        /// 하한은 <b>하강이 멎는 선</b>이지 발전기가 아니다. 이미 하한 아래인 bus 는 안 오른다 —
        /// 오르면 모듈이 전력을 만드는 물건이 되고, 그건 배터리 꽂기의 대체재다.
        /// </summary>
        [Test]
        public void ReservePowerDoesNotRaiseABusThatIsAlreadyBelowTheFloor()
        {
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Power);

            var state = NewHeatState(0.20f, thrust: 0.10f);
            state.BusPower = 0.30f;
            var pressures = LastShiftZonePressures.Uniform(1f);
            var containment = new LastShiftContainment { CoolingRestored = true, OxygenRestored = true };

            for (var elapsed = 0f; elapsed < 20f; elapsed += Step)
                LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, Breach, LastShiftDoorState.AllOpen, Step);

            Assert.That(state.BusPower, Is.EqualTo(0.30f).Within(1e-4f),
                $"하한 아래였던 bus 가 움직였다 — 실측 {state.BusPower:F4}. 모듈은 전력을 안 만든다.");
        }

        /// <summary>모듈이 실제로 시뮬 안에서 하한을 올린다. 상수 비교가 아니라 tick 이 답한다.</summary>
        [Test]
        public void ReservePowerStopsTheBusDropAtTheRaisedFloor()
        {
            var bare = BusAfterUnpoweredDrain();
            Assert.That(bare, Is.EqualTo(LastShiftRecoveryTuning.UnpoweredBusCeiling).Within(1e-4f));

            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Power);
            var withModule = BusAfterUnpoweredDrain();

            Assert.That(withModule, Is.EqualTo(
                    LastShiftRecoveryTuning.UnpoweredBusCeiling + LastShiftModuleEffects.BusFloorBonus).Within(1e-4f),
                $"예비 전력실이 서 있는데 bus 가 {withModule:F4} 까지 내려갔다.");
        }

        // ── 예비 아이템 ──────────────────────────────────────────────────────

        /// <summary>예비 전력실은 배터리 하나, 화물칸는 세 계통 한 벌이다(카탈로그 §3.3).</summary>
        [Test]
        public void SpareItemsFollowTheCatalogTable()
        {
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Power);
            var power = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(power.HasSpare(LastShiftItemRole.Battery), Is.True);
            Assert.That(power.HasSpare(LastShiftItemRole.CoolingCanister), Is.False);
            Assert.That(power.HasSpare(LastShiftItemRole.PatchPlate), Is.False);
            Assert.That(power.SpareItemCount, Is.EqualTo(1));

            LastShiftPlacedModules.Clear();
            Place(LastShiftModuleCatalog.CargoBay, LastShiftZone.Cooling);
            var depot = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(depot.HasSpare(LastShiftItemRole.Battery), Is.True);
            Assert.That(depot.HasSpare(LastShiftItemRole.CoolingCanister), Is.True);
            Assert.That(depot.HasSpare(LastShiftItemRole.PatchPlate), Is.True);
            Assert.That(depot.HasSpare(LastShiftItemRole.Tether), Is.False,
                "Tether 는 어느 계통도 안 되돌린다(CT-01 §6) — 예비로 비치할 물건이 아니다.");
            Assert.That(depot.SpareItemCount, Is.EqualTo(3));
        }

        /// <summary>
        /// <b>조항 E-2 — 예비는 역할당 하나다.</b> 예비 전력실과 화물칸을 둘 다 세워도
        /// 배터리는 하나이고, 같은 모듈을 둘 세워도 하나다.
        /// </summary>
        [Test]
        public void SpareItemsDoNotStackAcrossModules()
        {
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Power);
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Cockpit);
            Place(LastShiftModuleCatalog.CargoBay, LastShiftZone.Cooling);
            Place(LastShiftModuleCatalog.CargoBay, LastShiftZone.LifeSupport);

            var effects = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(effects.SpareItemCount, Is.EqualTo(3),
                $"모듈 넷을 세우니 예비가 {effects.SpareItemCount} 개다 — 역할당 하나여야 한다(조항 E-2).");
        }

        // ── 조항 E-1 · E-3 ───────────────────────────────────────────────────

        /// <summary>
        /// <b>조항 E-1 — 진공이 된 구역의 모듈은 효과를 잃는다.</b> 모듈도 방이고, 공기가 없는
        /// 방의 재생기·라디에이터·배전반이 계속 돌면 "어느 구역에 붙일까"(카탈로그 §7-C)가
        /// 결정이 아니게 된다.
        /// </summary>
        [Test]
        public void ModulesInAVacuumZoneStopWorking()
        {
            Place(LastShiftModuleCatalog.Hydroponics, Breach);
            Place(LastShiftModuleCatalog.Radiator, Breach);
            Place(LastShiftModuleCatalog.ReservePower, Breach);
            Place(LastShiftModuleCatalog.CargoBay, Breach);

            var pressures = LastShiftZonePressures.Uniform(1f);
            Assert.That(LastShiftModuleEffects.Collect(pressures).Any, Is.True);

            pressures[Breach] = LastShiftRecoveryTuning.VacuumOxygenPressure;
            var vacuum = LastShiftModuleEffects.Collect(pressures);

            Assert.That(vacuum.Any, Is.False,
                "진공이 된 구역의 모듈이 계속 돈다 — 그러면 파공 옆에 붙이는 것에 대가가 없다.");
        }

        /// <summary>
        /// <b>조항 E-3 — 같은 효과는 한 번만 쌓인다.</b> 계수를 아무리 잘 골라도 누적을 안 막으면
        /// 값을 두 번 사서 뚫는다: 재생기 둘이 곱으로 쌓이면 <c>0.85² = 0.72</c> 이고, 그때
        /// <c>HighHeatHighThrust</c> 방치가 <c>339</c>초로 도킹 타이머 밖이다.
        /// </summary>
        [Test]
        public void SameEffectDoesNotStack()
        {
            Place(LastShiftModuleCatalog.Hydroponics, Breach);
            Place(LastShiftModuleCatalog.Hydroponics, Breach);
            Place(LastShiftModuleCatalog.Radiator, LastShiftZone.Cooling);
            Place(LastShiftModuleCatalog.Radiator, LastShiftZone.Power);
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Power);
            Place(LastShiftModuleCatalog.ReservePower, LastShiftZone.Cockpit);

            var effects = LastShiftModuleEffects.Collect(LastShiftZonePressures.Uniform(1f));

            Assert.That(effects.OxygenLeakMultiplierFor(Breach),
                Is.EqualTo(1f - LastShiftModuleEffects.OxygenLeakReduction).Within(1e-5f),
                "재생기 둘이 곱으로 쌓였다 — 여력 4 로 방치의 대가를 통째로 살 수 있게 된다.");
            Assert.That(effects.HeatRiseMultiplier,
                Is.EqualTo(1f - LastShiftModuleEffects.HeatRiseReduction).Within(1e-5f),
                "라디에이터 둘이 쌓였다 — 밸브와 합쳐 열이 멎는다.");
            Assert.That(effects.BusFloor, Is.EqualTo(
                    LastShiftRecoveryTuning.UnpoweredBusCeiling + LastShiftModuleEffects.BusFloorBonus).Within(1e-5f),
                "예비 전력실 둘이 쌓였다 — 하한 0.50 은 프리셋 2 의 전력 시계를 죽인다.");
        }

        // ── 가격 재검산 (§9-2) ───────────────────────────────────────────────

        /// <summary>
        /// <b>격납고 <c>5</c> — 유지.</b> 조항 <c>B-1</c> 개정으로 한 기항 최대 수입이
        /// <c>5 → 4</c> 가 되면서 <b>단독 구매가 아예 불가능해졌다</b>
        /// (<c>campaign-scale-and-combat-balance-v1.md</c> §2.5). 이 문서가 <c>4</c> 로 내리는
        /// 안을 기각한 근거("단독 구매 확률 <c>20% → 40%</c>")가 개정판에서는 확률 <c>0%</c> 로
        /// 더 강해진다 — 그래서 가격이 아니라 수입을 조였다.
        /// </summary>
        [Test]
        public void HangarPriceStillRequiresSavingUp()
        {
            var frame = LastShiftModuleCatalog.At(LastShiftModuleCatalog.Hangar);

            Assert.That(frame.MaintenanceCost, Is.GreaterThan(LastShiftMaintenance.MaxPortIncome),
                "격납고를 한 기항 수입으로 산다 — 조항 B-1 개정의 §2.5 판정이 깨졌다.");

            // 최고 성적(4/4)으로도 못 닿는다. 어떤 성적이든 두 기항이다.
            Assert.That(LastShiftMaintenance.IncomeFor(LastShiftMaintenance.MaxLatches),
                Is.LessThan(frame.MaintenanceCost));
            Assert.That(frame.MaintenanceCost,
                Is.LessThanOrEqualTo(LastShiftMaintenance.MaxPortIncome + LastShiftMaintenance.IncomeFor(0)),
                "최악 성적 둘로도 못 닿으면 저축 구간이 설계보다 길다.");
        }

        /// <summary>
        /// <b>개방 <c>2</c>.</b> 래치 <c>4/4</c>(여력 <c>5</c>)로도 잠긴 방 셋을 다 열 수 없어야
        /// 한다(<c>voyage-run-structure-v1.md</c> §4.1-(가)). <c>1</c> 이면 <c>5</c> 로 셋이 다
        /// 열리고, <c>3</c> 이면 개방이 배치보다 비싸져 "원래 있는 문을 여는 것이 밖에 새로 짓는
        /// 것보다 비싸다" 가 된다 — 카탈로그 §0-2 의 읽힘이 깨진다.
        /// </summary>
        [Test]
        public void UnlockPriceKeepsTheThreeLockedRoomsOutOfReachInOnePort()
        {
            var unlock = LastShiftMaintenance.PriceOf(LastShiftMaintenanceItem.CompartmentUnlock);
            const int lockedRooms = 3;

            Assert.That(unlock * lockedRooms, Is.GreaterThan(LastShiftMaintenance.MaxPortIncome),
                "한 기항 최대 수입으로 잠긴 방 셋이 다 열린다 — §4.1-(가) 위반.");
            Assert.That(unlock * 2, Is.LessThanOrEqualTo(LastShiftMaintenance.MaxPortIncome),
                "최고 성적으로도 둘을 못 연다 — 개방이 지나치게 비싸다.");

            var cheapestPlacement = LastShiftModuleCatalog.At(LastShiftModuleCatalog.Corridor).MaintenanceCost;
            Assert.That(unlock, Is.GreaterThan(cheapestPlacement),
                "개방이 가장 싼 배치보다 싸거나 같다 — 위치 고정인 쪽이 더 싸야 두 계열이 갈려 읽힌다.");
        }

        /// <summary>
        /// <b>최소 보장 <c>1</c>.</b> 래치 <c>0</c> 항해도 기항에서 무언가는 할 수 있어야 한다 —
        /// <c>RG-3</c>(영구 잠금 금지)의 항해판이다. 그 <c>1</c> 로 실제로 살 수 있는 것이
        /// 있어야 보장이 성립하므로, 가격 <c>1</c> 짜리의 존재를 같이 건다.
        /// </summary>
        [Test]
        public void MinimumIncomeAlwaysBuysSomething()
        {
            var worstIncome = LastShiftMaintenance.IncomeFor(0);
            Assert.That(worstIncome, Is.EqualTo(LastShiftMaintenance.MinimumIncome).And.GreaterThan(0));

            Assert.That(LastShiftMaintenance.PriceOf(LastShiftMaintenanceItem.Repair),
                Is.LessThanOrEqualTo(worstIncome), "최악 기항에서 복구조차 못 산다.");
            Assert.That(LastShiftModuleCatalog.At(LastShiftModuleCatalog.Corridor).MaintenanceCost,
                Is.LessThanOrEqualTo(worstIncome), "최악 기항에서 가장 싼 배치도 못 산다.");
        }

        /// <summary>
        /// <b>카탈로그 v2 열 항목의 번호·이름·발자국·가격을 정본 표와 통째로 대조한다</b>
        /// (맵 개편 §3.3). 번호가 어긋나면 산 것과 다른 효과가 붙고, 그 어긋남은 화면 어디에도
        /// 안 보인다. <b>발자국까지 같이 거는 것은 v2 의 근거가 "이관 방의 현행 치수를 한 칸도
        /// 안 바꿨다" 이기 때문이다</b> — 치수가 움직이면 그 근거가 조용히 거짓이 된다.
        /// </summary>
        [Test]
        public void CatalogIndexConstantsMatchTheCatalogTable()
        {
            var expected = new (int Index, string Name, float LengthX, float WidthZ, int Cost)[]
            {
                (LastShiftModuleCatalog.Corridor, "연결 통로", 4f, 2f, 1),
                (LastShiftModuleCatalog.Observatory, "관측실", 3f, 4f, 1),
                (LastShiftModuleCatalog.ReservePower, "예비 전력실", 4f, 4f, 2),
                (LastShiftModuleCatalog.Radiator, "방열 라디에이터실", 3f, 6f, 2),
                (LastShiftModuleCatalog.ServerRoom, "서버/통신실", 4f, 6f, 2),
                (LastShiftModuleCatalog.Workshop, "정비창", 5f, 5f, 2),
                (LastShiftModuleCatalog.MedBay, "의무실", 5f, 5f, 2),
                (LastShiftModuleCatalog.Hydroponics, "수경재배", 6f, 6f, 3),
                (LastShiftModuleCatalog.CargoBay, "화물칸", 8f, 8f, 3),
                (LastShiftModuleCatalog.Hangar, "격납고", 8f, 10f, 5)
            };

            Assert.That(LastShiftModuleCatalog.Count, Is.EqualTo(expected.Length),
                "카탈로그 v2 는 열 종이다 — 구명정은 고정에도 카탈로그에도 없다(확정판 §2.1).");

            for (var slot = 0; slot < expected.Length; slot++)
            {
                var row = expected[slot];
                Assert.That(row.Index, Is.EqualTo(slot), $"{row.Name} 의 번호 상수가 표 자리와 갈렸다");

                var kind = LastShiftModuleCatalog.At(slot);
                Assert.That(kind.Name, Is.EqualTo(row.Name));
                Assert.That(kind.LengthX, Is.EqualTo(row.LengthX).Within(1e-5f), $"{row.Name} 의 깊이");
                Assert.That(kind.WidthZ, Is.EqualTo(row.WidthZ).Within(1e-5f), $"{row.Name} 의 접면 폭");
                Assert.That(kind.MaintenanceCost, Is.EqualTo(row.Cost), $"{row.Name} 의 가격");
            }
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 오버레이에 직접 등록한다. <b>판정기를 안 거친다</b> — 이 파일이 재는 것은 계수이지
        /// 배치 가능성이 아니고, 그건 <c>LastShiftPlacementCursorTests</c> 소관이다. 겹치지
        /// 않도록 등록 순서로 <c>x</c> 를 민다.
        /// </summary>
        private static void Place(int catalogIndex, LastShiftZone zone)
        {
            var minX = 1000f + LastShiftPlacedModules.Count * 20f;
            LastShiftPlacedModules.Register(minX, minX + 10f, 0f, 10f, zone, catalogIndex);
        }

        private static (LastShiftShipState, LastShiftZonePressures) AfterMeteor(LastShiftPreset preset)
        {
            var state = LastShiftPresetFactory.Create(preset);
            var pressures = LastShiftZonePressures.Uniform(state.OxygenPressure);
            state = LastShiftMeteorApplication.Apply(
                LastShiftMeteorStimulus.Canonical, state, ref pressures, Breach, null);
            return (state, pressures);
        }

        private static void Integrate(
            LastShiftDoorState doors, float seconds, LastShiftShipState state,
            LastShiftZonePressures pressures, out float breachVacuumSeconds)
        {
            var containment = new LastShiftContainment { CoolingRestored = true, PowerRestored = true };

            breachVacuumSeconds = float.PositiveInfinity;
            for (var elapsed = 0f; elapsed < seconds; elapsed += Step)
            {
                LastShiftDeterioration.Tick(ref state, ref pressures, containment, Breach, doors, Step);

                if (float.IsPositiveInfinity(breachVacuumSeconds) &&
                    LastShiftVerdictResolver.IsZoneVacuum(pressures[Breach], false))
                    breachVacuumSeconds = elapsed + Step;
            }
        }

        private static float SecondsUntilCockpitLosesSuccessLine(
            LastShiftDoorState doors, LastShiftShipState state, LastShiftZonePressures pressures)
        {
            var containment = new LastShiftContainment { CoolingRestored = true, PowerRestored = true };

            for (var elapsed = 0f; elapsed < LastShiftRecoveryTuning.DockingTimerSeconds; elapsed += Step)
            {
                LastShiftDeterioration.Tick(ref state, ref pressures, containment, Breach, doors, Step);
                if (pressures[LastShiftZone.Cockpit] < LastShiftRecoveryTuning.DockingSuccessOxygen)
                    return elapsed + Step;
            }
            return float.PositiveInfinity;
        }

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

            for (var elapsed = 0f; elapsed < 120f; elapsed += Step)
            {
                var report = LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, Breach, LastShiftDoorState.AllOpen, Step);
                if (report.HeatProtectionEngaged) return elapsed + Step;
            }
            return float.PositiveInfinity;
        }

        /// <summary>냉각을 복구한 배가 <c>30</c>초 뒤 남긴 열. 하강항이 안 바뀌었는지를 잰다.</summary>
        private static float HeatAfterCooling()
        {
            var state = NewHeatState(0.90f, thrust: 0.92f);
            var pressures = LastShiftZonePressures.Uniform(1f);
            var containment = new LastShiftContainment
            {
                CoolingRestored = true,
                PowerRestored = true,
                OxygenRestored = true
            };

            for (var elapsed = 0f; elapsed < 30f; elapsed += Step)
                LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, Breach, LastShiftDoorState.AllOpen, Step);
            return state.EngineHeat;
        }

        /// <summary>전력 미복구·미봉쇄로 충분히 오래 돌린 뒤의 bus. 하한에 앉아 있어야 한다.</summary>
        private static float BusAfterUnpoweredDrain()
        {
            var state = NewHeatState(0.20f, thrust: 0.10f);
            var pressures = LastShiftZonePressures.Uniform(1f);
            var containment = new LastShiftContainment { CoolingRestored = true, OxygenRestored = true };

            for (var elapsed = 0f; elapsed < 60f; elapsed += Step)
                LastShiftDeterioration.Tick(
                    ref state, ref pressures, containment, Breach, LastShiftDoorState.AllOpen, Step);
            return state.BusPower;
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
