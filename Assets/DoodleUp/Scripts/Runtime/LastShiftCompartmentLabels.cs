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
    ///         안 들어가는 좁은 방은 <c>x</c> 를 두고 <b>문 인방 위로 올린다</b> — 문 위
    ///         이름표는 실제 배에서도 하는 것이라 읽히는 자리가 되지, 글자가 잘리는 자리가
    ///         되지 않는다.</item>
    /// </list>
    ///
    /// <b>지금 배에서 인방으로 올라가는 방은 없다.</b> 이름표가 한글 정본 명칭이 되면서
    /// (<see cref="TextOf"/>) 가장 긴 문구가 <c>서버·통신실</c> 여섯 칸이고, 그 방은 잠겨
    /// 있어 라벨 벽에 구멍이 아예 없다. 규칙 2 의 뒷단을 남겨 두는 것은 전장(§2.2 의
    /// <c>36 → 38</c>)이나 문 자리가 움직이면 다시 걸리기 때문이다 — 지금 안 쓰인다고 빼면
    /// 그때 글자가 조용히 문틀에 잘린다.
    /// </summary>
    public static class LastShiftCompartmentLabels
    {
        private const float Epsilon = 0.001f;

        /// <summary>
        /// 라틴 글자 한 칸의 폭. 씬 빌더 <c>CreateZoneLabel</c> 의 <c>TextMesh</c> 설정
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

        /// <summary>
        /// 한글 글자 한 칸의 폭. 한글 음절은 전각이라 <b>진행 폭이 곧 em</b> 이고, em 은
        /// 이 설정에서 <see cref="LineHeight"/> 와 같은 값이다 — 라틴 <c>0.55</c> em 을
        /// 그대로 쓰면 폭을 절반 가까이 작게 잡아, 문을 피해 놓은 계산이 실제로는 문에
        /// 걸친 자리를 내놓는다. 이름표가 한글이 되면서 생긴 자리다.
        /// </summary>
        public const float FullWidthGlyphAdvance = LineHeight;

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

        /// <summary>
        /// 벽에 붙는 이름표 문구. 기획 정본 <c>docs/corridor-4p-redesign-v1.md</c> §9·§14·§17.4
        /// 의 한글 명칭 그대로다.
        ///
        /// <b>enum 이름을 대문자로 펼치던 예전 규칙을 버린 자리다.</b> 그 규칙은 표시 문구를
        /// 공짜로 얻는 대신 <c>CARGOBAY</c>·<c>HYDROPONICS</c> 처럼 문서에 한 번도 안 나온
        /// 말을 화면에 올렸고, 그래서 배 안에서 읽는 이름과 기획서에서 읽는 이름이 서로
        /// 달랐다. 여기서 <b>식별자와 표시 문구를 갈라 놓는다</b> — enum·오브젝트 이름
        /// (<see cref="LastShiftCompartments.NameOf"/>)은 영문 그대로 두고, 사람이 읽는
        /// 자리만 정본 한글을 쓴다.
        ///
        /// 서버/통신실은 <c>·</c> 로 잇는다. 문서 표기는 <c>서버/통신실</c> 이지만 이 문자열은
        /// 라벨 오브젝트 이름(<c>Label_…</c>)으로도 쓰여서, <c>/</c> 가 들어가면 하이어라키
        /// 경로 구분자와 겹친다.
        /// </summary>
        public static string TextOf(LastShiftCompartment compartment) => compartment switch
        {
            LastShiftCompartment.Quarters => "숙소",
            _ => ModuleText((int)compartment)
        };

        /// <summary>
        /// 표 한 칸의 이름표 문구. 고정이면 <see cref="TextOf(LastShiftCompartment)"/> 이고,
        /// 모듈이면 그 칸이 어느 카탈로그 항목으로 섰는지를 묻는다.
        ///
        /// <b>enum 만 받는 쪽으로는 모듈 이름을 낼 수 없다.</b> 종류를 아는 것은 표가 아니라
        /// 오버레이(<see cref="LastShiftCompartments.CatalogIndexOf"/>)이고, 그 물음의 열쇠는
        /// enum 값이 아니라 표 인덱스다. 고정 표가 하나로 줄면서 이름표가 붙는 방의 대다수가
        /// 모듈이 됐으므로 이 갈래가 이제 주 경로다.
        /// </summary>
        public static string TextOf(in LastShiftCompartmentSpec spec) =>
            spec.IsFixed ? TextOf(spec.Compartment) : ModuleText(spec.Index);

        private static string ModuleText(int index)
        {
            var catalogIndex = LastShiftCompartments.CatalogIndexOf(index);
            return catalogIndex == LastShiftPlacedModule.NoCatalogIndex
                ? LastShiftCompartments.ModuleName(index)
                : LastShiftModuleCatalog.At(catalogIndex).Name;
        }

        /// <summary>
        /// 글자 폭의 합. 한글은 전각이라 라틴과 진행 폭이 다르므로 글자 수에 상수 하나를
        /// 곱하는 것으로는 안 되고, 한 글자씩 어느 폭인지를 봐야 한다.
        /// </summary>
        public static float WidthOf(string text)
        {
            var width = 0f;
            foreach (var glyph in text)
                width += IsFullWidth(glyph) ? FullWidthGlyphAdvance : GlyphAdvance;
            return width;
        }

        /// <summary>이름표 반폭. <see cref="TextAnchor.MiddleCenter"/> 라 중심에서 이만큼씩 뻗는다.</summary>
        public static float HalfWidthOf(LastShiftCompartment compartment) =>
            WidthOf(TextOf(compartment)) * 0.5f;

        /// <summary>
        /// 전각으로 그려지는 글자인가. 지금 쓰는 문구는 한글 음절과 <c>·</c> 뿐이라 한글
        /// 음절 구간만 본다 — 가운뎃점은 반각이라 라틴 쪽 폭이 오히려 넉넉한 값이다.
        /// </summary>
        private static bool IsFullWidth(char glyph) => glyph >= '가' && glyph <= '힣';

        /// <summary>
        /// 라벨이 붙는 벽에 실제로 뚫리는 구멍들의 <c>x</c>. 둘을 합친다 —
        /// 자기 문(부모가 이 면에 뚫는다)과 자식 문이다. 잠긴 문은 구멍이 아니라
        /// 메운 판이라 빼고 센다.
        ///
        /// <b>회랑 문이 셋째 항목이었는데 회랑 둘이 폐지되면서 빠졌다</b>
        /// (<c>docs/bow-cockpit-central-plaza-layout-v1.md</c> §165·§166).
        /// </summary>
        public static float[] DoorwaysOnLabelWall(in LastShiftCompartmentSpec spec)
        {
            var face = spec.MinZ;
            var result = new List<float>();

            if (spec.DoorPlane == LastShiftDoorPlane.AlongZ && spec.IsPassable &&
                Mathf.Abs(spec.DoorPlaneCoordinate - face) < Epsilon)
                result.Add(spec.DoorCenter);

            // 모듈까지 본다. 이 면에 자식이 붙으면 그 자리에 구멍이 있어야 하고, 모듈이
            // 붙은 것도 자식이다 — 안 세면 벽이 통짜로 서서 그 문이 막힌다.
            foreach (var child in LastShiftCompartments.Specs)
                if (child.ParentIndex == spec.Index && child.IsPassable &&
                    child.DoorPlane == LastShiftDoorPlane.AlongZ &&
                    Mathf.Abs(child.DoorPlaneCoordinate - face) < Epsilon)
                    result.Add(child.DoorCenter);

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

            // 글자가 안 들어가는 좁은 방은 <b>안 옮긴다</b>. 억지로 밀면 글자 끝이 방 밖으로
            // 나가 벽 없는 자리에 뜬다 — 그런 방은 x 를 두고 인방 위로 올라간다.
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
