using System.Collections.Generic;
using System.Text;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CT-07 N7 — <c>RG-4</c> 조합 전수 검증(기획 §4.3).
    ///
    /// 대상은 <c>1,440</c> 조합이다. <c>240</c>(열 4 × 전력 4 × 산소 5 × 추진 3, §3.4)에
    /// 파공 구역 <c>3</c>종과 격리 여부 <c>2</c>종을 곱한 값이며 §0.2 의 <c>COMBO_TOTAL</c> 과 같다.
    ///
    /// <b>PASS 조건은 (5) 하나다.</b> "(1)과 (4)가 모두 거짓인 조합이 0개". 나머지 항목이 거짓인
    /// 조합은 정상이다 — 실패할 수 있어야 게임이고, 금지되는 것은 <b>아무것도 할 수 없는
    /// 상태</b>뿐이다. 특히 <c>(2)</c>·<c>(3)</c>은 <c>S-T2</c>(연료 여유 소실) 축에서 설계상
    /// 거짓이므로 세어서 보고만 한다.
    ///
    /// 조합을 조립한 뒤 <see cref="LastShiftSituationTracker"/> 로 <b>의도한 상황이 실제로
    /// 나오는지 먼저 확인</b>한다. 이게 없으면 조립이 틀렸을 때 검증이 엉뚱한 조합 1,440개를
    /// 통과시키고도 초록불을 낸다.
    /// </summary>
    public sealed class LastShiftRecoveryGuaranteeTests
    {
        private const float Step = 0.5f;
        private const float RepressurizeBudgetSeconds = 300f;
        private const float SurvivalBudgetSeconds =
            LastShiftRecoveryTuning.SuitOxygenInitial / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond; // 80s
        private const float LockReleaseBudgetSeconds = 60f;

        // ── 축 정의 ──────────────────────────────────────────────────────────
        // 각 축의 값 하나가 그 계통의 대표 상황 하나에 대응한다. 대표가 그것이 되도록 상태를
        // 고르되, 하위 상황이 함께 켜지는 것은 막지 않는다 — 실제 플레이가 그 모양이다
        // (열 1.00 이면 H1·H2·H3 이 전부 참이고 대표만 H3 다).

        private static readonly LastShiftSituation[] HeatAxis =
        {
            LastShiftSituation.None,
            LastShiftSituation.HeatCouplingLoose,
            LastShiftSituation.HeatRunaway,
            LastShiftSituation.HeatProtectionLock
        };

        private static readonly LastShiftSituation[] PowerAxis =
        {
            LastShiftSituation.None,
            LastShiftSituation.BusDetached,
            LastShiftSituation.PowerCascade,
            LastShiftSituation.PowerBlackout
        };

        private static readonly LastShiftSituation[] OxygenAxis =
        {
            LastShiftSituation.None,
            LastShiftSituation.HullLeak,
            LastShiftSituation.ZoneLowPressure,
            LastShiftSituation.DecompressionAlarm,
            LastShiftSituation.ZoneVacuum
        };

        private static readonly LastShiftSituation[] PropulsionAxis =
        {
            LastShiftSituation.None,
            LastShiftSituation.AttitudeDrift,
            LastShiftSituation.FuelMarginLost
        };

        private struct Combo
        {
            public LastShiftSituation Heat;
            public LastShiftSituation Power;
            public LastShiftSituation Oxygen;
            public LastShiftSituation Propulsion;
            public LastShiftZone BreachZone;
            public bool Isolated;

            public override string ToString() =>
                $"heat={Heat} power={Power} oxygen={Oxygen} prop={Propulsion} " +
                $"breach={LastShiftZoneAtlas.ShortLabelOf(BreachZone)} isolated={Isolated}";
        }

        private struct Scenario
        {
            public LastShiftShipState State;
            public LastShiftZonePressures Pressures;
            public LastShiftContainment Containment;
            public LastShiftDoorState Doors;
            public LastShiftZone BreachZone;
        }

        private static IEnumerable<Combo> AllCombos()
        {
            foreach (var heat in HeatAxis)
            foreach (var power in PowerAxis)
            foreach (var oxygen in OxygenAxis)
            foreach (var propulsion in PropulsionAxis)
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            foreach (var isolated in new[] { false, true })
                yield return new Combo
                {
                    Heat = heat,
                    Power = power,
                    Oxygen = oxygen,
                    Propulsion = propulsion,
                    BreachZone = (LastShiftZone)zone,
                    Isolated = isolated
                };
        }

        private static Scenario Build(in Combo combo)
        {
            var state = new LastShiftShipState
            {
                ShipAttitudeDegrees = 10f,
                ExistingDamage = 0.20f,
                HullIntegrity = 0.95f,
                FuelReserve = LastShiftRecoveryTuning.FuelReserveInitial,
                DockProgress = 0f,
                ThrustDemand = 0.50f
            };
            var containment = new LastShiftContainment
            {
                CoolingRestored = true,
                PowerRestored = true,
                OxygenRestored = true
            };

            // 열 축
            switch (combo.Heat)
            {
                case LastShiftSituation.HeatCouplingLoose:
                    state.EngineHeat = 0.75f; containment.CoolingRestored = false; break;
                case LastShiftSituation.HeatRunaway:
                    state.EngineHeat = 0.92f; containment.CoolingRestored = false; break;
                case LastShiftSituation.HeatProtectionLock:
                    state.EngineHeat = 1.00f; containment.CoolingRestored = false; break;
                default:
                    state.EngineHeat = 0.30f; break;
            }

            // 전력 축
            switch (combo.Power)
            {
                case LastShiftSituation.BusDetached:
                    state.BusPower = 0.60f; containment.PowerRestored = false; break;
                case LastShiftSituation.PowerCascade:
                    state.BusPower = 0.30f; containment.PowerRestored = false; break;
                case LastShiftSituation.PowerBlackout:
                    state.BusPower = 0.10f; containment.PowerRestored = false; break;
                default:
                    state.BusPower = 0.80f; break;
            }

            // 산소 축 — 파공 구역만 낮추고 나머지는 높게 둔다. 평준화가 상시 도는 배에서
            // 실제로 도달하는 모양이 그것이다(§3.4 가 5³ 를 채택하지 않은 이유와 같다).
            var pressures = LastShiftZonePressures.Uniform(0.90f);
            switch (combo.Oxygen)
            {
                case LastShiftSituation.HullLeak:
                    containment.OxygenRestored = false; state.HullIntegrity = 0.60f; break;
                case LastShiftSituation.ZoneLowPressure:
                    containment.OxygenRestored = false; state.HullIntegrity = 0.60f;
                    pressures[combo.BreachZone] = 0.30f; break;
                case LastShiftSituation.DecompressionAlarm:
                    containment.OxygenRestored = false; state.HullIntegrity = 0.60f;
                    pressures[combo.BreachZone] = 0.10f; break;
                case LastShiftSituation.ZoneVacuum:
                    containment.OxygenRestored = false; state.HullIntegrity = 0.60f;
                    pressures[combo.BreachZone] = 0.00f; break;
            }
            state.OxygenPressure = pressures[LastShiftZone.Cockpit];

            // 추진 축
            switch (combo.Propulsion)
            {
                case LastShiftSituation.AttitudeDrift:
                    state.ShipAttitudeDegrees = 70f; break;
                case LastShiftSituation.FuelMarginLost:
                    state.FuelReserve = 0.20f; break;
            }

            // 격리 — 파공 구역을 감싸는 문을 닫는다. 조종석·산소실은 경계 하나, 가운데 구역은
            // 양쪽 둘이다. 이 구조는 구역이 넷이 돼도 같은 식으로 나오므로 인덱스로 푼다.
            var doors = LastShiftDoorState.AllOpen;
            if (combo.Isolated)
            {
                var index = (int)combo.BreachZone;
                if (index - 1 >= 0) doors[index - 1] = false;
                if (index < LastShiftZoneAtlas.BoundaryCount) doors[index] = false;
            }

            return new Scenario
            {
                State = state,
                Pressures = pressures,
                Containment = containment,
                Doors = doors,
                BreachZone = combo.BreachZone
            };
        }

        // ── 조합 개수와 조립 정합성 ──────────────────────────────────────────

        [Test]
        public void CombinationCountMatchesTheDocumentedTotal()
        {
            var expected = HeatAxis.Length * PowerAxis.Length * OxygenAxis.Length * PropulsionAxis.Length
                           * LastShiftZoneAtlas.ZoneCount * 2;
            var actual = 0;
            foreach (var _ in AllCombos()) actual++;

            Assert.That(actual, Is.EqualTo(expected));

            // 이 검사가 지키려는 것은 <b>축</b>이다("축이 늘었거나 줄었다"). 총수 리터럴로 두면
            // 구역 수가 바뀔 때도 같이 걸려서, 의도한 구역 증설(§2.1)과 의도치 않은 축 변화를
            // 구분할 수 없다. 그래서 축의 곱만 고정한다.
            //
            // 기획 §0.2 의 COMBO_TOTAL 1,440 은 이 축 곱(240) x 구역 3 x 2 였다. 구역이 넷이
            // 되며 같은 식이 1,920 을 낸다 — 축은 그대로다. RG-4(파공 구역 조합 확률)는 이
            // 개수와 별개로 game-balance 재계산 대상이다(§7).
            Assert.That(HeatAxis.Length * PowerAxis.Length * OxygenAxis.Length * PropulsionAxis.Length,
                Is.EqualTo(240), "상황 축이 늘었거나 줄었다 — 구역 수 변화와 구분해서 봐야 한다.");
            Assert.That(expected, Is.EqualTo(240 * LastShiftZoneAtlas.ZoneCount * 2));
        }

        [Test]
        public void EveryComboProducesTheIntendedRepresentativeSituations()
        {
            // 조립이 틀리면 아래 검증들이 엉뚱한 1,440개를 통과시키고 초록불을 낸다.
            var failures = new StringBuilder();
            var checkedCount = 0;

            foreach (var combo in AllCombos())
            {
                var scenario = Build(combo);
                var tracker = new LastShiftSituationTracker();
                tracker.Evaluate(InputOf(scenario), 0f);

                checkedCount++;
                Expect(failures, combo, "열",
                    tracker.StatusOf(LastShiftSystemChannel.Heat).Situation, combo.Heat);
                Expect(failures, combo, "전력",
                    tracker.StatusOf(LastShiftSystemChannel.Power).Situation, combo.Power);
                Expect(failures, combo, "추진",
                    tracker.StatusOf(LastShiftSystemChannel.Propulsion).Situation, combo.Propulsion);
                Expect(failures, combo, "산소(파공구역)",
                    tracker.OxygenStatusOf(combo.BreachZone).Situation, combo.Oxygen);
            }

            Assert.That(checkedCount, Is.EqualTo(240 * LastShiftZoneAtlas.ZoneCount * 2));
            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        private static void Expect(
            StringBuilder failures, in Combo combo, string label,
            LastShiftSituation actual, LastShiftSituation expected)
        {
            if (actual == expected) return;
            if (failures.Length < 2000)
                failures.AppendLine($"[{combo}] {label} 대표가 {expected} 이어야 하는데 {actual} 이다.");
        }

        // ── RG-4 전수 검증 ───────────────────────────────────────────────────

        [Test]
        public void EveryCombinationLeavesAtLeastOneRecoveryAction()
        {
            var total = 0;
            var repairable = 0;      // (1)
            var dockReachable = 0;   // (2)
            var dockInTime = 0;      // (3)
            var survivable = 0;      // (4)
            var cockpitHoldable = 0; // (6)

            var blocked = new StringBuilder();     // RG-2 위반
            var stranded = new StringBuilder();    // (5) 위반 — PASS 조건
            var cockpitLost = new StringBuilder(); // (6) 위반
            var lockStuck = new StringBuilder();   // RG-3 위반

            foreach (var combo in AllCombos())
            {
                total++;
                var scenario = Build(combo);

                // (1) 최소 1개의 수리 동사가 실행 가능한가 + RG-2 순환 부재
                if (LastShiftRepairAvailability.AnyBlocked(scenario.Containment) && blocked.Length < 1500)
                    blocked.AppendLine($"[{combo}] 손상됐는데 못 고치는 계통이 있다 — RG-2 순환.");
                var canRepair = LastShiftRepairAvailability.AnyExecutable(scenario.Containment);
                if (canRepair) repairable++;

                // (6) 조종석 압력을 0.20 이상으로 되돌리거나 유지하는 경로
                var cockpitSeconds = SecondsToCockpitDockPressure(scenario);
                var cockpitOk = cockpitSeconds >= 0f;
                if (cockpitOk) cockpitHoldable++;
                else if (cockpitLost.Length < 1500)
                    cockpitLost.AppendLine($"[{combo}] 조종석 압력을 0.20 으로 되돌리는 경로가 없다.");

                // (2) 도킹 성공 조건 도달 경로 (남은 시간 무관) — 연료가 필요 추력적분을 덮어야 한다
                var fuelThrustSeconds = scenario.State.FuelReserve /
                                        LastShiftRecoveryTuning.FuelDrainPerThrustSecond;
                var dockOk = cockpitOk &&
                             fuelThrustSeconds >= LastShiftRecoveryTuning.DockTargetThrustSeconds;
                if (dockOk) dockReachable++;

                // (3) 남은 시간 안에 완주 가능한가 — 거짓이어도 정상이다
                if (dockOk)
                {
                    var thrustSecondsAvailable =
                        LastShiftRecoveryTuning.DockingTimerSeconds - cockpitSeconds;
                    if (thrustSecondsAvailable >= LastShiftRecoveryTuning.DockTargetThrustSeconds)
                        dockInTime++;
                }

                // (4) S-O4 조합의 생존 경로
                var survives = SurvivalPathExists(scenario, combo, out var viaEscape);
                if (survives) survivable++;

                // (5) PASS 조건 — (1)과 (4)가 모두 거짓인 조합은 0개여야 한다
                if (!canRepair && !survives && stranded.Length < 1500)
                    stranded.AppendLine($"[{combo}] 수리도 생존 경로도 없다 — 아무것도 할 수 없는 상태.");

                // RG-3 — 잠금은 아무것도 안 해도 풀려야 한다
                if (combo.Heat == LastShiftSituation.HeatProtectionLock)
                {
                    var releaseSeconds = SecondsToLockRelease(scenario);
                    if (releaseSeconds < 0f && lockStuck.Length < 1500)
                        lockStuck.AppendLine($"[{combo}] S-H3 이 {LockReleaseBudgetSeconds}초 안에 안 풀린다.");
                }

                // (4-b) 는 이동만으로 성립하므로 진공이 아닌 구역이 하나라도 있으면 참이다.
                // 전 구역이 진공이면 (4-a) 로만 살아야 하고, 그때 survives 가 그것을 본다.
                if (viaEscape) Assert.That(survives, Is.True);
            }

            Debug.Log($"[LAST_SHIFT_RG4] total={total} " +
                      $"(1)repairable={repairable} (2)dockReachable={dockReachable} " +
                      $"(3)dockInTime={dockInTime} (4)survivable={survivable} (6)cockpitHoldable={cockpitHoldable}");

            Assert.That(total, Is.EqualTo(240 * LastShiftZoneAtlas.ZoneCount * 2));
            Assert.That(blocked.Length, Is.Zero, $"RG-2 위반\n{blocked}");
            Assert.That(lockStuck.Length, Is.Zero, $"RG-3 위반\n{lockStuck}");
            Assert.That(cockpitLost.Length, Is.Zero, $"RG-4 (6) 위반\n{cockpitLost}");
            Assert.That(stranded.Length, Is.Zero, $"RG-4 (5) 위반 — PASS 조건\n{stranded}");
        }

        // ── 시뮬레이션 보조 ──────────────────────────────────────────────────

        private static LastShiftSituationInput InputOf(in Scenario scenario) =>
            LastShiftSituationInput.From(scenario.State, scenario.Pressures, scenario.Containment);

        /// <summary>
        /// 수리를 전부 마치고 문을 연 뒤 조종석 압력이 도킹 성공선에 닿기까지 걸리는 시간.
        /// 닿지 못하면 <c>-1</c>. 격리를 푸는 것은 §2.2.2 가 보장하는 되돌리기이며 전력을
        /// 요구하지 않는다.
        /// </summary>
        private static float SecondsToCockpitDockPressure(in Scenario scenario)
        {
            var state = scenario.State;
            var pressures = scenario.Pressures;
            var repaired = new LastShiftContainment
            {
                CoolingRestored = true, PowerRestored = true, OxygenRestored = true
            };
            var doors = LastShiftDoorState.AllOpen;
            state.ThrustDemand = LastShiftRecoveryTuning.DockingSuccessThrust;

            for (var elapsed = 0f; elapsed <= RepressurizeBudgetSeconds; elapsed += Step)
            {
                if (pressures[LastShiftZone.Cockpit] >= LastShiftRecoveryTuning.DockingSuccessOxygen)
                    return elapsed;
                LastShiftDeterioration.Tick(ref state, ref pressures, repaired, scenario.BreachZone, doors, Step);
            }
            return -1f;
        }

        /// <summary>
        /// <c>S-O4</c> 조합에서 <c>SuitOxygen 80초</c> 안에 생존 경로가 있는가(§4.3 (4)).
        /// <paramref name="viaEscape"/> 는 (4-b) 이동만으로 성립했는지를 돌려준다.
        /// </summary>
        private static bool SurvivalPathExists(in Scenario scenario, in Combo combo, out bool viaEscape)
        {
            viaEscape = false;
            if (combo.Oxygen != LastShiftSituation.ZoneVacuum) return true;   // 진공이 없으면 살 일도 없다

            // (4-b) 진공이 아닌 구역이 하나라도 있으면 걸어 나가는 것으로 끝난다. 이동은 어떤
            // 아이템도 전력도 요구하지 않고, 문은 안에서도 열린다(§2.2.2).
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                if (LastShiftVerdictResolver.IsZoneVacuum(scenario.Pressures[(LastShiftZone)zone], false)) continue;
                viaEscape = true;
                return true;
            }

            // (4-a) 전 구역이 진공이면 되돌리는 수밖에 없다. 80초 안에 그 구역 압력이
            // 0.00 위로 올라오는지 본다.
            var state = scenario.State;
            var pressures = scenario.Pressures;
            var repaired = new LastShiftContainment
            {
                CoolingRestored = true, PowerRestored = true, OxygenRestored = true
            };
            var doors = LastShiftDoorState.AllOpen;

            for (var elapsed = 0f; elapsed <= SurvivalBudgetSeconds; elapsed += Step)
            {
                if (pressures[scenario.BreachZone] > LastShiftRecoveryTuning.VacuumOxygenPressure) return true;
                LastShiftDeterioration.Tick(ref state, ref pressures, repaired, scenario.BreachZone, doors, Step);
            }
            return false;
        }

        /// <summary>
        /// <c>RG-3</c> — <b>아무 수리도 하지 않고</b> <c>S-H3</c> 이 해제선까지 내려가는 시간.
        /// 풀리지 않으면 <c>-1</c>. 잠금 중 추력 상한이 <c>0.25</c> 라 상승 분기는 스스로 꺼지고,
        /// 자연 냉각만으로 <c>1.00 → 0.80</c> 이 <c>25초</c> 여야 한다.
        /// </summary>
        private static float SecondsToLockRelease(in Scenario scenario)
        {
            var state = scenario.State;
            var pressures = scenario.Pressures;
            var containment = scenario.Containment;   // 손대지 않는다 — 그게 이 검사의 전부다
            var doors = scenario.Doors;
            state.ThrustDemand = 1f;                  // 플레이어가 추력을 최대로 두고 방치한 경우

            for (var elapsed = 0f; elapsed <= LockReleaseBudgetSeconds; elapsed += Step)
            {
                if (state.EngineHeat <= LastShiftSituationTable.HeatLockRelease) return elapsed;
                LastShiftDeterioration.Tick(ref state, ref pressures, containment, scenario.BreachZone, doors, Step);
            }
            return -1f;
        }
    }
}
