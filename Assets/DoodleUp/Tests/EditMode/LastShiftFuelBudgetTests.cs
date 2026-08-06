using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CT-06 N3·N4·N5 — 연료 예산 / 도킹 누적 진행 / <c>S-H2</c> 열 tick 가속.
    /// 근거는 <c>docs/ship-elements-and-situations-v1.md</c> §2.3 B-2, §3.3, §7.2 다.
    ///
    /// 수치(<c>0.0040</c>, <c>150 thrust·s</c>, <c>0.035/s</c>)는 <c>game-balance</c> 검증 전
    /// 초안이므로 여기서 리터럴로 다시 적지 않는다 — 값이 바뀌어도 <b>관계</b>는 그대로여야
    /// 하고, 리터럴을 박으면 밸런스 조정이 곧 테스트 실패가 되어 튜닝을 막는다.
    /// </summary>
    public sealed class LastShiftFuelBudgetTests
    {
        /// <summary>아무것도 복구·포기되지 않은 상태. 악화 분기가 전부 살아 있다.</summary>
        private static LastShiftContainment NothingContained => new();

        /// <summary>냉각만 복구된 상태. 열 분기를 끄고 연료만 볼 때 쓴다.</summary>
        private static LastShiftContainment CoolingFixed => new() { CoolingRestored = true };

        private static LastShiftShipState Nominal(float thrust)
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.PowerOverloadLooseBattery);
            state.ThrustDemand = thrust;
            state.EngineHeat = 0.10f;
            return state;
        }

        // ── N3 연료 예산 ──────────────────────────────────────────────────

        [Test]
        public void PresetsStartWithAFullFuelBudgetAndNoDockProgress()
        {
            foreach (LastShiftPreset preset in System.Enum.GetValues(typeof(LastShiftPreset)))
            {
                var state = LastShiftPresetFactory.Create(preset);
                Assert.That(state.FuelReserve, Is.EqualTo(LastShiftRecoveryTuning.FuelReserveInitial).Within(0.0001f),
                    $"{preset} 이 연료를 가득 채우지 않는다 — 프리셋은 사고 상황이지 항해 시점이 아니다.");
                Assert.That(state.DockProgress, Is.EqualTo(0f).Within(0.0001f),
                    $"{preset} 이 도킹 진행도를 0 이 아닌 값으로 시작한다.");
            }
        }

        [Test]
        public void FuelDrainsInProportionToThrustDemand()
        {
            // 추력을 두 배로 하면 같은 시간에 정확히 두 배가 탄다. 비례가 이 항목의 전부다.
            var low = Nominal(0.25f);
            var high = Nominal(0.50f);
            LastShiftDeterioration.Tick(ref low, CoolingFixed, 10f);
            LastShiftDeterioration.Tick(ref high, CoolingFixed, 10f);

            var lowBurn = LastShiftRecoveryTuning.FuelReserveInitial - low.FuelReserve;
            var highBurn = LastShiftRecoveryTuning.FuelReserveInitial - high.FuelReserve;

            Assert.That(lowBurn, Is.GreaterThan(0f), "추력이 있는데 연료가 안 준다.");
            Assert.That(highBurn, Is.EqualTo(lowBurn * 2f).Within(0.0001f),
                "연료 소모가 ThrustDemand 에 비례하지 않는다.");
            Assert.That(lowBurn,
                Is.EqualTo(LastShiftRecoveryTuning.FuelDrainPerThrustSecond * 0.25f * 10f).Within(0.0001f),
                "소모량이 튜닝 상수와 어긋난다.");
        }

        [Test]
        public void ZeroThrustBurnsNoFuel()
        {
            var state = Nominal(0f);
            LastShiftDeterioration.Tick(ref state, CoolingFixed, 60f);
            Assert.That(state.FuelReserve, Is.EqualTo(LastShiftRecoveryTuning.FuelReserveInitial).Within(0.0001f),
                "추력 0 인데 연료가 준다 — 엔진을 끈 것이 예산 집행이 되면 안 된다.");
        }

        [Test]
        public void FuelNeverRefillsAndNeverGoesNegative()
        {
            // 보급 지점 0개(§2.3 B-2). 어떤 복구 조합에서도 연료는 늘지 않는다.
            var combinations = new[]
            {
                NothingContained,
                CoolingFixed,
                new LastShiftContainment { PowerRestored = true },
                new LastShiftContainment { OxygenRestored = true },
                new LastShiftContainment { CoolingRestored = true, PowerRestored = true, OxygenRestored = true },
                new LastShiftContainment { CoolingSacrificed = true, PowerSacrificed = true, OxygenSacrificed = true }
            };

            // 걸음 수를 고정하지 않는다 — 복구 조합에 따라 추력 상한이 달라져(열 보호 0.25,
            // 냉각 포기 0.35) 같은 시간에 타는 양이 달라진다. 상한을 상수에서 뽑아 넉넉히 잡고
            // "언젠가 0 에 닿는다" 만 본다.
            var worstCaseSeconds = LastShiftRecoveryTuning.FuelReserveInitial /
                                   (LastShiftRecoveryTuning.FuelDrainPerThrustSecond *
                                    LastShiftRecoveryTuning.ProtectedThrustCeiling);

            foreach (var containment in combinations)
            {
                var state = Nominal(0.40f);
                var previous = state.FuelReserve;
                for (var step = 0; step < worstCaseSeconds + 10f && state.FuelReserve > 0f; step++)
                {
                    LastShiftDeterioration.Tick(ref state, containment, 1f);
                    Assert.That(state.FuelReserve, Is.LessThanOrEqualTo(previous + 0.0001f),
                        "연료가 늘었다 — 보급 수단은 존재하지 않아야 한다.");
                    Assert.That(state.FuelReserve, Is.GreaterThanOrEqualTo(0f), "연료가 음수가 됐다.");
                    previous = state.FuelReserve;
                }
                Assert.That(state.FuelReserve, Is.EqualTo(0f).Within(0.0001f),
                    "최악 상한으로 계산한 시간을 넘겨 태웠는데 연료가 남았다.");
            }
        }

        // ── N4 도킹 누적 진행 ─────────────────────────────────────────────

        [Test]
        public void DockProgressAccumulatesThrustSeconds()
        {
            var state = Nominal(0.50f);
            LastShiftDeterioration.Tick(ref state, CoolingFixed, 20f);
            Assert.That(state.DockProgress, Is.EqualTo(0.50f * 20f).Within(0.0001f),
                "DockProgress 가 ThrustDemand × dt 로 쌓이지 않는다.");
        }

        [Test]
        public void DockingRequiresAccumulatedProgressNotJustInstantThrust()
        {
            // 추력·산소는 성공선을 넘겼는데 누적만 모자란 상태. N4 이전에는 이것이 곧 성공이었다.
            var state = Nominal(0.90f);
            state.OxygenPressure = 1f;
            state.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds - 1f;
            Assert.That(LastShiftVerdictResolver.MeetsDockingConditions(state, true), Is.False,
                "누적 진행이 모자란데 도킹이 성립한다 — 순간 추력만 보던 예전 판정이 남아 있다.");

            state.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds;
            Assert.That(LastShiftVerdictResolver.MeetsDockingConditions(state, true), Is.True,
                "누적 진행이 목표에 닿았는데 도킹이 성립하지 않는다.");
        }

        [Test]
        public void InstantThrustAndOxygenGatesStillApplyOnTopOfProgress()
        {
            // N4 는 조건을 <b>더한</b> 것이지 기존 둘을 대체한 것이 아니다.
            var state = Nominal(0.90f);
            state.OxygenPressure = 1f;
            state.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds + 50f;

            state.ThrustDemand = LastShiftRecoveryTuning.DockingSuccessThrust - 0.01f;
            Assert.That(LastShiftVerdictResolver.MeetsDockingConditions(state, true), Is.False,
                "추력 성공선이 사라졌다.");

            state.ThrustDemand = 0.90f;
            state.OxygenPressure = LastShiftRecoveryTuning.DockingSuccessOxygen - 0.01f;
            Assert.That(LastShiftVerdictResolver.MeetsDockingConditions(state, true), Is.False,
                "조종석 압력 성공선이 사라졌다.");
        }

        [Test]
        public void FuelBudgetCoversTheDockingTargetWithMargin()
        {
            // §2.3 B-2 의 설계 의도: 총 예산 250 thrust·s 로 요구 150 을 채우고 67% 가 남는다.
            // 값이 아니라 "여유가 양수" 라는 관계만 고정한다 — 밸런스가 숫자를 바꿔도 이 성질은
            // 남아야 하고, 이게 깨지면 어떤 조종을 해도 도킹이 불가능해진다.
            var totalThrustSeconds =
                LastShiftRecoveryTuning.FuelReserveInitial / LastShiftRecoveryTuning.FuelDrainPerThrustSecond;
            Assert.That(totalThrustSeconds, Is.GreaterThan(LastShiftRecoveryTuning.DockTargetThrustSeconds),
                "연료 총 예산이 도킹 요구보다 적다 — 도킹이 원리적으로 불가능한 배다.");
        }

        [Test]
        public void BurningTheWholeBudgetReachesTheDockingTarget()
        {
            var state = Nominal(0.60f);
            for (var step = 0; step < 1000 && state.FuelReserve > 0f; step++)
                LastShiftDeterioration.Tick(ref state, CoolingFixed, 1f);

            Assert.That(state.FuelReserve, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.DockProgress,
                Is.GreaterThanOrEqualTo(LastShiftRecoveryTuning.DockTargetThrustSeconds),
                "예산을 전부 태웠는데 도킹 목표에 못 닿는다.");
        }

        [Test]
        public void EmptyTankStopsDockProgressEvenAtFullThrust()
        {
            // 연료 0 인 배가 추력 슬라이더만 올려 도킹을 채우면 표류 판정이 무의미해진다.
            var state = Nominal(1f);
            state.FuelReserve = 0f;
            var before = state.DockProgress;
            LastShiftDeterioration.Tick(ref state, CoolingFixed, 30f);

            Assert.That(state.DockProgress, Is.EqualTo(before).Within(0.0001f),
                "연료가 없는데 도킹 진행이 올랐다.");
        }

        [Test]
        public void ProgressStopsExactlyWhereTheFuelRanOut()
        {
            // 한 tick 안에서 연료가 소진되는 경계. 큰 dt 로 밀어도 남은 연료가 낼 수 있는
            // 추력적분만 실려야 한다 — 여기서 어긋나면 마지막 한 걸음이 공짜가 된다.
            var state = Nominal(0.80f);
            var expected = LastShiftRecoveryTuning.FuelReserveInitial /
                           LastShiftRecoveryTuning.FuelDrainPerThrustSecond;

            LastShiftDeterioration.Tick(ref state, CoolingFixed, 10000f);

            Assert.That(state.FuelReserve, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.DockProgress, Is.EqualTo(expected).Within(0.01f),
                "연료가 낼 수 있는 총 추력적분과 실제 누적이 다르다.");
        }

        // ── N3 연료 소진 즉시 판정 ────────────────────────────────────────

        [Test]
        public void EmptyTankShortOfTargetIsAdriftImmediately()
        {
            var state = Nominal(0.50f);
            state.FuelReserve = 0f;
            state.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds - 1f;

            Assert.That(LastShiftVerdictResolver.IsStrandedWithoutFuel(state), Is.True);
            Assert.That(LastShiftVerdictResolver.EvaluateContinuous(state, true),
                Is.EqualTo(LastShiftVerdict.FailureAdrift),
                "연료가 없고 도킹도 못 채웠는데 판정이 안 난다 — RG-3 이 금지한 영구 잠금이다.");
        }

        [Test]
        public void EmptyTankWithTargetAlreadyReachedIsNotAFailure()
        {
            var state = Nominal(0.50f);
            state.FuelReserve = 0f;
            state.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds;

            Assert.That(LastShiftVerdictResolver.IsStrandedWithoutFuel(state), Is.False);
            Assert.That(LastShiftVerdictResolver.EvaluateContinuous(state, true),
                Is.EqualTo(LastShiftVerdict.Pending),
                "도킹 요구를 이미 채웠는데 연료 0 이라고 실패시킨다.");
        }

        [Test]
        public void FuelWithLeftoverIsNotAFailureYet()
        {
            var state = Nominal(0.50f);
            state.FuelReserve = 0.01f;
            state.DockProgress = 0f;
            Assert.That(LastShiftVerdictResolver.EvaluateContinuous(state, true),
                Is.EqualTo(LastShiftVerdict.Pending),
                "연료가 남았는데 표류로 판정한다.");
        }

        [Test]
        public void CrewDeathStillOutranksFuelExhaustion()
        {
            // 원인이 둘이 됐으므로 우선순위를 못박는다. 사람이 다 죽은 배의 사인은 질식이다.
            var state = Nominal(0.50f);
            state.FuelReserve = 0f;
            state.DockProgress = 0f;
            Assert.That(LastShiftVerdictResolver.EvaluateContinuous(state, false),
                Is.EqualTo(LastShiftVerdict.FailureAsphyxiation));
        }

        // ── N5 S-H2 열 tick 가속 ──────────────────────────────────────────

        [Test]
        public void HeatRunawayNeedsBothHighHeatAndDisconnectedCooling()
        {
            var hot = Nominal(0.90f);
            hot.EngineHeat = LastShiftRecoveryTuning.HeatRunawayTrigger;
            Assert.That(LastShiftDeterioration.IsHeatRunaway(hot, NothingContained), Is.True);
            Assert.That(LastShiftDeterioration.IsHeatRunaway(hot, CoolingFixed), Is.False,
                "냉각이 연결됐는데 폭주로 본다.");

            var warm = Nominal(0.90f);
            warm.EngineHeat = LastShiftRecoveryTuning.HeatRunawayTrigger - 0.01f;
            Assert.That(LastShiftDeterioration.IsHeatRunaway(warm, NothingContained), Is.False,
                "발동선 아래인데 폭주로 본다.");
        }

        [Test]
        public void HeatRisesAtTheRunawayRateAboveTheTrigger()
        {
            var state = Nominal(0.90f);
            state.EngineHeat = LastShiftRecoveryTuning.HeatRunawayTrigger;
            LastShiftDeterioration.Tick(ref state, NothingContained, 1f);

            Assert.That(state.EngineHeat - LastShiftRecoveryTuning.HeatRunawayTrigger,
                Is.EqualTo(LastShiftRecoveryTuning.HeatRiseRunawayPerSecond).Within(0.0001f),
                "S-H2 조건인데 열이 가속하지 않는다.");
        }

        [Test]
        public void HeatRisesAtTheNormalRateBelowTheTrigger()
        {
            // 기존 tick 을 <b>대체하지 않고</b> 분기만 늘렸다는 것이 N5 의 요구다.
            var state = Nominal(0.90f);
            state.EngineHeat = 0.50f;
            LastShiftDeterioration.Tick(ref state, NothingContained, 1f);

            Assert.That(state.EngineHeat - 0.50f,
                Is.EqualTo(LastShiftRecoveryTuning.HeatRisePerSecond).Within(0.0001f),
                "발동선 아래에서 상승률이 바뀌었다 — 기존 tick 이 대체됐다.");
        }

        [Test]
        public void LowThrustStillStopsHeatEvenInRunawayRange()
        {
            // 가속 분기는 상승 조건 안쪽에만 있다. 추력을 내리는 것이 여전히 유효한 대응이어야
            // "추력을 내려 시간을 벌지" 라는 S-H2 의 선택지가 성립한다(§3.3).
            var state = Nominal(LastShiftRecoveryTuning.HeatRiseThrustThreshold);
            state.EngineHeat = LastShiftRecoveryTuning.HeatRunawayTrigger + 0.05f;
            var before = state.EngineHeat;
            LastShiftDeterioration.Tick(ref state, NothingContained, 5f);

            Assert.That(state.EngineHeat, Is.EqualTo(before).Within(0.0001f),
                "추력을 성공선까지 내렸는데도 열이 오른다.");
        }

        [Test]
        public void RunawayReachesProtectionSoonerThanTheNormalRate()
        {
            // 가속의 관측 가능한 결과. 같은 시작 열에서 폭주 쪽이 먼저 보호 발동에 닿는다.
            var runaway = Nominal(0.90f);
            runaway.EngineHeat = LastShiftRecoveryTuning.HeatRunawayTrigger;
            var normal = runaway;

            var runawaySeconds = SecondsToProtection(ref runaway, NothingContained);

            // 같은 열에서 가속 없이 올랐다면 걸렸을 시간. 상수로 직접 계산해 비교 대상을 만든다.
            var normalSeconds =
                (LastShiftRecoveryTuning.HeatProtectionTrigger - normal.EngineHeat) /
                LastShiftRecoveryTuning.HeatRisePerSecond;

            Assert.That(runawaySeconds, Is.LessThan(normalSeconds),
                "폭주 중인데 보호 발동까지 걸리는 시간이 평시와 같거나 더 길다.");
        }

        private static float SecondsToProtection(ref LastShiftShipState state, in LastShiftContainment containment)
        {
            const float step = 0.05f;
            var elapsed = 0f;
            while (state.EngineHeat < LastShiftRecoveryTuning.HeatProtectionTrigger && elapsed < 600f)
            {
                LastShiftDeterioration.Tick(ref state, containment, step);
                elapsed += step;
            }
            return elapsed;
        }
    }
}
