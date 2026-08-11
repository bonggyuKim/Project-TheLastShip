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
            new("AI_W_01", "씬 로드 직후. 화면 암전 · 입력 잠금", Long,
                "동면 해제 절차 개시."),
            new("AI_W_02", "페이드 인 시작", Quiet,
                "경과 항해 시간 1,000,000년. 오차 미보정."),
            new("AI_W_03", "페이드 인 완료 · 시점 입력 해금", Short,
                "주 동력 상실. 동면 유지 회로 정지됨."),
            new("AI_W_04", "AI_W_03 후 2초", Quiet,
                "각성은 일정이 아니라 고장의 결과임."),
            new("AI_W_05", "이동 입력 해금", Short,
                "기립 가능. 숙소 기압 정상.",
                "기립할 것. 남은 예비 전력은 안내에 쓰임.", 12f),
            new("AI_W_06", "첫 이동 입력 감지", Quiet,
                "선내 상태를 순서대로 안내함."),
            new("AI_W_07", "숙소 출입문 상호작용 사거리 진입", Short,
                "문 밖은 중앙 광장. 이 배의 모든 이동은 그곳을 지남.",
                "숙소 문으로 갈 것.", 16f)
        };

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
            // v1.15 — 긴 신호음이 여기 있었다. 블록 첫 줄이 아닌데 갖고 있었고, 본문도
            // AI_B_02("위로 올라가 밖으로 나감")와 같은 말이었다. 둘 다 v1.13 재매핑 자국이다.
            new("AI_F_01", "승강기 상호작용 사거리 최초 진입 (IsAtDeck)", Short,
                "입구는 조종석 방향 한 면임.",
                "나머지 세 면은 안 열림.", 16f),
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
            // v1.15 — 파밍의 첫 줄은 승강기가 아니라 잔해다. 긴 신호음이 여기로 왔다.
            new("AI_F_07", "잔해 상호작용 사거리 진입", Long,
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
            new("AI_F_W1", "선외에서 슈트 산소가 경고선 도달 (판당 1회)", Alert,
                "산소 {threshold}%. 하강과 재가압 시간까지 계산할 것."),
            new("AI_F_W2", "선외에서 슈트 산소가 임계선 도달 (판당 1회)", Alert,
                "산소 {threshold}%. 복귀 외 행동 권장하지 않음."),
            new("AI_F_W3", "O-7 자동 복귀 발생", Alert,
                "산소 고갈. 강제 회수됨."),
            new("AI_F_W4", "AI_F_W3 후 2초 (튜토리얼 한정 · 조항 T-8)", Quiet,
                "들고 있던 것은 잔해로 되돌아감. 다시 나갈 것.")
        };

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
        public static readonly Line[] InPlayOrder = Concat(Wake, Exit, Farming, Blueprint, HandsOff);

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
