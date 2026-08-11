using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 순회 블록의 진행(정본 §4-2 · 조항 <c>N-3</c>). <b>이 블록만 순서가 없다</b> —
    /// 다음 방이 아닌 문으로 들어가도 <b>그 방 줄이 그 자리에서</b> 나오고, 그 방이 목록에서
    /// 빠진다. 나머지 블록은 한 줄기라 <see cref="LastShiftNarrationDirector"/> 가 밀지만,
    /// 여기는 커서 하나로 못 민다: 건너뛴 방으로 <b>되돌아올 수 있어야</b> 하기 때문이다.
    ///
    /// 자유로운 것은 <b>방 넷의 순서</b>뿐이다. 방 안에서는 "무슨 방인지 → 무엇을 하는 방인지"
    /// 두 줄의 순서가 그대로 있고, 여는 두 줄(<c>AI_T_01</c>·<c>02</c>)과 닫는 한 줄
    /// (<c>AI_T_11</c>)도 자리가 고정이다.
    ///
    /// <b>방이 빠지는 조건은 "들어왔다" 하나다.</b> 설비 앞까지 가야 빠지게 하면, 들어왔다가
    /// 그냥 나온 방 때문에 안내가 영영 안 닫힌다 — 설비 줄은 재촉이 데려가지 강제하지 않는다.
    /// </summary>
    public static class LastShiftPatrolNarration
    {
        /// <summary>방 하나가 갖는 두 줄. 앞이 진입, 뒤가 그 방의 설비다.</summary>
        private readonly struct Room
        {
            public Room(LastShiftPlazaSpace space, string entryId, string fixtureId)
            {
                Space = space;
                EntryId = entryId;
                FixtureId = fixtureId;
            }

            public LastShiftPlazaSpace Space { get; }
            public string EntryId { get; }
            public string FixtureId { get; }
        }

        /// <summary>
        /// 대본이 적은 순서(조종석 → 전력실 → 산소실 → 냉각실)다. <b>권장 동선일 뿐</b>
        /// 강제가 아니다 — 재촉이 이 순서로 데려가고, 안 따라가도 안내는 따라온다.
        /// </summary>
        private static readonly Room[] Rooms =
        {
            new(LastShiftPlazaSpace.CockpitRoom, "AI_T_03", "AI_T_04"),
            new(LastShiftPlazaSpace.PowerRoom, "AI_T_05", "AI_T_06"),
            new(LastShiftPlazaSpace.LifeSupportRoom, "AI_T_07", "AI_T_08"),
            new(LastShiftPlazaSpace.CoolingRoom, "AI_T_09", "AI_T_10")
        };

        private static readonly bool[] Played = new bool[LastShiftNarrationScript.Patrol.Length];
        private static int showing = -1;
        private static float lineElapsed;

        public static bool IsRunning { get; private set; }

        public static bool HasLine => IsRunning && showing >= 0;

        public static LastShiftNarrationScript.Line Current =>
            LastShiftNarrationScript.Patrol[Mathf.Clamp(showing, 0, Played.Length - 1)];

        public static float LineElapsedSeconds => lineElapsed;

        /// <summary>안내가 닫혔는가 — <c>AI_T_11</c> 까지 나왔다.</summary>
        public static bool IsComplete => IsRunning && Played[IndexOf("AI_T_11")];

        /// <summary>
        /// 설비 앞까지 간 방 수. <b>순회 교육 내용의 절반이 이 넷에 있다</b> — 방 이름만 듣고
        /// 지나가면 배전반·게이지·냉각통·전면 스크린 이야기를 하나도 안 듣는다.
        ///
        /// 판정선은 이것을 못 잡는다(늘어짐만 잡는다). 그래서 안내가 닫히는 자리에서 한 줄
        /// 남긴다 — <b>진행을 막지는 않는다.</b> 설비 접근을 필수로 걸면 들어왔다 그냥 나온
        /// 방 하나에 온보딩이 서고, 그건 이 지표가 재려는 것보다 훨씬 무겁다.
        /// </summary>
        public static int FixturesReached
        {
            get
            {
                var reached = 0;
                foreach (var room in Rooms)
                    if (Played[IndexOf(room.FixtureId)]) reached++;
                return reached;
            }
        }

        /// <summary>세는 방 수. 지표의 분모다.</summary>
        public static int RoomCount => Rooms.Length;

        /// <summary>아직 안 들어간 방 수. 로그와 검사가 읽는다.</summary>
        public static int RoomsLeft
        {
            get
            {
                var left = 0;
                foreach (var room in Rooms)
                    if (!Played[IndexOf(room.EntryId)]) left++;
                return left;
            }
        }

        public static void Begin()
        {
            IsRunning = true;
            for (var i = 0; i < Played.Length; i++) Played[i] = false;
            showing = -1;
            lineElapsed = 0f;
        }

        /// <summary>
        /// 광장에 있다. <b>첫 진입이면 여는 줄</b>이고, 방을 다 돌았으면 <b>닫는 줄</b>이다 —
        /// 같은 신호가 블록의 처음과 끝을 둘 다 낸다(정본 표 그대로).
        /// </summary>
        public static void NotifyInPlaza()
        {
            if (!IsRunning) return;
            if (Play("AI_T_01")) return;
            if (RoomsLeft != 0 || !Play("AI_T_11")) return;

            // 안내가 닫히는 자리에서 <b>한 번만</b> 남긴다. 프레임마다 찍는 계기가 아니라
            // 판 하나의 결과이고, game-balance 가 판정선으로 못 잡는 축을 여기서 읽는다.
            var missed = new System.Text.StringBuilder();
            foreach (var room in Rooms)
                if (!Played[IndexOf(room.FixtureId)])
                    missed.Append(missed.Length > 0 ? "," : string.Empty).Append(room.FixtureId);
            Debug.Log($"[LAST_SHIFT_PATROL] action=CLOSE fixtures={FixturesReached}/{RoomCount}" +
                      $" missed={(missed.Length > 0 ? missed.ToString() : "none")}");
        }

        /// <summary><c>AI_T_02B</c> — 코어 사거리. 순서 무관 · 판당 한 번이다.</summary>
        public static void NotifyNearCore()
        {
            if (!IsRunning || !Played[IndexOf("AI_T_01")]) return;
            Play("AI_T_02B");
        }

        /// <summary>어느 방에 들어왔다. <b>순서를 안 본다</b> — 그 방 줄이 그 자리에서 나온다.</summary>
        public static void NotifyRoomEntered(LastShiftPlazaSpace space)
        {
            if (!IsRunning || !Played[IndexOf("AI_T_01")]) return;
            foreach (var room in Rooms)
                if (room.Space == space) Play(room.EntryId);
        }

        /// <summary>그 방 설비 앞이다. <b>그 방에 들어온 뒤에만</b> 받는다.</summary>
        public static void NotifyAtFixture(LastShiftPlazaSpace space)
        {
            if (!IsRunning) return;
            foreach (var room in Rooms)
            {
                if (room.Space != space) continue;
                if (Played[IndexOf(room.EntryId)]) Play(room.FixtureId);
            }
        }

        /// <summary>
        /// "앞줄 후 <c>N</c>초" 형을 민다. 대본에서 <b>바로 다음 줄</b>이 시간 형일 때만
        /// 흐르므로, 방을 건너뛰어도 엉뚱한 줄이 시간으로 따라붙지 않는다.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (!IsRunning || showing < 0) return;
            lineElapsed += deltaTime;

            var next = showing + 1;
            if (next >= Played.Length || Played[next]) return;
            var line = LastShiftNarrationScript.Patrol[next];
            if (!line.IsAutomatic || lineElapsed < line.AutoAfterSeconds) return;
            Play(line.Id);
        }

        public static void Clear()
        {
            IsRunning = false;
            for (var i = 0; i < Played.Length; i++) Played[i] = false;
            showing = -1;
            lineElapsed = 0f;
        }

        private static bool Play(string id)
        {
            var index = IndexOf(id);
            if (Played[index]) return false;
            Played[index] = true;
            showing = index;
            // 줄이 바뀌면 재촉 시계도 여기서 다시 선다 — 앞줄의 재촉이 새 줄 위에 남지 않는다.
            lineElapsed = 0f;
            return true;
        }

        private static int IndexOf(string id)
        {
            var lines = LastShiftNarrationScript.Patrol;
            for (var i = 0; i < lines.Length; i++)
                if (string.Equals(lines[i].Id, id, System.StringComparison.Ordinal)) return i;
            throw new System.ArgumentOutOfRangeException(nameof(id), id, "순회 라인이 아니다");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
