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

        /// <summary>
        /// 자연 냉각률(기획 §0.2 <c>HEAT_COOL</c>, §2.3 A-1 "자연 회복"). 추력이
        /// <see cref="HeatRiseThrustThreshold"/> 이하이면 냉각 복구 여부와 무관하게 돈다.
        ///
        /// <b>RG-3(잠금은 항상 자연 해제된다)이 이 상수에 걸려 있다.</b> <c>S-H3</c> 엔진 보호
        /// 잠금은 추력 상한을 <c>0.25</c> 로 눌러 상승 분기를 스스로 끄므로, 하강 경로가
        /// 냉각 복구뿐이면 냉각통을 못 가져온 팀은 열 <c>1.00</c> 에 영구히 갇힌다.
        /// <c>1.00 → 0.80</c> 해제선까지 <c>0.20 / 0.008 = 25초</c> 이며, 이 값이 기획이 말한
        /// "아무것도 하지 않아도 25초 후 풀린다" 를 성립시킨다.
        /// </summary>
        public const float HeatNaturalCoolPerSecond = 0.008f;

        /// <summary>
        /// 냉각실 수동 순환 밸브를 붙잡고 있는 동안 더해지는 <b>하강</b> 항
        /// (<c>interaction-verb-diversification-v1.md</c> §4.3 <c>C-3</c>).
        ///
        /// <b>냉각 복구(<see cref="HeatRecoveryPerSecond"/> <c>0.030</c>)의 절반인 것이 설계의
        /// 핵심이다.</b> 유지 동사가 냉각통 연결을 <i>대체</i>할 수 있게 되는 순간 "밸브만 잡고
        /// 버티기" 가 최적해가 되어, 동사를 늘리려던 카드가 오히려 동사를 다시 하나로 수렴시킨다.
        /// 이 값은 <b>복구까지 시간을 버는</b> 것이지 복구가 아니다.
        ///
        /// 상승 분기에서의 산수가 §4.3 이 든 예다 — <c>HighHeatHighThrust</c> 프리셋의 상승률
        /// <c>0.020/s</c> 에 이 항이 걸리면 순 상승이 <c>0.005/s</c> 가 되고, 냉각통 왕복
        /// <c>14</c>초 동안 열이 <c>0.86 → 0.93</c> 에 머물러 엔진 보호 잠금(<c>1.00</c>)을 피한다.
        /// 아무도 안 잡으면 <c>1.14</c> 로 잠기고 자연 해제까지 <c>25</c>초다.
        ///
        /// <b>동시에 둘이 잡아도 이 값 하나다.</b> 홀더 수로 곱하면 <c>2</c>인이 밸브 앞에 모이는
        /// 것이 답이 되어, 이 동사가 만들려던 "한 사람이 동시에 두 자리에 있을 수 없다"
        /// (<c>CT-01</c> §4.1)가 정반대로 뒤집힌다.
        ///
        /// <b><c>0.015</c> 는 <c>game-balance</c> 확정값이다(§7-3, 카드 <c>2245af31</c>).</b>
        /// §7-(나)가 남긴 "<c>14</c>초 왕복이 안 덮인다" 는 <b>이 값을 올려서 풀면 안 된다</b> —
        /// 결과가 <c>HeatRisePerSecond</c> 와의 <i>차</i>에 매달려 있어서 조금만 올려도
        /// 밸브가 냉각통의 대체재가 된다.
        ///
        /// <code>
        ///   이 값   §4.3 장면 잠금까지   폭주선 아래 순 상승   열 0.60 → 0.90
        ///   0.0150       13.2s              0.0050/s              60s
        ///   0.0155       14.2s              0.0045/s              67s
        ///   0.0170       19.2s              0.0030/s             100s
        ///   0.0180       26.2s              0.0020/s             150s
        /// </code>
        ///
        /// <c>14</c>초를 사려면 <c>0.0155</c> 가 필요한데 그건 상승률과의 차가 <c>0.0045</c> 라
        /// 두 상수 중 하나만 흔들려도 부호가 뒤집히고, <c>0.017</c> 이면 폭주선 아래에서 열이
        /// 사실상 멎어(<c>0.60 → 0.90</c> 에 <c>100</c>초) §4.3 이 "설계의 핵심" 이라 부른
        /// 성질 자체가 사라진다. 그래서 이 값은 두고 <b>폭주 증분 쪽</b>을 손봤다 —
        /// <see cref="LastShiftDeterioration.Tick"/> 의 열 시계 주석을 보라.
        /// </summary>
        public const float SustainedCoolingPerSecond = 0.015f;

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
        /// <c>S-H2</c> 활성 중 열 상승률. 0.020 → 0.035 는 1.75배이고, 0.90 에서 보호 발동
        /// (1.00)까지 5초가 2.9초로 줄어든다.
        ///
        /// <b><c>0.035</c> 유지가 <c>game-balance</c> 확정이다(§7-1b, 카드 <c>2245af31</c>).</b>
        /// 이 값을 내려 §4.3 의 <c>14</c>초 왕복을 덮으려면 <c>0.0317</c> 이 필요한데(밸브 유지
        /// <c>0.015</c> 기준), 그건 <c>0.035</c> 에서 <c>9%</c> 내린 것뿐이라 왕복 여유를 거의
        /// 못 사면서 <c>S-H2</c> 의 위협만 흐려진다 — 밸브를 안 잡았을 때 <c>0.90 → 1.00</c> 이
        /// <c>2.86</c>초에서 <c>3.16</c>초가 되는 정도다. <c>14</c>초를 실제로 사려면 상승률을
        /// 기본값 <c>0.020</c> 가까이 끌어내려야 하고, 그러면 폭주 분기가 존재할 이유가 없어진다.
        ///
        /// 대신 <b>붙잡고 있는 동안 이 증분이 안 실리게</b> 했다(<see cref="LastShiftDeterioration.Tick"/>).
        /// 상수는 그대로 두고 분기 하나가 늘 뿐이며, <c>S-H2</c> 상황 표시
        /// (<c>LastShiftSituationTable</c>)는 <c>CoolingConnected</c> 만 보므로 영향받지 않는다 —
        /// 밸브를 잡은 사람에게 <c>S-H2</c> 는 <b>여전히 켜져 있고</b>, 손을 떼면 즉시 이 값으로 돌아간다.
        /// </summary>
        public const float HeatRiseRunawayPerSecond = 0.035f;

        /// <summary>엔진 보호가 발동했을 때 강제되는 추력 상한. 성공선 0.30 아래라서 추력 성공선 자체를 잃는다.</summary>
        public const float ProtectedThrustCeiling = 0.25f;

        /// <summary>냉각 구역을 포기했을 때의 엔진 디레이트. 성공선 0.30 위라서 "절충 생환" 경로가 남는다.</summary>
        public const float SacrificedThrustCeiling = 0.35f;

        // ── R2 산소 시계 ──────────────────────────────────────────────────
        /// <summary>
        /// 파공 구역의 기준 누출률(<c>interaction-verb-diversification-v1.md</c> §4.1 <c>C-1</c>).
        ///
        /// <b>이 값이 문 격리(<c>Q</c>)·해치·덕트 세 동사의 존재 이유다.</b> 예전 값 <c>0.006</c>
        /// 에서는 선체를 완전히 부수고 문을 전부 열어 둔 채 방치해도 <c>300</c>초 타이머 안에
        /// 조종석이 성공선 <c>0.20</c> 에 닿지 않았다(§2 실측: 완파 · <c>300</c>초 후 <c>0.287</c>).
        /// 산소 실패 시계가 P0 타이머 안에서 한 번도 발동하지 않으므로 격리할 이유가 없었고,
        /// 격리할 이유가 없으니 그 격리가 열어 주는 덕트 우회로도 같이 죽어 있었다.
        ///
        /// <b><c>0.024</c> 는 <c>game-balance</c> 확정값이다(§7-1, 카드 <c>2245af31</c>).</b>
        /// 기획이 고정한 판정 조건 둘을 <b>세 프리셋 전부에서</b> 동시에 세운 최소값 부근이다.
        /// <list type="number">
        ///   <item>문을 전부 열어 둔 채 방치하면 P0 타이머(<c>300</c>초) 안에 조종석이 성공선을 잃는다</item>
        ///   <item>격리하면 파공 구역이 예비 산소 지속시간(<c>80</c>초) 안에 진공이 된다</item>
        /// </list>
        ///
        /// <b>재산정의 입력은 선체 <c>0.60</c> 이 아니라 프리셋이다.</b> §4.1·§7-(가)가 쓴
        /// "선체 <c>0.60</c> · 모든 구역 <c>1.00</c>" 은 <see cref="LastShiftPresetFactory"/> 의
        /// 어느 프리셋에서도 나오지 않는 상태다 — 실제 시작 압력은 <c>0.64</c>/<c>0.58</c>/<c>0.96</c>
        /// 이고, 운석(<see cref="LastShiftMeteorStimulus.Canonical"/> · severity <c>0.9924</c>) 뒤
        /// 선체는 <c>0.786</c>/<c>0.626</c>/<c>0.396</c> 이다. 그 값으로 적분한 결과가 이렇다.
        ///
        /// <code>
        ///                                   방치·성공선 상실     격리·진공 도달
        ///   leak   프리셋                    (조건 1, &lt; 300s)    (조건 2, &lt; 80s)
        ///   0.018  HighHeatHighThrust         292.8s  OK           81.8s  NG   &lt;- 여기서 깨진다
        ///          PowerOverloadLooseBattery  197.2s  OK           41.8s  OK
        ///          BadAttitudeHighOxygen      260.8s  OK           43.0s  OK
        ///   0.024  HighHeatHighThrust         245.0s  OK           61.2s  OK
        ///          PowerOverloadLooseBattery  182.5s  OK           31.5s  OK
        ///          BadAttitudeHighOxygen      245.0s  OK           32.2s  OK
        /// </code>
        ///
        /// <b>구속하는 것은 조건 (1)이 아니라 (2)이고, 구속하는 프리셋은 <c>HighHeatHighThrust</c>
        /// 하나다.</b> 그 프리셋만 선체가 <c>0.786</c> 로 높아 실효 누출이 작고, 그래서 격리해도
        /// 파공 구역이 <c>80</c>초 예산 안에 진공에 못 닿는다 — 격리에 대가가 없으면 §6-4 의
        /// "문 닫고 버티기" 가 비용 <c>0</c> 의 지배 전략이 된다. 두 조건 동시 성립의 하한은
        /// <c>0.0184</c> 이며, <c>0.018</c> 은 <c>1.8</c>초 차이로 못 넘는다.
        ///
        /// <b><c>0.021</c> 이 아니라 <c>0.024</c> 인 이유는 프리셋 재조정 내성이다.</b> 구속
        /// 입력인 <c>HighHeatHighThrust</c> 의 운석 후 선체(<c>0.786</c>)가 얼마나 올라가도 두
        /// 조건이 버티는지로 재면 <c>0.021</c> 은 <c>0.812</c>(여유 <c>2.6</c>pp), <c>0.024</c> 는
        /// <c>0.836</c>(여유 <c>5.0</c>pp)이다. 시작 산소 축으로도 <c>0.720</c> 대 <c>0.781</c> 로
        /// 갈린다. 그 프리셋은 이미 두 상수가 재조정된 이력이 있어(<c>BusPower</c>·
        /// <c>HullIntegrity</c> 주석 참조) <c>2.6</c>pp 는 한 번의 조정보다 얇다.
        ///
        /// <b><c>RG-1</c> 영향은 없다.</b> <c>rg1-recalc-cargo-procurement-v1.md</c> §5.1 이
        /// 직접 답했고, 그 논증은 <b>누출률 값과 무관</b>하므로 <c>0.024</c> 에도 그대로 선다 —
        /// <c>(1)</c>·<c>(3)</c> 은 좌표만 보고, <c>(2)</c> 의 항목표에는 누출률 항이
        /// 없으며(봉합 후 재가압은 <see cref="OxygenPumpRecoveryPerSecond"/> 로만 간다),
        /// <c>SuitOxygen</c> <c>80</c>초 시계도 "그 구역이 <c>0.00</c> 에 닿은 시점부터" 라
        /// 진공 도달이 빨라져도 길이가 안 바뀐다. 누출률은 진공에 닿기까지의 시간을 바꾸지
        /// <c>RG-1</c> 이 재는 구간을 안 바꾼다.
        ///
        /// <b>승리 가능성도 안 바꾼다.</b> 봉합만 끝나면 펌프
        /// (<see cref="OxygenPumpRecoveryPerSecond"/>)가 조종석 연결 구역을 되채우므로,
        /// <c>180</c>초에 봉합해도 타이머 끝 조종석은 세 프리셋 전부 <c>0.43</c> 위다.
        /// 이 상수가 겨누는 것은 <b>방치의 대가</b>뿐이다.
        ///
        /// 판정 조건은 <c>LastShiftVerbDemandTests</c> 가 프리셋으로 직접 붙든다.
        /// </summary>
        public const float OxygenLeakPerSecond = 0.024f;
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

        /// <summary>
        /// 추력 명령의 구조적 상한. <c>ThrustDemand</c> 가 <c>Clamp01</c> 이라는 사실을 상수로
        /// 올린 것이며, <see cref="LastShiftVerdictResolver.IsDockUnreachable"/> 이 "필요 추력이
        /// 낼 수 있는 값을 넘었다" 를 판정하는 기준이다.
        ///
        /// <b>현재 추력 천장(<see cref="ProtectedThrustCeiling"/> 등)이 아니다.</b> 그쪽은
        /// 일시적이고 <c>RG-3</c> 으로 반드시 풀리므로, 잠긴 동안의 천장으로 도달 가능성을
        /// 판정하면 아직 이길 수 있는 판을 실패로 끝낸다.
        /// </summary>
        public const float MaxThrustDemand = 1.00f;

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

        /// <summary>
        /// 임시 결속을 완성한 횟수. 결과 화면 요약 <c>4</c>칸의 <c>임시 수리</c> 칸이 유일한
        /// 소비자다(<c>CT-01</c> §5.5).
        ///
        /// <b><see cref="BypassLapseCount"/> 와 짝이어야 의미가 선다</b> — "임시 수리 3회 ·
        /// 재이탈 2회" 가 한 화면에 같이 있어야 플레이어가 다음 판에서 <c>4</c>초를 내고
        /// 안전 복구로 끝낼 근거가 된다. 재이탈만 세면 분모가 없다.
        /// </summary>
        public int QuickBypassCount { get; private set; }

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
            QuickBypassCount = 0;
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
            if (mode == LastShiftRepairMode.QuickBypass) QuickBypassCount++;
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

        /// <summary>
        /// 지금 누군가 냉각실 수동 순환 밸브를 붙잡고 있는가(§4.3 <c>C-3</c>).
        ///
        /// <b>봉쇄가 아니라 <see cref="LastShiftRecoveryTuning.SustainedCoolingPerSecond"/> 항의
        /// on/off 다.</b> <c>CoolingContained</c> 에 넣지 않는 것이 요점이다 — 넣으면 밸브를 잡은
        /// 동안 열 상승이 통째로 멎어 냉각통 연결과 구분이 사라지고, "유지는 복구가 아니다" 라는
        /// §4.3 의 설계 전제가 코드에서 무너진다.
        ///
        /// 이 플래그가 <c>LastShiftContainment</c> 에 붙는 것은 세 시계가 이미 이 구조체 하나로
        /// 조건을 받고 있기 때문이다. tick 인자를 늘리면 <see cref="LastShiftDeterioration.Tick"/>
        /// 의 두 오버로드와 호출부 전부가 따라오고, 그러면 기존 호출 경로가 기본값을 각자 적는다.
        /// </summary>
        public bool CoolingValveHeld;

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

            // ── 열 시계 ── 고추력 + 냉각 미복구에서만 오른다.
            //
            // CT-06 N5: 열이 S-H2 발동선을 넘으면 상승률이 갈아탄다(기획 §3.3). 기존 tick 을
            // 대체하는 것이 아니라 분기가 하나 늘 뿐이고, 발동선 아래는 그대로 0.020/s 다.
            //
            // CT-07: 추력이 상승선 아래면 냉각 상태와 무관하게 자연 냉각이 돈다(기획 §2.3 A-1
            // 자연 회복, §0.2 HEAT_COOL). <b>이 항이 RG-3 의 근거다.</b> 예전에는 하강 경로가
            // 냉각 복구 하나뿐이라, S-H3 (열 1.00, 추력 상한 0.25) 에 걸리면 상승 조건
            // (추력 > 0.60)도 하강 조건(냉각 복구)도 성립하지 않아 열이 1.00 에 얼어붙었다.
            // 냉각통을 못 가져오면 영구 잠금이었고, 그건 RG-3 이 "아무것도 하지 않아도 25초
            // 후 풀린다" 고 보장한 것과 정반대다.
            //
            // 구역을 포기(sacrifice)한 경우에도 자연 냉각은 돈다. 포기가 막는 것은 악화이지
            // 물리적 방열이 아니고, 포기 상태에서만 잠금이 안 풀리면 RG-3 에 구멍이 남는다.
            //
            // CT-08 C-3: 냉각실 수동 순환 밸브를 붙잡고 있으면 하강 항이 하나 더 붙는다(§4.3).
            // <b>분기를 늘리지 않고 두 분기 <i>바깥</i>에서 뺀다</b> — 상승 중이면 순 상승을 깎고
            // (0.020 → 0.005), 하강 중이면 하강을 더한다. 상승 분기 안에만 넣으면 열이 이미
            // 내려가는 국면에서 밸브가 아무것도 안 하게 되고, 그러면 "잡고 있는 동안 계속
            // 효과가 있다" 라는 유지 동사의 정의가 조건부가 된다.
            //
            // CT-08 §7-1b (balance, 카드 2245af31): <b>붙잡고 있는 동안에는 S-H2 폭주 증분이
            // 안 실린다.</b> 폭주(0.035/s)가 성립하는 조건은 "냉각이 연결되지 않음" 이고, 밸브를
            // 붙잡는 것은 그 순환을 사람이 손으로 돌리는 행위다. 순환이 도는 동안 순환 정지의
            // 현상인 증분까지 실리면 모델이 스스로와 어긋난다.
            //
            // 이 분기가 없으면 §4.3 이 사려던 14초 왕복이 안 덮인다 — 밸브를 잡아도 0.90 은
            // 지나가므로 그 뒤로 순 0.020/s 가 되어 0.86 에서 13.2초에 잠긴다. 증분을 걷어내면
            // 유지 중 순 상승이 구간 내내 0.005/s 로 고정되어 28.2초다(냉각실↔화물칸 실측 왕복
            // 16.3초의 1.7배). 하강 항(0.015)을 대신 올려 같은 시간을 사는 길은 §7-3 이 기각했다.
            //
            // <b>유지는 여전히 복구가 아니다.</b> 증분을 걷어내도 상승 자체는 남아 0.86 에서
            // 28.2초면 잠기고, 냉각 복구(-0.030/s)만이 열을 실제로 내린다. S-H2 상황 표시는
            // CoolingConnected 만 보므로 잡고 있는 동안에도 켜져 있다 — 손을 떼면 즉시 0.035 다.
            var heatDelta = !containment.CoolingContained &&
                            state.ThrustDemand > LastShiftRecoveryTuning.HeatRiseThrustThreshold
                ? IsHeatRunaway(state, containment) && !containment.CoolingValveHeld
                    ? LastShiftRecoveryTuning.HeatRiseRunawayPerSecond
                    : LastShiftRecoveryTuning.HeatRisePerSecond
                // 냉각을 복구했으면 능동 냉각률(0.030/s), 아니면 자연 냉각률(0.008/s).
                // 복구했는데 고추력이라 상승 분기에 안 걸린 경우도 여기로 온다 — 복구가
                // 자연 냉각보다 느려지는 일은 없어야 한다.
                : -(containment.CoolingRestored
                    ? LastShiftRecoveryTuning.HeatRecoveryPerSecond
                    : LastShiftRecoveryTuning.HeatNaturalCoolPerSecond);

            if (containment.CoolingValveHeld) heatDelta -= LastShiftRecoveryTuning.SustainedCoolingPerSecond;
            state.EngineHeat = Mathf.Clamp01(state.EngineHeat + heatDelta * deltaTime);

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
            // 조종석에서 그 구역까지 가는 길의 문을 전부 본다. 하나라도 닫혀 있으면 끊긴다.
            // 구역이 늘어도 경계 수만 따라오면 되도록 사슬을 순회로 적는다 - 예전 switch 는
            // "구역이 셋" 이라는 사실을 두 분기에 나눠 적고 있어서 넷이 되면 조용히 틀렸다.
            for (var boundary = 0; boundary < (int)zone; boundary++)
                if (!doors[boundary]) return false;
            return true;
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

        /// <summary>
        /// 남은 시간 안에 도킹 진척을 채우려면 지금부터 유지해야 하는 추력
        /// (<c>docs/game-feel-loop-review-v1.md</c> §3.2-b). HUD 추력 막대 위의 움직이는 선이
        /// 이 값이고, 런 시작 시 <c>(150-0)/300 = 0.50</c> 이다.
        ///
        /// <b>이 값이 HUD 가 가르치는 <see cref="LastShiftRecoveryTuning.DockingSuccessThrust"/>
        /// (0.30)과 다르다는 것이 요점이다.</b> 0.30 은 도킹 <b>순간</b>의 조건이고 이 값은
        /// 도킹을 <b>채우는</b> 조건이라, 0.30 만 보고 맞춘 배는 5분 뒤 138/150 으로 표류한다.
        ///
        /// 진척이 이미 목표에 닿았으면 더 낼 것이 없으므로 <c>0</c> 이다.
        /// </summary>
        public static float RequiredThrust(in LastShiftShipState state, float secondsRemaining)
        {
            var shortfall = LastShiftRecoveryTuning.DockTargetThrustSeconds - state.DockProgress;
            if (shortfall <= 0f) return 0f;
            if (secondsRemaining <= 0f) return float.PositiveInfinity;
            return shortfall / secondsRemaining;
        }

        /// <summary>
        /// 남은 시간에 도킹이 물리적으로 불가능한가(§3.2-c). 필요 추력이 추력 상한을 넘으면
        /// 최대로 밀어도 진척이 안 차므로 <b>타이머를 기다리지 않고 즉시 판정한다</b> —
        /// 연료 소진 표류(<see cref="IsStrandedWithoutFuel"/>)와 같은 논리이고, 기획 §4.3
        /// <c>RG-3</c> 이 금지한 "아무것도 할 수 없는 채로 시계만 보는" 구간을 없앤다.
        ///
        /// 판정은 <see cref="LastShiftVerdict.FailureAdrift"/> 로 같고 트리거 문자열만 다르다.
        /// </summary>
        public static bool IsDockUnreachable(in LastShiftShipState state, float secondsRemaining)
        {
            // 남은 시간 0 은 여기서 다루지 않는다 — 그 시점은 타이머 만료 판정의 자리이고,
            // 여기서 가로채면 추력 부족(FailureInsufficientThrust)이 영영 안 나온다.
            return secondsRemaining > 0f &&
                   RequiredThrust(state, secondsRemaining) > LastShiftRecoveryTuning.MaxThrustDemand;
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
