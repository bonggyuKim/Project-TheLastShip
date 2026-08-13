namespace DoodleUp.Runtime
{
    /// <summary>
    /// 단계 하나의 문안 한 벌 — <b>표의 한 행이다</b>. 열이 고정돼 있어(단계 · 제목 · 안내 ·
    /// 재촉 · 재촉까지의 초 · 조작 프롬프트) 그대로 CSV 한 줄이나 SO 한 항목이 된다.
    ///
    /// <b>왜 문장이 둘인가.</b> 안내는 <b>지금 할 것</b>이고 재촉은 <b>못 찾고 있을 때 짚어
    /// 주는 것</b>이다. 한 문장에 둘을 다 담으면 처음 읽는 사람에게는 길고, 헤매는 사람에게는
    /// 이미 읽어 버린 줄이라 도움이 안 된다 — 그래서 같은 자리에서 시간으로 갈아 끼운다.
    /// </summary>
    public readonly struct LastShiftTutorialLine
    {
        public LastShiftTutorialLine(
            LastShiftTutorialStep step, float nudgeAfterSeconds = 0f,
            LastShiftNarrationSfx sfx = LastShiftNarrationSfx.None, string trigger = "")
        {
            Step = step;
            NudgeAfterSeconds = nudgeAfterSeconds;
            Sfx = sfx;
            Trigger = trigger;
        }

        /// <summary>
        /// 있으면 문안, 없으면 빈 문자열. 안 쓰는 자리마다 "빠진 키" 경고가 뜨면 진짜
        /// 누락이 그 속에 묻힌다.
        /// </summary>
        private string Optional(string suffix)
        {
            // None 은 "단계가 없다" 를 뜻하는 자리표라 문안이 아예 없다. 표에 물으면 빠진
            // 키로 잡혀 화면에 ⟨tutorial.None.guide⟩ 가 뜬다.
            if (Step == LastShiftTutorialStep.None) return string.Empty;

            var key = $"tutorial.{Step}.{suffix}";
            return LastShiftText.Has(key) ? LastShiftText.Get(key) : string.Empty;
        }

        public LastShiftTutorialStep Step { get; }

        /// <summary>지금 어느 장면인가. 띠 머리줄에 단계 번호와 나란히 붙는 <b>짧은 말</b>이다.</summary>
        public string Title => Optional("title");

        /// <summary>지금 할 것. 단계에 들어온 직후부터 뜬다.</summary>
        public string Guide => Optional("guide");

        /// <summary>못 찾고 있을 때. <see cref="NudgeAfterSeconds"/> 를 넘기면 <see cref="Guide"/> 자리를 뺏는다.</summary>
        public string Nudge => Optional("nudge");

        /// <summary>
        /// 재촉까지의 초. §2 표의 <b>개산 × 2</b> 다 — 개산만큼 걸린 것은 정상이고, 두 배를
        /// 넘긴 것은 못 찾고 있다는 뜻이라 그 자리에서 갈아 끼운다.
        ///
        /// <b>판정 수치가 아니다.</b> §6 의 판정 셋은 로그로 재지므로(조항 <c>T-9</c>) 여기 값을
        /// 만져도 판정선은 안 움직인다 — 문안 갈아 끼우는 시점 하나다.
        /// </summary>
        public float NudgeAfterSeconds { get; }

        /// <summary>
        /// 조작 프롬프트 — 조항 <c>T-3</c> 이 "실패 사유가 아니라 조작으로 가르친다" 고 한
        /// 그 한 줄이다. <c>8</c>단계에만 있고 나머지는 빈 문자열이다.
        /// </summary>
        public string Prompt => Optional("prompt");

        public bool HasPrompt => !string.IsNullOrEmpty(Prompt);

        /// <summary>
        /// 이 줄과 함께 울리는 신호음(대본 §2). <b>재촉에는 안 붙는다</b>(조항 <c>N-1</c>) —
        /// 재촉은 같은 사건을 다시 말하는 것이라 소리를 또 내면 새 사건으로 읽힌다.
        /// </summary>
        public LastShiftNarrationSfx Sfx { get; }

        /// <summary>
        /// 이 줄이 뜨는 시점을 사람 말로 적은 것. <b>코드가 읽는 값이 아니라 대조용이다</b> —
        /// 대본과 배선이 갈렸을 때 어느 줄이 어긋났는지를 여기서 찾는다.
        /// </summary>
        public string Trigger { get; }

        /// <summary>이 단계에서 지금 띄울 한 줄. 머문 시간만으로 갈린다.</summary>
        public string GuideAt(float stepElapsedSeconds) =>
            stepElapsedSeconds >= NudgeAfterSeconds && !string.IsNullOrEmpty(Nudge) ? Nudge : Guide;
    }

    /// <summary>
    /// 튜토리얼 단계 안내 문안 — 미결 §8-<c>5</c> 중 <b>단계 문안 몫</b>이다. 정본은
    /// <c>docs/tutorial-o3-free-placement-farming-deposit-v1.md</c> §2 표이고, 각 행의
    /// <b>"배우는 것"</b> 칸이 곧 그 단계 문안이 겨누는 과녁이다 — 일어나는 일을 옮겨 적는
    /// 것이 아니라, 그 장면에서 <b>남아야 하는 문장 하나</b>를 적는다.
    ///
    /// <b>문안이 코드에서 흩어지지 않는 것이 이 자리의 요점이다.</b> 띠(<c>LastShiftSandboxController</c>)
    /// 와 배치 화면(<c>LastShiftPlacementUi</c>) 두 곳이 문안을 쓰는데, 각자 문자열을 들고
    /// 있으면 같은 단계를 두 말로 부르게 된다. 부르는 자리는 <see cref="Of"/> 하나다.
    ///
    /// <b>표를 코드에 둔 것은 임시가 아니라 범위다.</b> <see cref="Table"/> 은 열이 고정된 행
    /// 목록이라 CSV·SO 적재로 갈아 끼울 때 <see cref="Of"/> 안쪽만 바뀐다 — 부르는 자리는
    /// 그대로다. 지금 로더를 먼저 놓으면 문안 열 줄에 자산 파이프라인이 딸려 오고, 그건
    /// <c>game-tech-director</c> 몫이다.
    ///
    /// <b>같은 단계에 문안을 여러 벌 두고 돌리지 않는다.</b> 대사 풀은 창발 드라마 쪽 규약이고,
    /// 여기는 판정이 붙은 학습 구간이다(§6) — 같은 자리에서 문장이 판마다 달라지면 QA 가
    /// "이 단계에서 무엇이 뜨는가" 를 재현할 수 없고, 플레이어에게도 방금 읽은 줄이 다른 말로
    /// 다시 나오는 것으로 읽힌다. 변주는 단계 안에서 <b>시간</b>으로만 준다(안내 → 재촉).
    /// </summary>
    public static class LastShiftTutorialCopy
    {
        /// <summary>
        /// 단계 문안 표 — 행 하나가 단계 하나다. <b>순서가 <see cref="LastShiftTutorialStep"/>
        /// 번호 순이어야 한다</b>: <see cref="Of"/> 가 번호로 바로 집는다.
        ///
        /// 톤은 기존 화면과 같은 규약이다 — 존댓말도 감탄도 없고, 계기판이 사실을 적는 말투다
        /// (<c>"목표 — 추력과 산소를 선 위로 올려 도킹"</c> · <c>"자재가 모자란다"</c>).
        /// 튜토리얼 띠라고 톤을 올리면 이 배에서 유일하게 말을 거는 화면이 된다.
        /// </summary>
        private static readonly LastShiftTutorialLine[] Table =
        {
            // 1 — 배우는 것: "밖에 뭔가 있다". 잔해를 가리키는 것이 아니라 나갈 데를 가리킨다.
            new(LastShiftTutorialStep.SightSalvage, nudgeAfterSeconds: 10f),

            // 2 — "허브에서 문을 고른다". 문이 여럿인 것과 지금 고를 문이 하나인 것을 같이 적는다.
            //
            // <b>재촉이 문을 치우는 말에서 지도를 가리키는 말로 바뀌었다</b>(2026-08-13
            // 플레이테스트 — "어느 방이 어딘지 모름"). 옛 문장은 "여섯 문은 지금 볼 게 없다" 로
            // 둘레 문을 한꺼번에 치웠고, 그래서 이 배에서 배치를 처음 만나는 그 순간에 화면이
            // 하는 말이 "보지 말라" 였다. 헤매는 사람에게 필요한 것은 그 반대다 — 지금 갈 곳
            // 하나와, 나머지가 무엇인지 알아볼 수 있는 자리(지도 M)를 같이 준다.
            //
            // <b>"도면" 이라 쓰지 않는다.</b> 그 말은 아래 6단계가 자유 배치 청사진에 이미
            // 쓰고 있다. 배 배치를 보는 화면은 <c>지도</c>, 모듈을 놓는 화면은 <c>도면</c> 이다.
            new(LastShiftTutorialStep.CrossPlaza, nudgeAfterSeconds: 12f),

            // 3 — "밖에는 시계가 있다 · 시계의 시작선은 바닥 사각형이다". 두 문장으로 갈라
            // 앞에 시계를, 뒤에 시작선을 둔다. 게이지가 뜨는 그 프레임에 읽히는 줄이다.
            new(LastShiftTutorialStep.CentralLift, nudgeAfterSeconds: 10f),

            // 4 — "파밍 동사 · 한 번에 둘이다". 손이 차는 것을 미리 적어 둔다: §1-1 이 짚은
            // 오학습은 "세 번째가 왜 안 뜯기지" 를 고장으로 읽는 데서 난다.
            new(LastShiftTutorialStep.Harvest, nudgeAfterSeconds: 18f),

            // 5 — "적재의 경계 · 배 안은 안전하다". 동사가 아니라 선을 적는다(조항 T-2).
            new(LastShiftTutorialStep.Deposit, nudgeAfterSeconds: 30f),

            // 6 — "왕복이 루프의 단위다". 한 번 더 나가라고 시키는 것이 아니라, 왕복이
            // 세는 단위라는 것을 적는다.
            new(LastShiftTutorialStep.SecondTrip, nudgeAfterSeconds: 45f),

            // 7 — "화면 읽는 법". 열린 것을 셋으로 끊어 적는다: 탭 · 카탈로그 · 세울 자리.
            new(LastShiftTutorialStep.Schematic, nudgeAfterSeconds: 8f),

            // 8 — "고르기 → 자유면 → 회전 → 확정". 프롬프트는 조항 T-3 이 정한 그 한 줄이고,
            // 안내는 회전이 필요해질 이유("발자국이 안 맞는다")를 미리 적어 둔다.
            new(LastShiftTutorialStep.RotateFrame, nudgeAfterSeconds: 20f),

            // 9 — "자재가 0 인데 지을 수 있다"(조항 O-2). §2-1 이 이 튜토리얼에서 가장 값싼
            // 교육 장면이라 부른 자리다 — 잔액이 빈 것을 먼저 적고, 그래서 무엇으로 사는지를 적는다.
            new(LastShiftTutorialStep.HullUnlocked, nudgeAfterSeconds: 10f),

            // 10 — "규칙". 손을 떼는 단계라 시키는 말이 없다. 진입이 곧 완료여서
            // (LastShiftTutorial.HandOff) 띠에는 거의 안 걸리지만, 표에 구멍을 내지 않는다.
            new(LastShiftTutorialStep.HandsOff, nudgeAfterSeconds: 20f)
        };

        /// <summary>표의 행 수. 단계 <c>10</c>개와 같아야 한다 — 테스트가 이걸로 구멍을 잡는다.</summary>
        public static int LineCount => Table.Length;

        /// <summary>
        /// 이 단계의 문안 한 벌. <see cref="LastShiftTutorialStep.None"/> 과 표 밖의 값은
        /// <b>빈 행</b>이다 — 부르는 자리가 예외를 안 받고 "안 뜬다" 로 처리하게 한다.
        /// </summary>
        public static LastShiftTutorialLine Of(LastShiftTutorialStep step)
        {
            var index = (int)step - 1;
            return index >= 0 && index < Table.Length
                ? Table[index]
                : new LastShiftTutorialLine(LastShiftTutorialStep.None);
        }

        /// <summary>
        /// 띠 머리줄 — <c>튜토리얼 n/10 · 제목</c>. 진행이 몇 걸음인지가 보이는 유일한 자리라
        /// 번호가 문안에 붙는다(단계 번호는 조항 <c>T-9</c> 로그와 같은 번호다).
        /// </summary>
        public static string Heading(LastShiftTutorialStep step)
        {
            var line = Of(step);
            return line.Step == LastShiftTutorialStep.None
                ? string.Empty
                : $"튜토리얼 {(int)step}/{(int)LastShiftTutorialStep.HandsOff} · {line.Title}";
        }

        /// <summary>지금 띄울 안내 한 줄. 단계에 머문 시간이 넘으면 재촉으로 갈린다.</summary>
        public static string Guide(LastShiftTutorialStep step, float stepElapsedSeconds) =>
            Of(step).GuideAt(stepElapsedSeconds);
    }
}
