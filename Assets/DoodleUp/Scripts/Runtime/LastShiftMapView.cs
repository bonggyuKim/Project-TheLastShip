using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 항해 중에 여는 <b>보기 전용</b> 배 지도(<c>M</c>).
    ///
    /// <b>기항 청사진과 다른 화면이다.</b> <see cref="LastShiftPlacementUi"/>(<c>B</c>)는 골조를
    /// 놓는 편집기라 기항에서만 열리고, 배치·회전·확정 같은 조작을 들고 있다. 여기에는 그
    /// 조작이 하나도 없다 — 지금 내가 어디 있고 남들이 어디 있는지만 본다. 둘을 한 화면으로
    /// 합치면 항해 중에 건설 조작이 딸려 들어오고, 그러면 "볼 수만 있는 화면" 이 아니게 된다.
    ///
    /// <b>투영을 새로 만들지 않는다.</b> <see cref="LastShiftHullSchematic"/> 이 이미 월드
    /// <c>xz</c> → 화면 변환이고 등방 배율까지 지키고 있다. 여기서 하는 일은 그 자를 화면
    /// 가운데에 놓고, 방 사각형과 사람 표식을 그 위에 얹는 것뿐이다.
    ///
    /// <b><see cref="MonoBehaviour"/> 가 아니다.</b> 상태가 열림/닫힘 하나라, 화면 없이
    /// EditMode 에서 좌표를 잰다.
    /// </summary>
    public static class LastShiftMapView
    {
        /// <summary>지도가 먹는 화면 짧은 변의 비율. 나머지가 배경 여백이다.</summary>
        public const float ScreenFraction = 0.74f;

        /// <summary>배경 어둡기. 완전히 가리지 않는다 — 아래에서 무슨 일이 나는지는 보인다.</summary>
        public const float BackdropAlpha = 0.82f;

        /// <summary>남의 표식 한 변(px).</summary>
        public const float CrewMarkerSize = 16f;

        /// <summary>
        /// 내 표식 한 변(px). <b>남보다 크다</b> — 표식이 넷이 겹쳐 있을 때 어느 것이 나인지
        /// 크기로 먼저 갈리고, 색은 그다음이다.
        /// </summary>
        public const float SelfMarkerSize = 24f;

        /// <summary>방 테두리 두께(px).</summary>
        public const float RoomOutline = 2f;

        /// <summary>방 이름 글자 크기(px). 표식(<see cref="CrewMarkerSize"/>)보다 작다 — 이름은 배경이고 사람이 전경이다.</summary>
        public const int RoomNameFontSize = 15;

        /// <summary>방 부제 글자 크기(px). 이름보다 뚜렷하게 작아야 둘이 한 덩이로 안 읽힌다.</summary>
        public const int RoomPurposeFontSize = 11;

        /// <summary>이름 한 줄이 먹는 높이(px).</summary>
        public const float RoomNameLine = RoomNameFontSize + 4f;

        /// <summary>부제 한 줄이 먹는 높이(px).</summary>
        public const float RoomPurposeLine = RoomPurposeFontSize + 3f;

        /// <summary>글자와 테두리 사이 여유(px). 이만큼은 비어야 글자가 테두리에 붙어 보이지 않는다.</summary>
        public const float LabelPadding = 4f;

        /// <summary>
        /// 방 이름이 놓이는 자리 — <b>방 사각형의 위쪽 안쪽</b>이다.
        ///
        /// <b>한가운데가 아닌 것이 요점이다.</b> 사람 표식은 방 안 어디에나 서고 광장 한가운데에는
        /// 코어가 있어서, 이름을 중심에 두면 넷이 모인 방이나 광장에서 이름이 표식·코어에 깔린다.
        /// 위쪽 띠는 방마다 반드시 비어 있는 유일한 자리이고, 지도를 위에서 아래로 읽는 눈에는
        /// 그것이 그 사각형의 <b>머리표</b>로 읽힌다.
        ///
        /// 폭은 방 폭 그대로다 — 글자는 <see cref="UnityEngine.TextAnchor.MiddleCenter"/> 라
        /// 이름이 길어지면 좌우로 균등하게 번지고, 좁은 방에서 한쪽으로만 삐져나가지 않는다.
        /// </summary>
        public static Rect RoomNameRect(Rect room) => new(
            room.xMin,
            room.yMin + RoomOutline + LabelPadding,
            room.width,
            RoomNameLine);

        /// <summary>부제가 놓이는 자리. 이름 바로 아래 줄이다.</summary>
        public static Rect RoomPurposeRect(Rect room)
        {
            var name = RoomNameRect(room);
            return new Rect(room.xMin, name.yMax, room.width, RoomPurposeLine);
        }

        /// <summary>
        /// 이 방에 부제까지 들어가는가. <b>안 들어가면 이름만 적는다</b> — 두 줄을 억지로 넣으면
        /// 부제가 방 아래 테두리를 넘어 이웃 방 위에 겹치고, 그러면 그 부제가 어느 방 것인지
        /// 모르게 된다. 이름 한 줄만은 어느 방에서도 들어가므로 이 검사는 부제에만 붙는다.
        /// </summary>
        public static bool FitsPurpose(Rect room) =>
            room.height >= (RoomOutline + LabelPadding) * 2f + RoomNameLine + RoomPurposeLine;

        /// <summary>
        /// 중앙 승강구 이름이 놓이는 자리 — <b>코어 사각형 바로 아래</b>다.
        ///
        /// 코어는 <c>4 x 4m</c> 라 지도에서 한 변이 <c>70</c>px 남짓이고, 이름 다섯 자가 그 안에
        /// 안 들어간다. 안에 억지로 넣으면 글자가 코어 색 사각형 위에 겹쳐 둘 다 안 읽힌다 —
        /// 그래서 밖으로 내고, 폭을 코어 세 배로 잡아 좌우로 번질 자리를 준다. 아래로 내는 것은
        /// 위쪽이 광장 이름 자리이기 때문이다(<see cref="RoomNameRect"/>).
        /// </summary>
        public static Rect ShaftNameRect(Rect core) => new(
            core.center.x - core.width * 1.5f,
            core.yMax + LabelPadding,
            core.width * 3f,
            RoomNameLine);

        private static bool open;

        /// <summary>지금 지도가 떠 있는가.</summary>
        public static bool IsOpen => open;

        /// <summary>
        /// 지금 열 수 있는가. <b>도입부 중에는 못 연다</b> — 기상 연출은 화면을 암전으로 덮고
        /// 조작을 잠근 상태라, 그 위에 지도가 뜨면 잠긴 채로 지도만 보게 된다.
        /// </summary>
        public static bool CanOpen =>
            !LastShiftWakeSequence.IsRunning && !LastShiftRoomLobby.IsBlockingGameplay;

        /// <summary>
        /// <c>M</c> 한 번. 못 여는 상황에서는 <b>조용히 안 열린다</b> — 여기서 소리나 문구를
        /// 내면 도입부 대사와 겹친다.
        /// </summary>
        public static void Toggle()
        {
            if (open) { open = false; return; }
            if (!CanOpen) return;
            open = true;
        }

        public static void Close() => open = false;

        /// <summary>검사와 씬 전환이 상태를 지운다.</summary>
        public static void Clear() => open = false;

        /// <summary>
        /// 매 프레임 상태를 정리한다. 도입부가 <b>도중에</b> 시작하면(다음 기항) 열려 있던
        /// 지도를 닫는다 — <see cref="CanOpen"/> 을 열 때만 보면 그 경우를 놓친다.
        /// </summary>
        public static void Tick()
        {
            if (open && !CanOpen) open = false;
        }

        /// <summary>
        /// 지도가 놓이는 화면 사각형. <b>정사각형이다</b> — 가로세로를 화면 비율에 맞춰
        /// 늘리면 배가 넓은 화면에서만 납작해 보이고, 그러면 눈으로 잰 거리가 화면마다 다르다.
        /// </summary>
        public static Rect PlanRect(Vector2 screenSize)
        {
            var side = Mathf.Min(screenSize.x, screenSize.y) * ScreenFraction;
            return new Rect((screenSize.x - side) * 0.5f, (screenSize.y - side) * 0.5f, side, side);
        }

        /// <summary>지도를 그릴 자. 원반 전체가 들어간다.</summary>
        public static LastShiftHullSchematic Schematic(Vector2 screenSize) =>
            new(PlanRect(screenSize));

        /// <summary>표식 앞에 찍는 코의 거리(m). 지도 위에서 방 하나보다 작아야 한다.</summary>
        public const float NoseMeters = 1.6f;

        /// <summary>
        /// 내가 보는 쪽에 찍는 <b>코</b>의 화면 좌표.
        ///
        /// <b>회전각을 안 쓴다.</b> 화살표를 돌리려면 "지도가 <c>z</c> 를 뒤집는다 · GUI 는
        /// <c>y</c> 가 아래로 자란다 · 캔버스 회전은 반시계가 양수 · 월드 <c>yaw</c> 는 시계가
        /// 양수" 넷을 한 부호로 접어야 하는데, 하나만 놓쳐도 표식이 좌우 대칭으로 엉뚱한
        /// 데를 가리키고 <b>정면에서만 맞아 보여서 늦게 잡힌다</b>. 대신 월드에서 앞으로
        /// <see cref="NoseMeters"/> 나간 점을 <b>같은 자로</b> 투영한다 — 규약이 하나뿐이라
        /// 뒤집힐 자리가 없고, 좌표로 그대로 검증된다.
        ///
        /// <c>y</c> 성분은 버린다. 위를 보고 있다고 표식이 짧아지면 안 된다.
        /// </summary>
        public static Vector2 NosePoint(in LastShiftHullSchematic plan, Vector3 world, Vector3 forward)
        {
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 1e-6f) return plan.ToScreen(world);
            return plan.ToScreen(world + flat.normalized * NoseMeters);
        }

        /// <summary>표식 하나가 지도에서 차지하는 사각형. 점이 한가운데에 온다.</summary>
        public static Rect MarkerRect(Vector2 screenPoint, float size) =>
            new(screenPoint.x - size * 0.5f, screenPoint.y - size * 0.5f, size, size);

        /// <summary>
        /// 사각형 하나를 테두리 네 조각으로 쪼갠다. <b>속을 안 칠한다</b> — 방을 꽉 채우면
        /// 그 위에 얹은 표식이 배경에 묻히고, 겹친 방 경계도 안 보인다.
        /// </summary>
        public static void OutlineBands(Rect rect, float thickness, Rect[] into)
        {
            into[0] = new Rect(rect.xMin, rect.yMin, rect.width, thickness);
            into[1] = new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness);
            into[2] = new Rect(rect.xMin, rect.yMin, thickness, rect.height);
            into[3] = new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height);
        }
    }
}
