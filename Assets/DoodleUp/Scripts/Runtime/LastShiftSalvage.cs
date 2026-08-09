using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 잔해 계열 — <c>docs/outboard-outpost-and-map-final-v1.md</c> §4.2 의 셋이다.
    /// <b>자극이 셋이므로 잔해도 셋이다</b>: 직전 구간이 무엇이었는지가 이번 기항의 수확
    /// 종류를 정하고, 그러면 조항 <c>C-2</c>("막는 게 아니라 잃은 것을 대신한다")가 자재
    /// 축에서 한 번 더 성립한다 — 전력 사고 뒤에 전력 계열 잔해가 뜬다.
    /// </summary>
    public enum LastShiftSalvageKind
    {
        /// <summary>냉각 계열. 고열·고추력 구간이 남긴 것이다.</summary>
        Cooling,

        /// <summary>전력 계열. 과부하·배터리 이탈 구간이 남긴 것이다.</summary>
        Power,

        /// <summary>선체 계열. 자세 불량·측면 누출 구간이 남긴 것이다.</summary>
        Hull
    }

    /// <summary>
    /// 선외 잔해 — <b>기항마다 배 근처에 하나 뜨고, 뜯으면 자재가 나온다.</b>
    /// 기획 정본은 <c>docs/outboard-outpost-and-map-final-v1.md</c> §4.2·§5.2·§5.5 다.
    ///
    /// <b>덩이를 물리 물건으로 안 만든다.</b> §4.3 의 거점 확장 표가 정한 축이 "같은 시간에 더
    /// 많이 가져온다" 하나이고, 그중 <c>자재 정리대</c>가 곧 "한 번에 들고 올 수 있는 개수"
    /// 다 — <b>개수가 이미 기획의 단위</b>다. <see cref="LastShiftGrabbable"/> 로 덩이를 띄우면
    /// 그 개수를 물체 수로 다시 표현해야 하고, 저중력에서 뜬 물건이 선외로 흘러가는 회수
    /// 문제(<see cref="LastShiftBypassDuct.DeepestFallY"/> 가 배 안에서 답한 그것)를 진공에서
    /// 다시 풀어야 한다. <b>회수 → 운반 → 반입을 카운터 셋으로 두면 그 둘이 같이 사라진다.</b>
    ///
    /// <b>세 카운터가 서로 다른 것을 뜻한다.</b>
    /// <list type="bullet">
    /// <item><see cref="Remaining"/> — 잔해에 아직 붙어 있는 몫. 이번 기항의 상한이다.</item>
    /// <item><see cref="Carried"/> — 승무원이 들고 있고 <b>아직 안 들어온</b> 몫.
    /// <b>조항 <c>O-7</c> 이 잃게 만드는 것이 정확히 이 값이다.</b></item>
    /// <item><see cref="LastShiftMaterials.Balance"/> — 반입이 끝난 몫. 여기서만 쓸 수 있다.</item>
    /// </list>
    /// 가운데 칸이 없으면 산소가 마른 대가를 물릴 자리가 없다 — 뜯는 즉시 원장에 들어가면
    /// §5.5 가 "수확 상실" 로 정한 대가가 아무 데도 안 남는다.
    ///
    /// <see cref="LastShiftMaterials"/>·<see cref="LastShiftAirlock"/> 과 같은 규약으로 정적이다.
    /// </summary>
    public static class LastShiftSalvage
    {
        /// <summary>
        /// 잔해 하나에 붙어 있는 덩이 수. <b>수치는 <c>game-balance</c> 소관이다</b> — 여기서
        /// 정하는 것은 축 하나뿐이고, 그것은 <see cref="BaseCarryCapacity"/> 의 배수라서
        /// <b>왕복이 한 번으로 안 끝난다</b>는 것이다. 한 번에 다 들고 오면 §4.3 의 거점 확장
        /// 둘(<c>인양 팔</c>·<c>자재 정리대</c>)이 살 이유가 없다.
        /// </summary>
        public const int ChunksPerField = 4;

        /// <summary>
        /// 거점 확장 전, 한 번에 들고 올 수 있는 덩이 수. <c>자재 정리대</c>(§4.3 <c>2</c>단계)가
        /// 올리는 값이고 그 확장은 거점 배치 카드 몫이라 여기서는 기본값만 선다.
        /// </summary>
        public const int BaseCarryCapacity = 2;

        /// <summary>덩이 하나를 뜯는 데 드는 시간. 기존 복구 동사(<c>0.8~4.0초</c>)와 같은 대역이다.</summary>
        public const float HarvestSeconds = 1.5f;

        /// <summary>
        /// 잔해에 손이 닿는 거리. 잡기 사거리와 같은 값이다 — 선외에 새 사거리 규약을
        /// 만들면 "왜 여기선 더 멀리 닿는가" 를 화면이 설명해야 한다.
        /// </summary>
        public const float HarvestReach = 2.2f;

        /// <summary>
        /// 원반 테두리 밖으로 두는 여유. 잔해가 테두리에 닿으면 선외 판정
        /// (<see cref="LastShiftAirlock.IsOutside"/>)이 잔해 앞에서 깜빡인다.
        /// </summary>
        public const float FieldClearance = 5f;

        /// <summary>
        /// 잔해 중심 x. <b>선수 쪽</b>이다 — 조종석은 <c>-x</c> 끝이고(§5.2-1 이 "조종석 창 밖에
        /// 잔해가 보인다" 로 시작하므로 그 창에서 보이는 자리여야 한다), 에어록도 선수 승강구와
        /// 같은 x 라 나가는 자리와 가는 자리가 같은 쪽이다.
        /// </summary>
        public static float FieldCenterX => -(LastShiftHullShell.SemiMajorX * 0.5f + FieldClearance);

        /// <summary>
        /// 잔해 중심 z. <b>좌현</b>(<c>z &lt; 0</c>)이다 — 조항 <c>O-5</c> 의 고정 방향이고,
        /// 우현은 <see cref="LastShiftHullFrames"/> 의 배경막이 서는 쪽이라 비워 둔다.
        /// </summary>
        public static float FieldCenterZ => -(LastShiftHullShell.SemiMinorZ + FieldClearance);

        /// <summary>잔해 중심. y 는 선외 보행면이라 나간 높이 그대로 걸어간다.</summary>
        public static Vector3 FieldCenter =>
            new(FieldCenterX, LastShiftAirlock.OutsideWalkY, FieldCenterZ);

        /// <summary>
        /// 에어록 바깥 해치에서 잔해까지의 거리. <b>이 값이 산소 예산에 걸린다</b>(§5.2-4 —
        /// 첫 EVA 에서 산소로 겁을 주지 않는다). 테스트가 왕복 + 사이클 두 번 + 뜯기가
        /// <c>SuitOxygen</c> 예산의 절반 안인지를 이 값으로 잰다.
        /// </summary>
        public static float DistanceFromAirlock =>
            Vector3.Distance(LastShiftAirlock.ReturnPoint, FieldCenter);

        /// <summary>이번 기항에 잔해가 떠 있는가. 구간 중에는 언제나 거짓이다.</summary>
        public static bool HasField { get; private set; }

        /// <summary>이번 잔해의 계열. 직전 구간 프리셋이 정한다(§4.2).</summary>
        public static LastShiftSalvageKind Kind { get; private set; }

        /// <summary>잔해에 아직 붙어 있는 덩이 수.</summary>
        public static int Remaining { get; private set; }

        /// <summary>승무원이 들고 있고 아직 반입 안 한 덩이 수. 조항 <c>O-7</c> 의 대상이다.</summary>
        public static int Carried { get; private set; }

        /// <summary>한 번에 들고 올 수 있는 수. 거점 확장이 붙기 전이라 기본값 그대로다.</summary>
        public static int CarryCapacity => BaseCarryCapacity;

        /// <summary>
        /// 구간 자극 → 잔해 계열. <b>표를 코드에 두 벌로 안 적는다</b> — 프리셋이 늘면
        /// <c>switch</c> 가 컴파일 경고 없이 조용히 마지막 갈래로 흘러가므로, 새 프리셋은
        /// 여기서 명시적으로 갈래를 받아야 한다.
        /// </summary>
        public static LastShiftSalvageKind KindOf(LastShiftPreset preset) => preset switch
        {
            LastShiftPreset.HighHeatHighThrust => LastShiftSalvageKind.Cooling,
            LastShiftPreset.PowerOverloadLooseBattery => LastShiftSalvageKind.Power,
            _ => LastShiftSalvageKind.Hull
        };

        public static string LabelOf(LastShiftSalvageKind kind) => kind switch
        {
            LastShiftSalvageKind.Cooling => "냉각 계열 잔해",
            LastShiftSalvageKind.Power => "전력 계열 잔해",
            _ => "선체 계열 잔해"
        };

        /// <summary>이번 잔해의 이름. 프롬프트가 그대로 쓴다.</summary>
        public static string FieldLabel => LabelOf(Kind);

        // ── 기항 전환 ───────────────────────────────────────────────────────

        /// <summary>
        /// 기항에 들어온다 — 잔해 하나가 새로 뜬다. <paramref name="settledPreset"/> 은
        /// <b>방금 끝난 구간</b>의 자극이다(§4.2). 부르는 자리는
        /// <see cref="LastShiftVoyage.SettleSegment"/> 하나이고, 거기서는 구간 회차가 아직
        /// 안 올라갔으므로 <see cref="LastShiftVoyage.CurrentPreset"/> 이 그대로 직전 구간이다.
        ///
        /// <b>운반 중이던 몫은 여기서 날아간다.</b> 출항이 그 기항의 미회수분을 확정한다는
        /// 규칙(조항 <c>O-7</c> 의 같은 취지)이고, 잔해가 새로 떴는데 지난 기항 덩이를 아직
        /// 들고 있는 상태를 화면에 적을 말이 없다.
        /// </summary>
        public static void ArriveAtPort(LastShiftPreset settledPreset)
        {
            Carried = 0;
            harvestCooldown = 0f;
            Kind = KindOf(settledPreset);
            Remaining = ChunksPerField;
            HasField = true;
            LastShiftMaterials.ArriveAtPort();
        }

        /// <summary>
        /// 구간에 들어간다 — 잔해가 사라진다(조항 <c>O-5</c>: 거점·잔해는 정박한 동안만
        /// 존재하는 것으로 연출한다). 들고 있던 몫은 잃는다.
        /// </summary>
        public static void LeavePort()
        {
            HasField = false;
            Remaining = 0;
            Carried = 0;
            harvestCooldown = 0f;
        }

        // ── 회수 ────────────────────────────────────────────────────────────

        private static float harvestCooldown;

        /// <summary>
        /// 다음 덩이를 뜯을 때까지 남은 시간. <b>동사에 시간을 붙이는 자리다</b> —
        /// <see cref="HarvestSeconds"/> 가 상수로만 있으면 산소 예산 계산에는 들어가는데
        /// 실제 플레이에서는 연타로 잔해가 즉시 비어 §5.2-4("왕복이 한 단위다")가 안 선다.
        /// </summary>
        public static float HarvestCooldown => harvestCooldown;

        /// <summary>뜯기 쿨다운을 한 스텝 돌린다. sandbox 가 매 tick 부른다.</summary>
        public static void Tick(float deltaTime)
        {
            if (harvestCooldown <= 0f || deltaTime <= 0f) return;
            harvestCooldown = Mathf.Max(0f, harvestCooldown - deltaTime);
        }

        /// <summary>손이 닿는가. 잔해가 떠 있고, 남은 덩이가 있고, 손이 안 찼고, 사거리 안이다.</summary>
        public static bool CanHarvest(Vector3 position) =>
            HasField && Remaining > 0 && Carried < CarryCapacity &&
            harvestCooldown <= 0f && IsWithinReach(position);

        public static bool IsWithinReach(Vector3 position) =>
            HasField && Vector3.Distance(position, FieldCenter) <= HarvestReach;

        /// <summary>
        /// 덩이 하나를 뜯는다. <b>여기서 원장에 안 넣는다</b> — 들고 있는 것과 반입한 것을
        /// 가르는 것이 이 클래스가 존재하는 이유의 절반이다(클래스 주석 참조).
        /// </summary>
        public static bool TryHarvest(Vector3 position)
        {
            if (!CanHarvest(position)) return false;

            Remaining--;
            Carried++;
            harvestCooldown = HarvestSeconds;
            Debug.Log($"[LAST_SHIFT_SALVAGE] action=HARVEST kind={Kind} carried={Carried}/{CarryCapacity} remaining={Remaining}");
            return true;
        }

        /// <summary>
        /// 들고 온 몫을 원장에 넣는다. <b>부르는 조건은 "에어록으로 돌아왔다" 이고 그 판정은
        /// 부르는 쪽이 한다</b> — 여기서 좌표를 다시 보면 반입 조건이 두 벌이 된다.
        /// </summary>
        /// <returns>실제로 들어간 몫.</returns>
        public static int Deposit()
        {
            if (Carried <= 0) return 0;

            var deposited = LastShiftMaterials.Deposit(Carried);
            Carried = 0;
            Debug.Log($"[LAST_SHIFT_SALVAGE] action=DEPOSIT chunks={deposited} balance={LastShiftMaterials.Balance}");
            return deposited;
        }

        /// <summary>
        /// 조항 <c>O-7</c> — 선외에서 산소가 마르면 죽지 않고 <b>수확만 잃는다.</b>
        /// 대가를 시간이 아니라 수확으로 받는 이유는 <c>RG-3</c>(영구 잠금 금지)의 정신이다:
        /// 기항에서 승무원이 죽으면 남은 구간을 <c>3</c>인으로 도는 회복 불가능한 나선이 되고,
        /// 수확 상실은 아프지만 다음 기항에 회복된다.
        ///
        /// <b>잔해에 되돌리지 않는다.</b> 잃은 몫이 잔해로 복귀하면 산소가 마른 것이 시간
        /// 손해로만 남고, 그러면 "산소가 허락하는 만큼 최대한 뜯고 오는 것" 이 다시 최적이
        /// 된다 — §3.2-2 가 경계한 그것이다.
        /// </summary>
        /// <returns>잃은 몫.</returns>
        public static int AbandonCarried()
        {
            var lost = Carried;
            Carried = 0;
            if (lost > 0)
                Debug.Log($"[LAST_SHIFT_SALVAGE] action=ABANDON chunks={lost} reason=suit-oxygen-depleted");
            return lost;
        }

        // ── 네트워크 복원 ───────────────────────────────────────────────────

        /// <summary>서버가 보낸 잔해 상태를 그대로 앉힌다. 클라이언트 전용이다.</summary>
        public static void ApplyNetworkState(bool hasField, LastShiftSalvageKind kind, int remaining, int carried)
        {
            HasField = hasField;
            Kind = kind;
            Remaining = Mathf.Max(0, remaining);
            Carried = Mathf.Clamp(carried, 0, CarryCapacity);
        }

        public static void Clear()
        {
            HasField = false;
            Kind = LastShiftSalvageKind.Cooling;
            Remaining = 0;
            Carried = 0;
            harvestCooldown = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
