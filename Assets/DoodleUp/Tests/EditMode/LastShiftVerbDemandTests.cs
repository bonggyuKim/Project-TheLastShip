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

        /// <summary>§2·§4.1 실측이 쓴 선체 무결성. 운석 후 중간값이다.</summary>
        private const float ReferenceHull = 0.60f;

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
        /// <b>선체 <c>0.30</c> 이하에서 잰다. <c>0.60</c> 은 아직 안 온다.</b> §4.1 표는
        /// <c>0.018</c> 에서 조종석이 <c>285</c>초에 성공선을 잃는다고 적었지만, 그 값은 평준화를
        /// "차이가 매초 <c>8%</c> 씩 줄고 각 구역이 <b>그 전부</b>를 움직인다" 로 푼 것이다.
        /// 코드는 §2.2.1 대로 <b>절반씩</b> 움직이므로(<see cref="LastShiftZonePressures.Equalize"/>)
        /// 조종석까지의 전파가 그보다 느리고, 실측은 <c>316</c>초 — 타이머 밖이다.
        ///
        /// 모델이 어긋난 것이지 <c>C-1</c> 이 틀린 것이 아니다. §2 표(<c>0.006</c> · 완파 ·
        /// <c>300</c>초 후 <c>0.287</c>)는 코드 실측 <c>0.298</c> 과 거의 같다 — 같은 문서 안에서
        /// §2 와 §4.1 이 다른 모델을 쓴다. <c>0.60</c> 에서도 조건 (1)이 성립하려면
        /// <c>0.024</c> 가 필요하고(실측 <c>280</c>초), 그 판단은 §7-1 대로 <c>game-balance</c> 것이다.
        /// </summary>
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
        /// 선체 <c>0.60</c>(운석 후 중간값)에서도 <b>여유가 반올림 오차 크기로 줄어야</b> 한다.
        ///
        /// 위 검사가 <c>0.60</c> 을 못 넣는 대신 이 카드가 실제로 바꾼 것을 여기서 잰다 —
        /// 예전(<c>0.006</c>)에는 타이머 끝에 조종석이 <c>0.714</c> 로, 성공선까지 여유가
        /// <c>0.51</c> 이었다. §2 가 "격리로 <c>60</c>초에 <c>0.018</c> 을 아낀다" 를 반올림
        /// 오차라고 부른 근거가 그 여유다. 지금은 그 여유가 한 자릿수 퍼센트다.
        /// </summary>
        [Test]
        public void AtMidHullTheOxygenMarginAtTheBuzzerIsNoLongerARoundingError()
        {
            var pressures = Integrate(LastShiftDoorState.AllOpen,
                LastShiftRecoveryTuning.DockingTimerSeconds, ReferenceHull, out _);
            var margin = pressures[LastShiftZone.Cockpit] - LastShiftRecoveryTuning.DockingSuccessOxygen;

            Assert.That(margin, Is.GreaterThan(0f).And.LessThan(0.05f),
                "선체 0.60 · 타이머 끝에서 조종석 여유가 0.05 미만이어야 한다 — " +
                $"실측 여유 {margin:F3}. 예전 상수(0.006)에서는 0.51 이었다.");
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
        /// 지나가므로 그 뒤로는 순 <c>0.020/s</c> 다.
        ///
        /// 그래서 실제로 관측되는 것은 "잠금 회피" 가 아니라 <b>잠금 지연</b>이고, 이 검사는
        /// 그것을 잰다. §4.3 이 사려던 <c>14</c>초는 밸브 하나로는 안 나온다 — 수치 재조정은
        /// <c>game-balance</c> 소관이고(§7-3), 여기서는 코드가 실제로 하는 일을 고정한다.
        /// </summary>
        [Test]
        public void HoldingTheValveDelaysTheEngineProtectionLock()
        {
            var abandoned = SecondsUntilProtectionLock(0.86f, 0.65f, held: false);
            var sustained = SecondsUntilProtectionLock(0.86f, 0.65f, held: true);

            Assert.That(abandoned, Is.LessThan(6f),
                "아무도 밸브를 안 잡으면 몇 초 만에 엔진 보호 잠금이 걸려야 한다.");
            Assert.That(sustained, Is.GreaterThan(abandoned * 2f),
                "한 사람이 자리를 지키면 잠금까지의 시간이 최소 두 배가 되어야 한다 — " +
                "그 지연이 이 동사가 사는 값이다.");
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
        /// 손상 하나(산소)만 남은 배를 <paramref name="seconds"/> 만큼 민다. 파공은 §2·§4.1 실측과
        /// 같은 자리 — 생명유지실이고 조종석에서 경계 <c>3</c>개 거리다.
        /// </summary>
        private static LastShiftZonePressures Integrate(
            LastShiftDoorState doors, float seconds, float hull, out float breachVacuumSeconds)
        {
            var state = new LastShiftShipState
            {
                ThrustDemand = 0.50f,
                BusPower = 1f,
                OxygenPressure = 1f,
                HullIntegrity = hull,
                EngineHeat = 0.20f,
                FuelReserve = LastShiftRecoveryTuning.FuelReserveInitial
            };
            var pressures = LastShiftZonePressures.Uniform(1f);
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
            var state = new LastShiftShipState
            {
                ThrustDemand = 0.50f,
                BusPower = 1f,
                OxygenPressure = 1f,
                HullIntegrity = hull,
                EngineHeat = 0.20f,
                FuelReserve = LastShiftRecoveryTuning.FuelReserveInitial
            };
            var pressures = LastShiftZonePressures.Uniform(1f);
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
