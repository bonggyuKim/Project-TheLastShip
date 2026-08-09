using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 확장 모듈 효과 계수 정본 — <c>docs/module-effect-coefficients-v1.md</c>.
    /// <c>docs/port-module-catalog-v1.md</c> §6 의 <c>P-2</c> 이고 같은 문서 §9-4 를 닫는다.
    ///
    /// <b>계수를 여기에만 둔다.</b> <see cref="LastShiftRecoveryTuning"/> 과 같은 규약이다 —
    /// 값 조정이 재작업 없이 끝나야 하므로 <see cref="LastShiftDeterioration.Tick"/> 안에
    /// 리터럴이 하나도 안 들어간다.
    ///
    /// <b>세 계수를 관통하는 규칙은 하나다(조항 C-2).</b> 모듈은 <b>이번 구간에 잃은 기능을
    /// 다른 자리에 다시 세우는 것</b>이지 복구 동사의 대체재가 아니다. 그래서 셋 다
    /// "복구를 안 해도 되게 만드는 값" 바로 아래에서 잘렸고, 어느 값이 무엇에 걸려 잘렸는지가
    /// 각 상수 주석에 있다. <b>세 상수 전부 위쪽이 막힌 값이지 크기를 눈대중한 값이 아니다.</b>
    /// </summary>
    public static class LastShiftModuleEffects
    {
        // ── 수경재배 (카탈로그 7, 여력 3) ────────────────────────────────────

        /// <summary>
        /// 편입 구역의 <b>누출률 감속 비율</b>. 그 구역의 누출이
        /// <c>× (1 - 0.15)</c> 로 줄어든다(<see cref="LastShiftRecoveryTuning.OxygenLeakPerSecond"/>).
        ///
        /// <b>위쪽이 막혀 있다.</b> 누출률 <c>0.024</c> 를 고른 근거는 세 프리셋 전부에서 동시에
        /// 서야 하는 조건 둘이고(같은 상수 주석), 모듈이 그 둘을 깨면 안 된다. 구속하는 것은
        /// <b>조건 (2)</b> 이고, 구속하는 프리셋은 <c>0.024</c> 를 구속한 것과 같은
        /// <c>HighHeatHighThrust</c> 하나다 — 그 프리셋만 운석 후 선체가 <c>0.786</c> 로 높아
        /// 실효 누출이 작다.
        ///
        /// 실측(<c>EditMode</c>, 걸음 <c>0.25</c>s, <c>HighHeatHighThrust</c>):
        /// <code>
        ///   감속   방치·성공선 상실   격리·진공 도달
        ///          (&lt; 300s)          (&lt; 80s)
        ///   0.00     245.8s  OK         61.3s  OK
        ///   0.10     260.5s  OK         68.3s  OK
        ///   0.15     269.8s  OK         72.3s  OK   &lt;- 확정 (여유 30.2s / 7.7s)
        ///   0.20     280.5s  OK         76.8s  OK   (여유 19.5s / 3.2s)
        ///   0.25     293.0s  OK         81.8s  NG   &lt;- 여기서 깨진다
        /// </code>
        ///
        /// <c>0.25</c> 면 격리해도 파공 구역이 예비 산소 예산(<c>80</c>초) 안에 진공에 못 닿는다.
        /// <b>격리에 대가가 없으면 §6-4 의 "문 닫고 버티기" 가 여력 <c>2</c> 로 다시 비용 <c>0</c>
        /// 의 지배 전략이 된다</b> — 조항 C-2 위반이다. 두 조건 동시 성립의 상한은 보간으로
        /// <c>0.233</c> 근처다.
        ///
        /// <b><c>0.20</c> 이 아니라 <c>0.15</c> 인 이유는 <c>0.024</c> 를 <c>0.021</c> 대신 고른
        /// 이유와 같다 — 재조정 내성.</b> <c>0.20</c> 은 구속 조건 쪽 여유가 <c>3.2</c>초(<c>4%</c>)
        /// 뿐이라 프리셋 선체가 조금만 올라가도 넘어간다. <c>0.15</c> 는 양쪽 다 <c>10%</c> 근처다.
        ///
        /// <b>봉합을 대체하지 않는다.</b> 감속은 누출을 늦출 뿐이고 <c>0</c> 으로 만들지 않는다 —
        /// 재가압(<see cref="LastShiftRecoveryTuning.OxygenPumpRecoveryPerSecond"/>)은 여전히
        /// 봉합 + 전력 연결에서만 돈다.
        /// </summary>
        public const float OxygenLeakReduction = 0.15f;

        // ── 방열 라디에이터실 (카탈로그 3, 여력 2) ──────────────────────────

        /// <summary>
        /// <c>EngineHeat</c> <b>상승항</b>의 감속 비율. 하강항(냉각 복구·자연 냉각·밸브 유지)에는
        /// 안 걸린다 — 걸면 모듈이 냉각 성능을 올리는 물건이 되고, 그건 냉각통을 연결한 배가
        /// 더 빨리 식는다는 뜻이라 "잃은 기능을 다시 세운다" 가 아니라 증폭기다.
        ///
        /// <b>위쪽을 막는 것은 냉각 순환 밸브다.</b> 밸브 유지
        /// (<see cref="LastShiftRecoveryTuning.SustainedCoolingPerSecond"/> <c>0.015</c>)가 이미
        /// 상승률 <c>0.020</c> 의 <c>75%</c> 를 먹고 있어서, 모듈이 나머지를 조금만 더 먹으면
        /// <b>밸브 + 모듈이 열을 내리는 조합</b>이 된다. 그 순간 냉각통이 필요 없어지고
        /// <c>CT-08</c> §4.3 이 "설계의 핵심" 이라 부른 <b>유지는 복구가 아니다</b> 가 죽는다.
        ///
        /// 실측(<c>EditMode</c>, 열 <c>0.86</c> · 추력 <c>0.92</c> · 밸브 유지):
        /// <code>
        ///   감속   상승률   밸브+모듈 순 상승   0.86 -> 1.00 잠금   판정
        ///   0.000  0.0200        0.0050/s            28.00s
        ///   0.100  0.0180        0.0030/s            46.75s         &lt;- 확정
        ///   0.125  0.0175        0.0025/s            56.00s         밸브 단독의 절반
        ///   0.200  0.0160        0.0010/s           140.25s         NG (열이 사실상 멎는다)
        ///   0.250  0.0150        0.0000/s            영구            NG (냉각통 불필요)
        /// </code>
        ///
        /// <b>규칙은 "밸브 + 모듈의 순 상승이 밸브 단독의 절반 아래로 안 내려간다" 다</b> —
        /// <c>0.020 × (1-r) - 0.015 ≥ 0.0025</c> 에서 <c>r ≤ 0.125</c>. <c>0.10</c> 은 그 안쪽의
        /// 격자값이다.
        ///
        /// <b>그러면서 <c>CT-08</c> §7-(나)의 미결이 닫힌다.</b> 그 미결은 "밸브를 잡아도 냉각통
        /// 왕복 <c>16.3</c>초(냉각실↔화물칸 실측)를 못 덮는다" 였고, §7-3 은 <b>밸브 값을 올려
        /// 푸는 길을 기각</b>했다(상승률과의 차가 얇아 부호가 뒤집힌다). 모듈은 상승항 쪽을
        /// 건드리므로 그 위험이 없다 — 잠금까지가 <c>28.2 → 46.7</c>초로 왕복의 <c>2.9</c>배가
        /// 된다. <b>대신 값을 낸 배만 그렇다.</b>
        ///
        /// <b>모듈 단독으로는 거의 아무것도 아니다.</b> 밸브를 안 잡으면 <c>0.86</c> 에서 잠금까지
        /// <c>5.00 → 5.50</c>초, <c>+0.5</c>초뿐이다(실측). 왕복 <c>16.34</c>초 근처에도 못 간다 —
        /// <b>이 모듈은 사람이 밸브를 잡고 있을 때만 값을 한다.</b>
        /// </summary>
        public const float HeatRiseReduction = 0.10f;

        // ── 예비 전력실 (카탈로그 2, 여력 2) ────────────────────────────────

        /// <summary>
        /// 미연결 <c>BusPower</c> 하한(<see cref="LastShiftRecoveryTuning.UnpoweredBusCeiling"/>
        /// <c>0.40</c>)에 더해지는 몫. 전력이 안 붙은 배의 bus 가 <c>0.40</c> 이 아니라
        /// <c>0.45</c> 에서 멎는다.
        ///
        /// <b>고치는 것은 하나다 — <c>0.40</c> 이 <c>S-P2</c> 발동선과 같은 값이라는 것.</b>
        /// <c>LastShiftSituationTuning.PowerCascadeTrigger</c> 가 <c>0.40</c> 이고 하강이 정확히
        /// 거기서 멎으므로, 전력을 잃은 배는 <b>남은 항해 내내 <c>S-P2</c>(Fault)에 눌러앉는다.</b>
        /// <c>+0.05</c> 는 그 눌러앉기 하나를 없앤다.
        ///
        /// 운석 후 bus 에서 하한까지 남는 전력 시계(<c>BusDropPerSecond</c> <c>0.050</c> 기준):
        /// <code>
        ///   하한   S-P2(0.40)   S-P1(0.65)   프리셋1   프리셋2   프리셋2*  프리셋3
        ///                                    0.6051    0.6151    0.5266    0.5651
        ///   0.40   상시 발동     발동         4.10s     4.30s     2.53s     3.30s
        ///   0.45   안 걸림       발동         3.10s     3.30s     1.53s     2.30s   &lt;- 확정
        ///   0.48   안 걸림       발동         2.50s     2.70s     0.93s     1.70s
        ///   0.50   안 걸림       발동         2.10s     2.30s     0.53s     1.30s   NG
        /// </code>
        /// <c>프리셋2*</c> 는 배터리가 <c>0.55m</c> 굴러간 경우다 — 그 프리셋의 이름이 그것이고
        /// (<c>PowerOverloadLooseBattery</c>), 운석이 <c>batteryTravel × 0.16</c> 을 더 깎는다.
        /// <b>가장 짧은 시계가 여기서 나오므로 하한을 구속하는 것도 이 열이다.</b>
        ///
        /// <b>위쪽을 막는 것은 그 프리셋의 전력 시계다.</b> 시작값 <c>0.63</c> 은 "운석 후
        /// <c>0.40</c> 아래로 내려가면 전력 시계가 한 번도 안 돈다" 를 피해 고른 값이고
        /// (<see cref="LastShiftPresetFactory"/> 주석), 그 조정이 산 여유가 <c>79cm</c> 였다.
        /// 하한을 <c>0.50</c> 으로 올리면 남는 시계가 <c>0.53</c>초(<c>2.66cm</c>)라 <b>그 조정이
        /// 사 둔 것을 모듈이 도로 먹는다.</b> <c>0.45</c> 는 <c>S-P2</c> 발동선 위로 <c>5pp</c>
        /// 떨어져 경계에 안 붙으면서 시계를 <c>1.53</c>초 남긴다.
        ///
        /// <b>배터리 꽂기를 대체하지 않는다.</b> <c>0.45</c> 는 <c>S-P1</c> 발동선
        /// (<c>BusDetachedTrigger</c> <c>0.65</c>) 한참 아래라 "배터리 미연결" 은 그대로 켜져
        /// 있고, 조향 지연(<c>!PowerRestored</c>)과 산소 펌프 조건(<c>PowerRestored</c>)도 하나도
        /// 안 바뀐다 — 이 둘은 bus 값이 아니라 <b>복구 여부</b>를 본다.
        ///
        /// <b>전력을 만들지도 않는다.</b> 하강이 멎는 선을 올릴 뿐이라, bus 가 이미 하한 아래인
        /// 배에 모듈을 세워도 값이 안 오른다.
        /// </summary>
        public const float BusFloorBonus = 0.05f;

        // ── 예비 아이템 (카탈로그 2 · 8) ────────────────────────────────────

        /// <summary>
        /// 예비 아이템은 <b>역할당 최대 하나</b>다(조항 E-2). 예비 전력실과 화물칸을 둘 다
        /// 세워도 예비 배터리는 하나이고, 같은 모듈을 둘 세워도 하나다.
        ///
        /// <b>둘째부터는 재미가 아니라 짐이다.</b> 이 계열이 겨누는 것은
        /// <c>rg1-recalc-cargo-procurement-v1.md</c> 의 최악 복구 <b>경로 길이</b>이고, 그건
        /// 가장 가까운 하나까지의 거리로 정해진다 — 같은 방에 배터리가 둘 있어도 왕복은 안 줄고
        /// 판정기(<c>RG-1(2)</c>·<c>W-1</c>(c))만 배치마다 두 배로 돈다(카탈로그 §8-4).
        /// </summary>
        public const int SpareCountPerRole = 1;

        /// <summary>이 종류가 비치하는 예비 아이템 역할의 비트마스크.</summary>
        private static int SpareRoleMaskOf(int catalogIndex) => catalogIndex switch
        {
            // 예비 배터리 하나 상시 비치(카탈로그 §3.3). 전력실이 죽은 배의 배터리 왕복이 짧아진다.
            LastShiftModuleCatalog.ReservePower => 1 << (int)LastShiftItemRole.Battery,

            // 세 계통 한 벌. "화물칸" 이 한 계통만 든다면 그건 창고가 아니라 선반이고,
            // 무엇이 들었는지를 플레이어가 화면 밖에서 외워야 한다.
            //
            // 한 벌이 과하지 않은 이유는 셋이다. (가) 여력 3 은 보통 항해 총 수입 7 의 43% 다.
            // (나) 한 방에 모여 있으므로 그 구역이 진공이 되면 세 예비가 같이 죽는다 — 원본과
            // 같은 위험을 한 번 더 지는 것이지 위험을 지우는 것이 아니다. (다) 그래서 이 모듈의
            // 값은 "무엇이 들었나" 가 아니라 <b>어디 뒀나</b> 로 정해진다(카탈로그 §3.3 · §7-D).
            LastShiftModuleCatalog.CargoBay =>
                (1 << (int)LastShiftItemRole.Battery) |
                (1 << (int)LastShiftItemRole.CoolingCanister) |
                (1 << (int)LastShiftItemRole.PatchPlate),

            _ => 0
        };

        // ── 집계 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 지금 배에 선 모듈에서 효과를 걷는다. <b>매 tick 부른다</b> —
        /// <see cref="LastShiftPlacedModules.TryResolve"/> 가 이미 같은 배열을 매 tick 훑고
        /// 있으므로(칸 수는 타당성 검토 상한 <c>20</c>) 비용이 같은 차수다. 캐시를 안 두는 것은
        /// 무효화 조건이 표 개정·여력 환수·구역 압력 셋이라, 그 셋을 놓치는 쪽이 훑는 값보다
        /// 비싸기 때문이다.
        ///
        /// <b><paramref name="pressures"/> 를 받는 이유는 조항 E-1 이다 — 진공이 된 구역의 모듈은
        /// 효과를 잃는다.</b> 모듈도 방이고, 공기가 없는 방의 재생기·라디에이터·배전반이 계속
        /// 돌면 "어느 구역에 붙일까"(카탈로그 §7-C)가 결정이 아니게 된다. 산소실 옆에 붙인 배와
        /// 조종석 옆에 붙인 배가 갈리는 자리가 정확히 여기다.
        /// </summary>
        public static LastShiftModuleEffectState Collect(in LastShiftZonePressures pressures)
        {
            var oxygenZones = 0;
            var heatSlowed = false;
            var busBoosted = false;
            var spareRoles = 0;

            for (var handle = 0; handle < LastShiftPlacedModules.Count; handle++)
            {
                if (!LastShiftPlacedModules.TryGet(handle, out var module)) continue;

                // 조항 E-1. 진공선은 VacuumOxygenPressure(0.00) 이고, 그 위면 아직 방이다.
                if (pressures[module.Zone] <= LastShiftRecoveryTuning.VacuumOxygenPressure) continue;

                switch (module.CatalogIndex)
                {
                    case LastShiftModuleCatalog.Hydroponics:
                        // 구역당 한 번(조항 E-3). 비트마스크라 같은 구역에 둘을 세워도 한 번이다.
                        oxygenZones |= 1 << (int)module.Zone;
                        break;
                    case LastShiftModuleCatalog.Radiator:
                        heatSlowed = true;
                        break;
                    case LastShiftModuleCatalog.ReservePower:
                        busBoosted = true;
                        break;
                }

                spareRoles |= SpareRoleMaskOf(module.CatalogIndex);
            }

            return new LastShiftModuleEffectState(oxygenZones, heatSlowed, busBoosted, spareRoles);
        }
    }

    /// <summary>
    /// 한 tick 이 쓸 효과 묶음. <b>구조체이고 값이다</b> — 매 tick 걷으므로 할당이 나면 안 되고,
    /// 걷은 뒤에 배가 바뀌어도 이 tick 의 계산은 걷은 시점 그대로여야 한다.
    ///
    /// <b>조항 E-3 — 같은 효과는 한 번만 쌓인다.</b> 구역 효과(산소)는 <b>구역당</b> 한 번,
    /// 배 전체 효과(열·전력)는 <b>배당</b> 한 번이다. 곱으로 쌓게 두면 수경재배 둘이
    /// <c>0.85² = 0.72</c> 가 되어 <see cref="LastShiftModuleEffects.OxygenLeakReduction"/> 이
    /// 지키려던 <c>300</c>초 선을 여력 <c>4</c> 로 넘어간다(<c>245 / 0.7225 = 339</c>초).
    /// <b>계수를 아무리 잘 골라도 누적을 안 막으면 값을 두 번 사서 뚫는다.</b>
    /// </summary>
    public readonly struct LastShiftModuleEffectState
    {
        public LastShiftModuleEffectState(int oxygenZoneMask, bool heatSlowed, bool busBoosted, int spareRoleMask)
        {
            this.oxygenZoneMask = oxygenZoneMask;
            this.spareRoleMask = spareRoleMask;
            HeatRiseMultiplier = heatSlowed ? 1f - LastShiftModuleEffects.HeatRiseReduction : 1f;
            BusFloor = LastShiftRecoveryTuning.UnpoweredBusCeiling +
                       (busBoosted ? LastShiftModuleEffects.BusFloorBonus : 0f);
        }

        private readonly int oxygenZoneMask;
        private readonly int spareRoleMask;

        /// <summary>모듈이 하나도 없는 배. <c>Tick</c> 의 기본값이자 지금까지의 동작이다.</summary>
        public static LastShiftModuleEffectState None => new(0, false, false, 0);

        /// <summary><c>EngineHeat</c> 상승항에 곱하는 값. 모듈이 없으면 <c>1</c> 이다.</summary>
        public float HeatRiseMultiplier { get; }

        /// <summary>미연결 bus 하강이 멎는 선. 모듈이 없으면 <c>0.40</c> 그대로다.</summary>
        public float BusFloor { get; }

        /// <summary>이 구역의 누출률에 곱하는 값. 재생기가 안 붙은 구역은 <c>1</c> 이다.</summary>
        public float OxygenLeakMultiplierFor(LastShiftZone zone) =>
            (oxygenZoneMask & (1 << (int)zone)) != 0
                ? 1f - LastShiftModuleEffects.OxygenLeakReduction
                : 1f;

        /// <summary>이 역할의 예비가 배에 비치돼 있는가.</summary>
        public bool HasSpare(LastShiftItemRole role) => (spareRoleMask & (1 << (int)role)) != 0;

        /// <summary>
        /// 비치된 예비 아이템 총 개수. 스폰하는 쪽이 읽는 값이고, 역할당
        /// <see cref="LastShiftModuleEffects.SpareCountPerRole"/> 를 넘지 않는다.
        /// </summary>
        public int SpareItemCount
        {
            get
            {
                var count = 0;
                for (var role = 0; role < 32; role++)
                    if ((spareRoleMask & (1 << role)) != 0) count += LastShiftModuleEffects.SpareCountPerRole;
                return count;
            }
        }

        public bool Any => oxygenZoneMask != 0 || spareRoleMask != 0 ||
                           !Mathf.Approximately(HeatRiseMultiplier, 1f) ||
                           !Mathf.Approximately(BusFloor, LastShiftRecoveryTuning.UnpoweredBusCeiling);
    }
}
