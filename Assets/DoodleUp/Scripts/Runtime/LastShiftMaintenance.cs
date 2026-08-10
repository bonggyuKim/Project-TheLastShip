using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 여력을 쓰는 자리 중 <b>배치가 아닌</b> 것들. 가격은 <c>docs/port-module-catalog-v1.md</c>
    /// §4.3 표 그대로다. 배치 가격은 여기 없다 — 그건 종류마다 다르고
    /// <see cref="LastShiftModuleKind.MaintenanceCost"/> 가 이미 든다.
    ///
    /// <b>이 열거가 화면을 안 만든다.</b> 개방·복구·보급 화면은 아직 없고(같은 문서 §9-5,
    /// <c>game-art</c>), 여기 있는 것은 가격 정본과 그 값을 빼는 문 하나뿐이다. 화면이 붙을 때
    /// 가격을 다시 적는 일이 없게 하려고 미리 박는다.
    /// </summary>
    public enum LastShiftMaintenanceItem
    {
        /// <summary>잠긴 고정 구획 하나를 이번 항해 동안 연다(서버/통신실 · 수경재배 · 의무실).</summary>
        CompartmentUnlock,

        /// <summary>진공/봉인 구역 재가압 · <c>HullIntegrity</c> 회복 하나.</summary>
        Repair,

        /// <summary>연료 추가 보충 · 예비 아이템 하나.</summary>
        Supply,

        /// <summary>견인 봉인 해제(<c>docs/voyage-run-structure-v1.md</c> §5).</summary>
        TowSealRelease
    }

    /// <summary>
    /// 배치 하나가 얼마를 물고 <b>어느 기항에</b> 섰는가. 환수액이 기항 회차에 걸려 있어서
    /// (조항 M-4) 가격만으로는 못 돌려준다 — 같은 기항이면 전액, 출항한 뒤면 절반이다.
    /// </summary>
    public readonly struct LastShiftMaintenancePurchase
    {
        public LastShiftMaintenancePurchase(int catalogIndex, int cost, int portIndex)
        {
            CatalogIndex = catalogIndex;
            Cost = cost;
            PortIndex = portIndex;
        }

        public int CatalogIndex { get; }

        /// <summary>실제로 빠져나간 여력. 가격표를 나중에 고쳐도 이미 산 것은 산 값으로 돌려준다.</summary>
        public int Cost { get; }

        /// <summary>세운 기항 회차. <see cref="LastShiftMaintenance.PortIndex"/> 와 비교하는 값이다.</summary>
        public int PortIndex { get; }
    }

    /// <summary>
    /// 정비 여력 원장 — <b>잔액 · 획득 · 소모 · 이월 · 철거 환수</b>.
    /// <c>docs/port-module-catalog-v1.md</c> §4 의 P-1 이고, 같은 문서 §9-6("구현 위치가
    /// 항해 상태 객체 신설인가 기존 상태 확장인가")을 <b>신설</b>로 닫는다.
    ///
    /// <b>왜 신설인가.</b> 확장할 만한 항해 상태가 아직 없다 — 시뮬레이션
    /// (<see cref="LastShiftSimulation"/>)은 구간 <b>안</b>의 물리·자원이고 구간이 끝나면 리셋되는
    /// 물건이라, 기항을 건너 살아남아야 하는 잔액(조항 M-1)을 거기 얹으면 이월이 그날로 죽는다.
    /// 반대로 여력은 판 안 규칙을 하나도 안 건드린다 — 여기서 나가는 값이 시뮬로 들어가는 경로가
    /// 없다. <b>그래서 별도 정적 상태 하나가 가장 작다.</b>
    ///
    /// <b><see cref="MonoBehaviour"/> 가 아니고 정적이다.</b> <see cref="LastShiftCompartments"/> ·
    /// <see cref="LastShiftPlacedModules"/> 와 같은 규약이다 — 씬 없이 EditMode 에서 전부 재고,
    /// 도메인 리로드를 끈 에디터에서 지난 판 잔액이 다음 판에 남지 않도록
    /// <see cref="ResetOnEnterPlayMode"/> 를 단다.
    ///
    /// <b>세이브가 없다</b>(조항 M-2). 이월되는 것은 한 항해 안에서이고, 항해가 끝나면
    /// <see cref="BeginVoyage"/> 가 <c>0</c> 으로 되돌린다 —
    /// <c>docs/voyage-run-structure-v1.md</c> §0-3 의 "세이브 불필요" 전제 그대로다.
    /// </summary>
    public static class LastShiftMaintenance
    {
        /// <summary>도킹 래치 수 상한. <c>modular-docking-progression-review-v1.md</c> §2 의 넷.</summary>
        public const int MaxLatches = 4;

        /// <summary>
        /// <b>래치가 <c>0</c> 일 때만</b> 주는 몫 — 조항 <c>B-1</c>(개정,
        /// <c>campaign-scale-and-combat-balance-v1.md</c> §2.3). <b>이게 없으면 구간 <c>1</c> 을
        /// 망친 항해가 회복 경로 없이 굳고</b>, 그건 <c>RG-3</c>(영구 잠금 금지)의 항해판
        /// 위반이다(<c>voyage-run-structure-v1.md</c> §4.1-(나)).
        ///
        /// <b>더하기가 아니라 하한이다.</b> 개정 전에는 래치 수에 언제나 <c>1</c> 을 얹어서
        /// 결과 화면의 래치 <c>3/4</c> 와 기항 화면의 여력 <c>4</c> 가 어긋나 있었다 — 개정판은
        /// 래치 <c>3</c> 이 곧 여력 <c>3</c> 이라 §4.1 이 <c>1:1</c> 을 고른 이유가 여기서
        /// 완성된다.
        /// </summary>
        public const int MinimumIncome = 1;

        /// <summary>
        /// 한 기항 최대 수입. 조항 <c>B-1</c> 개정으로 <c>5 → 4</c> 다(래치 넷이 그대로 수입 넷).
        /// <b>카탈로그 최고가(격납고 <c>5</c>)가 이 값보다 커서 단독 구매가 아예 불가능해졌다</b> —
        /// 저축이 조건이 아니라 규칙이 된다(같은 문서 §2.3·§2.5).
        /// </summary>
        public const int MaxPortIncome = MaxLatches;

        /// <summary>
        /// 조항 <c>B-2</c>(신설) — <b>여력 잔액 상한. 초과분은 버린다.</b>
        /// <c>port-module-catalog-v1.md</c> 조항 <c>M-1</c>("상한 없음")을 개정한다.
        ///
        /// 상한이 없으면 잔액이 <c>50</c> 을 넘는 순간 "모아서 짓는다"가 "이미 다 모여 있다"가
        /// 되고 그 뒤 기항의 선택이 사라진다. <c>12</c> 는 격납고(<c>5</c>)의 <c>2.4</c>배 —
        /// <b>가장 비싼 것을 사고도 다음 것을 향해 모으는 여지가 남는 최소값</b>이다.
        /// </summary>
        public const int MaxBalance = 12;

        private static readonly List<LastShiftMaintenancePurchase> purchases = new();

        /// <summary>
        /// 지금 쓸 수 있는 여력. <b>기항을 건너 남지만</b>(조항 M-1) <see cref="MaxBalance"/> 를
        /// 넘지 않는다(조항 B-2).
        /// </summary>
        public static int Balance { get; private set; }

        /// <summary>
        /// 몇 번째 기항인가. <c>0</c> 은 아직 한 번도 기항하지 않은 상태다 — 구간 <c>1</c> 을
        /// 도는 동안이 그것이고, 그때는 살 수 있는 것이 없다.
        /// </summary>
        public static int PortIndex { get; private set; }

        /// <summary>가장 최근 기항에서 들어온 몫. 이월분을 뺀 <b>수입</b>이다(견인이면 <c>0</c>).</summary>
        public static int LastPortIncome { get; private set; }

        /// <summary>직전 기항 시작 시점의 잔액 — 즉 이월된 몫. 화면이 "이월 N" 을 적는 자리다.</summary>
        public static int LastCarriedOver { get; private set; }

        /// <summary>
        /// 가장 최근 기항 정산에서 <b>사라진</b> 여력 — 상한 초과분(조항 B-2)과 상한 접점 견인
        /// 보정(조항 B-13)의 합이다. 화면이 "버림 N" 을 적는 자리이고(§8-4, <c>game-art</c>),
        /// <b>둘을 한 값으로 두는 것은 정산 지점이 하나이기 때문</b>이다 — 견인이면 수입이
        /// <c>0</c> 이라 초과분이 날 수 없으므로 두 사유가 같은 기항에 섞이지 않는다.
        ///
        /// <b>파생값이 아니라 저장값이다.</b> <c>이월 + 수입 - 잔액</c> 으로 계산하면 그 기항에서
        /// 뭘 사는 순간 값이 같이 틀어진다.
        /// </summary>
        public static int LastPortForfeited { get; private set; }

        /// <summary>기항에 들어와 있는가. 아니면 살 수 없다.</summary>
        public static bool IsAtPort => PortIndex > 0;

        /// <summary>지금 원장이 들고 있는 배치 기록 수. 표의 모듈 수와 같아야 한다.</summary>
        public static int PurchaseCount => purchases.Count;

        // ── 획득 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 래치 수 → 그 기항의 수입. <b>환산은 정확히 <c>1:1</c> 이다</b>(조항 B-1 개정) —
        /// 결과 화면의 래치 <c>3/4</c> 와 기항 화면의 여력 <c>3</c> 이 <b>같은 수</b>여야
        /// 환산표를 안 읽는다. 최소 보장은 그 위에 얹는 상수가 아니라 <b>래치 <c>0</c> 에만
        /// 걸리는 하한</b>이다.
        ///
        /// <paramref name="towed"/> 면 <c>0</c> 이다 — 견인의 대가 셋 중 첫째
        /// (<c>voyage-run-structure-v1.md</c> §5). <b>수입만 <c>0</c> 이고 잔액은 안 건드린다</b>
        /// (조항 M-3): 모아 둔 것까지 날리면 아끼는 선택이 언제나 손해가 되고, 그러면 아무도
        /// 두 번은 안 아낀다. <b>단 하나의 예외가 조항 B-13</b> 이고, 그건 수입식이 아니라
        /// <see cref="ArriveAtPort"/> 의 정산에 붙는다 — 상한에 앉은 잔액에서는 "수입 0" 이
        /// 아무것도 안 뺏기 때문이다.
        /// </summary>
        public static int IncomeFor(int latches, bool towed = false) =>
            towed ? 0 : Mathf.Max(Mathf.Clamp(latches, 0, MaxLatches), MinimumIncome);

        /// <summary>
        /// 기항에 들어온다. 회차를 하나 올리고 수입을 <b>남은 잔액 위에 얹는다</b> — 이 한 줄이
        /// 조항 M-1(이월) 이고, 이월이 없으면 매 기항 다 쓰는 것이 언제나 최적이라 모을 이유가
        /// 없다(§0-1).
        ///
        /// <b>정산 지점이 여기 하나다.</b> 조항 B-2(상한 <c>12</c>)와 조항 B-13(상한 접점 견인
        /// 보정)이 같은 자리에 붙는 것은 <c>campaign-scale-and-combat-balance-v1.md</c> §8-13 이
        /// 지정한 것이고, 실제로 <b>둘을 떼어 놓으면 잔액이 상한을 넘은 중간 상태가 생겨</b>
        /// B-13 의 조건("상한에 있으면")이 무엇을 보는지가 애매해진다.
        /// </summary>
        /// <returns>이번 기항에 들어온 수입.</returns>
        public static int ArriveAtPort(int latches, bool towed = false)
        {
            LastCarriedOver = Balance;
            LastPortIncome = IncomeFor(latches, towed);

            PortIndex++;
            var settled = Balance + LastPortIncome;

            // 조항 B-13 — 잔액이 상한에 앉아 있으면 견인의 "그 기항 수입 0" 이 아무것도 안
            // 뺏는다. 방치하면 숙련 후반의 전투 패배가 -13 이 아니라 복구뿐인 -6 이 되어 전투
            // 기대값이 +5.2 로 튀고, 그건 §7 이 그은 전투 선택률 상한 70% 를 넘긴다.
            // 빼는 값이 래치 수인 이유는 그 수가 이미 결과 화면에 있기 때문이다 — 새 상수를
            // 안 만든다("래치 3 개를 걸었는데 견인돼서 3 을 잃었다").
            if (towed && settled >= MaxBalance) settled -= Mathf.Clamp(latches, 0, MaxLatches);

            // 조항 B-2 — 상한 12. 초과분은 버린다.
            Balance = Mathf.Clamp(settled, 0, MaxBalance);
            LastPortForfeited = LastCarriedOver + LastPortIncome - Balance;
            return LastPortIncome;
        }

        /// <summary>
        /// 항해를 시작한다 — 조항 M-2. 잔액·회차·배치 기록이 전부 <c>0</c> 이다.
        /// <b>배치 표는 안 건드린다</b>(<see cref="LastShiftCompartments.ClearModules"/> 는 부르는
        /// 쪽 몫이다) — 원장이 씬을 지우면 "여력을 리셋했더니 배가 뜯겼다" 가 된다.
        /// </summary>
        public static void BeginVoyage() => Clear();

        // ── 소모 ────────────────────────────────────────────────────────────

        /// <summary>§4.3 가격표의 배치 아닌 계열.</summary>
        public static int PriceOf(LastShiftMaintenanceItem item) => item switch
        {
            LastShiftMaintenanceItem.CompartmentUnlock => 2,
            _ => 1
        };

        /// <summary>살 수 있는가. <b>기항 밖에서는 언제나 거짓이다.</b></summary>
        public static bool CanAfford(int cost) => IsAtPort && cost >= 0 && cost <= Balance;

        /// <summary>
        /// 잔액에서 뺀다. <b>모자라면 한 푼도 안 빠진다</b> — 부분 지불을 두면 여력 <c>1</c> 로
        /// 개방을 절반 사 둔 상태가 생기고, 그 상태를 화면에 적을 말이 없다.
        /// </summary>
        public static bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;

            Balance -= cost;
            return true;
        }

        /// <summary>가격표를 보고 뺀다.</summary>
        public static bool TrySpend(LastShiftMaintenanceItem item) => TrySpend(PriceOf(item));

        // ── 배치와 철거 ─────────────────────────────────────────────────────

        /// <summary>
        /// 모듈 하나 값을 물고 기록을 남긴다. <paramref name="moduleSlot"/> 은 표의 모듈 자리
        /// (<c>인덱스 - <see cref="LastShiftCompartments.FixedCount"/></c>)이고, <b>반드시 지금
        /// 기록 수와 같아야 한다</b> — 표는 꼬리에만 붙으므로(<see cref="LastShiftCompartments.TryRegister"/>)
        /// 다른 값이 오면 그건 표와 원장이 이미 갈렸다는 뜻이다.
        ///
        /// <b>값을 먼저 확인하고 나중에 뺀다.</b> 표에 넣는 것은 부르는 쪽이고, 이 함수가 참을
        /// 돌려준 뒤에 표 등록이 실패하면 여력만 사라진다 — 그래서 부르는 쪽 규약은
        /// "표에 들어간 것을 확인하고 나서 이 함수를 부른다" 이고, 살 수 있는지는 그 전에
        /// <see cref="CanAfford"/> 로 묻는다.
        /// </summary>
        public static bool TryChargeModule(int moduleSlot, int catalogIndex, int cost)
        {
            if (moduleSlot != purchases.Count) return false;
            if (!TrySpend(cost)) return false;

            purchases.Add(new LastShiftMaintenancePurchase(catalogIndex, cost, PortIndex));
            return true;
        }

        /// <summary>카탈로그에서 가격을 읽어 무는 편의 문.</summary>
        public static bool TryChargeModule(int moduleSlot, int catalogIndex) =>
            TryChargeModule(moduleSlot, catalogIndex, LastShiftModuleCatalog.At(catalogIndex).MaintenanceCost);

        /// <summary>
        /// 철거 환수 — 조항 M-4. <b>같은 기항 안이면 전액</b>(무료 철거: 방금 놓은 자리를 무르는
        /// 것은 실수 정정이지 거래가 아니다), <b>출항한 뒤면 절반 내림</b>이다.
        ///
        /// 절반 내림이라 <c>1</c> 짜리는 <c>0</c> 이 돌아온다. 사고 팔면 언제나 손해라
        /// <b>매수–매도 루프가 안 생기고</b>, 그래도 돌아오는 것이 있어서 기항 <c>1</c> 의 배치가
        /// 항해를 망치는 결정이 아니게 된다(§4.4).
        /// </summary>
        public static int RefundFor(in LastShiftMaintenancePurchase purchase) =>
            purchase.PortIndex == PortIndex ? purchase.Cost : purchase.Cost / 2;

        /// <summary>
        /// 모듈 하나를 뜯고 환수한다. 기록을 지우고 뒤 기록을 당기는 것이
        /// <see cref="LastShiftCompartments.TryRemove"/> 가 표를 당기는 것과 <b>같은 모양이어야
        /// 한다</b> — 표는 빈 칸을 안 남기고 뒤를 당기므로, 원장이 무덤을 남기면 그 뒤로 모듈
        /// 자리와 기록 자리가 하나씩 어긋난 채 환수액이 남의 것으로 나간다.
        /// </summary>
        public static bool TryRefundModule(int moduleSlot, out int refunded)
        {
            refunded = 0;
            if (moduleSlot < 0 || moduleSlot >= purchases.Count) return false;

            refunded = RefundFor(purchases[moduleSlot]);
            purchases.RemoveAt(moduleSlot);
            // 상한(조항 B-2)은 여기에도 걸린다. 환수는 잔액이 느는 유일한 다른 경로이고,
            // 여기만 빼 두면 잔액이 12 를 넘은 상태가 만들어져 다음 기항의 B-13 조건이
            // 무엇을 보는지가 갈린다.
            Balance = Mathf.Min(Balance + refunded, MaxBalance);
            return true;
        }

        /// <summary>기록 하나를 읽는다. 화면이 "뜯으면 N 돌아온다" 를 미리 적는 자리다.</summary>
        public static bool TryGetPurchase(int moduleSlot, out LastShiftMaintenancePurchase purchase)
        {
            if (moduleSlot < 0 || moduleSlot >= purchases.Count)
            {
                purchase = default;
                return false;
            }

            purchase = purchases[moduleSlot];
            return true;
        }

        // ── 네트워크 복원 ───────────────────────────────────────────────────

        /// <summary>
        /// 서버가 보낸 원장 숫자를 그대로 앉힌다. <b>클라이언트 전용이고 여기서 산수를 하지
        /// 않는다</b> — 수입식(<see cref="IncomeFor"/>)을 클라이언트에서 다시 돌리면 래치 수가
        /// 한 tick 어긋난 순간 두 화면의 잔액이 갈리고, 그 차이는 배치를 눌러 봐야 드러난다.
        /// </summary>
        public static void ApplyNetworkLedger(
            int balance, int portIndex, int lastIncome, int lastCarried, int lastForfeited = 0)
        {
            Balance = balance;
            PortIndex = portIndex;
            LastPortIncome = lastIncome;
            LastCarriedOver = lastCarried;
            LastPortForfeited = lastForfeited;
        }

        /// <summary>
        /// 서버가 보낸 배치 기록을 통째로 갈아 끼운다. 환수 힌트(<see cref="RefundFor"/>)가
        /// 기항 회차를 보므로 잔액만 맞춰서는 클라이언트가 "뜯으면 얼마" 를 못 적는다.
        ///
        /// <b>기록은 표 모듈 자리의 앞부분이다.</b> <see cref="TryChargeModule"/> 이 꼬리에만
        /// 붙이므로 기록 수는 언제나 모듈 수 이하이고, 그 앞뒤가 뒤집힌 입력은 이미 서버에서
        /// 갈린 상태라 여기서 메워 봐야 어느 쪽이 옳은지 모른다.
        /// </summary>
        public static void ApplyNetworkPurchases(IReadOnlyList<LastShiftMaintenancePurchase> records)
        {
            purchases.Clear();
            if (records == null) return;
            for (var index = 0; index < records.Count; index++) purchases.Add(records[index]);
        }

        // ── 초기화 ──────────────────────────────────────────────────────────

        /// <summary>전부 되돌린다. 항해 시작과 테스트가 부른다.</summary>
        public static void Clear()
        {
            purchases.Clear();
            Balance = 0;
            PortIndex = 0;
            LastPortIncome = 0;
            LastCarriedOver = 0;
            LastPortForfeited = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
