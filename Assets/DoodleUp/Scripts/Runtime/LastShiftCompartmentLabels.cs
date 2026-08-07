using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 구획 이름표가 붙는 자리. <b>씬 빌더가 아니라 여기가 정본이다</b> — 이 자리는
    /// "글자 폭 대 문 폭" 이라는 순수한 좌표 문제이고, 그걸 에디터 코드에 두면
    /// EditMode 에서 확인할 방법이 프리팹을 굽는 것뿐이 된다.
    ///
    /// 라벨은 구획의 <b>좌현 쪽 긴 벽</b>(<c>MinZ</c>) 안쪽 면에 붙는다. 문제는 그 벽이
    /// 여러 구획에서 동시에 <b>문이 뚫리는 벽</b>이라는 것이다 — 화물칸은 관측 회랑 문이
    /// 방 중심 <c>x</c> 에 정확히 오고(<c>CargoLandingCenterX = CargoBay.CenterX</c>),
    /// 격납고·서버실·수경재배는 자기 문 <c>x</c> 가 방 중심이며, 의무실은 반 칸 어긋난
    /// 자리다. 라벨을 방 중심에 두는 예전 규칙은 이 다섯에서 전부 글자가 문 인방을
    /// 가로질렀다(아트 정본 <c>last-shift-bow-chain-dressing-v1.md</c> §7-5).
    ///
    /// 규칙은 둘이고 순서가 있다.
    /// <list type="number">
    ///   <item>방 중심이 비어 있으면 <b>안 옮긴다</b>. 여섯 구획은 지금 자리가 맞고,
    ///         "겹칠 때만 움직인다" 라야 좌표가 안 흔들린다.</item>
    ///   <item>겹치면 그 벽에서 <b>가장 넓은 빈 구간</b>의 중심으로 옮긴다. 그래도 글자가
    ///         안 들어가는 좁은 방(서버실 <c>4m</c>, 수경재배 <c>6m</c>)은 <c>x</c> 를 두고
    ///         <b>문 인방 위로 올린다</b> — 문 위 이름표는 실제 배에서도 하는 것이라
    ///         읽히는 자리가 되지, 글자가 잘리는 자리가 되지 않는다.</item>
    /// </list>
    /// </summary>
    public static class LastShiftCompartmentLabels
    {
        private const float Epsilon = 0.001f;

        /// <summary>
        /// 글자 한 칸의 폭. 씬 빌더 <c>CreateZoneLabel</c> 의 <c>TextMesh</c> 설정
        /// (<c>fontSize 48</c> · <c>characterSize 0.08</c>)에서 나오는 값이다 — 줄 높이가
        /// <c>48 x 0.08 / 10 = 0.384</c> 이고 대문자 한 칸이 그 <c>0.55</c> 배쯤이다.
        ///
        /// <b>근사인 것이 의도다.</b> 정확한 폭은 폰트 아틀라스가 있어야 나오고 그건
        /// 런타임 값이라, 여기서는 <b>넉넉한 쪽</b>으로 잡아 둔다. 실제보다 크게 잡으면
        /// 라벨이 문에서 더 멀어질 뿐이고, 작게 잡으면 글자 끝이 문에 걸린다.
        /// </summary>
        public const float GlyphAdvance = 0.21f;

        /// <summary>줄 높이. <c>fontSize 48 x characterSize 0.08 / 10</c> 이다.</summary>
        public const float LineHeight = 0.384f;

        /// <summary>글자 끝과 문 구멍 사이 여유. 이만큼은 벽이 보여야 "비켜 놓았다" 로 읽힌다.</summary>
        public const float DoorClearance = 0.25f;

        /// <summary>
        /// 인방 위로 올릴 때의 글자 중심 높이. 문 구멍 윗단과 천장 사이의 한가운데다 —
        /// 인방 띠가 <c>2.2 ~ 3.0</c> 이고 줄 높이가 <c>0.384</c> 라 위아래로 <c>0.2</c> 씩 남는다.
        /// </summary>
        public static float LintelLabelY =>
            (LastShiftZoneDoor.OpeningHeight + LastShiftCompartments.InteriorHeight) * 0.5f;

        /// <summary>겹치지 않을 때의 글자 중심 높이. 예전 값 그대로다.</summary>
        public static float WallLabelY => LastShiftCompartments.InteriorHeight - 0.75f;

        public static string TextOf(LastShiftCompartment compartment) =>
            compartment.ToString().ToUpperInvariant();

        /// <summary>이름표 반폭. <see cref="TextAnchor.MiddleCenter"/> 라 중심에서 이만큼씩 뻗는다.</summary>
        public static float HalfWidthOf(LastShiftCompartment compartment) =>
            TextOf(compartment).Length * GlyphAdvance * 0.5f;

        /// <summary>
        /// 라벨이 붙는 벽에 실제로 뚫리는 구멍들의 <c>x</c>. 셋을 합친다 —
        /// 자기 문(부모가 이 면에 뚫는다), 자식 문, 회랑 문. 잠긴 문은 구멍이 아니라
        /// 메운 판이라 빼고 센다(§15.2).
        /// </summary>
        public static float[] DoorwaysOnLabelWall(in LastShiftCompartmentSpec spec)
        {
            var face = spec.MinZ;
            var result = new List<float>();

            if (spec.DoorPlane == LastShiftDoorPlane.AlongZ && spec.IsPassable &&
                Mathf.Abs(spec.DoorPlaneCoordinate - face) < Epsilon)
                result.Add(spec.DoorCenter);

            foreach (var child in LastShiftCompartments.Specs)
                if (child.ParentIndex == (int)spec.Compartment && child.IsPassable &&
                    child.DoorPlane == LastShiftDoorPlane.AlongZ &&
                    Mathf.Abs(child.DoorPlaneCoordinate - face) < Epsilon)
                    result.Add(child.DoorCenter);

            result.AddRange(LastShiftUpperGallery.DoorwaysOn(
                spec.Compartment, LastShiftDoorPlane.AlongZ, face));
            result.AddRange(LastShiftObservationGallery.DoorwaysOn(
                spec.Compartment, LastShiftDoorPlane.AlongZ, face));

            result.Sort();
            return result.ToArray();
        }

        /// <summary>이름표 중심의 <c>x</c>.</summary>
        public static float ResolveX(in LastShiftCompartmentSpec spec) => Resolve(spec).X;

        /// <summary>이름표 중심의 <c>y</c>. 비켜 놓을 자리가 없으면 인방 위로 올라간다.</summary>
        public static float ResolveY(in LastShiftCompartmentSpec spec) =>
            Resolve(spec).OnLintel ? LintelLabelY : WallLabelY;

        /// <summary>이 구획의 이름표가 문을 피해 벽에 설 수 있는가.</summary>
        public static bool FitsBesideTheDoors(in LastShiftCompartmentSpec spec) =>
            !Resolve(spec).OnLintel;

        /// <summary>
        /// 자리 하나를 <c>x</c> 와 "인방 위인가" 로 같이 낸다. 둘을 따로 풀면 <c>x</c> 는
        /// 비켜 놓았는데 <c>y</c> 는 안 올라간(또는 그 반대) 조합이 조용히 생긴다.
        /// </summary>
        private static (float X, bool OnLintel) Resolve(in LastShiftCompartmentSpec spec)
        {
            var doorways = DoorwaysOnLabelWall(spec);
            if (doorways.Length == 0) return (spec.CenterX, false);

            var half = HalfWidthOf(spec.Compartment);
            var clear = true;
            foreach (var doorway in doorways)
                if (Mathf.Abs(doorway - spec.CenterX) < half + BlockedHalfWidth) clear = false;
            if (clear) return (spec.CenterX, false);

            var span = WidestClearSpan(spec, doorways);

            // 글자가 안 들어가는 좁은 방(서버실 4m, 수경재배 6m)은 <b>안 옮긴다</b>. 억지로
            // 밀면 글자 끝이 방 밖으로 나가 벽 없는 자리에 뜬다 — 그 방들은 x 를 두고
            // 인방 위로 올라간다.
            if (span.Width < half * 2f - Epsilon) return (spec.CenterX, true);
            return (span.Center, false);
        }

        /// <summary>문 구멍 반폭 + 여유. 글자 중심이 문 중심에서 이보다 가까우면 걸친 것이다.</summary>
        private static float BlockedHalfWidth =>
            LastShiftZoneDoor.OpeningWidth * 0.5f + DoorClearance;

        /// <summary>
        /// 벽에서 문에 안 걸리는 가장 넓은 구간의 중심. 같은 폭이 둘이면 <b>들어오는 문에
        /// 가까운 쪽</b>을 고른다 — 방에 들어서면서 읽히는 것이 이름표의 용도다. 그것마저
        /// 같으면 작은 <c>x</c> 쪽으로 정한다. 순서를 못 박아 두는 것은 값이 빌드마다 흔들리면
        /// 프리팹 diff 가 매번 나기 때문이다.
        /// </summary>
        private static (float Center, float Width) WidestClearSpan(
            in LastShiftCompartmentSpec spec, float[] doorways)
        {
            var entry = spec.DoorPlane == LastShiftDoorPlane.AlongX
                ? spec.DoorPlaneCoordinate
                : spec.DoorCenter;

            var bestCenter = spec.CenterX;
            var bestWidth = 0f;
            var bestDistance = float.MaxValue;

            var cursor = spec.MinX;
            for (var index = 0; index <= doorways.Length; index++)
            {
                var limit = index == doorways.Length ? spec.MaxX : doorways[index] - BlockedHalfWidth;
                var width = limit - cursor;
                if (width > 0f)
                {
                    var center = (cursor + limit) * 0.5f;
                    var distance = Mathf.Abs(center - entry);
                    if (width > bestWidth + Epsilon ||
                        (Mathf.Abs(width - bestWidth) <= Epsilon &&
                         (distance < bestDistance - Epsilon ||
                          (Mathf.Abs(distance - bestDistance) <= Epsilon && center < bestCenter))))
                    {
                        bestCenter = center;
                        bestWidth = width;
                        bestDistance = distance;
                    }
                }

                if (index < doorways.Length)
                    cursor = Mathf.Max(cursor, doorways[index] + BlockedHalfWidth);
            }

            return (bestCenter, bestWidth);
        }
    }
}
