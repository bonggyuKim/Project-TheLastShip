using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선외 거점의 표 — <b>배치 시스템의 두 번째 대상</b>이다
    /// (<c>docs/outboard-outpost-and-map-final-v1.md</c> §4.4).
    ///
    /// <b>왜 <see cref="LastShiftCompartments"/> 에 안 넣는가.</b> 저 표는 배다 — 압력 구역 귀속,
    /// 선체 침범, <c>RG-1</c> 이탈, 여력 원장이 전부 그 표를 입력으로 돈다. 거점을 같은 표에
    /// 담으면 진공에 떠 있는 골조가 <b>이탈 시간과 기밀 체적 계산에 들어간다.</b> §4.4 가
    /// "<c>RG-1</c> 이 안 걸린다 — 사람이 상주하지 않는다" 로 적은 것이 그 뜻이고, 안 걸리게
    /// 하는 가장 싼 방법은 재는 표를 아예 다르게 두는 것이다.
    ///
    /// <b>대신 자료형은 같은 것을 쓴다.</b> 칸은 <see cref="LastShiftCompartmentSpec"/> 이고
    /// 붙임 검사는 <see cref="LastShiftModuleAttachment"/>, 자유면은
    /// <see cref="LastShiftFreeFaces"/> 다 — §4.4 표의 마지막 줄("커서·자유면·복제: 완전히 같은
    /// 코드")이 그것이다. 판정 층은 <b>하나 적다</b>: 겹침(L0)과 접면(L1)뿐이고 구역·이탈·선체
    /// 침범이 없다.
    ///
    /// <b>주의 — <c>Index</c> 가 <c>0·1</c> 인 칸은 <see cref="LastShiftCompartmentSpec.IsFixed"/>
    /// 가 참으로 나온다.</b> 그 속성은 <see cref="LastShiftCompartments.FixedCount"/> 와 비교하는
    /// 값이라 <b>이 표에서는 뜻이 없다.</b> 그래서 여기서는 <see cref="LastShiftCompartments.NameOf"/>
    /// 도 <see cref="LastShiftCompartmentSpec.Compartment"/> 도 절대 안 읽는다 — 읽으면 골조가
    /// 숙소 이름을 달고 나온다. 이름은 <see cref="NameOf"/> 가 낸다.
    ///
    /// <b><c>0</c>번 칸은 잔해다.</b> 계류 골조가 "계류" 인 이유가 이것이고, 튜토리얼 §5.2-<c>5</c>
    /// 가 "자유면은 잔해 표면 한 면만 굵게" 로 적은 자리다. 잔해를 표의 <b>고정 뿌리</b>로 두면
    /// 자유면·접면 판정이 선체 쪽과 한 글자도 다르지 않게 돌고, 뿌리를 못 뜯는 규칙도
    /// 인덱스 하나로 선다.
    ///
    /// <see cref="LastShiftCompartments"/> 와 같은 규약으로 정적이다 — 씬 없이 EditMode 에서
    /// 전부 재고, 도메인 리로드를 끈 에디터를 위해 초기화 훅을 단다.
    /// </summary>
    public static class LastShiftOutpost
    {
        /// <summary>잔해 칸의 표 인덱스. <b>못 뜯는다</b> — 지은 것이 아니다.</summary>
        public const int AnchorIndex = 0;

        /// <summary>고정 칸 수. 잔해 하나다.</summary>
        public const int FixedCount = 1;

        /// <summary>
        /// 잔해 발자국 한 변. <see cref="LastShiftSalvage.HarvestReach"/>(<c>2.2m</c>) 안에 네 면이
        /// 전부 들어와야 뜯던 자리에서 그대로 골조를 댈 수 있고, 짝수라야 중심을 격자에 얹었을 때
        /// 네 면도 격자 위에 선다(커서가 <c>1m</c> 격자로 스냅한다).
        /// </summary>
        public const float AnchorSpan = 4f;

        /// <summary>
        /// 골조가 서는 높이. 선외 보행면 그대로다 — 거점은 배 밖이고, 나간 높이에서 그대로
        /// 걸어 들어갈 수 있어야 새 이동 동사가 안 생긴다(<see cref="LastShiftAirlock.OutsideWalkY"/>).
        /// </summary>
        public const float DeckY = LastShiftAirlock.OutsideWalkY;

        /// <summary>골조 한 층의 높이. 선체 구획과 같은 값을 써서 눈이 두 자를 안 배운다.</summary>
        public const float FrameHeight = LastShiftCompartments.InteriorHeight;

        /// <summary>
        /// 사슬 깊이 상한. <b>선체와 같은 <c>6</c> 이다</b> — §4.4 표가 "사슬 깊이 상한 <c>6</c>:
        /// 적용" 으로 적었다. 거점은 이탈 판정이 없으므로 깊이가 <b>유일한</b> 사슬 제동이다.
        /// </summary>
        public const int MaxChainDepth = LastShiftPlacementRules.MaxDoorDepth;

        private static readonly LastShiftCompartmentSpec[] fixedSpecs = { BuildAnchor() };

        private static LastShiftCompartmentSpec[] specs = fixedSpecs;

        /// <summary>칸마다 어느 카탈로그 항목으로 섰는가. <see cref="specs"/> 와 같은 순서다.</summary>
        private static int[] catalogIndices = Array.Empty<int>();

        /// <summary>칸마다 실제로 빠져나간 자재. 해제 환수가 이 값을 그대로 돌려준다.</summary>
        private static int[] paidMaterials = Array.Empty<int>();

        /// <summary>표 길이. 잔해 <c>1</c> + 세운 것.</summary>
        public static int Count => specs.Length;

        /// <summary>세운 구조물 수. <c>Count - FixedCount</c> 다.</summary>
        public static int PieceCount => specs.Length - FixedCount;

        /// <summary>표가 바뀔 때마다 오른다. 커서·도면이 자기 사본이 낡았는지 묻는 자리다.</summary>
        public static int Revision { get; private set; }

        /// <summary>지금 살아 있는 표. <c>0</c>번이 잔해이고 그 위가 배치 순이다.</summary>
        public static LastShiftCompartmentSpec[] Specs => specs;

        /// <summary>잔해 칸.</summary>
        public static LastShiftCompartmentSpec Anchor => fixedSpecs[AnchorIndex];

        public static LastShiftCompartmentSpec At(int index) => specs[index];

        /// <summary>다음 칸이 받을 인덱스. 후보 제원을 짓는 쪽이 이 값을 써야 미리 잰 것과 같은 물건이 된다.</summary>
        public static int NextIndex => specs.Length;

        /// <summary>
        /// 칸 이름. <b>선체 쪽 이름 함수를 절대 안 부른다</b> — 인덱스 규약이 다르다(클래스 주석).
        /// </summary>
        public static string NameOf(int index)
        {
            if (index == AnchorIndex) return LastShiftSalvage.FieldLabel;
            if (index < 0 || index >= specs.Length) return "?";

            var catalog = CatalogIndexOf(index);
            return catalog >= 0 && catalog < LastShiftOutpostCatalog.Count
                ? LastShiftOutpostCatalog.At(catalog).Name
                : "골조";
        }

        /// <summary>그 칸이 어느 카탈로그 항목인가. 잔해와 범위 밖은 <c>-1</c> 이다.</summary>
        public static int CatalogIndexOf(int index) =>
            index < FixedCount || index >= specs.Length ? -1 : catalogIndices[index - FixedCount];

        /// <summary>그 칸에 실제로 든 자재. 잔해와 범위 밖은 <c>0</c> 이다.</summary>
        public static int PaidFor(int index) =>
            index < FixedCount || index >= specs.Length ? 0 : paidMaterials[index - FixedCount];

        // ── 잔해 뿌리 ───────────────────────────────────────────────────────

        /// <summary>
        /// 잔해 칸을 짓는다. 좌표는 <see cref="LastShiftSalvage.FieldCenter"/> 를 <b>격자에
        /// 얹어서</b> 쓴다 — 커서가 <c>1m</c> 격자로 스냅하므로 뿌리 면이 격자에서 <c>0.5m</c>
        /// 벗어나 있으면 <b>어떤 회전으로도 계류면이 안 맞는다.</b> 그 실패는 화면에서 "왜 아무
        /// 자세도 초록이 안 되지" 로만 보인다.
        ///
        /// 계류면 규약을 채우려고 문 하나를 적어 두지만 <b>뿌리의 문은 아무도 안 본다</b> —
        /// 붙임 검사는 <c>ParentIndex</c> 가 가리키는 쪽의 <b>발자국</b>만 읽는다.
        /// </summary>
        private static LastShiftCompartmentSpec BuildAnchor()
        {
            var centerX = Mathf.Round(LastShiftSalvage.FieldCenterX / LastShiftOutpostCatalog.GridMeters) *
                          LastShiftOutpostCatalog.GridMeters;
            var centerZ = Mathf.Round(LastShiftSalvage.FieldCenterZ / LastShiftOutpostCatalog.GridMeters) *
                          LastShiftOutpostCatalog.GridMeters;
            var half = AnchorSpan * 0.5f;

            return new LastShiftCompartmentSpec(
                AnchorIndex,
                centerX - half, centerX + half, centerZ - half, centerZ + half,
                LastShiftDoorPlane.AlongX, centerX - half, centerZ,
                -1, LastShiftCompartmentAccess.Open);
        }

        // ── 판정 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 후보 하나를 <b>넣지 않고</b> 재 본다. 커서가 매 이동마다 부르는 자리다.
        ///
        /// <b>층이 둘뿐이다</b>(§4.4 표) — 겹침(L0)과 사슬(L1·깊이). 선체 침범도 구역 귀속도
        /// 이탈도 안 잰다. 사유는 선체와 <b>같은 플래그 형</b>을 쓴다:
        /// <see cref="LastShiftPlacementCommands.Reason"/> 이 문구를 하나로 내야 두 탭이 같은
        /// 문장으로 거부를 적는다.
        /// </summary>
        public static LastShiftPlacementRejection Judge(in LastShiftCompartmentSpec candidate)
        {
            var rejection = LastShiftPlacementRejection.None;

            for (var index = 0; index < specs.Length; index++)
            {
                if (index == candidate.Index) continue;
                if (!LastShiftCompartments.VolumesOverlap(candidate, specs[index])) continue;

                rejection |= LastShiftPlacementRejection.OverlapsPlacement;
                break;
            }

            var depth = ChainDepth(candidate);
            if (depth < 0) rejection |= LastShiftPlacementRejection.ChainBroken;
            else if (depth > MaxChainDepth) rejection |= LastShiftPlacementRejection.ChainTooDeep;

            return rejection;
        }

        /// <summary>
        /// 잔해까지 몇 칸을 거슬러 올라가는가. 잔해에 직접 계류하면 <c>1</c> 이고, 사슬이 표 밖을
        /// 가리키거나 순환이면 <c>-1</c> 이다 — <see cref="LastShiftCompartments.DoorDepth"/> 와
        /// 같은 규약이라 화면이 두 탭에서 같은 수를 읽는다.
        /// </summary>
        public static int ChainDepth(in LastShiftCompartmentSpec candidate)
        {
            var parent = candidate.ParentIndex;
            for (var depth = 1; depth <= specs.Length + 1; depth++)
            {
                // 뿌리는 잔해다. 거점에는 "선체 직결"(-1)이 없다 — 허공에 뜬 골조가 그것이다.
                if (parent == AnchorIndex) return depth;
                if (parent < 0 || parent >= specs.Length) return -1;
                parent = specs[parent].ParentIndex;
            }

            return -1;
        }

        // ── 등록·해제 ───────────────────────────────────────────────────────

        /// <summary>
        /// 배치 하나를 확정한다. <b>판정을 통과해야만 표에 들어간다</b> — 선체 표와 같은 규약이고,
        /// 판정을 건너뛰는 등록 문을 안 두는 것이 요지다.
        ///
        /// <paramref name="paid"/> 는 실제로 빠져나간 자재다. <b>여기서 원장을 안 만진다</b> —
        /// 지불 순서(넣은 것을 보고 나서 문다)는 <see cref="LastShiftOutpostCommands"/> 의 규약이고,
        /// 표가 원장을 직접 부르면 그 순서가 두 곳에 생긴다.
        /// </summary>
        public static bool TryRegister(
            in LastShiftCompartmentSpec candidate, int catalogIndex, int paid,
            out int index, out LastShiftPlacementRejection rejection)
        {
            if (candidate.Index != specs.Length)
                throw new ArgumentException(
                    $"outpost spec index must be {nameof(NextIndex)}({specs.Length}) but was {candidate.Index}",
                    nameof(candidate));

            index = -1;
            rejection = Judge(candidate);
            if (rejection != LastShiftPlacementRejection.None) return false;

            var grown = new LastShiftCompartmentSpec[specs.Length + 1];
            Array.Copy(specs, grown, specs.Length);
            grown[specs.Length] = candidate;

            var catalogs = new int[catalogIndices.Length + 1];
            Array.Copy(catalogIndices, catalogs, catalogIndices.Length);
            catalogs[catalogIndices.Length] = catalogIndex;

            var paidList = new int[paidMaterials.Length + 1];
            Array.Copy(paidMaterials, paidList, paidMaterials.Length);
            paidList[paidMaterials.Length] = paid;

            index = specs.Length;
            specs = grown;
            catalogIndices = catalogs;
            paidMaterials = paidList;
            Revision++;
            return true;
        }

        /// <summary>
        /// 마지막에 세운 것을 뺀다. <b>꼬리만 뺀다</b> — 표가 덧붙이기 전용이라 꼬리에는 자식이
        /// 있을 수 없고, 그러면 선체 표가 문 하나로 푸는 재색인
        /// (<see cref="LastShiftCompartments.TryRemove"/>)이 여기서는 통째로 필요 없다.
        /// </summary>
        /// <param name="refunded">돌아온 자재. 못 뺐으면 <c>0</c> 이다.</param>
        public static bool TryRemoveLast(out int refunded)
        {
            refunded = 0;
            if (PieceCount <= 0) return false;

            refunded = paidMaterials[paidMaterials.Length - 1];

            if (specs.Length - 1 == FixedCount)
            {
                specs = fixedSpecs;
                catalogIndices = Array.Empty<int>();
                paidMaterials = Array.Empty<int>();
                Revision++;
                return true;
            }

            var shrunk = new LastShiftCompartmentSpec[specs.Length - 1];
            Array.Copy(specs, shrunk, shrunk.Length);

            var catalogs = new int[catalogIndices.Length - 1];
            Array.Copy(catalogIndices, catalogs, catalogs.Length);

            var paidList = new int[paidMaterials.Length - 1];
            Array.Copy(paidMaterials, paidList, paidList.Length);

            specs = shrunk;
            catalogIndices = catalogs;
            paidMaterials = paidList;
            Revision++;
            return true;
        }

        /// <summary>세운 것을 전부 뺀다. 잔해 뿌리는 남는다.</summary>
        public static void ClearPieces()
        {
            if (ReferenceEquals(specs, fixedSpecs)) return;

            specs = fixedSpecs;
            catalogIndices = Array.Empty<int>();
            paidMaterials = Array.Empty<int>();
            Revision++;
        }

        /// <summary>
        /// 항해를 시작한다 — 거점이 없던 상태로 돌아간다.
        /// <see cref="LastShiftMaterials.BeginVoyage"/> 와 같은 자리·같은 이유다.
        ///
        /// <b>기항을 건너서는 안 지운다.</b> 조항 <c>O-5</c> 의 "정박한 동안만 존재한다" 는
        /// <b>연출</b>이고(같은 조항이 "배가 움직이는 동안 거점은 안 따라간다" 로 적는다), 조항
        /// <c>O-6</c> 이 골조를 항해 단위 지급으로 두므로 표는 항해 내내 산다. 기항마다 지우면
        /// §4.3 의 확장 넷이 살 이유가 통째로 사라진다.
        /// </summary>
        public static void BeginVoyage() => ClearPieces();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => ClearPieces();
    }
}
