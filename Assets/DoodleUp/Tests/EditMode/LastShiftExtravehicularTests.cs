using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 선외 파밍 거점 <c>O-0</c> — 에어록 개방 · EVA · 잔해 파밍.
    /// 정본은 <c>docs/outboard-outpost-and-map-final-v1.md</c> §3~§5 다.
    ///
    /// <b>여기서 재는 것은 다섯이다.</b>
    /// <list type="number">
    /// <item><b>조항 <c>O-4</c> — 에어록은 기항에서만 열린다.</b> 이게 새면 이탈 시간 계산의
    /// 종점이 배 밖으로 나가고 <c>RG-1(1)</c>·<c>(4-b)</c> 를 통째로 다시 짜야 한다.</item>
    /// <item><b>조항 <c>O-1</c> — 자재와 여력이 서로 환산되지 않는다.</b> 환산 경로가 하나라도
    /// 있으면 파밍이 여력을 주는 것과 같고, §3.2 의 근거 셋이 동시에 죽는다.</item>
    /// <item><b>조항 <c>O-7</c> — 산소가 마르면 죽지 않고 수확만 잃는다.</b> 잃은 몫이 잔해로
    /// 되돌아가면 대가가 시간 손해로만 남아 "최대한 뜯고 오는 것" 이 다시 최적이 된다.</item>
    /// <item><b>§5.2-4 — 첫 EVA 에서 산소로 겁을 주지 않는다.</b> 왕복 + 사이클 두 번 +
    /// 뜯기가 예비 산소 예산의 <b>절반 안</b>이어야 한다. 이건 연출이 아니라 좌표 검산이다.</item>
    /// <item><b>§4.2 — 직전 구간이 이번 기항의 수확 종류를 정한다.</b> 이게 어긋나면 조항
    /// <c>C-2</c>("잃은 것을 대신한다")가 자재 축에서 안 선다.</item>
    /// </list>
    ///
    /// 정적 상태를 만지므로 항해·원장·에어록·잔해를 앞뒤로 다 비운다.
    /// </summary>
    public sealed class LastShiftExtravehicularTests
    {
        private const float Tolerance = 0.0001f;

        [SetUp]
        public void ClearBefore() => LastShiftVoyage.Clear();

        [TearDown]
        public void ClearAfter() => LastShiftVoyage.Clear();

        /// <summary>기항에 들여보낸다 — 구간 하나를 판정해 전이를 <c>ToPort</c> 로 만든다.</summary>
        private static void EnterPort(LastShiftPreset preset = LastShiftPreset.HighHeatHighThrust)
        {
            LastShiftVoyage.EnterSegment(LastShiftVoyage.SegmentOf(preset));
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
        }

        /// <summary>바깥 해치까지 연다. 사이클은 시계라 <see cref="LastShiftAirlock.Tick"/> 로 민다.</summary>
        private static void OpenToSpace()
        {
            Assert.That(LastShiftAirlock.TryOpenInner(anyDeckHatchOpen: false), Is.True);
            Assert.That(LastShiftAirlock.TryBeginDepressurize(), Is.True);
            LastShiftAirlock.Tick(LastShiftAirlock.CycleSeconds);
            Assert.That(LastShiftAirlock.IsOuterHatchOpen, Is.True);
        }

        // ── 조항 O-4 ────────────────────────────────────────────────────────

        /// <summary>
        /// <b>구간 중에는 열리지 않는다.</b> 게이트를
        /// <see cref="LastShiftMaintenance.IsAtPort"/> 로 잡으면 한 번 기항한 뒤로는 구간
        /// 중에도 참이라 사실상 게이트가 없다 — 그 함정을 여기서 직접 잰다.
        /// </summary>
        [Test]
        public void TheAirlockOnlyOpensAtPort()
        {
            Assert.That(LastShiftAirlock.IsAtPort, Is.False, "항해 시작 구간에서 기항으로 잡힌다.");
            Assert.That(LastShiftAirlock.TryOpenInner(anyDeckHatchOpen: false), Is.False);

            EnterPort();
            Assert.That(LastShiftAirlock.IsAtPort, Is.True);
            Assert.That(LastShiftMaintenance.IsAtPort, Is.True, "여력 원장 쪽 회차도 올라가 있어야 한다.");
            Assert.That(LastShiftAirlock.TryOpenInner(anyDeckHatchOpen: false), Is.True);

            // 출항하면 잠긴다. 조회로만 두면 밖에 나간 채로 출항하는 상태가 남는다.
            OpenToSpaceFromInner();
            LastShiftVoyage.Advance();
            Assert.That(LastShiftMaintenance.IsAtPort, Is.True,
                "기항 회차는 그대로다 — 이 값으로 게이트를 잡으면 안 되는 이유가 이것이다.");
            Assert.That(LastShiftAirlock.IsAtPort, Is.False);
            Assert.That(LastShiftAirlock.IsSealed, Is.True, "출항했는데 에어록이 열린 채로 남았다.");
            Assert.That(LastShiftSalvage.HasField, Is.False, "출항했는데 잔해가 남아 있다(조항 O-5).");
        }

        private static void OpenToSpaceFromInner()
        {
            Assert.That(LastShiftAirlock.TryBeginDepressurize(), Is.True);
            LastShiftAirlock.Tick(LastShiftAirlock.CycleSeconds);
        }

        /// <summary>
        /// <b>두 해치가 동시에 안 열린다.</b> 에어록의 정의이자 감압 사이클이 존재할 이유다.
        /// 사이클이 도는 동안에는 <b>둘 다</b> 닫혀 있어야 한다 — 한쪽이라도 열려 있으면
        /// 그건 감압이 아니라 그냥 문이 두 개인 복도다.
        /// </summary>
        [Test]
        public void NeverBothHatchesAtOnce()
        {
            EnterPort();
            Assert.That(LastShiftAirlock.TryOpenInner(anyDeckHatchOpen: false), Is.True);
            Assert.That(LastShiftAirlock.IsOuterHatchOpen, Is.False);

            Assert.That(LastShiftAirlock.TryBeginDepressurize(), Is.True);
            Assert.That(LastShiftAirlock.IsCycling, Is.True);
            Assert.That(LastShiftAirlock.IsInnerHatchOpen, Is.False);
            Assert.That(LastShiftAirlock.IsOuterHatchOpen, Is.False);

            // 사이클은 시간이 든다 — 문(0.8초)과 같은 값이면 절차로 안 읽힌다.
            Assert.That(LastShiftAirlock.CycleSeconds,
                Is.GreaterThan(LastShiftRecoveryTuning.ZoneDoorTransitionSeconds));
            LastShiftAirlock.Tick(LastShiftAirlock.CycleSeconds * 0.5f);
            Assert.That(LastShiftAirlock.IsCycling, Is.True, "사이클이 절반에서 이미 끝났다.");
            LastShiftAirlock.Tick(LastShiftAirlock.CycleSeconds * 0.5f);
            Assert.That(LastShiftAirlock.IsOuterHatchOpen, Is.True);
            Assert.That(LastShiftAirlock.IsInnerHatchOpen, Is.False);

            // 돌아오는 길은 기항 게이트를 안 본다 — RG-3(영구 잠금 금지).
            LastShiftVoyage.EnterSegment(LastShiftVoyage.SegmentIndex + 1);
            LastShiftAirlock.ApplyNetworkState(
                LastShiftAirlockPhase.OuterOpen, LastShiftAirlockPhase.OuterOpen, 0f);
            Assert.That(LastShiftAirlock.IsAtPort, Is.False);
            Assert.That(LastShiftAirlock.TryBeginRepressurize(), Is.True,
                "밖에 있는 승무원이 돌아올 길이 막혔다 — RG-3 위반이다.");
        }

        // ── 조항 O-1 · O-2 ─────────────────────────────────────────────────

        /// <summary>
        /// <b>자재와 여력이 서로 환산되지 않는다.</b> 파밍을 아무리 돌아도 여력 잔액이 한 칸도
        /// 안 움직여야 한다 — 그게 §3.2 의 근거 셋(희소성 · 노동 · 모아 짓기)을 동시에 지킨다.
        /// </summary>
        [Test]
        public void SalvageNeverTouchesTheMaintenanceLedger()
        {
            EnterPort();
            var maintenanceBefore = LastShiftMaintenance.Balance;

            HarvestAndReturn(LastShiftSalvage.ChunksPerField);

            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(LastShiftSalvage.ChunksPerField));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(maintenanceBefore),
                "파밍이 여력을 늘렸다 — 조항 O-1 위반이고 §4.3 검산이 통째로 죽는다.");

            // 반대 방향도 없다. 여력을 다 써도 자재가 안 준다.
            LastShiftMaintenance.TrySpend(LastShiftMaintenance.Balance);
            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(LastShiftSalvage.ChunksPerField));
        }

        /// <summary>
        /// <b>자재는 기항을 건너 남는다</b>(조항 <c>M-1</c>, <c>O-3</c> 폐기 후). 항해가 끝날 때만
        /// <c>0</c> 으로 돌아간다 — 여력과 <b>같은 시점</b>이어야 새 항해 첫 기항에 지난 항해
        /// 자재가 안 남는다.
        /// </summary>
        [Test]
        public void MaterialsCarryAcrossPortsAndResetOnlyWithTheVoyage()
        {
            EnterPort();
            HarvestAndReturn(2);
            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(2));

            LastShiftVoyage.Advance();
            EnterPort(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(2), "기항을 넘으면서 자재가 사라졌다.");
            HarvestAndReturn(1);
            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(3));
            Assert.That(LastShiftMaterials.LifetimeSalvaged, Is.EqualTo(3));

            LastShiftVoyage.BeginVoyage();
            Assert.That(LastShiftMaterials.Balance, Is.Zero, "새 항해에 지난 항해 자재가 남았다.");
            Assert.That(LastShiftMaintenance.Balance, Is.Zero, "리셋 시점이 여력과 갈렸다.");
        }

        // ── §4.2 계열 ──────────────────────────────────────────────────────

        /// <summary>
        /// <b>직전 구간이 이번 기항의 잔해 계열을 정한다.</b> 셋이 서로 다른 값으로 갈려야
        /// 조항 <c>C-2</c> 가 자재 축에서 성립한다 — 전력 사고 뒤에 전력 계열이 뜬다.
        /// </summary>
        [Test]
        public void TheDebrisKindComesFromTheSegmentThatJustEnded()
        {
            Assert.That(LastShiftSalvage.KindOf(LastShiftPreset.HighHeatHighThrust),
                Is.EqualTo(LastShiftSalvageKind.Cooling));
            Assert.That(LastShiftSalvage.KindOf(LastShiftPreset.PowerOverloadLooseBattery),
                Is.EqualTo(LastShiftSalvageKind.Power));
            Assert.That(LastShiftSalvage.KindOf(LastShiftPreset.BadAttitudeHighOxygen),
                Is.EqualTo(LastShiftSalvageKind.Hull));

            EnterPort(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(LastShiftSalvage.HasField, Is.True);
            Assert.That(LastShiftSalvage.Kind, Is.EqualTo(LastShiftSalvageKind.Power),
                "구간 2(전력 과부하)를 끝냈는데 다른 계열 잔해가 떴다.");
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(LastShiftSalvage.ChunksPerField));
        }

        /// <summary>
        /// <b>견인이어도 잔해는 뜬다.</b> 견인의 대가는 여력 수입 <c>0</c> 이고(조항 <c>M-3</c>)
        /// 자재는 여력이 아니다(조항 <c>O-1</c>) — 여기에 걸면 최악 항해의 회복 경로가 통째로
        /// 사라져 <c>RG-3</c> 의 항해판 위반이 된다.
        /// </summary>
        [Test]
        public void BeingTowedStillLeavesDebrisToSalvage()
        {
            LastShiftVoyage.EnterSegment(LastShiftVoyage.FirstSegment);
            LastShiftVoyage.SettleSegment(LastShiftVerdict.FailureAdrift, 0);

            Assert.That(LastShiftMaintenance.LastPortIncome, Is.Zero, "견인인데 여력 수입이 들어왔다.");
            Assert.That(LastShiftSalvage.HasField, Is.True, "견인 기항에 회복 경로가 하나도 없다.");
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(LastShiftSalvage.ChunksPerField));
        }

        // ── 회수 · 조항 O-7 ────────────────────────────────────────────────

        /// <summary>
        /// <b>회수는 세 칸을 거친다</b> — 잔해에 붙은 몫 → 들고 있는 몫 → 반입한 몫.
        /// 가운데 칸이 없으면 조항 <c>O-7</c> 이 물릴 대가가 아무 데도 안 남는다.
        /// </summary>
        [Test]
        public void HarvestingMovesChunksThroughCarriedBeforeTheLedger()
        {
            EnterPort();
            var atField = LastShiftSalvage.FieldCenter;

            Assert.That(LastShiftSalvage.CanHarvest(LastShiftAirlock.ReturnPoint), Is.False,
                "에어록에서 잔해에 손이 닿는다 — 사거리가 무의미하다.");
            Assert.That(LastShiftSalvage.TryHarvest(atField), Is.True);
            Assert.That(LastShiftSalvage.Carried, Is.EqualTo(1));
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(LastShiftSalvage.ChunksPerField - 1));
            Assert.That(LastShiftMaterials.Balance, Is.Zero, "뜯자마자 원장에 들어갔다.");

            // 뜯기에는 시간이 든다 — 연타로 잔해가 즉시 비면 "왕복이 한 단위" 가 안 선다.
            Assert.That(LastShiftSalvage.TryHarvest(atField), Is.False, "쿨다운 없이 연달아 뜯힌다.");
            LastShiftSalvage.Tick(LastShiftSalvage.HarvestSeconds);
            Assert.That(LastShiftSalvage.TryHarvest(atField), Is.True);

            // 손이 차면 더 못 든다 — 그게 왕복을 강제하고 §4.3 의 거점 확장 둘이 살 이유다.
            LastShiftSalvage.Tick(LastShiftSalvage.HarvestSeconds);
            Assert.That(LastShiftSalvage.Carried, Is.EqualTo(LastShiftSalvage.CarryCapacity));
            Assert.That(LastShiftSalvage.TryHarvest(atField), Is.False, "손이 찼는데 더 들린다.");
            Assert.That(LastShiftSalvage.ChunksPerField,
                Is.GreaterThan(LastShiftSalvage.CarryCapacity),
                "한 번에 다 들고 오면 거점 확장 둘(인양 팔·자재 정리대)이 살 이유가 없다.");

            Assert.That(LastShiftSalvage.Deposit(), Is.EqualTo(LastShiftSalvage.CarryCapacity));
            Assert.That(LastShiftSalvage.Carried, Is.Zero);
            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(LastShiftSalvage.CarryCapacity));
        }

        /// <summary>
        /// <b>조항 <c>O-7</c> — 잃는 것은 수확이지 목숨이 아니다.</b> 그리고 잃은 몫은 잔해로
        /// <b>안 돌아간다</b>: 돌아가면 대가가 시간 손해로만 남아 "산소가 허락하는 만큼 최대한
        /// 뜯고 오는 것" 이 다시 최적이 되고, 그건 §3.2-2 가 경계한 노동이다.
        /// </summary>
        [Test]
        public void RunningOutOfAirCostsTheHarvestNotTheCrew()
        {
            EnterPort();
            var atField = LastShiftSalvage.FieldCenter;
            LastShiftSalvage.TryHarvest(atField);
            LastShiftSalvage.Tick(LastShiftSalvage.HarvestSeconds);
            LastShiftSalvage.TryHarvest(atField);

            var carried = LastShiftSalvage.Carried;
            var remaining = LastShiftSalvage.Remaining;
            Assert.That(carried, Is.GreaterThan(0));

            Assert.That(LastShiftSalvage.AbandonCarried(), Is.EqualTo(carried));
            Assert.That(LastShiftSalvage.Carried, Is.Zero);
            Assert.That(LastShiftMaterials.Balance, Is.Zero, "잃은 몫이 원장에 들어갔다.");
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(remaining),
                "잃은 몫이 잔해로 돌아갔다 — 대가가 시간 손해로만 남는다.");

            // 다음 기항에 회복된다. 그게 이 대가를 나선이 아니게 만든다.
            LastShiftVoyage.Advance();
            EnterPort(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(LastShiftSalvage.Remaining, Is.EqualTo(LastShiftSalvage.ChunksPerField));
        }

        // ── 좌표 검산 ──────────────────────────────────────────────────────

        /// <summary>
        /// <b>§5.2-4 — 첫 EVA 에서 산소로 겁을 주지 않는다.</b> 왕복 이동 + 감압/재가압 두 번 +
        /// 손이 찰 때까지 뜯기가 예비 산소 예산의 <b>절반 안</b>이어야 한다.
        ///
        /// <b>이건 연출 문구가 아니라 좌표 검산이다.</b> 잔해를 원반 밖으로 더 밀거나 사이클을
        /// 늘리면 그 순간 첫 EVA 가 산소 경고로 시작하고, §5 튜토리얼이 가르치려던 것
        /// ("밖에는 시계가 있다")이 "밖은 위험하다" 로 바뀐다.
        /// </summary>
        [Test]
        public void TheFirstRoundTripFitsInHalfTheSuitBudget()
        {
            var budgetSeconds = LastShiftRecoveryTuning.SuitOxygenInitial
                                / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond;
            Assert.That(budgetSeconds, Is.EqualTo(80f).Within(0.01f), "예비 산소 예산이 80초가 아니다.");

            var travelSeconds = LastShiftSalvage.DistanceFromAirlock * 2f
                                / LastShiftPlayerController.MoveSpeed;
            var cycleSeconds = LastShiftAirlock.CycleSeconds * 2f;
            var harvestSeconds = LastShiftSalvage.HarvestSeconds * LastShiftSalvage.CarryCapacity;
            var trip = travelSeconds + cycleSeconds + harvestSeconds;

            Assert.That(trip, Is.LessThan(budgetSeconds * 0.5f),
                $"왕복 한 번이 {trip:F1}초로 예산 절반({budgetSeconds * 0.5f:F1}초)을 넘는다 — " +
                "첫 EVA 가 산소 경고로 시작한다(§5.2-4).");
        }

        /// <summary>
        /// <b>잔해는 원반 밖 선수 좌현이다</b>(조항 <c>O-5</c>). 테두리 안에 있으면 배와 겹치고,
        /// 우현이면 <see cref="LastShiftHullFrames"/> 의 배경막과 부딪힌다.
        /// </summary>
        [Test]
        public void TheDebrisSitsOutsideTheDiscOnTheBowPortSide()
        {
            Assert.That(LastShiftHullShell.Contains(LastShiftSalvage.FieldCenterX, LastShiftSalvage.FieldCenterZ),
                Is.False, "잔해가 원반 안에 있다 — 배와 겹친다.");
            Assert.That(LastShiftSalvage.FieldCenterX, Is.LessThan(0f), "선수 쪽이 아니다(조종석은 -x 다).");
            Assert.That(LastShiftSalvage.FieldCenterZ, Is.LessThan(0f), "좌현이 아니다.");

            // 선외 판정이 잔해 자리와 에어록 밖을 둘 다 잡고, 배 안은 하나도 안 잡는다.
            Assert.That(LastShiftAirlock.IsOutside(LastShiftSalvage.FieldCenter), Is.True);
            Assert.That(LastShiftAirlock.IsOutside(
                new Vector3(LastShiftBypassDuct.AirlockCenterX,
                    LastShiftAirlock.OutsideWalkY - 1f, LastShiftBypassDuct.AirlockCenterZ)), Is.True);
            Assert.That(LastShiftAirlock.IsOutside(LastShiftShipDimensions.SpawnPoint), Is.False,
                "스폰 지점이 선외로 잡힌다 — 배 안에서 산소가 탄다.");
            Assert.That(LastShiftAirlock.IsOutside(LastShiftAirlock.ReturnPoint), Is.False,
                "에어록 바닥이 선외로 잡힌다 — 챔버와 진공의 경계가 사라진다.");
        }

        /// <summary>
        /// <b>선외 보행면과 바깥 해치가 같은 평면이다.</b> 갈리면 나가는 순간 발밑에 단차가
        /// 생기고, 그러면 추진·유영 같은 새 이동 동사가 필요해진다(§4.1-3 이 별도 씬을
        /// 물리친 것과 같은 절약이 여기서도 걸린다).
        /// </summary>
        [Test]
        public void TheOutsideWalkPlaneIsTheOuterHatchPlane()
        {
            Assert.That(LastShiftAirlock.OutsideWalkY,
                Is.EqualTo(LastShiftBypassDuct.AirlockFloorY).Within(Tolerance));
            Assert.That(LastShiftAirlock.ReturnPoint.y,
                Is.EqualTo(LastShiftAirlock.OutsideWalkY).Within(Tolerance));
            Assert.That(LastShiftSalvage.FieldCenter.y,
                Is.EqualTo(LastShiftAirlock.OutsideWalkY).Within(Tolerance),
                "잔해가 보행면과 다른 높이에 떠 있다 — 걸어서 못 닿는다.");
        }

        /// <summary>
        /// <b>기항 재충전은 구간 예산을 안 건드린다.</b> 채우는 문이
        /// <see cref="LastShiftCrewOxygen.RefillAtPort"/> 하나이고, 소모율·<c>80</c>초 예산은
        /// 그대로여야 <c>RG-1(4-b)</c> 가 재던 값이 안 움직인다.
        /// </summary>
        [Test]
        public void PortRefillDoesNotChangeTheInSegmentBudget()
        {
            Assert.That(LastShiftRecoveryTuning.SuitOxygenDrainPerSecond,
                Is.EqualTo(0.0125f).Within(Tolerance), "소모율이 바뀌면 RG-1(4-b)의 80초가 움직인다.");

            var refillSeconds = LastShiftRecoveryTuning.SuitOxygenInitial
                                / LastShiftRecoveryTuning.SuitOxygenRefillPerSecond;
            var travelSeconds = LastShiftSalvage.DistanceFromAirlock * 2f
                                / LastShiftPlayerController.MoveSpeed;
            Assert.That(refillSeconds, Is.LessThan(travelSeconds + LastShiftAirlock.CycleSeconds * 2f),
                $"재충전 {refillSeconds:F0}초가 왕복보다 길다 — 기항이 게이지 채우는 대기 화면이 된다.");
        }

        /// <summary>
        /// 뜯고 → 에어록으로 돌아와 → 반입까지 한 번에 돈다. 반입 조건(가압 구역 복귀)은
        /// sandbox 가 좌표로 판정하므로, 여기서는 원장 쪽 경계만
        /// <see cref="LastShiftSalvage.Deposit"/> 로 민다.
        /// </summary>
        private static void HarvestAndReturn(int chunks)
        {
            var atField = LastShiftSalvage.FieldCenter;
            for (var index = 0; index < chunks; index++)
            {
                LastShiftSalvage.Tick(LastShiftSalvage.HarvestSeconds);
                Assert.That(LastShiftSalvage.TryHarvest(atField), Is.True);
                if (LastShiftSalvage.Carried < LastShiftSalvage.CarryCapacity) continue;
                LastShiftSalvage.Deposit();
            }

            LastShiftSalvage.Deposit();
        }
    }
}
