using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선내 관리 시스템 내레이션 대본(정본 <c>docs/tutorial-ai-narration-onboarding-v1.md</c> v1.13).
    ///
    /// <b>단계 띠(<see cref="LastShiftTutorialCopy"/>)와 다른 물건이다.</b> 띠는 단계마다 한 줄이고
    /// 이쪽은 한 단계 안에서 여러 줄이 사건마다 뜬다. 그래서 키가 단계가 아니라 <c>id</c> 다.
    ///
    /// <b>여기 있는 것은 대본뿐이고 배선은 아직 아니다.</b> 각 줄의 <c>Trigger</c> 는 사람이 읽는
    /// 문장이고 코드가 해석하지 않는다 — 실제로 그 사건에 이 줄을 띄우는 것은 뒤따르는 작업이다.
    /// 대본과 배선을 한 커밋에 넣지 않는 이유는, 트리거가 아직 없는 줄이 섞여 있어
    /// (기상 블록 일곱 · 순회 여섯) 어느 줄이 실제로 배선된 것인지 구분이 안 되기 때문이다.
    ///
    /// 지금 담은 것은 <b>트리거가 이미 코드에 있는 블록</b>뿐이다:
    ///   나가는 길  <c>AI_B_01</c>·<c>AI_B_02</c> + <c>AI_F_01</c>~<c>AI_F_06</c>
    ///   파밍       <c>AI_F_07</c>~<c>AI_F_16</c>
    ///   도면       <c>AI_B_11</c>~<c>AI_B_17</c>
    /// </summary>
    public static class LastShiftNarrationScript
    {
        /// <summary>대본 한 줄. 열 구성은 정본 §3 그대로다.</summary>
        public readonly struct Line
        {
            public Line(string id, string trigger, LastShiftNarrationSfx sfx,
                float nudgeAfterSeconds = 0f, float autoAfterSeconds = 0f, bool optional = false)
            {
                Id = id;
                Trigger = trigger;
                Sfx = sfx;
                NudgeAfterSeconds = nudgeAfterSeconds;
                AutoAfterSeconds = autoAfterSeconds;
                IsOptional = optional;
            }

            public string Id { get; }

            /// <summary>뜨는 시점. <b>사람이 읽는 문장이고 코드가 해석하지 않는다</b> — 대조용이다.</summary>
            public string Trigger { get; }

            /// <summary>
            /// 이 줄과 함께 울리는 소리. <b>재촉에는 안 울린다</b>(조항 <c>N-1</c>) — 재촉은 같은
            /// 사건을 다시 말하는 것이라 소리를 또 내면 새 사건으로 읽힌다. 그래서 소리가
            /// 줄에 하나뿐이고 <see cref="Nudge"/> 쪽에는 자리가 없다.
            /// </summary>
            public LastShiftNarrationSfx Sfx { get; }

            /// <summary>
            /// 화면에 뜨는 말. <b>문안은 여기 없다</b> — 대사 표(<see cref="LastShiftText"/>)의
            /// <c>narration.&lt;줄 id&gt;.text</c> 다. 키를 줄마다 손으로 적지 않는 것은 줄에 이미
            /// 이름(<see cref="Id"/>)이 있어서다: 두 벌로 적으면 그 둘이 언젠가 어긋난다.
            /// </summary>
            public string Text => LastShiftText.Get($"narration.{Id}.text");

            /// <summary>못 찾고 있을 때 갈아 끼우는 말. 없는 줄이 많아 표에 있을 때만 뜬다.</summary>
            public string Nudge => Optional("nudge");
            public float NudgeAfterSeconds { get; }

            /// <summary>상시로 남는 한 줄. 대부분의 줄에는 없다.</summary>
            public string Prompt => Optional("prompt");

            /// <summary>
            /// 있으면 문안, 없으면 빈 문자열. <see cref="LastShiftText.Get"/> 로 바로 물으면
            /// 애초에 안 쓰는 자리마다 "빠진 키" 경고가 떠서, 진짜 누락이 그 속에 묻힌다.
            /// </summary>
            private string Optional(string suffix)
            {
                var key = $"narration.{Id}.{suffix}";
                return LastShiftText.Has(key) ? LastShiftText.Get(key) : string.Empty;
            }

            /// <summary>
            /// <b>앞줄이 뜬 뒤 이만큼 지나면 저절로 온다</b>(정본 표의 "앞줄 후 N초" 형 트리거).
            /// <c>0</c> 이면 플레이어 행동이 밀어야 한다.
            ///
            /// 앞줄이 <b>표의 그 줄</b>이 아니라 <b>실제 진행 순서의 앞줄</b>인 것이 조건이다 —
            /// <c>AI_F_15</c> 의 트리거가 "AI_F_14 후 2초" 로 적혀 있지만 조항 <c>N-6</c> 이 그
            /// 줄을 도면 블록 뒤로 보내므로, 실제로 재는 앞줄은 <c>AI_B_17</c> 이다.
            /// </summary>
            public float AutoAfterSeconds { get; }

            /// <summary>
            /// <b>안 뜨고 지나갈 수 있는 줄</b>인가. 진행의 뼈대가 아니라 곁가지라, 조건이
            /// 안 맞으면 다음 줄이 이 줄을 뛰어넘는다.
            ///
            /// 둘뿐이다 — <c>AI_T_02B</c>(코어는 순서 무관이라 아예 안 다가설 수 있다)와
            /// <c>AI_F_13</c>("잔해에 아직 남음" 은 한 번에 다 뜯으면 거짓말이 된다).
            /// 이 표시가 없으면 그 둘에서 안내가 영영 막힌다.
            /// </summary>
            public bool IsOptional { get; }

            public bool HasNudge => !string.IsNullOrEmpty(Nudge);
            public bool HasPrompt => !string.IsNullOrEmpty(Prompt);
            public bool IsAutomatic => AutoAfterSeconds > 0f;
        }

        private const LastShiftNarrationSfx Long = LastShiftNarrationSfx.ChimeLong;
        private const LastShiftNarrationSfx Short = LastShiftNarrationSfx.ChimeShort;
        private const LastShiftNarrationSfx Alert = LastShiftNarrationSfx.ChimeAlert;
        private const LastShiftNarrationSfx Quiet = LastShiftNarrationSfx.None;

        /// <summary>
        /// 블록 <c>1</c> — 기상(정본 §4-1). <b>대본에서 입력이 잠기는 구간은 여기 하나다</b>
        /// (<c>AI_W_01</c>), 그리고 그 잠금이 조항 <c>N-2</c>("긴 신호음 중 이동을 막지 않는다")
        /// 의 유일한 예외다.
        ///
        /// 앞 넷은 <b>플레이어가 아무것도 안 해도 흐른다</b> — 암전·페이드·해금이 시간으로
        /// 이어지기 때문이고, 그래서 이 블록만 진행을 <see cref="LastShiftWakeSequence"/> 가
        /// 쥔다. 뒤 셋(<c>W_05</c>~<c>W_07</c>)부터 다시 플레이어 행동이 민다.
        ///
        /// 과녁은 <c>AI_W_04</c> 다(정본). 1,000,000년은 놀라움을 만들지만 행동을 안 만들고,
        /// 행동을 만드는 것은 "고장의 결과" 한 줄이다.
        /// </summary>
        public static readonly Line[] Wake =
        {
            new("AI_W_01", "씬 로드 직후. 화면 암전 · 입력 잠금", Long),
            new("AI_W_02", "페이드 인 시작", Quiet),
            new("AI_W_03", "페이드 인 완료 · 시점 입력 해금", Short),
            new("AI_W_04", "AI_W_03 후 2초", Quiet),
            new("AI_W_05", "이동 입력 해금", Short, nudgeAfterSeconds: 12f),
            new("AI_W_06", "첫 이동 입력 감지", Quiet),
            new("AI_W_07", "숙소 출입문 상호작용 사거리 진입", Short, nudgeAfterSeconds: 16f)
        };

        /// <summary>
        /// 블록 <c>2</c> — 방 순회(정본 §4-2). <b>방마다 두 줄이다</b> — 들어온 방이 무엇인지,
        /// 무엇을 하는 방인지. 뒤쪽 줄의 과녁은 전부 방 안쪽 끝벽에 붙은 설비이고, 그 자리는
        /// 게이지를 문틀에서 방 안으로 옮긴 <c>SIMUL_ZONES</c> 장치와 같은 좌표다.
        ///
        /// 순서는 <b>광장 둘레를 도는 순서</b>다 — 조종석 → 전력실 → 산소실 → 냉각실.
        /// v1.14 가 옛 순서(조종석 → 산소실 → 전력실 → 냉각실)를 고쳤다: 그쪽은 서 → 동 →
        /// 남 → 북이라 <b>광장 코어를 두 번 가로지른다</b>. 문서가 스스로 "둘레를 도는 순서"
        /// 라고 적어 놓고 정작 둘레를 안 돌고 있었고, 보행이 <c>53.5 → 41.5m</c> 로 줄었다.
        /// <b>문안은 한 줄도 안 바뀌었고 두 쌍의 내용만 맞바꿨다.</b>
        /// </summary>
        public static readonly Line[] Patrol =
        {
            new("AI_T_01", "광장 최초 진입", Long),
            new("AI_T_02", "AI_T_01 후 3초", Quiet, nudgeAfterSeconds: 14f, autoAfterSeconds: 3f),
            // 코어는 지나가다 볼 수도, 안 볼 수도 있다 — 그래서 유일하게 건너뛸 수 있는 줄이다.
            new("AI_T_02B", "광장 코어 사거리 진입 (순서 무관 · 1회)", Quiet, optional: true),
            new("AI_T_03", "조종석 진입", Short),
            new("AI_T_04", "전면 스크린 시야 진입", Quiet, nudgeAfterSeconds: 12f),
            new("AI_T_04B", "AI_T_04 후 2초", Quiet, autoAfterSeconds: 2f),
            new("AI_T_05", "전력실 압력문 통과", Short),
            new("AI_T_06", "배전반 근접", Quiet, nudgeAfterSeconds: 12f),
            new("AI_T_07", "산소실 압력문 통과", Short),
            new("AI_T_08", "산소실 게이지 근접", Quiet, nudgeAfterSeconds: 12f),
            new("AI_T_09", "냉각실 압력문 통과", Short),
            new("AI_T_10", "냉각통 근접", Quiet, nudgeAfterSeconds: 12f),
            new("AI_T_11", "마지막 미방문 방 퇴장 후 광장 재진입", Quiet)
        };

        /// <summary>
        /// 블록 <c>3</c> — 나가는 길. 전이 조건이 <c>CrewVacuum</c> 이라 이 블록이 끝나는 조건은
        /// "밖에 나갔다" 하나다(정본 §4-3).
        /// </summary>
        public static readonly Line[] Exit =
        {
            new("AI_B_01", "AI_T_11 후 광장 중앙 근접", Long),
            new("AI_B_02", "AI_B_01 후 2초", Quiet, nudgeAfterSeconds: 14f, autoAfterSeconds: 2f),
            // v1.15 — 긴 신호음이 여기 있었다. 블록 첫 줄이 아닌데 갖고 있었고, 본문도
            // AI_B_02("위로 올라가 밖으로 나감")와 같은 말이었다. 둘 다 v1.13 재매핑 자국이다.
            new("AI_F_01", "승강기 상호작용 사거리 최초 진입 (IsAtDeck)", Short, nudgeAfterSeconds: 16f),
            new("AI_F_01B", "승강 플랫폼 탑승 (발판에 올라선 프레임)", Short),
            new("AI_F_02", "TryAscend 성공 (상승 개시)", Quiet),
            new("AI_F_03", "DepressurizeStopY 도착 · 감압 진행 중", Quiet),
            new("AI_F_04", "감압 완료 → 2단 상승 시작", Quiet),
            new("AI_F_05", "IsAtHullTop · 상단 해치 열림", Short, nudgeAfterSeconds: 12f),
            new("AI_F_06", "상단 해치 통과 (선체 외부 진입)", Quiet)
        };

        /// <summary>블록 <c>4</c> — 파밍. 왕복 두 번이 여기서 닫힌다(정본 §4-4).</summary>
        public static readonly Line[] Farming =
        {
            // v1.15 — 파밍의 첫 줄은 승강기가 아니라 잔해다. 긴 신호음이 여기로 왔다.
            new("AI_F_07", "잔해 상호작용 사거리 진입", Long, nudgeAfterSeconds: 18f),
            new("AI_F_08", "첫 채취 성공", Quiet),
            new("AI_F_09", "적재량이 상한에 도달", Short, nudgeAfterSeconds: 18f),
            new("AI_F_10", "TryDescend 성공 (하강 개시)", Quiet),
            new("AI_F_11", "자동 반입이 일어난 프레임", Short),
            new("AI_F_12", "AI_F_11 후 2초", Quiet, autoAfterSeconds: 2f),
            // 한 번에 다 뜯으면 "아직 남음" 이 거짓말이 된다 — 그때는 건너뛴다.
            new("AI_F_13", "잔해 잔량이 0 보다 큰 상태에서 AI_F_12 종료", Quiet, nudgeAfterSeconds: 45f, optional: true),
            new("AI_F_14", "잔해 필드 소진", Short)
        };

        /// <summary>블록 <c>5</c> — 도면. 옛 <c>7</c>·<c>8</c>·<c>9</c>단계 그대로다(정본 §4-3).</summary>
        public static readonly Line[] Blueprint =
        {
            new("AI_B_11", "필드 소진 후 자재 잔액이 골조 값에 도달", Long),
            new("AI_B_12", "도면 자동 열림", Quiet, nudgeAfterSeconds: 8f),
            new("AI_B_13", "커서가 자유면 진입", Quiet),
            new("AI_B_14", "확정 시도 실패(발자국 불일치)", Quiet, nudgeAfterSeconds: 20f),
            new("AI_B_15", "배치 확정 성공", Short),
            new("AI_B_16", "AI_B_15 후 2초 · 선체 탭 해금과 동시", Quiet, autoAfterSeconds: 2f),
            new("AI_B_17", "선체 탭 최초 표시", Quiet)
        };

        /// <summary>
        /// 마무리 두 줄. <b>표에서는 파밍 끝에 있지만 순서는 도면 뒤다</b>(조항 <c>N-6</c>) —
        /// <c>AI_F_15</c> 가 루프 셋을 요약하는데 그중 하나가 도면이라, 도면을 아직 안 본
        /// 사람에게는 셋 중 하나가 거짓말이 된다.
        /// </summary>
        public static readonly Line[] HandsOff =
        {
            new("AI_F_15", "AI_F_14 후 2초 (도면 블록 뒤에 온다 — 조항 N-6)", Long, autoAfterSeconds: 2f),
            new("AI_F_16", "AI_F_15 후 3초 (손 떼기)", Quiet, autoAfterSeconds: 3f)
        };

        /// <summary>
        /// <b>상시 라인</b>. 어느 블록에도 안 든다 — 단계 진행이 아니라 <b>상태</b>로 뜨기
        /// 때문이다(정본 §4-4). 판당 한 번씩만 뜬다.
        ///
        /// <b>임계 숫자를 <c>{threshold}</c> 로 둔다</b>(조항 <c>N-7</c>). 정본 표에는 <c>40%</c>·
        /// <c>25%</c> 가 문자열에 박혀 있는데, 그 값은 오늘 <c>45</c>/<c>30</c> 으로 옮겨갔다 —
        /// 박아 두면 경고가 뜨는 조건과 화면 숫자가 갈린다. <see cref="Format"/> 가 그 자리에
        /// 지금 값을 넣는다. game-art 규격서도 같은 <c>{threshold}</c> 바인딩을 전제한다.
        ///
        /// <c>AI_F_W3</c>·<c>W4</c> 는 조항 <c>O-7</c> 자동 회수다. <b>사망 통보가 아니다</b> —
        /// 잃는 것은 목숨이 아니라 그 왕복이라, 죽음·위험 계열 단어를 쓰지 않는다.
        /// </summary>
        public static readonly Line[] Standing =
        {
            new("AI_F_W1", "선외에서 슈트 산소가 경고선 도달 (판당 1회)", Alert),
            new("AI_F_W2", "선외에서 슈트 산소가 임계선 도달 (판당 1회)", Alert),
            new("AI_F_W3", "O-7 자동 복귀 발생", Alert),
            new("AI_F_W4", "AI_F_W3 후 2초 (튜토리얼 한정 · 조항 T-8)", Quiet)
        };

        /// <summary>
        /// 한 줄이 다 찍히는 데 걸리는 시간. <b>글자 수와 무관하다</b> — 초당 몇 자로 두면
        /// 긴 줄이 다음 줄에 잡아먹히는데, 이 대본은 줄마다 길이가 두 배 넘게 차이 난다.
        /// <b>독립 상수다</b>(조항 §7-22). 전에는 <c>BeatSeconds</c> 의 절반이었는데, 그러면
        /// 타이핑을 조절하려다 도입부 길이(8초)와 상시 경고 체류(2초)가 같이 움직인다 —
        /// game-balance 가 "그 손잡이는 건드리지 말고 판정선을 올리자" 로 닫은 자리다.
        /// 값은 그때와 같으므로 화면은 안 바뀐다.
        /// </summary>
        public const float TypingSeconds = 1f;

        /// <summary>
        /// <paramref name="elapsed"/> 시점까지 찍힌 만큼. 다 찍히면 원문 그대로다.
        /// <b>글자를 세서 자를 뿐 새 문자열 규칙을 만들지 않는다</b> — 서식이 붙으면
        /// 여기가 아니라 그리는 쪽이 바뀐다.
        /// </summary>
        public static string Reveal(string text, float elapsed) =>
            Reveal(text, elapsed, TypingSeconds);

        /// <summary>
        /// 찍히는 시간을 부르는 쪽이 정한다. <b>순회만 짧게 쓴다</b> — 그 블록은 다음 줄이
        /// 걸어가는 동안 바로 따라오므로, 찍는 데 오래 걸리면 읽을 시간이 안 남는다.
        /// </summary>
        public static string Reveal(string text, float elapsed, float duration)
        {
            if (string.IsNullOrEmpty(text) || duration <= 0f) return text;
            if (elapsed >= duration) return text;
            if (elapsed <= 0f) return string.Empty;
            var shown = Mathf.CeilToInt(text.Length * (elapsed / duration));
            return text.Substring(0, Mathf.Clamp(shown, 0, text.Length));
        }

        /// <summary>
        /// 화면에 낼 문장. <c>{threshold}</c> 자리에 <b>지금 값</b>을 넣는다 — 경고를 띄우는
        /// 조건과 화면에 적히는 숫자가 같은 상수에서 나와야 갈리지 않는다(조항 <c>N-7</c>).
        /// </summary>
        public static string Format(in Line line)
        {
            if (!line.Text.Contains("{threshold}")) return line.Text;
            var ratio = line.Id == "AI_F_W2"
                ? LastShiftRecoveryTuning.SuitOxygenCriticalThreshold
                : LastShiftRecoveryTuning.SuitOxygenWarningThreshold;
            return line.Text.Replace("{threshold}", Mathf.RoundToInt(ratio * 100f).ToString());
        }

        /// <summary>
        /// 실제로 뜨는 순서. <b>배열 정의 순서가 아니라 이쪽이 정본이다</b> — 조항 <c>N-6</c> 이
        /// 마무리를 도면 뒤로 보내므로, 표 순서를 그대로 읽으면 어긋난다.
        /// </summary>
        public static readonly Line[] InPlayOrder = Concat(Wake, Patrol, Exit, Farming, Blueprint, HandsOff);

        /// <summary>
        /// 한 줄기로 흐르는 블록들. <see cref="LastShiftNarrationDirector"/> 가 이 순서로 민다.
        ///
        /// <b>셋이 빠져 있고 이유가 각각 다르다.</b> 기상은 시간과 입력 해금이 함께 묶여 있고
        /// (<see cref="LastShiftWakeSequence"/>), 순회는 방 순서가 자유롭고
        /// (<see cref="LastShiftPatrolNarration"/>), 상시는 진행이 아니라 상태로 뜬다
        /// (<see cref="LastShiftStandingNarration"/>).
        /// </summary>
        public static readonly Line[] Directed = Concat(Exit, Farming, Blueprint, HandsOff);

        /// <summary>상시 라인을 포함한 전체. 정본 총계 <c>50</c> 중 적재분이다.</summary>
        public static readonly Line[] All = Concat(InPlayOrder, Standing);

        public static int Count => InPlayOrder.Length;

        /// <summary>id 로 한 줄을 찾는다. 없으면 예외다 — 오타를 조용히 넘기지 않는다.</summary>
        public static Line Of(string id)
        {
            foreach (var line in All)
                if (string.Equals(line.Id, id, StringComparison.Ordinal)) return line;
            throw new ArgumentOutOfRangeException(nameof(id), id, "대본에 없는 라인 id 다");
        }

        private static Line[] Concat(params Line[][] blocks)
        {
            var total = 0;
            foreach (var block in blocks) total += block.Length;
            var result = new Line[total];
            var at = 0;
            foreach (var block in blocks)
            {
                Array.Copy(block, 0, result, at, block.Length);
                at += block.Length;
            }
            return result;
        }
    }
}
