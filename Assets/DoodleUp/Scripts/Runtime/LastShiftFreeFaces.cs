using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선체 도면에 굵게 긋는 <b>자유면</b> 한 구간 — 지금 배에서 새 방을 붙일 수 있는 벽면이다.
    ///
    /// <b>값만 든다.</b> 화면 좌표도 색도 안 갖는다 — 도면이 이것을 그리는 자리이고
    /// (<see cref="LastShiftHullSchematic"/>), 그리는 쪽을 바꾼다고 자유면이 달라지면 안 된다.
    /// </summary>
    public readonly struct LastShiftFreeFace
    {
        public LastShiftFreeFace(
            int ownerIndex, LastShiftModuleFace face, float planeCoordinate, float spanMin, float spanMax)
        {
            OwnerIndex = ownerIndex;
            Face = face;
            PlaneCoordinate = planeCoordinate;
            SpanMin = spanMin;
            SpanMax = spanMax;
        }

        /// <summary>이 면의 주인. <see cref="LastShiftFreeFaces.HullOwner"/>(<c>-1</c>)면 선체 외곽이다.</summary>
        public int OwnerIndex { get; }

        /// <summary>주인의 네 면 중 어느 쪽인가. 바깥 방향이 여기서 나온다.</summary>
        public LastShiftModuleFace Face { get; }

        /// <summary>면이 놓인 평면. <see cref="OnXFace"/> 면 <c>x</c>, 아니면 <c>z</c> 다.</summary>
        public float PlaneCoordinate { get; }

        /// <summary>면의 자유축 위 구간. <see cref="OnXFace"/> 면 <c>z</c>, 아니면 <c>x</c> 다.</summary>
        public float SpanMin { get; }

        public float SpanMax { get; }

        /// <summary>평면의 법선이 <c>x</c> 축인가. 후보의 <c>DoorPlane</c> 과 같은 규약이다.</summary>
        public bool OnXFace => Face == LastShiftModuleFace.MinX || Face == LastShiftModuleFace.MaxX;

        /// <summary>이 면에 붙는 후보가 가져야 할 문 평면.</summary>
        public LastShiftDoorPlane DoorPlane =>
            OnXFace ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ;

        /// <summary>주인 바깥으로 나가는 방향. <c>-1</c> 또는 <c>+1</c> 이다.</summary>
        public float Outward =>
            Face == LastShiftModuleFace.MinX || Face == LastShiftModuleFace.MinZ ? -1f : 1f;

        public float Length => SpanMax - SpanMin;

        /// <summary>문 중심이 놓일 수 있는 가장 작은 값. 구멍 반폭만큼 안으로 들어와 있다.</summary>
        public float DoorCenterMin => SpanMin + LastShiftZoneDoor.OpeningWidth * 0.5f;

        public float DoorCenterMax => SpanMax - LastShiftZoneDoor.OpeningWidth * 0.5f;

        public Vector3 Start => OnXFace
            ? new Vector3(PlaneCoordinate, 0f, SpanMin)
            : new Vector3(SpanMin, 0f, PlaneCoordinate);

        public Vector3 End => OnXFace
            ? new Vector3(PlaneCoordinate, 0f, SpanMax)
            : new Vector3(SpanMax, 0f, PlaneCoordinate);

        /// <summary>이 면 위에 문 중심 하나가 통째로 얹히는가.</summary>
        public bool Accepts(float doorCenter) =>
            doorCenter >= DoorCenterMin - 0.001f && doorCenter <= DoorCenterMax + 0.001f;
    }

    /// <summary>
    /// 자유면 계산. <b>선체 도면 개편에서 유일하게 새로 드는 계산이다</b> —
    /// <c>docs/core-four-rooms-and-hull-schematic-v1.md</c> §4.6 이 나머지 넷(도면 그리기·판정
    /// 재사용·네트워크·버리는 것)을 <c>0</c> 으로 적고 이 항목만 "없던 코드" 로 남겼다.
    ///
    /// <b>왜 이것이 개편의 실질 이득 전부인가</b>(§4.3 표 1행). 배를 위에서 보여주는 것만으로는
    /// 도면이 아니라 그림이다. <c>1</c>인칭이 절대 못 보여주던 것은 배 그림이 아니라 <b>붙일 수
    /// 있는 면</b>이고, 그게 없으면 플레이어는 도면을 켜 놓고도 여전히 벽을 눈으로 훑는다.
    ///
    /// <b>규칙을 새로 만들지 않는다.</b> 여기서 나오는 것은 판정이 아니라 <b>후보 구간</b>이다 —
    /// 실제 가부는 <see cref="LastShiftCompartments.Judge"/> 와
    /// <see cref="LastShiftModuleAttachment.Check"/> 가 확정 순간에 그대로 낸다. 자유면이
    /// 넉넉하게 나오는 쪽으로 틀리는 것이 의도다: 여기서 미리 막으면 판정기가 통과시키는 자리를
    /// 화면이 숨기게 되고, 그 어긋남은 "왜 여긴 안 되지" 가 아니라 <b>아예 시도조차 안 하는</b>
    /// 형태로만 드러나서 아무도 못 찾는다.
    ///
    /// <b>비용.</b> 면 <c>4(N+1)</c> 개 × 막는 것 <c>N+1</c> 개라 <c>O(N²)</c> 인데
    /// <c>N &lt; 20</c> 이므로 산술 수천 회다 — 매 프레임 돌려도 되지만 표
    /// <see cref="LastShiftCompartments.Revision"/> 이 안 바뀌면 결과도 안 바뀌므로 부르는 쪽이
    /// 캐시한다.
    /// </summary>
    public static class LastShiftFreeFaces
    {
        /// <summary>선체 외곽을 가리키는 주인 번호. 후보의 <c>ParentIndex</c> 규약과 같은 값이다.</summary>
        public const int HullOwner = -1;

        /// <summary>
        /// 이보다 짧은 구간은 안 그린다. <b>문 구멍 폭이다</b> — 그보다 좁은 벽면은 문을 못 내므로
        /// 붙일 수 있는 면이 아니다(<see cref="LastShiftModuleAttachment"/> 의
        /// <c>DoorOutsideParentSpan</c> 이 확정 순간에 같은 값으로 물린다).
        /// </summary>
        public const float MinimumRunMeters = LastShiftZoneDoor.OpeningWidth;

        /// <summary>
        /// 면 바깥으로 이만큼 안이 비어 있어야 자유면이다. <b>같은 <c>1.6m</c> 를 쓰는 것이
        /// 의도다</b> — 폭과 깊이에 서로 다른 자를 대면 "왜 이 선은 굵은데 아무것도 안 들어가는가"
        /// 를 두 수로 설명해야 한다. 여기서 묻는 것은 "문 하나가 실제로 열릴 만한 정사각 여유가
        /// 그 면 밖에 있는가" 하나다.
        /// </summary>
        public const float ClearanceMeters = LastShiftZoneDoor.OpeningWidth;

        private const float Epsilon = 0.001f;

        /// <summary>
        /// 막힌 구간 임시 저장소. <b>정적인 것이 의도다</b> — 도면이 표가 바뀔 때마다 부르는데,
        /// 매번 리스트를 새로 잡으면 기항마다 쓰레기가 는다. Unity 메인 스레드 전용이다.
        /// </summary>
        private static readonly List<Vector2> Blocked = new();

        private static readonly Comparison<Vector2> ByStart = (a, b) => a.x.CompareTo(b.x);

        /// <summary>지금 살아 있는 표에서 잰다. 도면이 부르는 문이다.</summary>
        public static void Collect(List<LastShiftFreeFace> into) =>
            Collect(LastShiftCompartments.Specs, into);

        /// <summary>
        /// 표 하나에서 자유면을 전부 모은다.
        /// </summary>
        /// <param name="table">고정 구획 + 배치된 모듈. 선체 외곽은 표에 없고 여기서 더한다.</param>
        /// <param name="into">결과를 담을 리스트. 비우고 채운다.</param>
        /// <param name="clearance">면 바깥으로 비어 있어야 하는 깊이.</param>
        /// <param name="minimumRun">이보다 짧은 구간은 버린다.</param>
        public static void Collect(
            IReadOnlyList<LastShiftCompartmentSpec> table, List<LastShiftFreeFace> into,
            float clearance = ClearanceMeters, float minimumRun = MinimumRunMeters)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (into == null) throw new ArgumentNullException(nameof(into));

            into.Clear();

            for (var owner = HullOwner; owner < table.Count; owner++)
            {
                Bounds(table, owner, out var minX, out var maxX, out var minZ, out var maxZ);

                for (var face = 0; face < 4; face++)
                {
                    var kind = (LastShiftModuleFace)face;
                    var onXFace = kind == LastShiftModuleFace.MinX || kind == LastShiftModuleFace.MaxX;
                    var outward = kind == LastShiftModuleFace.MinX || kind == LastShiftModuleFace.MinZ ? -1f : 1f;

                    var plane = kind switch
                    {
                        LastShiftModuleFace.MinX => minX,
                        LastShiftModuleFace.MaxX => maxX,
                        LastShiftModuleFace.MinZ => minZ,
                        _ => maxZ
                    };

                    var spanMin = onXFace ? minZ : minX;
                    var spanMax = onXFace ? maxZ : maxX;

                    CollectBlocked(table, owner, onXFace, plane, outward, spanMin, spanMax, clearance);
                    EmitRuns(into, owner, kind, plane, spanMin, spanMax, minimumRun);
                }
            }
        }

        /// <summary>
        /// 면 바깥 <paramref name="clearance"/> 띠를 실제로 물고 있는 구조물의 구간을 모은다.
        ///
        /// <b>맞닿은 것만 빼는 것이 아니다.</b> 띠 안에 걸치기만 해도 뺀다 — 벽에서 <c>0.5m</c>
        /// 떨어져 선 방은 그 사이에 아무것도 못 들어가므로, 맞닿음만 보면 실제로는 막힌 면이
        /// 굵은 선으로 남는다.
        /// </summary>
        private static void CollectBlocked(
            IReadOnlyList<LastShiftCompartmentSpec> table, int owner,
            bool onXFace, float plane, float outward, float spanMin, float spanMax, float clearance)
        {
            Blocked.Clear();

            var stripLow = outward > 0f ? plane : plane - clearance;
            var stripHigh = outward > 0f ? plane + clearance : plane;

            for (var other = HullOwner; other < table.Count; other++)
            {
                if (other == owner) continue;

                Bounds(table, other, out var otherMinX, out var otherMaxX, out var otherMinZ, out var otherMaxZ);

                var depthMin = onXFace ? otherMinX : otherMinZ;
                var depthMax = onXFace ? otherMaxX : otherMaxZ;
                if (depthMin >= stripHigh - Epsilon || depthMax <= stripLow + Epsilon) continue;

                var low = Mathf.Max(spanMin, onXFace ? otherMinZ : otherMinX);
                var high = Mathf.Min(spanMax, onXFace ? otherMaxZ : otherMaxX);
                if (high - low <= Epsilon) continue;

                Blocked.Add(new Vector2(low, high));
            }

            Blocked.Sort(ByStart);
        }

        /// <summary>막힌 구간을 뺀 나머지 중 <paramref name="minimumRun"/> 이상만 남긴다.</summary>
        private static void EmitRuns(
            List<LastShiftFreeFace> into, int owner, LastShiftModuleFace face,
            float plane, float spanMin, float spanMax, float minimumRun)
        {
            var open = spanMin;
            for (var index = 0; index < Blocked.Count; index++)
            {
                var interval = Blocked[index];
                if (interval.x - open >= minimumRun - Epsilon)
                    into.Add(new LastShiftFreeFace(owner, face, plane, open, interval.x));
                if (interval.y > open) open = interval.y;
            }

            if (spanMax - open >= minimumRun - Epsilon)
                into.Add(new LastShiftFreeFace(owner, face, plane, open, spanMax));
        }

        /// <summary>
        /// 표 인덱스 하나의 발자국. <paramref name="index"/> 가 <see cref="HullOwner"/> 면
        /// <b>선체 내부 영역</b>이다 — <see cref="LastShiftModuleAttachment"/> 가 선체 직결을
        /// 판정할 때 쓰는 것과 같은 사각형이라 두 자가 안 갈린다.
        /// </summary>
        private static void Bounds(
            IReadOnlyList<LastShiftCompartmentSpec> table, int index,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            if (index < 0)
            {
                minX = -LastShiftShipDimensions.HalfLength;
                maxX = LastShiftShipDimensions.HalfLength;
                minZ = -LastShiftShipDimensions.HalfWidth;
                maxZ = LastShiftShipDimensions.HalfWidth;
                return;
            }

            var spec = table[index];
            minX = spec.MinX;
            maxX = spec.MaxX;
            minZ = spec.MinZ;
            maxZ = spec.MaxZ;
        }
    }
}
