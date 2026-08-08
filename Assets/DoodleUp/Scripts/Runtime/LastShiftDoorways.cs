using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 문 한 짝. 평면 하나 위의 구간이라 <see cref="LastShiftDoorPlane"/> 과 같은 규약을 쓴다 —
    /// <see cref="Plane"/> 이 놓인 축이 <see cref="PlaneAxis"/> 이고, 그 평면 위 자유 좌표가
    /// <see cref="Center"/> 다.
    /// </summary>
    public readonly struct LastShiftDoorway
    {
        public LastShiftDoorway(string name, LastShiftDoorPlane planeAxis, float plane, float center)
        {
            Name = name;
            PlaneAxis = planeAxis;
            Plane = plane;
            Center = center;
        }

        /// <summary>로그에 찍히는 이름. 위반이 어느 문인지 좌표가 아니라 이름으로 읽혀야 한다.</summary>
        public string Name { get; }

        public LastShiftDoorPlane PlaneAxis { get; }

        public float Plane { get; }

        public float Center { get; }

        public float MinFree => Center - LastShiftZoneDoor.OpeningWidth * 0.5f;

        public float MaxFree => Center + LastShiftZoneDoor.OpeningWidth * 0.5f;

        /// <summary>문을 지나는 축의 좌표를 뽑는다. 문틀 평면의 법선 방향이다.</summary>
        public float ThroughOf(Vector3 point) =>
            PlaneAxis == LastShiftDoorPlane.AlongX ? point.x : point.z;

        /// <summary>문틀 평면 위 자유축 좌표를 뽑는다.</summary>
        public float FreeOf(Vector3 point) =>
            PlaneAxis == LastShiftDoorPlane.AlongX ? point.z : point.x;

        /// <summary>문을 지나는 축의 소품 크기.</summary>
        public float ThroughSizeOf(Vector3 size) =>
            PlaneAxis == LastShiftDoorPlane.AlongX ? size.x : size.z;

        public float FreeSizeOf(Vector3 size) =>
            PlaneAxis == LastShiftDoorPlane.AlongX ? size.z : size.x;
    }

    /// <summary>
    /// 승무원이 실제로 지나다니는 문 전부의 정본. 좌표는 하나도 새로 안 적고
    /// <see cref="LastShiftShipDimensions"/>·<see cref="LastShiftCompartments"/>·
    /// <see cref="LastShiftObservationGallery"/> 에서 뽑는다.
    ///
    /// <b>이 목록이 따로 있는 이유는 드레싱이다.</b> 문이 뚫린 자리는 벽 빌더가 알고 있지만,
    /// 그 앞에 소품을 놓아도 되는지는 아무도 안 보고 있었다 — 2026-08-08 플레이테스트에서
    /// 냉각실 <c>CrateStack_Aft</c> 가 냉각실↔통로B 문을 통째로 막았고, 선수·선미 끝벽 문도
    /// 소품에 눌려 사람이 통과할 수 없는 폭까지 좁아져 있었다(카드 955678c7). 문이 어디
    /// 있는지를 여기 한 번 모아 두어야 <see cref="LastShiftDressingRules"/> 가 그것을 기계로
    /// 잡는다.
    ///
    /// <b>잠긴 문은 안 넣는다.</b> 그레이박스에서 잠긴 구획의 문은 구멍이 아니라 메운 판이라
    /// (§15.2) 그 앞을 비워 둘 이유가 없다 — 넣으면 서버실·수경재배·의무실 벽 앞이 전부
    /// 통행 예약 구역이 되어, 정작 열린 문 앞을 비우라는 요구가 소음에 묻힌다.
    /// </summary>
    public static class LastShiftDoorways
    {
        /// <summary>
        /// 문 앞뒤로 비어 있어야 하는 깊이. 문틀 평면에 딱 붙은 것만 보면 문설주에서
        /// <c>0.1m</c> 떨어져 선 상자가 검사를 통과하고, 실플레이에서는 그것도 똑같이 막는다.
        /// 승무원 지름(<c>0.56m</c>)보다 넓게 잡아 <b>문 앞에서 몸을 돌릴 수 있는 만큼</b>을 뺀다.
        /// </summary>
        public const float ApproachDepth = 0.8f;

        /// <summary>
        /// 문 구멍에 남아 있어야 하는 연속 통행 폭. 물리적 하한은 승무원 캡슐 지름
        /// <c>0.56m</c> 에 <c>CharacterController</c> 스킨 두 겹 <c>0.16m</c> 을 더한 <c>0.72m</c>
        /// 지만, 그 폭은 조작 오차 없이 정중앙으로 밀어야 지나가진다. 2026-08-08 플레이테스트에서
        /// <c>0.95m</c> 로 좁아져 있던 선수 끝벽 문이 "막혀 있다" 로 보고됐으므로 그보다 위에 둔다 —
        /// 문 구멍 폭 <c>1.6m</c> 의 약 <c>2/3</c> 다.
        /// </summary>
        public const float MinClearWidth = 1.1f;

        private static readonly LastShiftDoorway[] all = BuildAll();

        public static LastShiftDoorway[] All => all;

        private static LastShiftDoorway[] BuildAll()
        {
            var result = new List<LastShiftDoorway>();

            // 개구부 다섯. 문이 달리든(압력 경계 셋) 안 달리든 승무원이 지나는 자리는 같다.
            for (var opening = 0; opening < LastShiftShipDimensions.OpeningCount; opening++)
                result.Add(new LastShiftDoorway($"Opening_{opening}", LastShiftDoorPlane.AlongX,
                    LastShiftShipDimensions.OpeningX(opening),
                    LastShiftShipDimensions.OpeningCenterZ(opening)));

            // 구획 문. 잠긴 구획은 구멍이 아니므로 뺀다.
            //
            // <b>고정 구획만이다.</b> 이 표는 정적 생성자가 한 번 짓고 그 뒤로 안 다시 짓는데,
            // 그 시점에는 배치된 모듈이 하나도 없다 — 여기서 Specs 를 훑으면 "모듈까지 본다" 는
            // 모양만 갖추고 실제로는 언제나 열하나만 담긴다. 모듈 문이 필요해지면 이 표를
            // Revision 으로 다시 짓는 것이 먼저다(free-placement-compartment-table-v1.md §6).
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                if (!spec.IsPassable) continue;
                result.Add(new LastShiftDoorway(LastShiftCompartments.NameOf(spec.Compartment),
                    spec.DoorPlane, spec.DoorPlaneCoordinate, spec.DoorCenter));
            }

            // 관측 회랑 양 끝. 화물칸 쪽은 구획 면에, 조종석 쪽은 선체 좌현 벽에 뚫린다.
            if (LastShiftObservationGallery.CockpitDoorwayIsOpen)
                result.Add(new LastShiftDoorway("ObservationGallery_Cockpit", LastShiftDoorPlane.AlongZ,
                    LastShiftObservationGallery.CockpitDoorwayFaceZ,
                    LastShiftObservationGallery.CockpitLandingCenterX));

            foreach (var x in LastShiftObservationGallery.DoorwaysOn(LastShiftCompartment.CargoBay,
                         LastShiftDoorPlane.AlongZ, LastShiftObservationGallery.CargoDoorwayFaceZ))
                result.Add(new LastShiftDoorway("ObservationGallery_Cargo", LastShiftDoorPlane.AlongZ,
                    LastShiftObservationGallery.CargoDoorwayFaceZ, x));

            return result.ToArray();
        }

        /// <summary>
        /// 이 상자가 문 앞 통행 구역을 침범하는가. 침범하면 구멍의 어느 구간을 무는지를
        /// <paramref name="span"/> 으로 돌려준다.
        ///
        /// <b>바닥에서 뜬 것은 안 센다.</b> 케이블 트레이나 인방 장식은 문 구멍 위를 지나가고,
        /// 그것까지 세면 천장 배선이 전부 문을 막은 것이 된다.
        /// </summary>
        public static bool Intrudes(in LastShiftDoorway door, Vector3 center, Vector3 size,
            float bottomY, out Vector2 span)
        {
            span = default;
            if (bottomY > WalkUnderHeight) return false;
            if (bottomY + size.y <= WalkOverHeight) return false;

            var through = door.ThroughOf(center);
            var throughHalf = door.ThroughSizeOf(size) * 0.5f;
            if (through - throughHalf > door.Plane + ApproachDepth) return false;
            if (through + throughHalf < door.Plane - ApproachDepth) return false;

            var free = door.FreeOf(center);
            var freeHalf = door.FreeSizeOf(size) * 0.5f;
            var min = Mathf.Max(free - freeHalf, door.MinFree);
            var max = Mathf.Min(free + freeHalf, door.MaxFree);
            if (max - min <= 0f) return false;

            span = new Vector2(min, max);
            return true;
        }

        /// <summary>
        /// 밑면이 이 높이보다 위면 문 앞을 막은 것으로 안 본다. 승무원 키(<c>1.7m</c>)와
        /// 문 인방(<c>2.2m</c>) 사이라 그 아래로 지나갈 수 있다.
        /// </summary>
        public const float WalkUnderHeight = LastShiftShipPhysics.StandingHeight;

        /// <summary>
        /// 윗면이 이 높이 아래면 밟고 지나간다. 갑판 띠·격자·서리 데칼이 여기 걸리는데,
        /// 그것들을 세면 문 앞 갑판 표시가 전부 통행 방해가 되어 검사가 소음이 된다.
        /// <see cref="LastShiftBypassDuct.StepHeight"/> 와 같은 값이고 근거도 같다 —
        /// <c>CharacterController.stepOffset</c> 이다.
        /// </summary>
        public const float WalkOverHeight = LastShiftBypassDuct.StepHeight;

        /// <summary>
        /// 무는 구간들을 합치고 구멍에 남는 <b>가장 긴 연속</b> 통행 폭을 돌려준다.
        /// 남은 <b>합</b>이 아니라 한 토막인 것이 요점이다 — 상자가 구멍 한가운데를 물면
        /// 양쪽에 반씩 남지만 사람은 어느 쪽으로도 못 지나간다.
        ///
        /// <paramref name="spans"/> 는 정렬돼 있지 않아도 된다.
        /// </summary>
        public static float ClearWidth(in LastShiftDoorway door, List<Vector2> spans)
        {
            if (spans == null || spans.Count == 0) return LastShiftZoneDoor.OpeningWidth;

            spans.Sort((a, b) => a.x.CompareTo(b.x));
            var widest = 0f;
            var cursor = door.MinFree;
            foreach (var span in spans)
            {
                if (span.x > cursor) widest = Mathf.Max(widest, span.x - cursor);
                cursor = Mathf.Max(cursor, span.y);
            }

            return Mathf.Max(widest, door.MaxFree - cursor);
        }
    }
}
