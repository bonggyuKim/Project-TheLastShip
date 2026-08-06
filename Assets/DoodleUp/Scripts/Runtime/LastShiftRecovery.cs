using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 세 악화 시계가 각각 겨누는 계통. CT-01 §2.3 은 이 셋을 하나의 통합 게이지로 합치지 말라고
    /// 명시한다. 합치면 매 run 우선순위 판단이 같아져 "무엇을 먼저 포기할지" 라는 선택이 사라진다.
    /// </summary>
    public enum LastShiftShipSystem
    {
        Cooling,
        Power,
        Oxygen
    }

    /// <summary>
    /// R1 수리 동사의 3계열. 같은 손상을 세 가지 비용으로 되돌린다.
    /// </summary>
    public enum LastShiftRepairMode
    {
        /// <summary>제자리 완전 복구. 시간이 가장 오래 걸리고 되돌림이 영구하다.</summary>
        SafeRestore,

        /// <summary>임시 결속. 즉시에 가깝지만 <see cref="LastShiftRecoveryTuning.QuickBypassLifetimeSeconds"/> 후 재이탈한다.</summary>
        QuickBypass,

        /// <summary>성능 포기. 대기 시간 없이 해당 구역을 차단한다. 악화는 멈추지만 회복도 없다.</summary>
        PerformanceSacrifice
    }

    /// <summary>
    /// R3 판정 결과. 즉사는 없다(CT-01 §2.1). 실패는 타이머 만료 또는 서서히 진행되는 산소 고갈로만 발생한다.
    /// </summary>
    public enum LastShiftVerdict
    {
        Pending,
        SuccessNominalDocking,
        SuccessCompromised,
        FailureAsphyxiation,
        FailureAdrift,
        FailureInsufficientThrust
    }

    /// <summary>
    /// CT-01 초안 수치 정본. 악화 계수·타이머·회복 소요는 game-balance 소관이므로
    /// 코드 어디에도 리터럴로 흩뿌리지 않고 전부 여기에만 둔다. 값 조정이 재작업 없이 끝나야 한다.
    /// </summary>
    public static class LastShiftRecoveryTuning
    {
        // ── R1 수리 동사 소요 시간 ─────────────────────────────────────────
        public const float SafeRestoreSeconds = 4.0f;
        public const float QuickBypassSeconds = 0.8f;
        public const float PerformanceSacrificeSeconds = 0f;
        public const float QuickBypassLifetimeSeconds = 60f;

        // ── R2 열 시계 ────────────────────────────────────────────────────
        public const float HeatRiseThrustThreshold = 0.60f;
        public const float HeatRisePerSecond = 0.020f;
        public const float HeatRecoveryPerSecond = 0.030f;
        public const float HeatProtectionTrigger = 1.00f;

        // ── CT-06 N5 열 폭주 가속 (기획 §3.3 S-H2) ────────────────────────
        /// <summary>
        /// <c>S-H2</c> 엔진 열 폭주 발동선. 냉각 미연결 상태에서 열이 이 값에 닿으면 tick 이
        /// <see cref="HeatRisePerSecond"/> 에서 <see cref="HeatRiseRunawayPerSecond"/> 로 갈아탄다.
        ///
        /// <b>기존 tick 을 대체하지 않고 분기만 늘린다.</b> 0.90 아래에서는 여전히 0.020/s 다.
        ///
        /// 기획 §3.3 은 해제선 <c>0.84</c> 도 함께 정의하지만 여기서는 쓰지 않는다 —
        /// 히스테리시스가 관측되려면 열이 내려갔다 올라와야 하는데, 이 분기가 도는 조건
        /// (<c>냉각 미봉쇄 AND 추력 &gt; 0.60</c>)에서 열은 <b>단조 증가</b>다. 열이 내려가는
        /// 유일한 경로는 냉각 복구이고 그때는 이 분기 자체가 꺼진다. 해제선은 상황 표시 계층
        /// (<c>S-H2</c> 연출·HUD, CT-07 N6·N7)의 것이며 그쪽에서 다뤄야 한다.
        /// </summary>
        public const float HeatRunawayTrigger = 0.90f;

        /// <summary>
        /// <c>S-H2</c> 활성 중 열 상승률. <b>초안값이며 <c>game-balance</c> 검증 대상이다.</b>
        /// 0.020 → 0.035 는 1.75배이고, 0.90 에서 보호 발동(1.00)까지 5초가 2.9초로 줄어든다.
        /// </summary>
        public const float HeatRiseRunawayPerSecond = 0.035f;

        /// <summary>엔진 보호가 발동했을 때 강제되는 추력 상한. 성공선 0.30 아래라서 추력 성공선 자체를 잃는다.</summary>
        public const float ProtectedThrustCeiling = 0.25f;

        /// <summary>냉각 구역을 포기했을 때의 엔진 디레이트. 성공선 0.30 위라서 "절충 생환" 경로가 남는다.</summary>
        public const float SacrificedThrustCeiling = 0.35f;

        // ── R2 산소 시계 ──────────────────────────────────────────────────
        public const float OxygenLeakPerSecond = 0.006f;
        public const float OxygenLeakHullReference = 0.5f;

        /// <summary>봉합 완료 + bus 연결 상태에서만 도는 산소 펌프 회복률.</summary>
        public const float OxygenPumpRecoveryPerSecond = 0.004f;

        // ── 구역 격리 (CT-05 N0 / 기획 v0.3 §2.2.1) ───────────────────────
        /// <summary>
        /// 문이 열린 두 구역의 압력 평준화율. 매초 두 구역의 <b>차이</b>가 이 비율만큼 줄고,
        /// 각 구역은 그 절반씩 움직인다. 차 0.5 인 두 구역이 같아지는 데 약 28초다 —
        /// 문을 열어둔 채로도 즉사하지 않을 만큼 느리고, 방치하면 확실히 전파될 만큼 빠르다.
        /// </summary>
        public const float ZoneEqualizeRatePerSecond = 0.08f;

        /// <summary>문 열기·닫기 소요. 임시 수리(QuickBypass)와 같은 값이다.</summary>
        public const float ZoneDoorTransitionSeconds = 0.8f;

        // ── 개인 예비 산소 (CT-05 N1) ─────────────────────────────────────
        /// <summary>승무원 각자의 항해 1회 예산. 보충 지점은 0개다 — 한 번 쓰면 돌아오지 않는다.</summary>
        public const float SuitOxygenInitial = 1.00f;

        /// <summary>진공 구역에 있을 때만 도는 소모율. 1.00 에서 0.00 까지 정확히 80초다.</summary>
        public const float SuitOxygenDrainPerSecond = 0.0125f;

        /// <summary>이 값 이하의 구역 압력이 진공이다. 사이렌선(0.15)과 겹치지 않아야 한다.</summary>
        public const float VacuumOxygenPressure = 0.00f;

        /// <summary>
        /// 이 값 미만의 구역 압력은 0.00 으로 스냅한다. 평준화(§2.2.1)는 지수 접근이라
        /// 파공 구역이 0 에 고정돼도 이웃 구역은 정확히 0 에 닿지 않는다. 스냅이 없으면
        /// 진공선(0.00)이 사실상 도달 불가가 되어 RG-1 의 "80초 보장" 구간 자체가 시작되지 않는다.
        /// 0.005 는 압력 0.5% 로, 관측·판정 어디에도 의미를 갖지 않는 크기다.
        /// </summary>
        public const float ZonePressureVacuumSnap = 0.005f;

        /// <summary>막대 적색 점멸 + 호흡음 증폭 구간. 남은 20초를 시각으로 알린다.</summary>
        public const float SuitOxygenCriticalThreshold = 0.25f;

        // ── S-O3 전선 사이렌 (CT-05 N9) ───────────────────────────────────
        /// <summary>
        /// 사이렌 발동선. CT-01 §3.3 국소 정보 규칙의 유일한 명시적 예외로, 모든 구역에서 들린다.
        /// 예비 산소 소모 개시선(<see cref="VacuumOxygenPressure"/>)과 절대 겹치지 않아야 한다 —
        /// 겹치면 사이렌이 곧 사망 예고가 되어 24초 대응 창이 사라진다.
        /// </summary>
        public const float OxygenSirenTrigger = 0.15f;

        /// <summary>해제선. 발동선보다 위라서 경계에서 사이렌이 떨리지 않는다.</summary>
        public const float OxygenSirenRelease = 0.20f;

        // ── R2 전력 시계 ──────────────────────────────────────────────────
        public const float UnpoweredBusCeiling = 0.40f;
        public const float BusDropPerSecond = 0.050f;
        public const float BusRecoveryPerSecond = 0.060f;
        public const float UnpoweredSteeringDelaySeconds = 0.5f;

        // ── CT-06 N3 연료 예산 (기획 §2.3 B-2) ────────────────────────────
        /// <summary>항해 1회 예산. 보급 지점은 0개다.</summary>
        public const float FuelReserveInitial = 1.00f;

        /// <summary>
        /// 연료 소모율. <c>-FuelDrainPerThrustSecond × ThrustDemand /s</c> 로 줄어든다 —
        /// 추력에 비례하므로 <b>엔진을 켜 두는 것 자체가 예산 집행</b>이다.
        ///
        /// <b>초안값이며 <c>game-balance</c> 검증 대상이다.</b> 이 값이 정하는 총 예산은
        /// <c>1.00 / 0.0040 = 250 thrust·s</c> 이고, 도킹 요구
        /// (<see cref="DockTargetThrustSeconds"/> 150)에 대해 여유 67% 다. 여유가 과하면 연료가
        /// 사건이 되지 않고, 부족하면 <c>S-T2</c> 가 상시 활성이 된다(기획 §7.4).
        /// </summary>
        public const float FuelDrainPerThrustSecond = 0.0040f;

        // ── CT-06 N4 도킹 누적 진행 (기획 §2.3 B-2) ───────────────────────
        /// <summary>
        /// 도킹 성립에 필요한 추력적분. 단위 <c>thrust·s</c> 이며 <c>DockProgress</c> 가 이 값에
        /// 닿아야 도킹이 성립한다.
        ///
        /// <b>초안값이며 <c>game-balance</c> 검증 대상이다.</b> 추력 0.50 을 300초 유지하면
        /// 150 이므로 제한시간(<see cref="DockingTimerSeconds"/>)과 아슬아슬하게 맞물린다 —
        /// 이 관계가 "추력을 내려 열을 식힐지" 를 실제 비용 있는 선택으로 만든다.
        /// </summary>
        public const float DockTargetThrustSeconds = 150f;

        // ── R3 판정 ───────────────────────────────────────────────────────
        /// <summary>CT-01 §2.4 권고. 6분은 "다 고치고 기다리기" 가 되어 압박이 사라졌다.</summary>
        public const float DockingTimerSeconds = 300f;
        public const float DockingSuccessThrust = 0.30f;
        public const float DockingSuccessOxygen = 0.20f;

        /// <summary>
        /// 질식 실패선. CT-05 N2 로 판정 주체가 선체 압력에서 개인 예비 산소로 옮겨졌으므로
        /// 이 값은 <see cref="LastShiftCrewOxygen.SuitOxygen"/> 에 걸린다.
        /// </summary>
        public const float AsphyxiationSuitOxygenThreshold = 0.00f;

        public static float DurationFor(LastShiftRepairMode mode)
        {
            return mode switch
            {
                LastShiftRepairMode.SafeRestore => SafeRestoreSeconds,
                LastShiftRepairMode.QuickBypass => QuickBypassSeconds,
                _ => PerformanceSacrificeSeconds
            };
        }
    }

    /// <summary>
    /// 아이템 역할과 계통의 대응. Tether 는 어느 계통도 되돌리지 못한다 — CT-01 §6 은 Tether 를
    /// 상태를 직접 바꾸지 않는 조력 도구로 못박았다.
    /// </summary>
    public static class LastShiftSystemMap
    {
        public const int SystemCount = 3;

        public static bool TryResolve(LastShiftItemRole role, out LastShiftShipSystem system)
        {
            switch (role)
            {
                case LastShiftItemRole.CoolingCanister:
                    system = LastShiftShipSystem.Cooling;
                    return true;
                case LastShiftItemRole.Battery:
                    system = LastShiftShipSystem.Power;
                    return true;
                case LastShiftItemRole.PatchPlate:
                    system = LastShiftShipSystem.Oxygen;
                    return true;
                default:
                    system = LastShiftShipSystem.Cooling;
                    return false;
            }
        }

        public static LastShiftItemRole RoleFor(LastShiftShipSystem system)
        {
            return system switch
            {
                LastShiftShipSystem.Cooling => LastShiftItemRole.CoolingCanister,
                LastShiftShipSystem.Power => LastShiftItemRole.Battery,
                _ => LastShiftItemRole.PatchPlate
            };
        }
    }

    /// <summary>
    /// 계통별 회복 장부. "복구되었는가" 자체는 여기 두지 않는다 — 그건 아이템의
    /// <see cref="LastShiftGrabbable.Secured"/> 가 정본이고 이미 네트워크로 동기화된다.
    /// 장부는 아이템 상태만으로 알 수 없는 것, 즉 <b>어떤 계열로</b> 되돌렸는지와
    /// 임시 결속의 남은 수명, 구역 차단 여부, 진행 중인 작업 채널만 관리한다.
    /// 같은 사실을 두 곳에 두면 반드시 어긋난다.
    /// </summary>
    public sealed class LastShiftRepairLedger
    {
        public struct Entry
        {
            public LastShiftRepairMode Mode;
            public bool HasCompletedRepair;
            public bool Sacrificed;
            public float BypassRemainingSeconds;
            public float ChannelRemainingSeconds;
            public bool ChannelActive;
            public LastShiftRepairMode ChannelMode;
        }

        private readonly Entry[] entries = new Entry[LastShiftSystemMap.SystemCount];

        public int SacrificeCount { get; private set; }
        public int BypassLapseCount { get; private set; }
        public bool SacrificeUsed => SacrificeCount > 0;

        public Entry this[LastShiftShipSystem system] => entries[(int)system];

        public bool IsSacrificed(LastShiftShipSystem system) => entries[(int)system].Sacrificed;
        public bool IsChanneling(LastShiftShipSystem system) => entries[(int)system].ChannelActive;
        public float ChannelRemaining(LastShiftShipSystem system) => entries[(int)system].ChannelRemainingSeconds;
        public LastShiftRepairMode ModeOf(LastShiftShipSystem system) => entries[(int)system].Mode;
        public float BypassRemaining(LastShiftShipSystem system) => entries[(int)system].BypassRemainingSeconds;

        public byte SacrificeMask
        {
            get
            {
                byte mask = 0;
                for (var index = 0; index < entries.Length; index++)
                    if (entries[index].Sacrificed) mask |= (byte)(1 << index);
                return mask;
            }
        }

        public void Reset()
        {
            for (var index = 0; index < entries.Length; index++) entries[index] = default;
            SacrificeCount = 0;
            BypassLapseCount = 0;
        }

        /// <summary>클라이언트 표시용. 서버 장부를 스냅샷 마스크로 되살린다.</summary>
        public void ApplyReplicatedSacrificeMask(byte mask)
        {
            SacrificeCount = 0;
            for (var index = 0; index < entries.Length; index++)
            {
                entries[index].Sacrificed = (mask & (1 << index)) != 0;
                if (entries[index].Sacrificed) SacrificeCount++;
            }
        }

        /// <summary>
        /// 작업 채널을 연다. 성능 포기는 소요 0 이라 이 호출 안에서 즉시 확정된다.
        /// 이미 차단된 구역이거나 같은 계통 작업이 진행 중이면 거부한다.
        /// </summary>
        public bool BeginChannel(LastShiftShipSystem system, LastShiftRepairMode mode)
        {
            var index = (int)system;
            if (entries[index].Sacrificed || entries[index].ChannelActive) return false;

            var duration = LastShiftRecoveryTuning.DurationFor(mode);
            if (duration <= 0f)
            {
                CompleteChannel(system, mode);
                return true;
            }

            entries[index].ChannelActive = true;
            entries[index].ChannelMode = mode;
            entries[index].ChannelRemainingSeconds = duration;
            return true;
        }

        public void CancelChannel(LastShiftShipSystem system)
        {
            var index = (int)system;
            entries[index].ChannelActive = false;
            entries[index].ChannelRemainingSeconds = 0f;
        }

        /// <summary>
        /// 채널을 진행시킨다. 완료된 계통을 호출자에게 알려 아이템 고정·구역 차단을 반영하게 한다.
        /// </summary>
        public bool TryAdvanceChannel(LastShiftShipSystem system, float deltaTime, out LastShiftRepairMode completedMode)
        {
            completedMode = default;
            var index = (int)system;
            if (!entries[index].ChannelActive) return false;

            entries[index].ChannelRemainingSeconds -= deltaTime;
            if (entries[index].ChannelRemainingSeconds > 0f) return false;

            completedMode = entries[index].ChannelMode;
            CompleteChannel(system, completedMode);
            return true;
        }

        /// <summary>
        /// 임시 결속 수명을 소진시킨다. 만료된 계통은 재이탈로 판정해 호출자가 아이템 고정을 풀게 한다.
        /// </summary>
        public bool TryLapseBypass(LastShiftShipSystem system, float deltaTime)
        {
            var index = (int)system;
            if (!entries[index].HasCompletedRepair ||
                entries[index].Mode != LastShiftRepairMode.QuickBypass ||
                entries[index].BypassRemainingSeconds <= 0f) return false;

            entries[index].BypassRemainingSeconds -= deltaTime;
            if (entries[index].BypassRemainingSeconds > 0f) return false;

            entries[index].BypassRemainingSeconds = 0f;
            entries[index].HasCompletedRepair = false;
            BypassLapseCount++;
            return true;
        }

        private void CompleteChannel(LastShiftShipSystem system, LastShiftRepairMode mode)
        {
            var index = (int)system;
            entries[index].ChannelActive = false;
            entries[index].ChannelRemainingSeconds = 0f;
            entries[index].Mode = mode;

            if (mode == LastShiftRepairMode.PerformanceSacrifice)
            {
                entries[index].Sacrificed = true;
                entries[index].HasCompletedRepair = false;
                entries[index].BypassRemainingSeconds = 0f;
                SacrificeCount++;
                return;
            }

            entries[index].HasCompletedRepair = true;
            entries[index].BypassRemainingSeconds = mode == LastShiftRepairMode.QuickBypass
                ? LastShiftRecoveryTuning.QuickBypassLifetimeSeconds
                : 0f;
        }
    }

    /// <summary>
    /// 악화 tick 한 스텝의 결과. 상태 구조체에 담기지 않는 파생값만 실어 보낸다.
    /// </summary>
    public struct LastShiftTickReport
    {
        public float ThrustCeiling;
        public bool HeatProtectionEngaged;
        public bool SteeringDelayed;
        public bool OxygenPumpRunning;

        public static LastShiftTickReport Idle => new() { ThrustCeiling = 1f };
    }

    /// <summary>
    /// 계통별 억제 여부를 한 곳에서 판정한다. 세 시계가 같은 기준을 읽어야
    /// "고쳤는데도 계속 나빠진다" 또는 그 반대가 생기지 않는다.
    /// </summary>
    public struct LastShiftContainment
    {
        public bool CoolingRestored;
        public bool CoolingSacrificed;
        public bool PowerRestored;
        public bool PowerSacrificed;
        public bool OxygenRestored;
        public bool OxygenSacrificed;

        public bool CoolingContained => CoolingRestored || CoolingSacrificed;
        public bool PowerContained => PowerRestored || PowerSacrificed;
        public bool OxygenContained => OxygenRestored || OxygenSacrificed;
    }

    /// <summary>
    /// R2 상태 악화·회복. 열·산소·전력이 각각 다른 것을 뺏는다:
    /// 열은 추력 성공선 자체를, 산소는 남은 시간을, 전력은 다른 두 복구의 속도를 뺏는다.
    /// </summary>
    public static class LastShiftDeterioration
    {
        /// <summary>
        /// 구역별 압력이 없는 호출 경로(EditMode 최소 조립 등)를 위한 호환 진입점.
        /// 세 구역이 모두 같은 압력이고 문이 전부 열려 있는 것과 같다.
        /// </summary>
        public static LastShiftTickReport Tick(
            ref LastShiftShipState state,
            in LastShiftContainment containment,
            float deltaTime)
        {
            var pressures = LastShiftZonePressures.Uniform(state.OxygenPressure);
            return Tick(ref state, ref pressures, containment, LastShiftZone.LifeSupport, LastShiftDoorState.AllOpen, deltaTime);
        }

        /// <summary>
        /// 산소 시계는 구역별로 돈다(기획 v0.3 §2.2). 파공이 있는 구역에서만 새고, 문이 열린
        /// 구역끼리는 평준화로 끌려간다. <paramref name="breachZone"/> 은 봉합 지점(PatchPlate
        /// nominal)이 있는 구역이며, 그 구역을 격리하면 나머지 구역의 하강이 그 자리에서 멈춘다.
        /// </summary>
        public static LastShiftTickReport Tick(
            ref LastShiftShipState state,
            ref LastShiftZonePressures pressures,
            in LastShiftContainment containment,
            LastShiftZone breachZone,
            in LastShiftDoorState doors,
            float deltaTime)
        {
            var report = LastShiftTickReport.Idle;
            if (deltaTime <= 0f)
            {
                state.OxygenPressure = pressures[LastShiftZone.Cockpit];
                return ReportOnly(ref state, containment);
            }

            // ── 전력 시계 ── bus 미연결이면 0.40 을 넘지 못한다. 다른 두 복구의 속도를 뺏는 자리.
            // 구역을 포기하면 하강이 멈춘다(회복은 없다). 열·산소와 같은 규칙이어야 세 계통에서
            // "포기" 의 의미가 같다. 단 조향 지연은 남는다 — 포기는 bus 를 연결해주지 않는다.
            if (containment.PowerRestored)
            {
                state.BusPower = Mathf.Clamp01(state.BusPower + LastShiftRecoveryTuning.BusRecoveryPerSecond * deltaTime);
            }
            else if (!containment.PowerContained && state.BusPower > LastShiftRecoveryTuning.UnpoweredBusCeiling)
            {
                state.BusPower = Mathf.Max(
                    LastShiftRecoveryTuning.UnpoweredBusCeiling,
                    state.BusPower - LastShiftRecoveryTuning.BusDropPerSecond * deltaTime);
            }

            // ── 열 시계 ── 고추력 + 냉각 미복구에서만 오른다. 복구 후에만 내려간다.
            // 구역을 포기한 경우 악화는 멈추지만 회복도 없다(그 자리에 얼어붙는다).
            //
            // CT-06 N5: 열이 S-H2 발동선을 넘으면 상승률이 갈아탄다(기획 §3.3). 기존 tick 을
            // 대체하는 것이 아니라 분기가 하나 늘 뿐이고, 발동선 아래는 그대로 0.020/s 다.
            if (!containment.CoolingContained && state.ThrustDemand > LastShiftRecoveryTuning.HeatRiseThrustThreshold)
            {
                var rise = IsHeatRunaway(state, containment)
                    ? LastShiftRecoveryTuning.HeatRiseRunawayPerSecond
                    : LastShiftRecoveryTuning.HeatRisePerSecond;
                state.EngineHeat = Mathf.Clamp01(state.EngineHeat + rise * deltaTime);
            }
            else if (containment.CoolingRestored)
            {
                state.EngineHeat = Mathf.Clamp01(state.EngineHeat - LastShiftRecoveryTuning.HeatRecoveryPerSecond * deltaTime);
            }

            // ── 산소 시계 ── 이제 구역별로 돈다(기획 v0.3 §2.2).
            // 새는 것은 파공이 있는 구역 하나뿐이고, 나머지 구역은 문이 열려 있는 동안 평준화로
            // 끌려 내려간다. 문을 닫으면 그 전파가 그 자리에서 멈추는 것이 격리의 즉시 효과다.
            if (!containment.OxygenContained)
            {
                var leak = LastShiftRecoveryTuning.OxygenLeakPerSecond *
                           (1f - state.HullIntegrity) / LastShiftRecoveryTuning.OxygenLeakHullReference;
                pressures[breachZone] -= leak * deltaTime;
            }
            else if (containment.OxygenRestored && containment.PowerRestored)
            {
                // 펌프는 재가압한다. 다만 "격리된 구역은 재가압되지 않는다"(§2.2.2 대가) 이므로
                // 조종석에서 열린 문으로 이어지는 구역만 공기를 받는다. 버린 구역은 버린 채 남는다.
                var recovery = LastShiftRecoveryTuning.OxygenPumpRecoveryPerSecond * deltaTime;
                for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
                {
                    var zone = (LastShiftZone)index;
                    if (!IsConnectedToCockpit(zone, doors)) continue;
                    pressures[zone] += recovery;
                }
                report.OxygenPumpRunning = true;
            }

            pressures.Equalize(doors, deltaTime);
            SnapNearVacuum(ref pressures);

            // OxygenPressure 는 조종석 압력의 파생값이다. 도킹 성공 판정·leak 점수·네트워크
            // 스냅샷이 모두 이 필드를 읽고 있고, 판정 기준은 세 구역 평균이 아니라 조종석 하나다.
            state.OxygenPressure = pressures[LastShiftZone.Cockpit];

            var ceiling = ResolveThrustCeiling(state, containment);
            if (state.ThrustDemand > ceiling) state.ThrustDemand = ceiling;

            // ── 연료·도킹 적분 (CT-06 N3·N4) ──
            // 추력 상한을 적용한 <b>뒤에</b> 적분한다. 열 보호가 추력을 0.25 로 눌렀는데 연료는
            // 0.92 어치를 태우고 도킹 진행은 0.92 어치를 버는 그림이 되면, 플레이어가 화면에서
            // 보는 추력과 예산이 어긋난다. 상한이 곧 실제 추력이므로 그 값으로 재야 한다.
            AdvanceFuelAndDocking(ref state, deltaTime);

            report.ThrustCeiling = ceiling;
            report.HeatProtectionEngaged = state.EngineHeat >= LastShiftRecoveryTuning.HeatProtectionTrigger;
            report.SteeringDelayed = !containment.PowerRestored;
            return report;
        }

        /// <summary>
        /// <c>S-H2</c> 엔진 열 폭주가 성립하는가(기획 §3.3). 조건은 <c>EngineHeat ≥ 0.90</c> 이고
        /// 냉각이 연결되지 않은 상태다.
        ///
        /// 봉쇄(<c>Contained</c>)를 "냉각이 연결됨" 으로 읽는다 — 구역을 포기해 봉쇄한 경우도
        /// 열이 더는 오르지 않으므로 폭주가 아니다. 어차피 호출부의 상승 분기가 같은 조건으로
        /// 먼저 걸러지지만, 이 술어 자체가 단독으로 참이어야 테스트가 조건을 직접 겨눌 수 있다.
        /// </summary>
        public static bool IsHeatRunaway(in LastShiftShipState state, in LastShiftContainment containment)
        {
            return !containment.CoolingContained &&
                   state.EngineHeat >= LastShiftRecoveryTuning.HeatRunawayTrigger;
        }

        /// <summary>
        /// 연료를 태우고 도킹 진행을 쌓는다(CT-06 N3·N4, 기획 §2.3 B-2).
        ///
        /// <b>연료가 바닥나면 추력이 나오지 않는다.</b> 그래서 남은 연료로 낼 수 있는
        /// 추력적분만큼만 <c>DockProgress</c> 에 실린다 — 연료 0 인 배가 추력 슬라이더만 올려
        /// 도킹을 채우는 일이 없어야 한다. 한 tick 안에서 연료가 소진되는 경계에서도
        /// 어긋나지 않도록, 태운 연료에서 실제 추력적분을 되돌려 계산한다.
        /// </summary>
        private static void AdvanceFuelAndDocking(ref LastShiftShipState state, float deltaTime)
        {
            if (deltaTime <= 0f || state.ThrustDemand <= 0f) return;

            var demandedThrustSeconds = state.ThrustDemand * deltaTime;
            var demandedFuel = LastShiftRecoveryTuning.FuelDrainPerThrustSecond * demandedThrustSeconds;
            var burnedFuel = Mathf.Min(state.FuelReserve, demandedFuel);

            state.FuelReserve = Mathf.Clamp01(state.FuelReserve - burnedFuel);
            state.DockProgress += burnedFuel / LastShiftRecoveryTuning.FuelDrainPerThrustSecond;
        }

        /// <summary>
        /// 조종석에서 열린 문만 따라가 이 구역에 닿을 수 있는가. 구역이 일렬로 셋이므로
        /// 조종석↔엔진실 문 하나, 그리고 그 다음 엔진실↔산소실 문 하나를 차례로 본다.
        /// 재가압 대상 판정에 쓴다 — 격리한 구역은 재가압되지 않는 것이 격리의 대가다.
        /// </summary>
        public static bool IsConnectedToCockpit(LastShiftZone zone, in LastShiftDoorState doors)
        {
            return zone switch
            {
                LastShiftZone.Cockpit => true,
                LastShiftZone.Utility => doors[0],
                _ => doors[0] && doors[1]
            };
        }

        /// <summary>
        /// 평준화는 지수 접근이라 이웃 구역이 0 에 정확히 닿지 않는다. 진공선(0.00)이 도달
        /// 불가가 되면 RG-1 의 80초 보장 구간 자체가 시작되지 않으므로 아주 작은 잔압은 버린다.
        /// </summary>
        private static void SnapNearVacuum(ref LastShiftZonePressures pressures)
        {
            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var zone = (LastShiftZone)index;
                if (pressures[zone] < LastShiftRecoveryTuning.ZonePressureVacuumSnap) pressures[zone] = 0f;
            }
        }

        /// <summary>
        /// 엔진 보호가 최우선이다. 열이 한계면 냉각 포기 디레이트보다 더 강한 상한이 걸린다.
        /// </summary>
        public static float ResolveThrustCeiling(in LastShiftShipState state, in LastShiftContainment containment)
        {
            if (state.EngineHeat >= LastShiftRecoveryTuning.HeatProtectionTrigger)
                return LastShiftRecoveryTuning.ProtectedThrustCeiling;
            return containment.CoolingSacrificed ? LastShiftRecoveryTuning.SacrificedThrustCeiling : 1f;
        }

        /// <summary>
        /// 시간을 진행시키지 않고 상한만 확정한다. 상한 적용(clamp)은 여기서도 해야 한다 —
        /// 운석 적용 직후처럼 tick 이전에 이미 열이 한계인 상태에서 잠금이 한 프레임 늦으면
        /// 그 프레임 동안 성공선 위 추력이 유효한 것처럼 보인다.
        /// </summary>
        private static LastShiftTickReport ReportOnly(ref LastShiftShipState state, in LastShiftContainment containment)
        {
            var ceiling = ResolveThrustCeiling(state, containment);
            if (state.ThrustDemand > ceiling) state.ThrustDemand = ceiling;
            return new LastShiftTickReport
            {
                ThrustCeiling = ceiling,
                HeatProtectionEngaged = state.EngineHeat >= LastShiftRecoveryTuning.HeatProtectionTrigger,
                SteeringDelayed = !containment.PowerRestored,
                OxygenPumpRunning = false
            };
        }
    }

    /// <summary>
    /// R3 승패 판정. 도킹 트리거 진입과 타이머 0 두 시점만 판정하며, 그 사이에는
    /// 산소 고갈만 결과를 확정할 수 있다. 즉사 경로는 만들지 않는다.
    /// </summary>
    public static class LastShiftVerdictResolver
    {
        public static bool IsResolved(LastShiftVerdict verdict) => verdict != LastShiftVerdict.Pending;

        public static bool IsSuccess(LastShiftVerdict verdict) =>
            verdict is LastShiftVerdict.SuccessNominalDocking or LastShiftVerdict.SuccessCompromised;

        /// <summary>
        /// 매 tick 검사. 산소 고갈만 시간 안에서 결과를 확정한다.
        ///
        /// CT-05 N2 로 판정 주체가 선체 압력에서 개인 예비 산소로 옮겨졌다. 압력 0.00 은
        /// 이제 실패가 아니라 개인 예비 산소 소모의 시작 조건이고, 그 사이에 80초 완충이 있다.
        /// 승무원 한 명이 죽어도 항해는 계속되며 <b>전원 사망일 때만</b> 실패다.
        /// </summary>
        public static LastShiftVerdict EvaluateContinuous(in LastShiftShipState state, bool anyCrewAlive)
        {
            if (!anyCrewAlive) return LastShiftVerdict.FailureAsphyxiation;
            return IsStrandedWithoutFuel(state) ? LastShiftVerdict.FailureAdrift : LastShiftVerdict.Pending;
        }

        /// <summary>
        /// 연료가 바닥났고 도킹 진행도 모자란가 — 표류 확정(CT-06 N3, 기획 §2.3 B-2).
        ///
        /// <b>타이머를 기다리지 않고 즉시 판정한다.</b> 연료가 0 이면 추력이 안 나오고
        /// <c>DockProgress</c> 도 더는 오르지 않으므로 도킹은 물리적으로 불가능하다. 그 상태로
        /// 남은 시간을 흘려보내게 두면 플레이어는 <b>아무것도 할 수 없는 채로 시계만 보게 되고</b>,
        /// 그건 기획 §4.3 <c>RG-3</c> 이 금지한 영구 잠금과 같은 것이다.
        ///
        /// 진행도가 이미 목표에 닿았으면 연료가 0 이어도 실패가 아니다 — 도킹은 이미 성립
        /// 가능하고 남은 것은 트리거로 들어가는 일뿐이다.
        /// </summary>
        public static bool IsStrandedWithoutFuel(in LastShiftShipState state)
        {
            return state.FuelReserve <= 0f &&
                   state.DockProgress < LastShiftRecoveryTuning.DockTargetThrustSeconds;
        }

        /// <summary>
        /// 도킹 조건 충족 여부. 미달이면 도킹이 성립하지 않을 뿐이고 실패가 아니다 —
        /// 플레이어는 남은 시간 안에 조건을 갖춰 다시 시도할 수 있다.
        /// 승무원이 한 명도 살아 있지 않으면 도킹시킬 주체가 없으므로 성공선에 포함한다(N2).
        /// </summary>
        public static bool MeetsDockingConditions(in LastShiftShipState state, bool anyCrewAlive)
        {
            return anyCrewAlive &&
                   state.DockProgress >= LastShiftRecoveryTuning.DockTargetThrustSeconds &&
                   state.ThrustDemand >= LastShiftRecoveryTuning.DockingSuccessThrust &&
                   state.OxygenPressure >= LastShiftRecoveryTuning.DockingSuccessOxygen;
        }

        public static LastShiftVerdict EvaluateDocking(in LastShiftShipState state, bool sacrificeUsed, bool anyCrewAlive)
        {
            if (!MeetsDockingConditions(state, anyCrewAlive)) return LastShiftVerdict.Pending;
            return sacrificeUsed ? LastShiftVerdict.SuccessCompromised : LastShiftVerdict.SuccessNominalDocking;
        }

        /// <summary>
        /// 구역이 진공인가. 개인 예비 산소 소모의 유일한 조건이다(N1).
        /// 판정 대상은 <b>그 구역의</b> 압력이다 — N0 이전에는 선체 단일 압력이었고, 그때는
        /// 진공이 언제나 배 전체였다. 구역 차단(산소 계통 성능 포기)으로 밀폐된 구역은
        /// 압력과 무관하게 진공으로 본다 — 밀폐가 곧 그 구역의 공기를 버린 것이기 때문이다.
        /// </summary>
        public static bool IsZoneVacuum(float zonePressure, bool zoneSealedOff)
        {
            return zoneSealedOff || zonePressure <= LastShiftRecoveryTuning.VacuumOxygenPressure;
        }

        /// <summary>
        /// S-O3 사이렌 상태. 발동선과 해제선이 달라 경계에서 떨리지 않는다(N9).
        /// <b>어느 구역이든</b> 0.15 이하면 울리므로 최저 구역 압력을 본다(기획 §2.2 A-2 연쇄).
        /// 사이렌은 배 전체 하나이며, 국소 정보 규칙의 유일한 명시적 예외다.
        /// </summary>
        public static bool EvaluateSiren(in LastShiftZonePressures pressures, bool sirenActive)
        {
            var lowest = pressures.Lowest;
            if (lowest <= LastShiftRecoveryTuning.OxygenSirenTrigger) return true;
            return sirenActive && lowest < LastShiftRecoveryTuning.OxygenSirenRelease;
        }

        /// <summary>타이머 0. 추력이 성공선 아래면 추력 부족, 그 외에는 표류다.</summary>
        public static LastShiftVerdict EvaluateTimeout(in LastShiftShipState state)
        {
            return state.ThrustDemand < LastShiftRecoveryTuning.DockingSuccessThrust
                ? LastShiftVerdict.FailureInsufficientThrust
                : LastShiftVerdict.FailureAdrift;
        }
    }
}
