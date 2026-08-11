using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 상시 라인 넷(<c>AI_F_W1</c>~<c>W4</c>, 정본 §4-4). <b>진행이 아니라 상태로 뜬다</b> —
    /// 그래서 <see cref="LastShiftNarrationDirector"/> 의 순서에 안 들어가고 여기가 따로 쥔다.
    ///
    /// <b>판당 한 번씩이다.</b> 산소는 한 상태에서 단조로워서(진공에서는 줄기만 한다) 경계를
    /// 한 번 넘으면 되돌아오지 않는다 — 그래서 "이미 떴는가" 하나로 충분하고 히스테리시스가
    /// 필요 없다. 회수로 예비가 다시 차도 다시 안 뜨는 것이 <b>의도</b>다: 두 번째 경고는
    /// 첫 번째가 이미 가르친 것을 반복할 뿐이다.
    ///
    /// <b>진행 대사보다 위에 그린다.</b> 이 넷은 지금 하던 일과 무관하게 끼어드는 상태 통지라,
    /// 뜨는 동안은 안내 줄을 덮는다. 덮는 시간은 정본이 <c>W3 → W4</c> 사이에 쓴 <c>2</c>초
    /// 그대로다 — 새 숫자를 만들지 않는다.
    /// </summary>
    public static class LastShiftStandingNarration
    {
        /// <summary>한 줄이 화면에 머무는 시간. 정본이 <c>W3 → W4</c> 에 쓴 간격 그대로다.</summary>
        public static float DwellSeconds => LastShiftWakeSequence.BeatSeconds;

        private static readonly bool[] Spent = new bool[LastShiftNarrationScript.Standing.Length];
        private static int showing = -1;
        private static float shownFor;
        private static float rescueElapsed = float.PositiveInfinity;

        /// <summary>화면에 낼 상시 줄이 있는가.</summary>
        public static bool HasLine => showing >= 0;

        /// <summary>지금 떠 있는 상시 줄.</summary>
        public static LastShiftNarrationScript.Line Current =>
            LastShiftNarrationScript.Standing[Mathf.Clamp(showing, 0, Spent.Length - 1)];

        /// <summary>그 줄이 뜬 뒤 시간. 상시 라인에는 재촉이 없어 표시용이다.</summary>
        public static float LineElapsedSeconds => shownFor;

        /// <summary>
        /// 지금 줄이 쓰는 띠 그림. <b>위기색은 임계 한 줄뿐이다</b>(game-art 규격) — 회수는
        /// 실패 통보가 아니라 왕복 손실이라 주의색을 쓰고, 첫 경고도 아직 되돌릴 수 있다.
        /// </summary>
        public static LastShiftOnboardingPanelTone PanelTone =>
            HasLine && Current.Id == "AI_F_W2"
                ? LastShiftOnboardingPanelTone.Crisis
                : LastShiftOnboardingPanelTone.Warning;

        /// <summary>그 줄이 이번 판에 이미 떴는가.</summary>
        public static bool HasSpent(string id) => Spent[IndexOf(id)];

        /// <summary>
        /// 한 프레임의 상태. <paramref name="warningOutside"/> 는 <b>선외에서</b> 경고선을
        /// 넘었는가다 — 배 안에서는 예비가 안 줄어서 경고가 뜰 일이 없지만, 조건을 적어 두는
        /// 쪽이 "왜 안 뜨지" 를 나중에 안 만든다.
        /// </summary>
        public static void Observe(bool warningOutside, bool criticalOutside, float deltaTime)
        {
            if (showing >= 0)
            {
                shownFor += deltaTime;
                if (shownFor >= DwellSeconds) showing = -1;
            }

            rescueElapsed += deltaTime;
            // 회수 두 줄이 먼저다. 회수는 방금 일어난 사건이고 경고는 이미 지나간 선이다.
            if (rescueElapsed >= DwellSeconds && !Spent[IndexOf("AI_F_W4")] && Spent[IndexOf("AI_F_W3")])
            {
                Show("AI_F_W4");
                return;
            }

            // 임계가 경고보다 앞이다. 둘이 같은 프레임에 넘어가면(큰 deltaTime) 나중 것이
            // 남아야 화면에 최신 상태가 뜬다.
            if (criticalOutside && Show("AI_F_W2")) return;
            if (warningOutside) Show("AI_F_W1");
        }

        /// <summary>조항 <c>O-7</c> 자동 복귀가 일어났다.</summary>
        public static void NotifyAutoReturn()
        {
            if (!Show("AI_F_W3")) return;
            rescueElapsed = 0f;
        }

        public static void Clear()
        {
            for (var i = 0; i < Spent.Length; i++) Spent[i] = false;
            showing = -1;
            shownFor = 0f;
            rescueElapsed = float.PositiveInfinity;
        }

        private static bool Show(string id)
        {
            var index = IndexOf(id);
            if (Spent[index]) return false;
            Spent[index] = true;
            showing = index;
            shownFor = 0f;
            return true;
        }

        private static int IndexOf(string id)
        {
            var lines = LastShiftNarrationScript.Standing;
            for (var i = 0; i < lines.Length; i++)
                if (string.Equals(lines[i].Id, id, System.StringComparison.Ordinal)) return i;
            throw new System.ArgumentOutOfRangeException(nameof(id), id, "상시 라인이 아니다");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
