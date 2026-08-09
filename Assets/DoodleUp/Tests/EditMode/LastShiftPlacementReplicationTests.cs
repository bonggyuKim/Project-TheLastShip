using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 자유 배치의 <b>네트워크 왕복</b>을 잰다 — 서버가 담은 값으로 클라이언트가 같은 배를
    /// 세우는가.
    ///
    /// <b>세션 없이 잰다.</b> 재는 것은 <see cref="LastShiftPlacementReplication"/> 이고 그건
    /// 네트워크를 하나도 모르는 순수 함수라, host/client 프로세스를 띄우지 않고도
    /// "담은 것 == 푼 것" 을 전부 볼 수 있다. 실제 RPC·권위 배선은 PlayMode 쪽이 잰다.
    ///
    /// <b>여기서 지키는 것은 넷이다.</b>
    /// <list type="number">
    /// <item><b>표가 그대로 선다.</b> 벽 좌표 하나가 반올림으로 어긋나면 두 화면의 문틀이 다른
    /// 자리에 뚫린다.</item>
    /// <item><b>종류가 같이 건너간다.</b> 안 실으면 클라이언트의 방은 발자국만 같고 효과가
    /// 하나도 안 붙는다(<see cref="LastShiftModuleEffects"/>).</item>
    /// <item><b>구역 귀속이 판정값 그대로다</b>(조항 F-1). 좌표로 다시 계산하면 문을 닫아도
    /// 격리가 안 되는 배가 클라이언트에만 선다.</item>
    /// <item><b>원장이 값도 회차도 같이 온다.</b> 잔액만 맞추면 환수액이 갈린다(조항 M-4).</item>
    /// </list>
    /// </summary>
    public sealed class LastShiftPlacementReplicationTests
    {
        private const float Tolerance = 0.0005f;

        [SetUp]
        public void ClearBefore() => ClearAll();

        [TearDown]
        public void ClearAfter() => ClearAll();

        private static void ClearAll()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftVoyage.Clear();
            LastShiftPlacementAuthority.Revoke();
        }

        // ── 표 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 서버가 놓은 두 칸을 담아서 빈 클라이언트에 풀면 <b>같은 표</b>가 선다.
        /// 사슬(부모 인덱스)까지 같아야 한다 — 부모가 어긋나면 이탈 계산이 그때부터 갈린다.
        /// </summary>
        [Test]
        public void CapturedTableRebuildsIdenticallyOnAClient()
        {
            var server = PlaceServerChain();

            var records = new List<LastShiftPlacementRecord>();
            LastShiftPlacementReplication.Capture(records);
            Assert.That(records, Has.Count.EqualTo(2));

            WipeAsIfFreshClient();
            Assert.That(LastShiftPlacementReplication.Apply(records), Is.True,
                "서버가 통과시킨 배치가 클라이언트 판정에 물렸다 — 규칙이 갈렸다는 뜻이다.");

            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(2));
            for (var slot = 0; slot < server.Count; slot++)
            {
                var index = LastShiftCompartments.FixedCount + slot;
                var actual = LastShiftCompartments.At(index);
                var expected = server[slot];

                Assert.That(actual.MinX, Is.EqualTo(expected.MinX).Within(Tolerance));
                Assert.That(actual.MaxX, Is.EqualTo(expected.MaxX).Within(Tolerance));
                Assert.That(actual.MinZ, Is.EqualTo(expected.MinZ).Within(Tolerance));
                Assert.That(actual.MaxZ, Is.EqualTo(expected.MaxZ).Within(Tolerance));
                Assert.That(actual.DoorPlane, Is.EqualTo(expected.DoorPlane));
                Assert.That(actual.DoorPlaneCoordinate, Is.EqualTo(expected.DoorPlaneCoordinate).Within(Tolerance));
                Assert.That(actual.DoorCenter, Is.EqualTo(expected.DoorCenter).Within(Tolerance));
                Assert.That(actual.ParentIndex, Is.EqualTo(expected.ParentIndex),
                    "사슬이 어긋났다 — 이탈·깊이 계산이 그때부터 갈린다.");
            }
        }

        /// <summary>
        /// 종류와 구역이 같이 건너간다. <b>구역은 판정이 정한 값이지 좌표에서 다시 나온 값이
        /// 아니다</b>(조항 F-1) — 그래서 오버레이에 물어본 답이 서버와 같아야 한다.
        /// </summary>
        [Test]
        public void CatalogKindAndZoneSurviveTheRoundTrip()
        {
            PlaceServerChain();

            var zones = new List<LastShiftZone>();
            var kinds = new List<int>();
            for (var index = LastShiftCompartments.FixedCount; index < LastShiftCompartments.Count; index++)
            {
                kinds.Add(LastShiftCompartments.CatalogIndexOf(index));
                var spec = LastShiftCompartments.At(index);
                Assert.That(
                    LastShiftPlacedModules.TryResolve(new Vector3(spec.CenterX, 0f, spec.CenterZ), out var zone),
                    Is.True);
                zones.Add(zone);
            }

            var records = new List<LastShiftPlacementRecord>();
            LastShiftPlacementReplication.Capture(records);

            WipeAsIfFreshClient();
            Assert.That(LastShiftPlacementReplication.Apply(records), Is.True);

            for (var slot = 0; slot < kinds.Count; slot++)
            {
                var index = LastShiftCompartments.FixedCount + slot;
                Assert.That(LastShiftCompartments.CatalogIndexOf(index), Is.EqualTo(kinds[slot]),
                    "종류가 안 건너갔다 — 클라이언트 방은 발자국만 같고 효과가 없다.");

                var spec = LastShiftCompartments.At(index);
                Assert.That(
                    LastShiftPlacedModules.TryResolve(new Vector3(spec.CenterX, 0f, spec.CenterZ), out var zone),
                    Is.True);
                Assert.That(zone, Is.EqualTo(zones[slot]));
            }
        }

        /// <summary>
        /// 서버가 뺀 모듈은 클라이언트에서도 빠진다. <b>덧붙이지 않고 비우고 다시 세우기</b>가
        /// 그 조건이다 — 덧붙이면 뺀 방이 클라이언트에만 남아, 그 자리를 다시 사려 할 때만
        /// 겹침으로 드러난다.
        /// </summary>
        [Test]
        public void ApplyDropsWhatTheServerRemoved()
        {
            PlaceServerChain();

            var records = new List<LastShiftPlacementRecord>();
            LastShiftPlacementReplication.Capture(records);
            Assert.That(LastShiftPlacementReplication.Apply(records), Is.True);
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(2));

            // 서버가 잎을 뺐다 — 꼬리 한 줄이 사라진 목록이 그대로 온다.
            records.RemoveAt(records.Count - 1);
            Assert.That(LastShiftPlacementReplication.Apply(records), Is.True);

            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(1));
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.EqualTo(1),
                "표에서는 빠졌는데 구역 오버레이에 남았다 — 압력이 그 방에서만 다르게 답한다.");
        }

        /// <summary>빈 목록은 배를 고정 구획으로 되돌린다. 새 항해가 그 상태다.</summary>
        [Test]
        public void AnEmptyListStandsTheFixedShip()
        {
            PlaceServerChain();

            Assert.That(LastShiftPlacementReplication.Apply(new List<LastShiftPlacementRecord>()), Is.True);

            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.Zero);
            Assert.That(LastShiftMaintenance.PurchaseCount, Is.Zero);
        }

        // ── 원장 · 항해 · 커서 ──────────────────────────────────────────────

        /// <summary>
        /// 값과 <b>회차</b>가 같이 온다. 회차를 안 실으면 환수액이 갈린다 — 같은 기항이면
        /// 전액, 출항한 뒤면 절반이다(조항 M-4).
        /// </summary>
        [Test]
        public void PurchasesCarryCostAndPortSoRefundsAgree()
        {
            PlaceServerChain();
            var serverRefund = RefundOfLastModule();

            var records = new List<LastShiftPlacementRecord>();
            LastShiftPlacementReplication.Capture(records);
            var ledger = LastShiftPlacementReplication.CaptureLedger();

            WipeAsIfFreshClient();
            LastShiftPlacementReplication.Apply(records);
            LastShiftPlacementReplication.ApplyLedger(ledger);

            Assert.That(LastShiftMaintenance.PurchaseCount, Is.EqualTo(2));
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(ledger.Balance));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(ledger.PortIndex));
            Assert.That(RefundOfLastModule(), Is.EqualTo(serverRefund),
                "환수액이 갈렸다 — 잔액만 맞추고 회차를 안 실은 상태다.");
        }

        /// <summary>
        /// 항해 진행과 커서 주인이 값 하나로 건너간다. <b>클라이언트는 구간 판정을 다시 내지
        /// 않는다</b> — 다시 내면 <see cref="LastShiftMaintenance.ArriveAtPort"/> 가 한 번 더
        /// 돌아 잔액이 서버보다 커진다.
        /// </summary>
        [Test]
        public void VoyageProgressAndCursorHolderCrossAsValues()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 3);
            LastShiftPlacementAuthority.Revoke();
            Assert.That(LastShiftPlacementAuthority.TryClaim(2), Is.True);

            var ledger = LastShiftPlacementReplication.CaptureLedger();
            var balance = LastShiftMaintenance.Balance;
            var port = LastShiftMaintenance.PortIndex;

            LastShiftVoyage.Clear();
            LastShiftPlacementAuthority.Revoke();

            LastShiftPlacementReplication.ApplyLedger(ledger);

            Assert.That(LastShiftVoyage.SegmentIndex, Is.EqualTo(ledger.SegmentIndex));
            Assert.That(LastShiftVoyage.LastTransition, Is.EqualTo(LastShiftSegmentTransition.ToPort));
            Assert.That(LastShiftVoyage.LastLatchCount, Is.EqualTo(3));
            Assert.That(LastShiftVoyage.IsRunning, Is.True);
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(balance),
                "클라이언트에서 수입이 한 번 더 들어왔다.");
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(port));
            Assert.That(LastShiftPlacementAuthority.HolderId, Is.EqualTo(2));
        }

        /// <summary>
        /// 커서를 놓은 상태도 값이다. <c>-1</c> 이 건너가지 않으면 주인이 나간 뒤에도 남들
        /// 화면에서 커서가 잡힌 채로 남는다.
        /// </summary>
        [Test]
        public void ReleasedCursorCrossesAsNoHolder()
        {
            Assert.That(LastShiftPlacementAuthority.TryClaim(1), Is.True);
            LastShiftPlacementAuthority.Revoke();

            var ledger = LastShiftPlacementReplication.CaptureLedger();
            LastShiftPlacementAuthority.TryClaim(3);
            LastShiftPlacementReplication.ApplyLedger(ledger);

            Assert.That(LastShiftPlacementAuthority.IsHeld, Is.False);
            Assert.That(LastShiftPlacementAuthority.HolderId,
                Is.EqualTo(LastShiftPlacementAuthority.NoHolder));
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 서버가 기항 하나를 열고 두 칸을 잇는다. 표본은 축 B·E 테스트가 쓰는 냉각실 우현
        /// 사슬 그대로다 — 배치 규칙과 무관한 사유로 이 파일이 빨개지지 않게 한다.
        /// </summary>
        private static List<LastShiftCompartmentSpec> PlaceServerChain()
        {
            LastShiftMaintenance.Clear();
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);

            var first = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, 0, -1));
            Charge(0, LastShiftModuleCatalog.Corridor);
            var second = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, 1, first));
            Charge(1, LastShiftModuleCatalog.Radiator);

            return new List<LastShiftCompartmentSpec>
            {
                LastShiftCompartments.At(first), LastShiftCompartments.At(second)
            };
        }

        private static int Register(in LastShiftCompartmentSpec candidate)
        {
            var catalogIndex = LastShiftCompartments.ModuleCount == 0
                ? LastShiftModuleCatalog.Corridor
                : LastShiftModuleCatalog.Radiator;
            Assert.That(
                LastShiftCompartments.TryRegister(candidate, out var index, out var verdict, catalogIndex),
                Is.True, $"표본이 판정기에 물린다({verdict.Rejection}) — 복제와 무관한 사유다.");
            return index;
        }

        private static void Charge(int slot, int catalogIndex) =>
            Assert.That(LastShiftMaintenance.TryChargeModule(slot, catalogIndex), Is.True,
                "표본이 여력을 못 문다 — 기항 수입보다 비싼 표본이다.");

        private static int RefundOfLastModule()
        {
            Assert.That(LastShiftMaintenance.TryGetPurchase(
                LastShiftMaintenance.PurchaseCount - 1, out var purchase), Is.True);
            return LastShiftMaintenance.RefundFor(purchase);
        }

        /// <summary>합류 직전의 클라이언트 — 표도 원장도 비어 있다.</summary>
        private static void WipeAsIfFreshClient()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftMaintenance.Clear();
        }

        /// <summary>축 B·E 테스트와 같은 표본. 냉각실 우현 벽에서 선수 쪽으로 길게 눕는 칸.</summary>
        private static LastShiftCompartmentSpec CoolingSpur(int index, int link, int parentIndex)
        {
            const float roomDepth = 2f;
            var doorX = LastShiftShipDimensions.ZoneCenterX(LastShiftZone.Cooling);
            var minZ = LastShiftShipDimensions.SideWallZ + link * roomDepth;

            return new LastShiftCompartmentSpec(
                index, doorX - 9f, doorX + 1f, minZ, minZ + roomDepth,
                LastShiftDoorPlane.AlongZ, minZ, doorX,
                parentIndex, LastShiftCompartmentAccess.Open);
        }
    }
}
