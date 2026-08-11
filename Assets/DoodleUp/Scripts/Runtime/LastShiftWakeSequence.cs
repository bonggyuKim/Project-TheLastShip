using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>어느 입력이 살아 있는가. 기상 블록에서만 <c>Free</c> 가 아니다.</summary>
    public enum LastShiftWakeGate
    {
        /// <summary>시점·이동 둘 다 막힘. 대본 전체에서 이 상태는 <c>AI_W_01</c>·<c>02</c> 구간뿐이다.</summary>
        Locked,

        /// <summary>둘러볼 수는 있고 걷지는 못함. 누운 채로 깬 시간이다.</summary>
        LookOnly,

        /// <summary>평상. 기상 블록 밖에서는 항상 이것이다.</summary>
        Free
    }

    /// <summary>
    /// 기상 블록(정본 §4-1)의 진행. <b>이 블록만 시간이 민다</b> — 나머지 블록은 전부
    /// 플레이어 행동이 다음 줄을 여는데, 앞 넷(<c>AI_W_01</c>~<c>04</c>)은 아무것도 안 해도
    /// 흘러야 한다. 암전·페이드·해금이 서로 이어져 있어서다.
    ///
    /// <b>박자가 하나다.</b> 정본이 적어 둔 간격은 <c>AI_W_04 = AI_W_03 후 2초</c> 하나뿐이라,
    /// 암전 길이와 페이드 길이를 따로 지어내는 대신 <b>그 2초를 그대로 네 번 쓴다</b>
    /// (<see cref="BeatSeconds"/>). 값 하나를 옮기면 도입부 전체가 같은 비율로 늘고 준다 —
    /// <c>game-balance</c> 가 판정선을 볼 때 만질 손잡이가 셋이 아니라 하나다.
    ///
    /// <b>정본에 없는 것 하나를 여기서 정했다.</b> <c>AI_W_05</c> 의 트리거가 "이동 입력 해금"
    /// 인데 무엇이 해금을 부르는지가 표에 없다(자기 자신을 가리킨다). 같은 박자로 <c>AI_W_04</c>
    /// 다음 박에 풀도록 했고, 그래서 도입부 총 길이가 <see cref="StandSeconds"/> 다.
    /// 다른 값이어야 하면 그 하나만 바꾸면 된다.
    ///
    /// <b>안 도는 것이 기본이다.</b> <see cref="Begin"/> 을 안 부르면 <see cref="Gate"/> 는
    /// <see cref="LastShiftWakeGate.Free"/> 이므로, 이 상태기가 죽어 있거나 첫 기항이 아닐 때
    /// 조작이 잠기는 경로가 없다.
    /// </summary>
    public static class LastShiftWakeSequence
    {
        /// <summary>정본 §4-1 이 적은 유일한 간격. 도입부의 네 구간이 전부 이 박자다.</summary>
        public const float BeatSeconds = 2f;

        /// <summary>암전 유지. <c>AI_W_01</c> 한 줄을 읽는 시간이다.</summary>
        public static float BlackoutSeconds => BeatSeconds;

        /// <summary>페이드 인. 이 구간 동안 <c>AI_W_02</c> 가 떠 있다.</summary>
        public static float FadeSeconds => BeatSeconds;

        /// <summary>시점이 풀리는 시각(= 페이드 인 완료). <c>AI_W_03</c> 의 트리거다.</summary>
        public static float LookSeconds => BlackoutSeconds + FadeSeconds;

        /// <summary>이동이 풀리는 시각. <c>AI_W_04</c>(<c>LookSeconds + 1</c>박) 다음 박이다.</summary>
        public static float StandSeconds => LookSeconds + BeatSeconds * 2f;

        /// <summary>
        /// 첫 줄이 뜨기 전 <b>아무것도 없는 검정</b>. game-art 규격(2026-08-11)이 정한 값이고
        /// 유도할 데가 없다 — 소리와 글자가 같이 들어오기 전에 화면이 비어 있는 시간이다.
        /// 글자가 떠오르는 데도 같은 길이를 쓴다: 하나를 옮기면 도입부의 첫 숨이 통째로 갈린다.
        /// </summary>
        public const float LogHoldSeconds = 0.35f;

        /// <summary>시간이 미는 줄 수. 나머지 셋은 행동이 민다.</summary>
        public const int TimedLineCount = 5;

        private static float elapsed;
        private static float lineElapsed;
        private static int fired;

        /// <summary>도는 중인가. 첫 기항이 아니면 아예 안 돈다.</summary>
        public static bool IsRunning { get; private set; }

        /// <summary><see cref="Begin"/> 이후 시간.</summary>
        public static float Elapsed => elapsed;

        /// <summary>지금 줄이 뜬 뒤 시간. 재촉으로 갈리는 기준이다.</summary>
        public static float LineElapsedSeconds => lineElapsed;

        /// <summary>마지막 줄까지 다 떴는가.</summary>
        public static bool IsComplete => fired >= LastShiftNarrationScript.Wake.Length;

        /// <summary>화면에 낼 줄이 있는가. <b>첫 0.35초는 없다</b> — 검정만 있는 구간이다.</summary>
        public static bool HasLine => IsRunning && fired > 0;

        /// <summary>
        /// 지금 뜬 줄이 <b>도입부의 첫 로그</b>인가. 그 한 줄만 타이핑이 아니라 페이드로
        /// 들어온다(art 규격) — 검정 위에 떠오르는 시스템 로그의 그림이고, 둘을 겹치면
        /// "추가 페이드 없음" 이 그 자리에서 깨진다.
        /// </summary>
        public static bool IsOpeningLine => IsRunning && fired == 1;

        /// <summary>
        /// 지금 줄의 진하기. 첫 줄만 <see cref="LogHoldSeconds"/> 동안 떠오르고 나머지는 바로
        /// <c>1</c> 이다 — 줄이 바뀔 때마다 페이드하면 대사가 읽히기 전에 다음이 온다.
        /// </summary>
        public static float LineAlpha => fired == 1
            ? Mathf.Clamp01((elapsed - LogHoldSeconds) / LogHoldSeconds)
            : 1f;

        /// <summary>
        /// 띠가 떠오르는 정도. <c>0</c> 이면 아직 검정 위의 글자 하나뿐이고, <c>1</c> 이면
        /// 평상시 온보딩 띠다. 월드 페이드와 <b>같은 구간</b>에서 움직인다 — 배가 보이기
        /// 시작하는 것과 계기가 돌아오는 것이 한 동작이어야 두 번 페이드한 것으로 안 보인다.
        /// </summary>
        public static float PanelAlpha => !IsRunning || elapsed <= BlackoutSeconds
            ? 0f
            : Mathf.Clamp01((elapsed - BlackoutSeconds) / FadeSeconds);

        /// <summary>지금 떠 있는 줄. <see cref="HasLine"/> 이 참일 때만 의미가 있다.</summary>
        public static LastShiftNarrationScript.Line Current =>
            LastShiftNarrationScript.Wake[Mathf.Clamp(fired - 1, 0, LastShiftNarrationScript.Wake.Length - 1)];

        /// <summary>
        /// 살아 있는 입력. <b>안 도는 동안은 항상 <see cref="LastShiftWakeGate.Free"/></b> 다 —
        /// 잠그는 쪽이 아니라 푸는 쪽을 기본값으로 둬야, 이 상태기가 안 돌 때 조작이 죽지 않는다.
        /// </summary>
        public static LastShiftWakeGate Gate
        {
            get
            {
                if (!IsRunning) return LastShiftWakeGate.Free;
                if (elapsed < LookSeconds) return LastShiftWakeGate.Locked;
                if (elapsed < StandSeconds) return LastShiftWakeGate.LookOnly;
                return LastShiftWakeGate.Free;
            }
        }

        /// <summary>시점을 돌릴 수 있는가.</summary>
        public static bool CanLook => Gate != LastShiftWakeGate.Locked;

        /// <summary>걸을 수 있는가.</summary>
        public static bool CanMove => Gate == LastShiftWakeGate.Free;

        /// <summary>
        /// 화면을 덮는 검정의 진하기. <c>1</c> 에서 시작해 페이드 구간에서 <c>0</c> 으로 간다.
        /// </summary>
        public static float BlackoutAlpha
        {
            get
            {
                if (!IsRunning) return 0f;
                if (elapsed <= BlackoutSeconds) return 1f;
                if (elapsed >= LookSeconds) return 0f;
                return 1f - (elapsed - BlackoutSeconds) / FadeSeconds;
            }
        }

        /// <summary>
        /// 도입부 시작. <b>첫 줄은 여기서 안 뜬다</b> — <see cref="LogHoldSeconds"/> 동안
        /// 아무것도 없는 검정이 먼저 오고, 그 뒤에 소리와 글자가 같이 들어온다(art 규격).
        /// </summary>
        public static void Begin()
        {
            IsRunning = true;
            elapsed = 0f;
            lineElapsed = 0f;
            fired = 0;
        }

        public static void Tick(float deltaTime)
        {
            if (!IsRunning || IsComplete) return;
            elapsed += deltaTime;
            lineElapsed += deltaTime;

            while (fired < TimedLineCount && elapsed >= ScheduledAt(fired))
            {
                fired++;
                lineElapsed = 0f;
            }
        }

        /// <summary>
        /// <c>AI_W_06</c> — 첫 이동 입력. <b><c>AI_W_05</c> 가 떠 있을 때만 받는다</b>: 이동이
        /// 풀리기 전에는 입력 자체가 막혀 있고, 그 뒤에는 이미 지나간 줄이다.
        /// </summary>
        public static void NotifyFirstMove()
        {
            if (!IsRunning || fired != TimedLineCount) return;
            fired++;
            lineElapsed = 0f;
        }

        /// <summary>
        /// 마지막 줄만 남았는가. <b>문을 찾는 조회를 이 동안만 돌리려고</b> 있다 — 사거리 판정은
        /// 씬 조회라 매 프레임 무조건 돌릴 것이 아니다.
        /// </summary>
        public static bool IsAwaitingQuartersDoor => IsRunning && fired == TimedLineCount + 1;

        /// <summary><c>AI_W_07</c> — 숙소 출입문 사거리 진입. 걷기 시작한 뒤에만 온다.</summary>
        public static void NotifyQuartersDoorInRange()
        {
            if (!IsRunning || fired != TimedLineCount + 1) return;
            fired++;
            lineElapsed = 0f;
        }

        public static void Clear()
        {
            IsRunning = false;
            elapsed = 0f;
            lineElapsed = 0f;
            fired = 0;
        }

        /// <summary>
        /// 시간이 미는 다섯 줄의 예정 시각. 여섯째부터는 시간으로 안 오므로 무한이다.
        /// </summary>
        public static float ScheduledAt(int index) => index switch
        {
            // 첫 줄은 0 이 아니라 0.35 다. 그 앞은 아무것도 없는 검정이어야 한다(art 규격).
            0 => LogHoldSeconds,
            1 => BlackoutSeconds,
            2 => LookSeconds,
            3 => LookSeconds + BeatSeconds,
            4 => StandSeconds,
            _ => float.PositiveInfinity
        };
    }
}
