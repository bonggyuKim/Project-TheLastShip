using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 기상 뒤의 내레이션 진행. <b>정본 순서를 그대로 강제한다</b> —
    /// <see cref="LastShiftNarrationScript.Directed"/> 의 다음 줄이 아닌 사건은 무시한다.
    ///
    /// <b>순서를 코드가 지키는 것이 요점이다.</b> 온보딩은 한 줄기이고, 사건 신호는 여러 곳에서
    /// 오며 그중 몇은 <b>여러 번</b> 온다(사거리 진입은 드나들 때마다, 채취는 덩이마다). 받는
    /// 쪽이 "다음 줄인가" 하나만 보면 그 중복이 저절로 걸러지고, 부르는 쪽은 조건을 안 센다.
    /// 조항 <c>N-6</c>(마무리는 도면 뒤)도 배열 순서로 이미 지켜져서 따로 검사할 것이 없다.
    ///
    /// <b>안 도는 것이 기본이다.</b> <see cref="Begin"/> 전에는 아무 신호도 안 받고 아무 줄도
    /// 안 뜬다 — 튜토리얼을 끝낸 판에서 대사가 새는 경로가 없다.
    /// </summary>
    public static class LastShiftNarrationDirector
    {
        private static int fired;
        private static float lineElapsed;

        public static bool IsRunning { get; private set; }

        /// <summary>화면에 낼 줄이 있는가.</summary>
        public static bool HasLine => IsRunning && fired > 0;

        /// <summary>지금 떠 있는 줄.</summary>
        public static LastShiftNarrationScript.Line Current =>
            LastShiftNarrationScript.Directed[Mathf.Clamp(fired - 1, 0,
                LastShiftNarrationScript.Directed.Length - 1)];

        /// <summary>다음에 받을 줄의 id. 다 끝났으면 <c>null</c> 이다.</summary>
        public static string NextId => IsRunning && !IsComplete
            ? LastShiftNarrationScript.Directed[fired].Id
            : null;

        /// <summary>지금 줄이 뜬 뒤 시간. 재촉으로 갈리는 기준이다.</summary>
        public static float LineElapsedSeconds => lineElapsed;

        public static bool IsComplete => fired >= LastShiftNarrationScript.Directed.Length;

        /// <summary>지금까지 뜬 줄 수. 검사와 로그가 읽는다.</summary>
        public static int FiredCount => fired;

        public static void Begin()
        {
            IsRunning = true;
            fired = 0;
            lineElapsed = 0f;
        }

        /// <summary>
        /// 그 사건이 났다고 알린다. <b>다음 줄일 때만 받는다</b> — 순서가 어긋난 신호도, 같은
        /// 신호가 두 번 온 것도 여기서 조용히 버려진다.
        /// </summary>
        public static bool Notify(string id)
        {
            if (!IsRunning || IsComplete) return false;

            // 선택 줄만 뛰어넘는다. 뼈대 줄에서 막히면 그건 배선이 빠진 것이지 건너뛸 일이
            // 아니므로, 여기서 멀리 내다보면 안 걸린 신호가 안내를 통째로 앞질러 끝낸다.
            var at = fired;
            var lines = LastShiftNarrationScript.Directed;
            while (at < lines.Length
                   && !string.Equals(id, lines[at].Id, System.StringComparison.Ordinal))
            {
                if (!lines[at].IsOptional) return false;
                at++;
            }

            if (at >= lines.Length) return false;
            fired = at + 1;
            lineElapsed = 0f;
            return true;
        }

        /// <summary>
        /// 조건이 참일 때만 알린다. 부르는 쪽이 <c>if</c> 를 안 쓰게 하려는 것이고, 그래서
        /// 신호를 나열하는 자리가 <b>조건문 없는 표</b>로 읽힌다.
        /// </summary>
        public static bool Notify(string id, bool when) => when && Notify(id);

        /// <summary>
        /// "앞줄 후 <c>N</c>초" 형 트리거를 민다. 사람이 아무것도 안 해도 오는 줄만 여기서
        /// 흐르고, 나머지는 <see cref="Notify"/> 를 기다리며 그대로 선다.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (!IsRunning || IsComplete) return;
            if (fired > 0) lineElapsed += deltaTime;

            while (!IsComplete)
            {
                var next = LastShiftNarrationScript.Directed[fired];
                if (!next.IsAutomatic || lineElapsed < next.AutoAfterSeconds) break;
                Advance();
            }
        }

        public static void Clear()
        {
            IsRunning = false;
            fired = 0;
            lineElapsed = 0f;
        }

        private static void Advance()
        {
            fired++;
            lineElapsed = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
