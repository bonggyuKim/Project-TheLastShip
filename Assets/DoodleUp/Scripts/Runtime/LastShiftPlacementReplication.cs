using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 배치된 모듈 한 칸을 <b>값으로</b> 나른 것. 씬 참조도 핸들도 없다 —
    /// <see cref="LastShiftPlacedModule"/> 이 매 tick 경로에서 씬을 안 보는 것과 같은 이유이고,
    /// 여기서는 한 가지가 더 있다: <b>핸들은 프로세스 안에서만 뜻이 있다.</b>
    ///
    /// <b>표의 한 줄을 그대로 싣는다</b>(<see cref="LastShiftCompartmentSpec"/> + 종류 + 값).
    /// 요청은 <c>카탈로그 번호 · 회전 · 모서리</c> 만 싣지만(서버가 치수를 자기 카탈로그에서
    /// 읽어야 하므로) <b>복제는 반대다</b> — 서버가 확정한 제원을 클라이언트가 그대로 받아야
    /// 두 화면의 벽이 같은 자리에 선다. 클라이언트에서 후보를 다시 지어 내면 부동소수 반올림
    /// 하나로 방이 <c>1cm</c> 어긋나고, 그 어긋남은 문틀을 뚫을 때만 드러난다.
    /// </summary>
    [Serializable]
    public struct LastShiftPlacementRecord : INetworkSerializable, IEquatable<LastShiftPlacementRecord>
    {
        /// <summary>원장 기록이 없는 칸. 표에 직접 등록된 것(테스트·조립 경로)이 이 값이다.</summary>
        public const int NoPurchase = -1;

        public float MinX;
        public float MaxX;
        public float MinZ;
        public float MaxZ;

        /// <summary>문이 놓인 평면과 그 좌표. <see cref="LastShiftDoorPlane"/> 를 바이트로 싣는다.</summary>
        public byte DoorPlane;
        public float DoorPlaneCoordinate;
        public float DoorCenter;

        /// <summary>붙은 상대. <c>-1</c> 이면 선체다.</summary>
        public int ParentIndex;

        /// <summary><see cref="LastShiftCompartmentAccess"/> 를 바이트로 싣는다.</summary>
        public byte Access;

        /// <summary>어느 카탈로그 항목인가. <b>효과가 이 값에 매달린다</b>(<see cref="LastShiftModuleEffects"/>).</summary>
        public int CatalogIndex;

        /// <summary>실제로 빠져나간 여력. <see cref="NoPurchase"/> 면 원장에 기록이 없는 칸이다.</summary>
        public int Cost;

        /// <summary>세운 기항 회차. 환수액이 여기 걸린다(조항 M-4).</summary>
        public int PortIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MinX);
            serializer.SerializeValue(ref MaxX);
            serializer.SerializeValue(ref MinZ);
            serializer.SerializeValue(ref MaxZ);
            serializer.SerializeValue(ref DoorPlane);
            serializer.SerializeValue(ref DoorPlaneCoordinate);
            serializer.SerializeValue(ref DoorCenter);
            serializer.SerializeValue(ref ParentIndex);
            serializer.SerializeValue(ref Access);
            serializer.SerializeValue(ref CatalogIndex);
            serializer.SerializeValue(ref Cost);
            serializer.SerializeValue(ref PortIndex);
        }

        public bool Equals(LastShiftPlacementRecord other) =>
            MinX.Equals(other.MinX) && MaxX.Equals(other.MaxX) &&
            MinZ.Equals(other.MinZ) && MaxZ.Equals(other.MaxZ) &&
            DoorPlane == other.DoorPlane &&
            DoorPlaneCoordinate.Equals(other.DoorPlaneCoordinate) &&
            DoorCenter.Equals(other.DoorCenter) &&
            ParentIndex == other.ParentIndex &&
            Access == other.Access &&
            CatalogIndex == other.CatalogIndex &&
            Cost == other.Cost &&
            PortIndex == other.PortIndex;

        public override bool Equals(object obj) => obj is LastShiftPlacementRecord other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            MinX, MinZ, DoorPlaneCoordinate, ParentIndex, CatalogIndex, Cost, PortIndex);
    }

    /// <summary>
    /// 배치 화면이 읽는 <b>숫자 전부</b> — 여력 원장 · 항해 진행 · 커서 주인. 모듈 표는 여기
    /// 없다(그건 <see cref="LastShiftPlacementRecord"/> 목록이다).
    ///
    /// <b>셋을 한 칸에 묶는 것이 의도다.</b> 잔액과 기항 회차와 커서 주인은 같은 사건에서 같이
    /// 움직인다 — 구간이 끝나면 회차가 오르고 수입이 들어오고, 커서를 든 사람이 나가면 그 자리에서
    /// 주인이 풀린다. 따로 실으면 한 프레임 동안 "기항 2 인데 잔액은 기항 1 의 것" 인 화면이 서고,
    /// 그 한 프레임에 확정을 누르면 어느 회차의 값으로 무는지가 갈린다.
    /// </summary>
    [Serializable]
    public struct LastShiftPlacementLedger : INetworkSerializable, IEquatable<LastShiftPlacementLedger>
    {
        public int Balance;
        public int PortIndex;
        public int LastPortIncome;
        public int LastCarriedOver;

        public int SegmentIndex;

        /// <summary><see cref="LastShiftSegmentTransition"/> 를 바이트로 싣는다.</summary>
        public byte Transition;
        public int LatchCount;
        public bool VoyageRunning;

        /// <summary>
        /// 커서를 잡고 있는 클라이언트. <c>-1</c> 이 <see cref="LastShiftPlacementAuthority.NoHolder"/> 다.
        /// <c>ulong</c> 클라이언트 번호를 <c>int</c> 로 좁히는 것은 권위 클래스가 <c>int</c> 로
        /// 서 있기 때문이고, NGO 가 <c>0</c> 부터 차례로 나눠 주는 번호라 좁혀도 겹치지 않는다.
        /// </summary>
        public int CursorHolder;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Balance);
            serializer.SerializeValue(ref PortIndex);
            serializer.SerializeValue(ref LastPortIncome);
            serializer.SerializeValue(ref LastCarriedOver);
            serializer.SerializeValue(ref SegmentIndex);
            serializer.SerializeValue(ref Transition);
            serializer.SerializeValue(ref LatchCount);
            serializer.SerializeValue(ref VoyageRunning);
            serializer.SerializeValue(ref CursorHolder);
        }

        public bool Equals(LastShiftPlacementLedger other) =>
            Balance == other.Balance &&
            PortIndex == other.PortIndex &&
            LastPortIncome == other.LastPortIncome &&
            LastCarriedOver == other.LastCarriedOver &&
            SegmentIndex == other.SegmentIndex &&
            Transition == other.Transition &&
            LatchCount == other.LatchCount &&
            VoyageRunning == other.VoyageRunning &&
            CursorHolder == other.CursorHolder;

        public override bool Equals(object obj) => obj is LastShiftPlacementLedger other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            Balance, PortIndex, SegmentIndex, Transition, LatchCount, VoyageRunning, CursorHolder);
    }

    /// <summary>
    /// 자유 배치 상태를 <b>값으로 담고 값에서 되세우는</b> 자리. 네트워크를 하나도 모른다 —
    /// <see cref="LastShiftNetworkPlacement"/> 가 이 값을 실어 나르고, 여기는 담고 푸는 일만 한다.
    ///
    /// <b>왜 갈랐는가.</b> 배치 사슬은 정적 전역 넷에 걸쳐 있다(표 · 구역 오버레이 · 원장 ·
    /// 항해). 그 넷을 왕복시키는 코드가 <see cref="NetworkBehaviour"/> 안에 있으면 씬과 세션
    /// 없이는 한 줄도 못 잰다. 여기 있는 것은 전부 EditMode 에서 도는 순수 함수라,
    /// <b>"서버가 담은 것을 클라이언트가 풀면 같은 배가 서는가" 를 세션 없이 검사한다.</b>
    ///
    /// <b>씬은 안 세운다.</b> <see cref="Apply"/> 는 표까지만 되돌리고
    /// <see cref="LastShiftModuleAssembler.Rebuild"/> 는 부르는 쪽 몫이다 — 조립은 씬을 알고,
    /// 표를 여러 번 받는 동안 씬을 매번 세우면 방 전체를 지웠다 세우는 값을 그만큼 문다.
    /// </summary>
    public static class LastShiftPlacementReplication
    {
        /// <summary>
        /// 지금 표·원장을 값 목록으로 담는다. <b>서버 전용 경로다</b> — 담는 순간의 표가 정본이다.
        /// </summary>
        public static void Capture(List<LastShiftPlacementRecord> into)
        {
            if (into == null) return;
            into.Clear();

            for (var index = LastShiftCompartments.FixedCount; index < LastShiftCompartments.Count; index++)
            {
                var spec = LastShiftCompartments.At(index);
                var slot = index - LastShiftCompartments.FixedCount;
                var hasPurchase = LastShiftMaintenance.TryGetPurchase(slot, out var purchase);

                into.Add(new LastShiftPlacementRecord
                {
                    MinX = spec.MinX,
                    MaxX = spec.MaxX,
                    MinZ = spec.MinZ,
                    MaxZ = spec.MaxZ,
                    DoorPlane = (byte)spec.DoorPlane,
                    DoorPlaneCoordinate = spec.DoorPlaneCoordinate,
                    DoorCenter = spec.DoorCenter,
                    ParentIndex = spec.ParentIndex,
                    Access = (byte)spec.Access,
                    CatalogIndex = LastShiftCompartments.CatalogIndexOf(index),
                    Cost = hasPurchase ? purchase.Cost : LastShiftPlacementRecord.NoPurchase,
                    PortIndex = hasPurchase ? purchase.PortIndex : 0
                });
            }
        }

        /// <summary>
        /// 값 목록에서 표·구역 오버레이·원장 기록을 되세운다. <b>클라이언트 전용이고 표를 먼저
        /// 비운다</b> — 덧붙이면 서버가 뺀 모듈이 클라이언트에만 남는다.
        ///
        /// <b>판정을 건너뛰지 않는다.</b> <see cref="LastShiftCompartments.TryRegister"/> 로
        /// 다시 넣으므로 서버가 통과시킨 배치는 클라이언트에서도 통과해야 한다 — 규칙이 순수
        /// 함수이고 표가 같으니 통과하는 것이 정상이고, <b>안 통과하면 그것이 곧 어긋남의
        /// 신호다</b>. 판정을 우회하는 등록 문을 새로 내면 그 신호가 사라진다.
        ///
        /// 원장 기록은 <see cref="LastShiftPlacementRecord.NoPurchase"/> 를 만나면 멈춘다 —
        /// 기록은 언제나 모듈 자리의 앞부분이고(<see cref="LastShiftMaintenance.TryChargeModule"/>
        /// 가 꼬리에만 붙인다), 중간에 구멍을 메우면 그 뒤 환수가 전부 한 칸씩 밀린다.
        /// </summary>
        /// <returns>모든 줄이 표에 들어갔는가. 하나라도 물리면 <c>false</c> 다.</returns>
        public static bool Apply(IReadOnlyList<LastShiftPlacementRecord> records)
        {
            LastShiftCompartments.ClearModules();
            if (records == null || records.Count == 0)
            {
                LastShiftMaintenance.ApplyNetworkPurchases(null);
                return true;
            }

            var purchases = new List<LastShiftMaintenancePurchase>(records.Count);
            var complete = true;

            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                var spec = new LastShiftCompartmentSpec(
                    LastShiftCompartments.NextModuleIndex,
                    record.MinX, record.MaxX, record.MinZ, record.MaxZ,
                    (LastShiftDoorPlane)record.DoorPlane, record.DoorPlaneCoordinate, record.DoorCenter,
                    record.ParentIndex, (LastShiftCompartmentAccess)record.Access);

                if (!LastShiftCompartments.TryRegister(spec, out _, out var verdict, record.CatalogIndex))
                {
                    UnityEngine.Debug.LogError(
                        $"[LAST_SHIFT_PLACEMENT_SYNC] row={index} result=REJECT " +
                        $"reason={verdict.Rejection} zone={verdict.Zone} parent={record.ParentIndex}");
                    complete = false;
                    break;
                }

                if (record.Cost == LastShiftPlacementRecord.NoPurchase) continue;
                if (purchases.Count != index) continue;
                purchases.Add(new LastShiftMaintenancePurchase(record.CatalogIndex, record.Cost, record.PortIndex));
            }

            LastShiftMaintenance.ApplyNetworkPurchases(purchases);
            return complete;
        }

        /// <summary>원장·항해·커서 주인을 값 하나로 담는다.</summary>
        public static LastShiftPlacementLedger CaptureLedger() => new()
        {
            Balance = LastShiftMaintenance.Balance,
            PortIndex = LastShiftMaintenance.PortIndex,
            LastPortIncome = LastShiftMaintenance.LastPortIncome,
            LastCarriedOver = LastShiftMaintenance.LastCarriedOver,
            SegmentIndex = LastShiftVoyage.SegmentIndex,
            Transition = (byte)LastShiftVoyage.LastTransition,
            LatchCount = LastShiftVoyage.LastLatchCount,
            VoyageRunning = LastShiftVoyage.IsRunning,
            CursorHolder = LastShiftPlacementAuthority.HolderId
        };

        /// <summary>
        /// 그 값을 클라이언트에 앉힌다. <b>산수를 하나도 안 한다</b> — 수입식도 전이표도 여기서
        /// 다시 돌리지 않는다(각 <c>ApplyNetworkState</c> 주석).
        /// </summary>
        public static void ApplyLedger(in LastShiftPlacementLedger ledger)
        {
            LastShiftMaintenance.ApplyNetworkLedger(
                ledger.Balance, ledger.PortIndex, ledger.LastPortIncome, ledger.LastCarriedOver);
            LastShiftVoyage.ApplyNetworkState(
                ledger.SegmentIndex, (LastShiftSegmentTransition)ledger.Transition,
                ledger.LatchCount, ledger.VoyageRunning);
            LastShiftPlacementAuthority.ApplyNetworkHolder(ledger.CursorHolder);
        }
    }
}
