using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 자재 원장 — 선외 파밍의 산출물이다.
    /// 기획 정본은 <c>docs/outboard-outpost-and-map-final-v1.md</c> §3 이고, 조항 두 개가
    /// 이 파일의 존재 이유 전부다.
    ///
    /// <list type="bullet">
    /// <item><b>조항 <c>O-1</c>.</b> 선외 파밍의 산출물은 자재다. <b>자재는 정비 여력으로
    /// 환산되지 않고 그 역도 없다.</b> 그래서 이 클래스에는 <see cref="LastShiftMaintenance"/>
    /// 를 부르는 줄이 하나도 없고, 앞으로도 없어야 한다 — 환산 문이 하나 생기는 순간
    /// 파밍이 여력을 주는 것과 같아지고, §3.2 가 세운 근거 셋(희소성·노동·모아 짓기)이
    /// 동시에 죽는다.</item>
    /// <item><b>조항 <c>O-2</c>.</b> 여력은 배에 붙이는 것만, 자재는 고치고 채우고 거점을
    /// 키우는 것만 산다. 한 항목이 둘을 같이 요구하지 않는다 — 그래서 이 원장의 지불 문은
    /// <see cref="TrySpend"/> 하나이고 여력 잔액을 인자로도 안 받는다.</item>
    /// </list>
    ///
    /// <b>가격표는 아직 여기 없다.</b> §3.3 이 복구·보급·견인 봉인 해제를 여력에서 자재로
    /// 옮기라고 적었지만, 그 이관은 <see cref="LastShiftMaintenance"/> 쪽 검산
    /// (§4.1-(가)·(나), §4.3 최악 항해)을 통째로 다시 세우는 일이라 기항 화면 카드 몫이다.
    /// <b>이 카드가 닫는 것은 "자재가 어디서 나오는가" 하나다</b> — 쓰는 자리가 붙을 때
    /// <see cref="TrySpend"/> 가 이미 서 있다.
    ///
    /// <b>이월은 여력과 같다</b>(조항 <c>M-1</c>). 원래 조항 <c>O-3</c> 이 항해 종료 리셋을
    /// 걸어 뒀는데 그 근거였던 "세이브 없음" 전제가
    /// <c>campaign-structure-20h-and-save-v1.md</c> 에서 사라지면서 폐기됐다 —
    /// 리셋 시점 자체가 없으므로 항해 전체에 걸쳐 남는다.
    ///
    /// <see cref="LastShiftMaintenance"/> 와 같은 규약으로 정적이다 — 씬 없이 EditMode 에서
    /// 전부 재고, 도메인 리로드를 끈 에디터에서 지난 항해 잔액이 다음 판에 남지 않도록
    /// <see cref="ResetOnEnterPlayMode"/> 를 단다.
    /// </summary>
    public static class LastShiftMaterials
    {
        /// <summary>지금 쓸 수 있는 자재. 기항을 건너 남는다(조항 <c>M-1</c>). 상한은 없다.</summary>
        public static int Balance { get; private set; }

        /// <summary>
        /// 이 항해에서 지금까지 반입한 총량. <b>잔액과 따로 든다</b> — 잔액은 쓰면 줄지만
        /// "얼마나 밖에 나갔는가" 는 줄면 안 된다. 결과 화면이 항해를 요약하는 축이고,
        /// 거점 확장이 실제로 왕복을 줄였는지도 이 값과 기항 수로만 잴 수 있다.
        /// </summary>
        public static int LifetimeSalvaged { get; private set; }

        /// <summary>가장 최근 기항에서 반입한 몫. 화면이 "이번에 N" 을 적는 자리다.</summary>
        public static int LastPortSalvaged { get; private set; }

        // ── 획득 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 잔해에서 뜯어 온 덩이를 원장에 넣는다. <b>부르는 자리는
        /// <see cref="LastShiftSalvage.Deposit"/> 하나다</b> — 회수와 반입 사이에
        /// "들고 있는" 상태가 있고(조항 <c>O-7</c> 이 그것을 잃게 만든다), 그 상태를 아는 것은
        /// 잔해 쪽이지 원장이 아니다.
        /// </summary>
        /// <returns>실제로 들어온 몫. 음수 입력은 <c>0</c> 이다.</returns>
        public static int Deposit(int chunks)
        {
            if (chunks <= 0) return 0;

            Balance += chunks;
            LifetimeSalvaged += chunks;
            LastPortSalvaged = chunks;
            return chunks;
        }

        /// <summary>새 기항에 들어온다. 반입 표시만 비우고 <b>잔액은 안 건드린다</b>(이월).</summary>
        public static void ArriveAtPort() => LastPortSalvaged = 0;

        /// <summary>
        /// 항해를 시작한다. 자재는 항해 단위 자원이라 여기서만 <c>0</c> 으로 돌아간다 —
        /// <see cref="LastShiftMaintenance.BeginVoyage"/> 와 같은 자리·같은 이유다.
        /// </summary>
        public static void BeginVoyage() => Clear();

        // ── 소모 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 살 수 있는가. <b>여력과 달리 기항 게이트가 없다.</b> 자재로 사는 것은 복구·보급·
        /// 거점인데 거점은 선외에서 짓고(§4.4), 선외는 기항 안이지만 기항 <b>화면</b> 밖이다 —
        /// <see cref="LastShiftMaintenance.CanAfford"/> 처럼 화면 게이트를 걸면 거점 배치가
        /// 자기 자리에서 지불을 못 한다.
        /// </summary>
        public static bool CanAfford(int cost) => cost >= 0 && cost <= Balance;

        /// <summary>
        /// 잔액에서 뺀다. 모자라면 한 푼도 안 빠진다 —
        /// <see cref="LastShiftMaintenance.TrySpend"/> 와 같은 규약이다.
        /// </summary>
        public static bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;

            Balance -= cost;
            return true;
        }

        // ── 네트워크 복원 ───────────────────────────────────────────────────

        /// <summary>
        /// 서버가 보낸 자재 원장을 그대로 앉힌다. <b>클라이언트 전용이고 여기서 산수를 하지
        /// 않는다</b> — <see cref="LastShiftMaintenance.ApplyNetworkLedger"/> 와 같은 이유다.
        /// 회수는 서버에서만 성립하므로(<see cref="LastShiftSalvage"/>) 클라이언트가 스스로
        /// 더할 수 있는 값이 애초에 없다.
        /// </summary>
        public static void ApplyNetworkLedger(int balance, int lifetimeSalvaged, int lastPortSalvaged)
        {
            Balance = balance;
            LifetimeSalvaged = lifetimeSalvaged;
            LastPortSalvaged = lastPortSalvaged;
        }

        // ── 초기화 ──────────────────────────────────────────────────────────

        public static void Clear()
        {
            Balance = 0;
            LifetimeSalvaged = 0;
            LastPortSalvaged = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
