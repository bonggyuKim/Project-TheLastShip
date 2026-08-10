using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 정비 여력 원장(P-1) — <c>docs/port-module-catalog-v1.md</c> §4.
    ///
    /// <b>여기서 재는 것은 넷이다.</b>
    /// <list type="number">
    /// <item><b>이월이 실제로 일어난다</b>(조항 M-1). 이게 고장 나면 매 기항 다 쓰는 것이 언제나
    /// 최적이라 "모아서 큰 것을 짓는다"(§0-1)가 통째로 죽는다 — <b>정본이 이 문장 하나를 위해
    /// 쓰였다.</b></item>
    /// <item><b>기획이 못 박은 검산 셋이 실제로 성립한다</b>: 래치 <c>4/4</c> 로도 개방 셋은 못
    /// 한다(§4.1-(가)), 래치 <c>0</c> 이어도 여력이 <c>0</c> 이 아니다(§4.1-(나)), 최악 항해에도
    /// 회복 경로가 남는다(§4.3).</item>
    /// <item><b>환수가 루프를 안 만든다</b>(조항 M-4). 사고 팔아 잔액이 느는 경로가 하나라도
    /// 있으면 그건 무한 자원이다.</item>
    /// <item><b>원장이 표와 안 갈린다.</b> 표는 모듈을 빼면 뒤를 당기므로
    /// (<see cref="LastShiftCompartments.TryRemove"/>), 원장이 무덤을 남기면 그 뒤로 환수액이
    /// 남의 것으로 나간다.</item>
    /// </list>
    ///
    /// 정적 상태를 만지므로 원장·표·오버레이를 앞뒤로 다 비운다.
    /// </summary>
    public sealed class LastShiftMaintenanceTests
    {
        [SetUp]
        public void ClearBefore() => ClearAll();

        [TearDown]
        public void ClearAfter() => ClearAll();

        private static void ClearAll()
        {
            LastShiftMaintenance.Clear();
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftPlacementAuthority.Revoke();
        }

        // ── 획득 (§4.1) ─────────────────────────────────────────────────────

        /// <summary>
        /// 조항 <c>B-1</c>(개정) — 환산이 <b>정확히</b> <c>1:1</c> 이다. 래치 하나가 여력 하나이고,
        /// 최소 보장은 그 위에 얹는 상수가 아니라 <b>래치 <c>0</c> 에만 걸리는 하한</b>이다.
        /// <b>결과 화면의 래치와 기항 화면의 여력이 같은 수여야 한다</b>는 것이 이 개정의
        /// 판독성 쪽 근거다(<c>campaign-scale-and-combat-balance-v1.md</c> §2.3).
        ///
        /// 개정 전 표(<c>1/2/3/4/5</c>)를 그대로 두면 <c>70</c>기항 총 수입이 <c>245</c> 라
        /// 지출처 <c>137</c> 의 <c>1.79</c>배가 되고, 후반 여력이 남아 기항의 선택이 사라진다.
        /// </summary>
        [Test]
        public void EachLatchIsOneBudgetAndTheGuaranteedOneOnlyCatchesZero()
        {
            var expected = new[] { 1, 1, 2, 3, 4 };

            for (var latches = 0; latches <= LastShiftMaintenance.MaxLatches; latches++)
                Assert.That(LastShiftMaintenance.IncomeFor(latches), Is.EqualTo(expected[latches]),
                    $"래치 {latches} 의 수입이 조항 B-1 개정표와 다르다");

            Assert.That(LastShiftMaintenance.IncomeFor(0), Is.EqualTo(1), "§4.1-(나) — 여력 0 인 기항이 생겼다");
            Assert.That(LastShiftMaintenance.IncomeFor(1), Is.EqualTo(LastShiftMaintenance.IncomeFor(0)),
                "래치 1 이 래치 0 보다 이득이면 최소 보장이 조건부가 아니다");
            Assert.That(LastShiftMaintenance.IncomeFor(LastShiftMaintenance.MaxLatches),
                Is.EqualTo(LastShiftMaintenance.MaxPortIncome).And.EqualTo(4),
                "한 기항 최대 수입이 4 가 아니다 — 격납고 단독 구매가 다시 열린다");
        }

        /// <summary>래치 수가 범위를 벗어나도 수입은 <c>1~4</c> 밖으로 안 나간다.</summary>
        [Test]
        public void OutOfRangeLatchCountsAreClampedNotTrusted()
        {
            Assert.That(LastShiftMaintenance.IncomeFor(-3), Is.EqualTo(LastShiftMaintenance.MinimumIncome));
            Assert.That(LastShiftMaintenance.IncomeFor(99), Is.EqualTo(LastShiftMaintenance.MaxPortIncome));
        }

        /// <summary>
        /// 조항 M-3 — <b>견인이 <c>0</c> 으로 만드는 것은 그 기항의 수입이지 잔액이 아니다.</b>
        /// 모아 둔 것까지 날리면 기항 <c>1</c> 에서 아끼는 선택이 언제나 손해가 되고, 그러면
        /// 아무도 두 번은 안 아낀다 — 이월이 함정이 되는 자리가 정확히 여기다.
        /// </summary>
        [Test]
        public void BeingTowedZeroesTheIncomeButNeverTheSavings()
        {
            LastShiftMaintenance.ArriveAtPort(3);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(3));

            var income = LastShiftMaintenance.ArriveAtPort(0, towed: true);

            Assert.That(income, Is.Zero, "견인의 대가 첫째 — 그 기항 수입은 0");
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(3), "모아 둔 것이 견인에 날아갔다");
            Assert.That(LastShiftMaintenance.CanAfford(LastShiftMaintenance.PriceOf(LastShiftMaintenanceItem.TowSealRelease)),
                Is.True, "견인 봉인을 풀 길이 없으면 RG-3 의 항해판 위반이다");
        }

        // ── 이월 (§4.2) ─────────────────────────────────────────────────────

        /// <summary>
        /// 조항 M-1 — 안 쓴 것이 다음 기항에 얹힌다. <b>정본 §7-B 사례를 조항 <c>B-1</c> 개정
        /// 수입으로 다시 잰다</b>: 기항 <c>1</c>(래치 <c>3</c>)에서 예비 전력실(<c>2</c>)만 사고
        /// <c>1</c> 을 남기면, 기항 <c>2</c> 의 수입 <c>4</c> 와 합쳐 <c>5</c> 가 되어
        /// <b>한 기항 수입만으로는 영영 못 사는 격납고(<c>5</c>)가 열린다.</b>
        ///
        /// <b>개정판에서 이 사례가 더 강해졌다.</b> 개정 전에는 래치 <c>4/4</c> 한 번이면
        /// 격납고를 단독으로 살 수 있어 이월이 <b>빠른 길</b>일 뿐이었는데, 지금은 최대 수입이
        /// <c>4</c> 라 이월이 <b>유일한 길</b>이다(§2.5).
        /// </summary>
        [Test]
        public void UnspentBudgetCarriesIntoTheNextPort()
        {
            LastShiftMaintenance.ArriveAtPort(3);
            Assert.That(LastShiftMaintenance.TrySpend(2), Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(1));

            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);

            Assert.That(LastShiftMaintenance.LastCarriedOver, Is.EqualTo(1));
            Assert.That(LastShiftMaintenance.LastPortIncome, Is.EqualTo(4));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(5));

            var frame = LastShiftModuleCatalog.At(LastShiftModuleCatalog.Count - 1);
            Assert.That(frame.MaintenanceCost, Is.EqualTo(5));
            Assert.That(LastShiftMaintenance.TrySpend(frame.MaintenanceCost), Is.True,
                "이월이 없으면 §0-1 의 '모아서 짓는다' 가 일어날 자리가 없다");
            Assert.That(LastShiftMaintenance.Balance, Is.Zero);
        }

        // ── 잔액 상한 (조항 B-2 · B-13) ─────────────────────────────────────

        /// <summary>
        /// 조항 <c>B-2</c> — <b>잔액은 <c>12</c> 를 안 넘고 초과분은 버린다.</b> 상한이 없으면
        /// 잔액이 <c>50</c> 을 넘는 순간 "모아서 짓는다"가 "이미 다 모여 있다"가 되고 그 뒤
        /// 기항의 선택이 사라진다(<c>campaign-scale-and-combat-balance-v1.md</c> §2.3).
        ///
        /// <b>버린 몫이 화면에 적을 수 있는 값으로 남는다</b>(§8-4) — 초과분이 소리 없이 사라지면
        /// 플레이어는 상한이 있다는 것을 영영 못 배운다.
        /// </summary>
        [Test]
        public void TheBalanceStopsAtTwelveAndTheOverflowIsDiscarded()
        {
            for (var port = 0; port < 3; port++) LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxBalance),
                "기항 셋 × 최대 수입 4 = 12 — 상한에 정확히 앉는다");
            Assert.That(LastShiftMaintenance.LastPortForfeited, Is.Zero, "상한에 닿기만 한 기항이 버렸다");

            LastShiftMaintenance.ArriveAtPort(3);

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxBalance),
                "상한을 넘겼다 — 조항 B-2 위반");
            Assert.That(LastShiftMaintenance.LastPortIncome, Is.EqualTo(3), "수입 자체는 성적 그대로여야 한다");
            Assert.That(LastShiftMaintenance.LastPortForfeited, Is.EqualTo(3), "버린 몫이 화면에 안 남는다");
        }

        /// <summary>
        /// 상한은 <b>환수에도</b> 걸린다. 환수는 잔액이 느는 유일한 다른 경로라, 여기만 빼 두면
        /// 잔액이 <c>12</c> 를 넘은 상태가 만들어지고 그 뒤 조항 <c>B-13</c> 의 조건("상한에
        /// 있으면")이 무엇을 보는지가 갈린다.
        /// </summary>
        [Test]
        public void RefundsCannotPushTheBalanceOverTheCap()
        {
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            const int storage = LastShiftModuleCatalog.CargoBay;
            Assert.That(LastShiftMaintenance.TryChargeModule(0, storage), Is.True);

            for (var port = 0; port < 4; port++) LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxBalance));

            Assert.That(LastShiftMaintenance.TryRefundModule(0, out var refunded), Is.True);
            Assert.That(refunded, Is.EqualTo(1), "출항한 뒤라 절반 내림이다(조항 M-4)");
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxBalance),
                "환수가 상한을 넘겼다");
        }

        /// <summary>
        /// 조항 <c>B-13</c> — <b>상한에 앉은 잔액에서는 "그 기항 수입 <c>0</c>" 이 아무것도 안
        /// 뺏는다.</b> 그 자리에서만 직전 구간 래치 수를 잔액에서 뺀다(§6.5.1-(마)).
        ///
        /// 이걸 안 막으면 숙련 후반의 전투 패배가 <c>-13</c> 이 아니라 복구뿐인 <c>-6</c> 이 되고
        /// 전투 기대값이 <c>+5.2</c> 로 튄다 — 조항 <c>B-9</c> 가 이미 만든 후반 전투 우위 위에
        /// 감가까지 겹치는 이중 가속이라 §7 의 전투 선택률 상한 <c>70%</c> 를 넘긴다.
        /// </summary>
        [Test]
        public void BeingTowedAtTheCapCostsTheLatchesThatWereEarned()
        {
            for (var port = 0; port < 3; port++) LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxBalance));

            var income = LastShiftMaintenance.ArriveAtPort(3, towed: true);

            Assert.That(income, Is.Zero, "견인 수입은 여전히 0 이다");
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxBalance - 3),
                "상한에 앉은 채 견인됐는데 잃은 것이 없다 — 조항 B-13 이 안 걸렸다");
            Assert.That(LastShiftMaintenance.LastPortForfeited, Is.EqualTo(3),
                "'래치 3 개를 걸었는데 견인돼서 3 을 잃었다' 를 화면이 못 적는다");
        }

        /// <summary>
        /// 조항 <c>B-13</c> 은 <b>상한 접점에서만</b> 산다. 잔액이 상한 아래면 견인은 조항
        /// <c>M-3</c> 그대로 수입만 <c>0</c> 이고, 모아 둔 것은 한 푼도 안 뺏긴다 — 이월이
        /// 함정이 되는 자리가 정확히 여기라서 초·중반 체감이 <c>0</c> 이어야 한다(§2.3).
        /// </summary>
        [Test]
        public void BeingTowedBelowTheCapStillNeverTouchesTheSavings()
        {
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(8));

            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches, towed: true);

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(8), "상한 아래인데 견인이 잔액을 깎았다");
            Assert.That(LastShiftMaintenance.LastPortForfeited, Is.Zero);
        }

        /// <summary>
        /// 상한 접점 보정도 <b>래치 수만큼</b>이라 성적을 안 낸 견인은 아무것도 더 안 잃는다.
        /// 별도 상수를 안 만든 것이 조항 <c>B-13</c> 이 고른 형태다(§6.5.1-(마)).
        /// </summary>
        [Test]
        public void TowingWithoutLatchesTakesNothingEvenAtTheCap()
        {
            for (var port = 0; port < 3; port++) LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);

            LastShiftMaintenance.ArriveAtPort(0, towed: true);

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxBalance));
            Assert.That(LastShiftMaintenance.LastPortForfeited, Is.Zero);
        }

        /// <summary>조항 M-2 — 항해가 끝나면 잔액도 기록도 <c>0</c> 이다. 세이브가 없다.</summary>
        [Test]
        public void StartingAVoyageResetsTheLedger()
        {
            LastShiftMaintenance.ArriveAtPort(4);
            LastShiftMaintenance.TryChargeModule(0, 0);

            LastShiftMaintenance.BeginVoyage();

            Assert.That(LastShiftMaintenance.Balance, Is.Zero);
            Assert.That(LastShiftMaintenance.PortIndex, Is.Zero);
            Assert.That(LastShiftMaintenance.PurchaseCount, Is.Zero);
            Assert.That(LastShiftMaintenance.IsAtPort, Is.False);
        }

        // ── 소모 (§4.3) ─────────────────────────────────────────────────────

        /// <summary>
        /// 기항 밖에서는 아무것도 안 팔린다. 여력은 <b>기항 화면에서만 존재하는 것</b>이고
        /// (§4.1 어휘 주의) 구간을 도는 동안 잔액이 줄어드는 경로가 있으면 그 구분이 무너진다.
        /// </summary>
        [Test]
        public void NothingSellsWhileTheShipIsUnderway()
        {
            Assert.That(LastShiftMaintenance.IsAtPort, Is.False);
            Assert.That(LastShiftMaintenance.CanAfford(0), Is.False);
            Assert.That(LastShiftMaintenance.TrySpend(1), Is.False);
            Assert.That(LastShiftMaintenance.TryChargeModule(0, 0), Is.False);
        }

        /// <summary>모자라면 한 푼도 안 빠진다 — 부분 지불을 두면 화면에 적을 말이 없는 상태가 생긴다.</summary>
        [Test]
        public void AnUnaffordablePurchaseLeavesTheBalanceUntouched()
        {
            LastShiftMaintenance.ArriveAtPort(0);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(1));

            Assert.That(LastShiftMaintenance.TrySpend(2), Is.False);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(1));
            Assert.That(LastShiftMaintenance.TryChargeModule(0, LastShiftModuleCatalog.Count - 1), Is.False);
            Assert.That(LastShiftMaintenance.PurchaseCount, Is.Zero, "못 산 것이 기록에 남았다");
        }

        /// <summary>
        /// 검산 §4.1-(가) — <b>래치 <c>4/4</c> 로도 잠긴 방 셋을 다 열 수 없다.</b> 다 열리면
        /// 선택이 아니라 순서 문제가 된다. 개방 <c>2</c> 짜리 둘이 한 기항의 한계이고, 셋째는
        /// 다음 기항으로 넘어간다.
        /// </summary>
        [Test]
        public void EvenAPerfectPortCannotOpenAllThreeLockedCompartments()
        {
            var unlock = LastShiftMaintenance.PriceOf(LastShiftMaintenanceItem.CompartmentUnlock);
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);

            Assert.That(LastShiftMaintenance.TrySpend(LastShiftMaintenanceItem.CompartmentUnlock), Is.True);
            Assert.That(LastShiftMaintenance.TrySpend(LastShiftMaintenanceItem.CompartmentUnlock), Is.True);
            Assert.That(LastShiftMaintenance.TrySpend(LastShiftMaintenanceItem.CompartmentUnlock), Is.False,
                "한 기항에서 셋이 다 열렸다 — §4.1-(가) 위반");

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxPortIncome - 2 * unlock));

            LastShiftMaintenance.ArriveAtPort(2);
            Assert.That(LastShiftMaintenance.TrySpend(LastShiftMaintenanceItem.CompartmentUnlock), Is.True,
                "셋째는 두 기항에 걸쳐 열린다");
        }

        /// <summary>
        /// 검산 §4.3 최악 항해 — 래치 <c>0/0</c> 이어도 총 수입 <c>2</c> 다. <b>확장은 못 하지만
        /// 회복 경로가 있다</b>: 복구 둘, 또는 연결 통로 둘, 또는 개방 하나.
        /// </summary>
        [Test]
        public void TheWorstVoyageStillAffordsARecoveryPath()
        {
            LastShiftMaintenance.ArriveAtPort(0);
            LastShiftMaintenance.ArriveAtPort(0);

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(2));
            Assert.That(LastShiftMaintenance.TrySpend(LastShiftMaintenanceItem.Repair), Is.True);
            Assert.That(LastShiftMaintenance.TrySpend(LastShiftMaintenanceItem.Repair), Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.Zero);
        }

        /// <summary>
        /// 개정 검산(§2.3) — <b>카탈로그 최고가가 한 기항 최대 수입보다 비싸다.</b> 래치
        /// <c>4/4</c> 로도 격납고를 한 기항에 못 사므로 <b>저축이 조건이 아니라 규칙</b>이 된다.
        /// 최고가가 최대 수입 이하로 내려오면 그 순간 "모아서 짓는다"(§0-1)가 선택 사항이 된다.
        ///
        /// <b>두 기항이면 반드시 닿는다</b>는 것을 같이 건다 — 최고가가 <c>2</c>기항 최대 수입
        /// 위로 올라가면 이번에는 저축 기간이 길어져 기항 하나가 통째로 아무 선택도 아닌 칸이 된다.
        /// </summary>
        [Test]
        public void TheDearestModuleAlwaysNeedsTwoPortsWorthOfIncome()
        {
            var dearest = 0;
            for (var index = 0; index < LastShiftModuleCatalog.Count; index++)
                dearest = Mathf.Max(dearest, LastShiftModuleCatalog.At(index).MaintenanceCost);

            Assert.That(dearest, Is.GreaterThan(LastShiftMaintenance.MaxPortIncome),
                "최고가를 한 기항 수입으로 산다 — 조항 B-1 개정이 세운 §4.3 검산 위반");
            Assert.That(dearest, Is.LessThanOrEqualTo(2 * LastShiftMaintenance.MaxPortIncome),
                "최고가가 두 기항으로도 안 닿는다 — 저축 구간이 설계보다 길다");
        }

        // ── 철거 환수 (§4.4) ────────────────────────────────────────────────

        /// <summary>
        /// 조항 M-4 앞절 — <b>같은 기항 안에서 무른 것은 전액 돌아온다.</b> 방금 놓은 자리를
        /// 되돌리는 것은 거래가 아니라 실수 정정이다.
        /// </summary>
        [Test]
        public void UndoingWithinTheSamePortCostsNothing()
        {
            LastShiftMaintenance.ArriveAtPort(4);
            const int slot = LastShiftModuleCatalog.CargoBay;
            var cost = LastShiftModuleCatalog.At(slot).MaintenanceCost;

            Assert.That(LastShiftMaintenance.TryChargeModule(0, slot), Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxPortIncome - cost));

            Assert.That(LastShiftMaintenance.TryRefundModule(0, out var refunded), Is.True);
            Assert.That(refunded, Is.EqualTo(cost));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxPortIncome));
            Assert.That(LastShiftMaintenance.PurchaseCount, Is.Zero);
        }

        /// <summary>
        /// 조항 M-4 뒷절 — 출항한 뒤에 뜯으면 <b>절반 내림</b>이다. 정본 §4.4 가 적어 둔 네 값을
        /// 그대로 건다: <c>1→0 · 2→1 · 3→1 · 5→2</c>.
        /// </summary>
        [Test]
        public void TearingDownAfterDepartureRefundsHalfRoundedDown()
        {
            var expected = new[] { (1, 0), (2, 1), (3, 1), (5, 2) };

            foreach (var (cost, refund) in expected)
            {
                LastShiftMaintenance.Clear();
                // 최고가 5 는 한 기항 수입 4 로 못 문다(조항 B-1 개정) — 이 표를 다 돌리려면
                // 기항 둘이 필요하고, 그게 곧 §2.5 가 말한 "격납고는 단독 구매 불가" 다.
                LastShiftMaintenance.ArriveAtPort(4);
                LastShiftMaintenance.ArriveAtPort(4);
                Assert.That(LastShiftMaintenance.TryChargeModule(0, 0, cost), Is.True);

                LastShiftMaintenance.ArriveAtPort(4);
                Assert.That(LastShiftMaintenance.TryRefundModule(0, out var refunded), Is.True);
                Assert.That(refunded, Is.EqualTo(refund), $"가격 {cost} 의 환수가 정본과 다르다");
            }
        }

        /// <summary>
        /// <b>사고 팔면 언제나 손해다.</b> 환수 &lt; 가격이 아니면 매수–매도 루프가 무한 자원이
        /// 되고, 그 순간 가격표 전체가 장식이 된다. 같은 기항 전액 환수는 잔액을 <b>원래대로</b>
        /// 되돌릴 뿐 늘리지 않는다.
        /// </summary>
        [Test]
        public void BuyingAndSellingNeverCreatesBudget()
        {
            for (var index = 0; index < LastShiftModuleCatalog.Count; index++)
            {
                LastShiftMaintenance.Clear();
                // 기항 둘 — 최고가(격납고 5)가 한 기항 수입 4 를 넘으므로(조항 B-1 개정)
                // 목록 전체를 도는 데 한 기항으로는 모자란다.
                LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
                LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
                var opening = LastShiftMaintenance.Balance;

                // 같은 기항: 무르고 나면 제자리다.
                Assert.That(LastShiftMaintenance.TryChargeModule(0, index), Is.True);
                Assert.That(LastShiftMaintenance.TryRefundModule(0, out _), Is.True);
                Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(opening),
                    $"{LastShiftModuleCatalog.At(index).Name} 을 같은 기항에 사고 무른 것이 잔액을 바꿨다");

                // 기항을 건너면 반드시 준다.
                Assert.That(LastShiftMaintenance.TryChargeModule(0, index), Is.True);
                LastShiftMaintenance.ArriveAtPort(0);
                var beforeTeardown = LastShiftMaintenance.Balance;
                Assert.That(LastShiftMaintenance.TryRefundModule(0, out var refunded), Is.True);

                Assert.That(refunded, Is.LessThan(LastShiftModuleCatalog.At(index).MaintenanceCost),
                    $"{LastShiftModuleCatalog.At(index).Name} 의 환수가 가격 이상이다 — 루프가 생긴다");
                Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(beforeTeardown + refunded));
            }
        }

        /// <summary>
        /// 정본 §7-D 사례를 그대로 돈다 — 기항 <c>1</c> 에 화물칸(<c>3</c>)을 선미에 붙이고,
        /// 기항 <c>2</c> 에 뜯어(<c>+1</c>) 수입 <c>3</c> 과 합쳐 선수 쪽에 다시 세운다.
        /// <b>손해는 <c>2</c> 지만 배가 이번 항해에 맞게 다시 짜인다</b> — 여력이 예산이 아니라
        /// 재료로 읽히는 것이 이 흐름이다.
        /// </summary>
        [Test]
        public void CaseDRebuildsTheShipMidVoyage()
        {
            const int storage = LastShiftModuleCatalog.CargoBay;
            var price = LastShiftModuleCatalog.At(storage).MaintenanceCost;
            Assert.That(price, Is.EqualTo(3));

            // 래치 3 — 조항 B-1 개정에서 화물칸(3)을 한 기항 수입으로 무는 최소 성적이다.
            LastShiftMaintenance.ArriveAtPort(3);
            Assert.That(LastShiftMaintenance.TryChargeModule(0, storage), Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.Zero);

            LastShiftMaintenance.ArriveAtPort(3);
            Assert.That(LastShiftMaintenance.TryRefundModule(0, out var refunded), Is.True);
            Assert.That(refunded, Is.EqualTo(1));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(4));

            Assert.That(LastShiftMaintenance.TryChargeModule(0, storage), Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(1));
        }

        // ── 원장과 표 ───────────────────────────────────────────────────────

        /// <summary>
        /// 기록은 <b>표가 자리를 주는 순서로만</b> 쌓인다. 자리를 건너뛴 청구를 받아 주면
        /// 그 뒤로 모듈 자리와 기록 자리가 하나씩 어긋난 채 환수액이 남의 것으로 나간다.
        /// </summary>
        [Test]
        public void ChargesMustFollowTheTablesSlotOrder()
        {
            LastShiftMaintenance.ArriveAtPort(4);

            Assert.That(LastShiftMaintenance.TryChargeModule(1, 0), Is.False, "빈 자리를 건너뛴 청구가 통과했다");
            Assert.That(LastShiftMaintenance.PurchaseCount, Is.Zero);
            Assert.That(LastShiftMaintenance.TryChargeModule(0, 0), Is.True);
            Assert.That(LastShiftMaintenance.TryChargeModule(0, 0), Is.False, "같은 자리에 두 번 청구됐다");
        }

        /// <summary>
        /// 가운데를 빼면 뒤 기록이 <b>표와 같은 모양으로</b> 당겨진다
        /// (<see cref="LastShiftCompartments.TryRemove"/> 가 표를 당기는 것과 같다).
        /// </summary>
        [Test]
        public void RemovingAMiddleModulePullsTheLedgerLikeTheTable()
        {
            LastShiftMaintenance.ArriveAtPort(4);
            LastShiftMaintenance.ArriveAtPort(4);

            for (var slot = 0; slot < 3; slot++)
                Assert.That(LastShiftMaintenance.TryChargeModule(slot, slot), Is.True);

            Assert.That(LastShiftMaintenance.TryRefundModule(1, out _), Is.True);

            Assert.That(LastShiftMaintenance.PurchaseCount, Is.EqualTo(2));
            Assert.That(LastShiftMaintenance.TryGetPurchase(0, out var first), Is.True);
            Assert.That(first.CatalogIndex, Is.EqualTo(0));
            Assert.That(LastShiftMaintenance.TryGetPurchase(1, out var second), Is.True);
            Assert.That(second.CatalogIndex, Is.EqualTo(2), "뒤 기록이 안 당겨졌다");
        }

        /// <summary>
        /// 실제 배치 사슬로 한 바퀴 — 커서로 확정하고 값을 물고, 뜯고 돌려받는다. <b>표의 모듈
        /// 자리와 원장 기록 자리가 같은 값을 가리키는지를 여기서만 실물로 확인한다.</b>
        /// </summary>
        [Test]
        public void APlacementChargesAndATeardownRefundsAlongTheRealTable()
        {
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            var cursor = HullAttachedCursor();
            var cost = cursor.Kind.MaintenanceCost;

            Assert.That(cursor.TryCommit(out var index, out _), Is.True);
            var slot = index - LastShiftCompartments.FixedCount;
            Assert.That(slot, Is.Zero);
            Assert.That(LastShiftMaintenance.TryChargeModule(slot, cursor.CatalogIndex, cost), Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxPortIncome - cost));

            Assert.That(LastShiftCompartments.TryRemove(index), Is.True);
            Assert.That(LastShiftMaintenance.TryRefundModule(slot, out var refunded), Is.True);

            Assert.That(refunded, Is.EqualTo(cost), "같은 기항이라 전액이어야 한다");
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(LastShiftMaintenance.MaxPortIncome));
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(LastShiftMaintenance.PurchaseCount));
        }

        /// <summary>
        /// 냉각실 바깥 면(<c>z = +11</c>)에 문을 얹은 표본 커서 —
        /// <see cref="LastShiftPlacementCursorTests"/> 가 쓰는 것과 같은 자리다.
        /// </summary>
        private static LastShiftPlacementCursor HullAttachedCursor(int catalogIndex = 0)
        {
            var cursor = new LastShiftPlacementCursor();
            cursor.Select(catalogIndex);
            cursor.Rotate(3);

            var footprint = LastShiftModuleCatalog.At(catalogIndex).Footprint.Rotated(3);
            cursor.MoveAnchorTo(new Vector3(
                -footprint.LengthX * 0.5f, 0f,
                LastShiftShipDimensions.RoomMaxZ(LastShiftZone.Cooling)));
            return cursor;
        }
    }
}
