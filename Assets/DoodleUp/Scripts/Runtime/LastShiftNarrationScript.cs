using System;

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
            public Line(string id, string trigger, LastShiftNarrationSfx sfx, string text,
                string nudge = "", float nudgeAfterSeconds = 0f, string prompt = "")
            {
                Id = id;
                Trigger = trigger;
                Sfx = sfx;
                Text = text;
                Nudge = nudge;
                NudgeAfterSeconds = nudgeAfterSeconds;
                Prompt = prompt;
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

            public string Text { get; }
            public string Nudge { get; }
            public float NudgeAfterSeconds { get; }
            public string Prompt { get; }

            public bool HasNudge => !string.IsNullOrEmpty(Nudge);
            public bool HasPrompt => !string.IsNullOrEmpty(Prompt);
        }

        private const LastShiftNarrationSfx Long = LastShiftNarrationSfx.ChimeLong;
        private const LastShiftNarrationSfx Short = LastShiftNarrationSfx.ChimeShort;
        private const LastShiftNarrationSfx Quiet = LastShiftNarrationSfx.None;

        /// <summary>
        /// 블록 <c>3</c> — 나가는 길. 전이 조건이 <c>CrewVacuum</c> 이라 이 블록이 끝나는 조건은
        /// "밖에 나갔다" 하나다(정본 §4-3).
        /// </summary>
        public static readonly Line[] Exit =
        {
            new("AI_B_01", "AI_T_11 후 광장 중앙 근접", Long,
                "선체 밖으로 나갈 길은 하나뿐임."),
            new("AI_B_02", "AI_B_01 후 2초", Quiet,
                "코어가 그 길임. 위로 올라가 밖으로 나감.", "코어로 갈 것.", 14f),
            new("AI_F_01", "승강기 상호작용 사거리 최초 진입 (IsAtDeck)", Long,
                "코어가 이 배의 유일한 선외 출입구임. 위로 나감.",
                "입구는 조종석 방향 한 면임.", 16f),
            new("AI_F_01B", "승강 플랫폼 탑승 (발판에 올라선 프레임)", Short,
                "발판이 산소 시계의 시작선임. 지금부터 흐름."),
            new("AI_F_02", "TryAscend 성공 (상승 개시)", Quiet,
                "상승 개시. 올라가는 동안 감압이 같이 돎."),
            new("AI_F_03", "DepressurizeStopY 도착 · 감압 진행 중", Quiet,
                "감압 중. 챔버 안에서 기다릴 것."),
            new("AI_F_04", "감압 완료 → 2단 상승 시작", Quiet,
                "감압 완료. 해치 문턱까지 마저 올라감."),
            new("AI_F_05", "IsAtHullTop · 상단 해치 열림", Short,
                "탑 정상. 여기부터 선외임.", "해치로 걸어 나갈 것.", 12f),
            new("AI_F_06", "상단 해치 통과 (선체 외부 진입)", Quiet,
                "잔해는 선수 좌현 위쪽임. 0이 되면 강제 회수됨.")
        };

        /// <summary>블록 <c>4</c> — 파밍. 왕복 두 번이 여기서 닫힌다(정본 §4-4).</summary>
        public static readonly Line[] Farming =
        {
            new("AI_F_07", "잔해 상호작용 사거리 진입", Short,
                "잔해. 자재를 뜯을 수 있음.", "잔해에 붙어 자재를 뜯을 것.", 18f),
            new("AI_F_08", "첫 채취 성공", Quiet,
                "자재 1. 손은 두 덩이까지임."),
            new("AI_F_09", "적재량이 상한에 도달", Short,
                "손이 참. 더 안 뜯김. 돌아설 것.", "탑 정상으로 복귀할 것.", 18f),
            new("AI_F_10", "TryDescend 성공 (하강 개시)", Quiet,
                "하강 개시. 내려가는 동안 재가압이 같이 돎."),
            new("AI_F_11", "자동 반입이 일어난 프레임", Short,
                "들고 있던 것이 하치대로 들어감."),
            new("AI_F_12", "AI_F_11 후 2초", Quiet,
                "경계는 문이 아니라 기압임. 따로 할 것 없음."),
            new("AI_F_13", "잔해 잔량이 0 보다 큰 상태에서 AI_F_12 종료", Quiet,
                "잔해에 아직 남음. 왕복이 세는 단위임.", "한 번 더 나갈 것.", 45f),
            new("AI_F_14", "잔해 필드 소진", Short,
                "필드 소진. 자재 4.")
        };

        /// <summary>블록 <c>5</c> — 도면. 옛 <c>7</c>·<c>8</c>·<c>9</c>단계 그대로다(정본 §4-3).</summary>
        public static readonly Line[] Blueprint =
        {
            new("AI_B_11", "필드 소진 후 자재 잔액이 골조 값에 도달", Long,
                "모은 자재로 계류 골조 하나를 세울 수 있음."),
            new("AI_B_12", "도면 자동 열림", Quiet,
                "거점 탭. 이번에는 계류 골조 하나임.", "골조를 집을 것.", 8f),
            new("AI_B_13", "커서가 자유면 진입", Quiet,
                "굵게 표시된 면이 세울 자리임."),
            new("AI_B_14", "확정 시도 실패(발자국 불일치)", Quiet,
                "발자국이 어긋남. 돌려서 맞출 것.", "초록이 되면 확정됨.", 20f, "회전 R / 휠"),
            new("AI_B_15", "배치 확정 성공", Short,
                "계류 골조 설치됨. 자재 잔량 0."),
            new("AI_B_16", "AI_B_15 후 2초 · 선체 탭 해금과 동시", Quiet,
                "선체는 자재가 아니라 정비 여력으로 지음. 잔량 0에서도 가능함."),
            new("AI_B_17", "선체 탭 최초 표시", Quiet,
                "붙일 수 있는 면이 배 둘레 전체로 넓어짐.")
        };

        /// <summary>
        /// 마무리 두 줄. <b>표에서는 파밍 끝에 있지만 순서는 도면 뒤다</b>(조항 <c>N-6</c>) —
        /// <c>AI_F_15</c> 가 루프 셋을 요약하는데 그중 하나가 도면이라, 도면을 아직 안 본
        /// 사람에게는 셋 중 하나가 거짓말이 된다.
        /// </summary>
        public static readonly Line[] HandsOff =
        {
            new("AI_F_15", "AI_F_14 후 2초 (도면 블록 뒤에 온다 — 조항 N-6)", Long,
                "루프는 셋임. 나가서 뜯고, 돌아와 쌓고, 도면에서 붙임."),
            new("AI_F_16", "AI_F_15 후 3초 (손 떼기)", Quiet,
                "안내 종료. 다음 결정은 승무원이 함.")
        };

        /// <summary>
        /// 실제로 뜨는 순서. <b>배열 정의 순서가 아니라 이쪽이 정본이다</b> — 조항 <c>N-6</c> 이
        /// 마무리를 도면 뒤로 보내므로, 표 순서를 그대로 읽으면 어긋난다.
        /// </summary>
        public static readonly Line[] InPlayOrder = Concat(Exit, Farming, Blueprint, HandsOff);

        public static int Count => InPlayOrder.Length;

        /// <summary>id 로 한 줄을 찾는다. 없으면 예외다 — 오타를 조용히 넘기지 않는다.</summary>
        public static Line Of(string id)
        {
            foreach (var line in InPlayOrder)
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
