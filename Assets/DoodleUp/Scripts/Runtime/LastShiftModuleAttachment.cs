using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 배치 커서가 잡는 결함. <b><see cref="LastShiftPlacementRejection"/> 과 다른 물건이다</b> —
    /// 저쪽은 배가 성립하는가(겹침·사슬·이탈)를 묻고, 이쪽은 <b>문이 실제로 남의 벽에 닿는가</b>
    /// 를 묻는다.
    ///
    /// <b>왜 판정기에 안 넣었는가.</b> 판정기(축 D)는 정본 구획 열하나로 값이 고정돼 있고,
    /// 거기에 거부 사유를 하나 더 넣으면 그 열하나가 다시 판정 대상이 된다. 그리고 이 물음은
    /// 배치 UI 밖에서는 안 나온다 — 표에 손으로 넣는 자리(테스트·씬 빌더)는 좌표를 이미
    /// 알고 적는다. 그래서 커서 층에 둔다.
    ///
    /// <b>안 잡으면 무엇이 되는가.</b> <see cref="LastShiftBakedDoorways.Open"/> 은 문이 놓인
    /// 평면의 판 주인을 찾아 자르는데, 그 평면에 판이 없으면 아무 일도 안 하고
    /// <see cref="LastShiftBakedDoorwayReport.Clear"/> 로 센다. 즉 <b>판정을 통과하고, 씬에
    /// 서고, 문이 없는 방</b>이 된다 — 배치한 사람은 들어갈 수 없는 방을 얻고 로그는 정상이다.
    /// </summary>
    [Flags]
    public enum LastShiftPlacementFault
    {
        None = 0,

        /// <summary>부모 인덱스가 표 밖이다. 선체는 <c>-1</c> 이고 그건 정상이다.</summary>
        ParentMissing = 1 << 0,

        /// <summary>문이 자기 경계면 위에 없거나 구멍이 그 면을 넘친다.</summary>
        DoorOffOwnFace = 1 << 1,

        /// <summary>문이 놓인 평면이 부모(또는 선체 외곽)의 면이 아니다 — 허공에 문이 선다.</summary>
        DoorOffParentFace = 1 << 2,

        /// <summary>평면은 맞는데 구멍이 그 면의 폭 밖으로 나간다 — 벽 끝을 지나친 자리다.</summary>
        DoorOutsideParentSpan = 1 << 3
    }

    /// <summary>
    /// 모듈의 문이 누구의 벽에 붙는가. <b>값만 본다</b> — 씬을 안 보므로 EditMode 에서 전부 잰다.
    ///
    /// 규약은 정본 구획 열하나가 이미 지키고 있는 것 그대로다: 문이 놓인 평면이 부모의 면과
    /// 같은 좌표에 있고, 구멍이 그 면의 폭 안에 다 들어간다. 선체 직결(<c>ParentIndex == -1</c>)
    /// 은 <b>모듈일 때만</b> 선체 외곽면을 뜻한다 — 고정 구획도 선체 직결이지만 그쪽 문은 주
    /// 통로를 향하고, 그 통로 벽이 곧 선체 외곽면이라 같은 식으로 성립한다.
    /// </summary>
    public static class LastShiftModuleAttachment
    {
        private const float Epsilon = 0.001f;

        /// <summary>문 구멍 반폭. 구획 정본이 자기 면을 잴 때 쓰는 것과 같은 값이다.</summary>
        private static float OpeningHalf => LastShiftZoneDoor.OpeningWidth * 0.5f;

        /// <summary>
        /// 후보 하나의 붙임을 잰다. <paramref name="table"/> 은 지금 살아 있는 표이고,
        /// <paramref name="candidate"/> 는 그 안에 있어도 되고 아직 없어도 된다.
        /// </summary>
        public static LastShiftPlacementFault Check(
            in LastShiftCompartmentSpec candidate, IReadOnlyList<LastShiftCompartmentSpec> table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var fault = LastShiftPlacementFault.None;
            if (!LastShiftCompartments.DoorSitsOnOwnBoundary(candidate))
                fault |= LastShiftPlacementFault.DoorOffOwnFace;

            var parent = candidate.ParentIndex;
            if (parent < -1 || parent >= table.Count)
                return fault | LastShiftPlacementFault.ParentMissing;

            var alongX = candidate.DoorPlane == LastShiftDoorPlane.AlongX;

            float faceNear, faceFar, spanMin, spanMax;
            if (parent < 0)
            {
                // 선체 직결. <b>사각형 하나가 아니라 고정 공간 일곱의 면 전부다</b> — 방사형
                // 발자국은 플러스 모양이라 배를 정확히 덮는 사각형이 없고, 경계 상자를 쓰면
                // 실제로는 벽이 없는 자리(팔 사이 빈 사분면)에 붙임이 성립한다.
                return FitsHull(candidate) ? fault : fault | LastShiftPlacementFault.DoorOffParentFace;
            }

            {
                var owner = table[parent];
                faceNear = alongX ? owner.MinX : owner.MinZ;
                faceFar = alongX ? owner.MaxX : owner.MaxZ;
                spanMin = alongX ? owner.MinZ : owner.MinX;
                spanMax = alongX ? owner.MaxZ : owner.MaxX;
            }

            if (Mathf.Abs(candidate.DoorPlaneCoordinate - faceNear) > Epsilon &&
                Mathf.Abs(candidate.DoorPlaneCoordinate - faceFar) > Epsilon)
                fault |= LastShiftPlacementFault.DoorOffParentFace;

            if (candidate.DoorCenter - OpeningHalf < spanMin - Epsilon ||
                candidate.DoorCenter + OpeningHalf > spanMax + Epsilon)
                fault |= LastShiftPlacementFault.DoorOutsideParentSpan;

            return fault;
        }

        /// <summary>
        /// <b>선체 갈래가 없는</b> 같은 검사 — 부모가 반드시 표 안에 있어야 한다.
        /// 선외 거점(<see cref="LastShiftOutpost"/>)이 쓰는 문이다: 거점에는 "선체 직결" 이라는
        /// 것이 없고, <c>-1</c> 을 허용하면 <see cref="FitsHull"/> 이 <b>배의 고정 발자국</b>을
        /// 재기 시작한다 — 원반 바깥 진공에 뜬 골조가 광장 벽에 붙은 것으로 통과한다.
        /// </summary>
        public static LastShiftPlacementFault CheckWithin(
            in LastShiftCompartmentSpec candidate, IReadOnlyList<LastShiftCompartmentSpec> table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var fault = LastShiftPlacementFault.None;
            if (!LastShiftCompartments.DoorSitsOnOwnBoundary(candidate))
                fault |= LastShiftPlacementFault.DoorOffOwnFace;

            var parent = candidate.ParentIndex;
            if (parent < 0 || parent >= table.Count)
                return fault | LastShiftPlacementFault.ParentMissing;

            var alongX = candidate.DoorPlane == LastShiftDoorPlane.AlongX;
            var owner = table[parent];
            var faceNear = alongX ? owner.MinX : owner.MinZ;
            var faceFar = alongX ? owner.MaxX : owner.MaxZ;
            var spanMin = alongX ? owner.MinZ : owner.MinX;
            var spanMax = alongX ? owner.MaxZ : owner.MaxX;

            if (!OnFace(candidate.DoorPlaneCoordinate, faceNear, faceFar))
                fault |= LastShiftPlacementFault.DoorOffParentFace;

            if (!WithinSpan(candidate.DoorCenter, spanMin, spanMax))
                fault |= LastShiftPlacementFault.DoorOutsideParentSpan;

            return fault;
        }

        /// <summary>
        /// <b>선체 갈래가 없는</b> 부모 찾기. <see cref="CheckWithin"/> 과 같은 이유로 거점이 쓴다 —
        /// 못 찾으면 <paramref name="parent"/> 가 <c>-1</c> 이고, 그 값은 거점 판정에서
        /// <see cref="LastShiftPlacementRejection.ChainBroken"/> 이 된다.
        /// </summary>
        public static bool TryResolveParentWithin(
            in LastShiftCompartmentSpec candidate, IReadOnlyList<LastShiftCompartmentSpec> table, out int parent)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            for (var index = table.Count - 1; index >= 0; index--)
            {
                if (index == candidate.Index) continue;
                if (Fits(candidate, table[index])) { parent = index; return true; }
            }

            parent = -1;
            return false;
        }

        /// <summary>
        /// 후보의 문이 지금 <b>누구의</b> 벽에 얹혀 있는지 찾는다. 커서가 매 이동마다 불러서
        /// 부모를 자동으로 정하는 자리다 — 부모를 사람이 목록에서 고르게 하면 벽에 붙여 놓고
        /// 엉뚱한 부모를 고른 배치가 판정을 통과한다(사슬은 좌표를 안 본다).
        ///
        /// <b>표를 뒤에서부터 본다.</b> 나중에 붙인 모듈이 고정 구획의 면 위에 겹쳐 서는 일이
        /// 있고, 그때 문이 실제로 닿는 것은 나중 것이다.
        /// </summary>
        /// <returns>맞는 벽 주인을 찾으면 <c>true</c>. 선체 외곽이면 <paramref name="parent"/> 가 <c>-1</c> 이다.</returns>
        public static bool TryResolveParent(
            in LastShiftCompartmentSpec candidate, IReadOnlyList<LastShiftCompartmentSpec> table, out int parent)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            for (var index = table.Count - 1; index >= 0; index--)
            {
                if (index == candidate.Index) continue;
                if (Fits(candidate, table[index])) { parent = index; return true; }
            }

            if (FitsHull(candidate)) { parent = -1; return true; }

            parent = -1;
            return false;
        }

        private static bool Fits(in LastShiftCompartmentSpec candidate, in LastShiftCompartmentSpec owner)
        {
            var alongX = candidate.DoorPlane == LastShiftDoorPlane.AlongX;
            var faceNear = alongX ? owner.MinX : owner.MinZ;
            var faceFar = alongX ? owner.MaxX : owner.MaxZ;
            var spanMin = alongX ? owner.MinZ : owner.MinX;
            var spanMax = alongX ? owner.MaxZ : owner.MaxX;

            return OnFace(candidate.DoorPlaneCoordinate, faceNear, faceFar) &&
                   WithinSpan(candidate.DoorCenter, spanMin, spanMax);
        }

        /// <summary>
        /// 후보의 문이 <b>고정 구조물 어느 하나의 바깥 면</b>에 얹혀 있는가. 광장 둘레
        /// (§5.1 자유면 여섯)와 방 여섯의 바깥 면이 전부 여기 들어온다 — §7-(a) 가 확장
        /// 모듈을 "고정 방 바깥 면에" 붙인다고 적은 자리이고, 광장 둘레만 보면 그 여섯이
        /// 전부 붙일 수 없는 배가 된다.
        /// </summary>
        private static bool FitsHull(in LastShiftCompartmentSpec candidate)
        {
            var alongX = candidate.DoorPlane == LastShiftDoorPlane.AlongX;

            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                var faceNear = alongX ? footprint.MinX : footprint.MinZ;
                var faceFar = alongX ? footprint.MaxX : footprint.MaxZ;
                var spanMin = alongX ? footprint.MinZ : footprint.MinX;
                var spanMax = alongX ? footprint.MaxZ : footprint.MaxX;

                if (OnFace(candidate.DoorPlaneCoordinate, faceNear, faceFar) &&
                    WithinSpan(candidate.DoorCenter, spanMin, spanMax))
                    return true;
            }

            return false;
        }

        private static bool OnFace(float coordinate, float near, float far) =>
            Mathf.Abs(coordinate - near) <= Epsilon || Mathf.Abs(coordinate - far) <= Epsilon;

        private static bool WithinSpan(float center, float min, float max) =>
            center - OpeningHalf >= min - Epsilon && center + OpeningHalf <= max + Epsilon;
    }
}
