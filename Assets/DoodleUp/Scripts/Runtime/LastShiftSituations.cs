using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// P0 상황 12개(기획 §3.3). 번호는 <b>테이블 순서</b>이고, 그 순서가 동급 타이브레이크의
    /// 근거이므로 임의로 바꾸면 안 된다 — §3.2 는 "같은 등급이 둘 이상 참이면 테이블 순서상
    /// 뒤쪽이 이긴다" 고 정하고, 산소의 <c>S-O3</c>·<c>S-O4</c> 가 그 유일한 사례다.
    /// </summary>
    public enum LastShiftSituation
    {
        None = 0,
        HeatCouplingLoose = 1,   // S-H1 냉각 결합부 이탈
        HeatRunaway = 2,         // S-H2 엔진 열 폭주
        HeatProtectionLock = 3,  // S-H3 엔진 보호 잠금
        BusDetached = 4,         // S-P1 bus 분리
        PowerCascade = 5,        // S-P2 전력 부족 연쇄
        PowerBlackout = 6,       // S-P3 배전 차단
        HullLeak = 7,            // S-O1 측면 누출
        ZoneLowPressure = 8,     // S-O2 구역 저압
        DecompressionAlarm = 9,  // S-O3 감압 경보  (유일한 전역 상황)
        ZoneVacuum = 10,         // S-O4 구역 진공
        AttitudeDrift = 11,      // S-T1 자세 이탈
        FuelMarginLost = 12      // S-T2 연료 여유 소실
    }

    /// <summary>상황 등급(기획 §3.2). 상황 없음이 <see cref="Normal"/> 다.</summary>
    public enum LastShiftSituationGrade
    {
        Normal = 0,
        Unstable = 1,
        Fault = 2,
        Crisis = 3
    }

    /// <summary>
    /// 계통. 상황은 계통 안에서 배타적이고(최대 1개) 계통끼리는 병렬이다(최대 4개 동시).
    /// </summary>
    public enum LastShiftSystemChannel
    {
        Heat = 0,
        Power = 1,
        Oxygen = 2,
        Propulsion = 3
    }

    /// <summary>
    /// 상황 평가에 필요한 입력 전부. 평가층이 샌드박스를 직접 읽지 않게 하려고 값으로 받는다 —
    /// <see cref="LastShiftRecoveryGuaranteeTests"/> 가 1,440 조합을 조립해 넣어야 하는데,
    /// 그때마다 <c>MonoBehaviour</c> 를 세우는 것은 전수 검증 비용에 맞지 않는다.
    ///
    /// <c>CoolingConnected</c>/<c>BatteryOnBus</c>/<c>HullPatched</c> 는 기획 §3.3 의 이름이고
    /// 코드에서는 <see cref="LastShiftContainment"/> 의 <c>*Restored</c> 에 대응한다.
    /// <b>성능 포기(sacrifice)는 "연결됨" 이 아니다</b> — 포기는 악화를 멈출 뿐 계통을 되돌리지
    /// 않으므로, 포기한 채로 상황이 꺼지면 HUD 가 "고쳤다" 고 거짓말을 한다.
    /// </summary>
    public struct LastShiftSituationInput
    {
        public LastShiftShipState State;
        public LastShiftZonePressures Pressures;
        public bool CoolingConnected;
        public bool BatteryOnBus;
        public bool HullPatched;

        public static LastShiftSituationInput From(
            in LastShiftShipState state,
            in LastShiftZonePressures pressures,
            in LastShiftContainment containment) => new()
        {
            State = state,
            Pressures = pressures,
            CoolingConnected = containment.CoolingRestored,
            BatteryOnBus = containment.PowerRestored,
            HullPatched = containment.OxygenRestored
        };
    }

    /// <summary>
    /// 상황 테이블의 정적 부분 — 등급, 계통, 발동/해제 조건(기획 §3.3).
    ///
    /// 조건을 여기 한 곳에 모으는 이유는 <c>RG-4</c> 전수 검증 때문이다. 조건이 평가기 안에
    /// if 사슬로 흩어져 있으면 테스트가 "이 조합에서 어떤 상황이 참인가" 를 독립적으로 다시
    /// 계산할 수 없고, 그러면 전수 검증이 구현을 구현으로 검사하는 꼴이 된다.
    /// </summary>
    public static class LastShiftSituationTable
    {
        public const int SituationCount = 12;

        /// <summary>
        /// 최소 활성 시간(기획 §0.2 <c>MIN_ACTIVE</c>, §3.5). 활성 후 이 시간 동안은 해제하지
        /// 않는다 — 조건이 즉시 반전돼도 연출(증기 분출, 경보음, HUD 색 전환)이 잘리지 않게
        /// 하려는 것이고, 소관이 <c>game-balance</c> 가 아니라 <c>game-tech-director</c> 인
        /// 유일한 수치다(§0.2 표).
        /// </summary>
        public const float MinimumActiveSeconds = 1.5f;

        // ── 임계값 (기획 §3.3 표) ────────────────────────────────────────────
        // 발동선과 해제선이 다른 것이 히스테리시스다. 같은 값을 두 번 적지 않고 이름을 나눠
        // 두는 이유는, 둘이 같아지는 순간 임계 근처에서 상황이 매 프레임 깜빡이기 때문이다.

        public const float HeatCouplingTrigger = 0.70f;
        public const float HeatCouplingRelease = 0.62f;
        public const float HeatRunawayTrigger = LastShiftRecoveryTuning.HeatRunawayTrigger;   // 0.90
        public const float HeatRunawayRelease = 0.84f;
        public const float HeatLockTrigger = LastShiftRecoveryTuning.HeatProtectionTrigger;   // 1.00
        public const float HeatLockRelease = 0.80f;

        public const float BusDetachedTrigger = 0.65f;
        public const float PowerCascadeTrigger = 0.40f;
        public const float PowerCascadeRelease = 0.48f;
        public const float PowerBlackoutTrigger = 0.15f;
        public const float PowerBlackoutRelease = 0.25f;

        public const float HullLeakTrigger = 0.75f;
        public const float ZoneLowPressureTrigger = 0.35f;
        public const float ZoneLowPressureRelease = 0.42f;
        public const float AlarmTrigger = LastShiftRecoveryTuning.OxygenSirenTrigger;         // 0.15
        public const float AlarmRelease = LastShiftRecoveryTuning.OxygenSirenRelease;         // 0.20
        public const float VacuumThreshold = LastShiftRecoveryTuning.VacuumOxygenPressure;    // 0.00

        public const float AttitudeTriggerDegrees = 60f;
        public const float AttitudeReleaseDegrees = 45f;

        /// <summary><c>S-T2</c> 여유 계수. "남은 연료로 필요 추력적분을 15% 여유로 채울 수 없다".</summary>
        public const float FuelMarginFactor = 1.15f;

        public static LastShiftSituationGrade GradeOf(LastShiftSituation situation) => situation switch
        {
            LastShiftSituation.HeatCouplingLoose => LastShiftSituationGrade.Unstable,
            LastShiftSituation.HeatRunaway => LastShiftSituationGrade.Fault,
            LastShiftSituation.HeatProtectionLock => LastShiftSituationGrade.Crisis,
            LastShiftSituation.BusDetached => LastShiftSituationGrade.Unstable,
            LastShiftSituation.PowerCascade => LastShiftSituationGrade.Fault,
            LastShiftSituation.PowerBlackout => LastShiftSituationGrade.Crisis,
            LastShiftSituation.HullLeak => LastShiftSituationGrade.Unstable,
            LastShiftSituation.ZoneLowPressure => LastShiftSituationGrade.Fault,
            LastShiftSituation.DecompressionAlarm => LastShiftSituationGrade.Crisis,
            LastShiftSituation.ZoneVacuum => LastShiftSituationGrade.Crisis,
            LastShiftSituation.AttitudeDrift => LastShiftSituationGrade.Unstable,
            LastShiftSituation.FuelMarginLost => LastShiftSituationGrade.Fault,
            _ => LastShiftSituationGrade.Normal
        };

        public static LastShiftSystemChannel ChannelOf(LastShiftSituation situation) => situation switch
        {
            LastShiftSituation.HeatCouplingLoose or
            LastShiftSituation.HeatRunaway or
            LastShiftSituation.HeatProtectionLock => LastShiftSystemChannel.Heat,
            LastShiftSituation.BusDetached or
            LastShiftSituation.PowerCascade or
            LastShiftSituation.PowerBlackout => LastShiftSystemChannel.Power,
            LastShiftSituation.HullLeak or
            LastShiftSituation.ZoneLowPressure or
            LastShiftSituation.DecompressionAlarm or
            LastShiftSituation.ZoneVacuum => LastShiftSystemChannel.Oxygen,
            _ => LastShiftSystemChannel.Propulsion
        };

        /// <summary>
        /// 구역마다 따로 평가되는가. 산소 계통 넷 중 <c>S-O3</c> 만 전역이다(§3.3) — 사이렌은
        /// 배 전체 하나이고 국소 정보 규칙의 유일한 명시적 예외다.
        ///
        /// <c>S-O1</c> 은 산소 계통이지만 조건(<c>HullIntegrity</c>)이 배 전체 값이라 구역별로
        /// 평가해도 세 구역이 같은 답을 낸다. 그래도 구역별로 두는 이유는 §3.3 이 산소 계통을
        /// 구역 단위로 정의했고, 계통 등급을 구역마다 합산해야 HUD 3칸이 성립하기 때문이다.
        /// </summary>
        public static bool IsPerZone(LastShiftSituation situation) =>
            ChannelOf(situation) == LastShiftSystemChannel.Oxygen &&
            situation != LastShiftSituation.DecompressionAlarm;

        /// <summary>
        /// 발동 조건(기획 §3.3 표의 "발동 조건" 열 그대로).
        /// <paramref name="zone"/> 는 구역별 상황에만 쓰이고 전역 상황은 무시한다.
        /// </summary>
        public static bool Triggers(LastShiftSituation situation, in LastShiftSituationInput input, LastShiftZone zone)
        {
            var state = input.State;
            return situation switch
            {
                LastShiftSituation.HeatCouplingLoose =>
                    !input.CoolingConnected && state.EngineHeat >= HeatCouplingTrigger,
                LastShiftSituation.HeatRunaway =>
                    state.EngineHeat >= HeatRunawayTrigger && !input.CoolingConnected,
                LastShiftSituation.HeatProtectionLock =>
                    state.EngineHeat >= HeatLockTrigger,

                LastShiftSituation.BusDetached =>
                    !input.BatteryOnBus && state.BusPower <= BusDetachedTrigger,
                LastShiftSituation.PowerCascade =>
                    state.BusPower <= PowerCascadeTrigger,
                LastShiftSituation.PowerBlackout =>
                    state.BusPower <= PowerBlackoutTrigger,

                LastShiftSituation.HullLeak =>
                    !input.HullPatched && state.HullIntegrity <= HullLeakTrigger,
                LastShiftSituation.ZoneLowPressure =>
                    input.Pressures[zone] <= ZoneLowPressureTrigger,
                LastShiftSituation.DecompressionAlarm =>
                    input.Pressures.Lowest <= AlarmTrigger,
                LastShiftSituation.ZoneVacuum =>
                    input.Pressures[zone] <= VacuumThreshold,

                LastShiftSituation.AttitudeDrift =>
                    Mathf.Abs(state.ShipAttitudeDegrees) >= AttitudeTriggerDegrees,
                LastShiftSituation.FuelMarginLost =>
                    FuelMarginIsLost(state),
                _ => false
            };
        }

        /// <summary>
        /// 해제 조건(기획 §3.3 표의 "해제 조건" 열 그대로). <b>발동 조건의 부정이 아니다</b> —
        /// 그게 히스테리시스의 정의다. 부정으로 대신 쓰면 임계 근처에서 깜빡인다.
        /// </summary>
        public static bool Releases(LastShiftSituation situation, in LastShiftSituationInput input, LastShiftZone zone)
        {
            var state = input.State;
            return situation switch
            {
                LastShiftSituation.HeatCouplingLoose =>
                    input.CoolingConnected || state.EngineHeat <= HeatCouplingRelease,
                LastShiftSituation.HeatRunaway =>
                    state.EngineHeat <= HeatRunawayRelease,
                LastShiftSituation.HeatProtectionLock =>
                    state.EngineHeat <= HeatLockRelease,

                LastShiftSituation.BusDetached =>
                    input.BatteryOnBus,
                LastShiftSituation.PowerCascade =>
                    state.BusPower >= PowerCascadeRelease,
                LastShiftSituation.PowerBlackout =>
                    state.BusPower >= PowerBlackoutRelease,

                LastShiftSituation.HullLeak =>
                    input.HullPatched,
                LastShiftSituation.ZoneLowPressure =>
                    input.Pressures[zone] >= ZoneLowPressureRelease,
                // 전역 해제. 한 구역이라도 0.20 아래면 사이렌은 계속 울린다(§3.3).
                LastShiftSituation.DecompressionAlarm =>
                    input.Pressures.Lowest >= AlarmRelease,
                LastShiftSituation.ZoneVacuum =>
                    input.Pressures[zone] > VacuumThreshold,

                LastShiftSituation.AttitudeDrift =>
                    Mathf.Abs(state.ShipAttitudeDegrees) <= AttitudeReleaseDegrees,
                // 표의 "조건 반전". 여유 계수가 곧 히스테리시스 역할을 한다.
                LastShiftSituation.FuelMarginLost =>
                    !FuelMarginIsLost(state),
                _ => true
            };
        }

        /// <summary>
        /// <c>S-T2</c> 조건 — 남은 연료로 도킹 필요 추력적분을 <c>15%</c> 여유로 채울 수 없다.
        /// 기획 표기는 <c>FuelReserve × 250 &lt; (150 - DockProgress) × 1.15</c> 이고, 250 은
        /// 연료 총 예산(<c>1.00 / 0.0040</c>)이라 상수 대신 파생식으로 적는다 — 밸런스가
        /// 소모율을 바꾸면 250 도 따라 움직여야 한다.
        /// </summary>
        public static bool FuelMarginIsLost(in LastShiftShipState state)
        {
            var remainingThrustSeconds = state.FuelReserve / LastShiftRecoveryTuning.FuelDrainPerThrustSecond;
            var needed = LastShiftRecoveryTuning.DockTargetThrustSeconds - state.DockProgress;
            if (needed <= 0f) return false;
            return remainingThrustSeconds < needed * FuelMarginFactor;
        }
    }
}
