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
            LastShiftTutorialStep step, string title, string guide, string nudge, float nudgeAfterSeconds, string prompt = "")
        {
            Step = step;
            Title = title;
            Guide = guide;
            Nudge = nudge;
            NudgeAfterSeconds = nudgeAfterSeconds;
            Prompt = prompt;
        }

        public LastShiftTutorialStep Step { get; }

        /// <summary>지금 어느 장면인가. 띠 머리줄에 단계 번호와 나란히 붙는 <b>짧은 말</b>이다.</summary>
        public string Title { get; }

        /// <summary>지금 할 것. 단계에 들어온 직후부터 뜬다.</summary>
        public string Guide { get; }

        /// <summary>못 찾고 있을 때. <see cref="NudgeAfterSeconds"/> 를 넘기면 <see cref="Guide"/> 자리를 뺏는다.</summary>
        public string Nudge { get; }

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
        public string Prompt { get; }

        public bool HasPrompt => !string.IsNullOrEmpty(Prompt);

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
            new(LastShiftTutorialStep.SightSalvage,
                "전면 스크린",
                "스크린 밖에 잔해 하나. 뒤쪽 개구부로 나간다",
                "몸을 돌린다 — 뒤가 열려 있다",
                10f),

            // 2 — "허브에서 문을 고른다". 문이 여럿인 것과 지금 고를 문이 하나인 것을 같이 적는다.
            new(LastShiftTutorialStep.CrossPlaza,
                "광장",
                "왼쪽 앞 문이 에어록 홀이다",
                "문 넷 중 왼쪽 앞. 나머지는 지금 볼 것 없다",
                12f),

            // 3 — "밖에는 시계가 있다 · 시계의 시작선은 바닥 사각형이다". 두 문장으로 갈라
            // 앞에 시계를, 뒤에 시작선을 둔다. 게이지가 뜨는 그 프레임에 읽히는 줄이다.
            new(LastShiftTutorialStep.AirlockHall,
                "에어록 홀",
                "바닥 우물을 넘으면 선외다 — 넘는 순간부터 산소가 흐른다",
                "우물이 시계의 시작선이다. 발을 딛는다",
                10f),

            // 4 — "파밍 동사 · 한 번에 둘이다". 손이 차는 것을 미리 적어 둔다: §1-1 이 짚은
            // 오학습은 "세 번째가 왜 안 뜯기지" 를 고장으로 읽는 데서 난다.
            new(LastShiftTutorialStep.Harvest,
                "선외 — 잔해",
                "자재를 뜯는다. 손은 두 덩이까지다",
                "세 번째는 안 뜯긴다 — 두 덩이면 돌아선다",
                18f),

            // 5 — "적재의 경계 · 배 안은 안전하다". 동사가 아니라 선을 적는다(조항 T-2).
            new(LastShiftTutorialStep.Deposit,
                "복귀",
                "홀 바닥에 서면 들고 있는 것이 하치대로 들어간다",
                "챔버를 올라와 바닥에 선다 — 그 바닥이 경계다",
                30f),

            // 6 — "왕복이 루프의 단위다". 한 번 더 나가라고 시키는 것이 아니라, 왕복이
            // 세는 단위라는 것을 적는다.
            new(LastShiftTutorialStep.SecondTrip,
                "두 번째 왕복",
                "잔해가 아직 남았다. 같은 길을 한 번 더 — 왕복이 세는 단위다",
                "필드를 비워야 도면이 열린다",
                45f),

            // 7 — "화면 읽는 법". 열린 것을 셋으로 끊어 적는다: 탭 · 카탈로그 · 세울 자리.
            new(LastShiftTutorialStep.Schematic,
                "도면",
                "거점 탭 · 카탈로그는 계류 골조 하나 · 굵은 면이 세울 자리다",
                "골조를 집어 굵은 면에 댄다",
                8f),

            // 8 — "고르기 → 자유면 → 회전 → 확정". 프롬프트는 조항 T-3 이 정한 그 한 줄이고,
            // 안내는 회전이 필요해질 이유("발자국이 안 맞는다")를 미리 적어 둔다.
            new(LastShiftTutorialStep.RotateFrame,
                "계류 골조",
                "굵은 면에 댄다 — 발자국이 안 맞으면 돌린다",
                "회전 R 또는 휠. 초록이 되면 확정된다",
                20f,
                "회전 R / 휠"),

            // 9 — "자재가 0 인데 지을 수 있다"(조항 O-2). §2-1 이 이 튜토리얼에서 가장 값싼
            // 교육 장면이라 부른 자리다 — 잔액이 빈 것을 먼저 적고, 그래서 무엇으로 사는지를 적는다.
            new(LastShiftTutorialStep.HullUnlocked,
                "선체 탭",
                "자재가 0 인데 방을 지을 수 있다 — 선체는 여력으로 산다",
                "선체 탭. 자유면이 배 둘레 전체로 번졌다",
                10f),

            // 10 — "규칙". 손을 떼는 단계라 시키는 말이 없다. 진입이 곧 완료여서
            // (LastShiftTutorial.HandOff) 띠에는 거의 안 걸리지만, 표에 구멍을 내지 않는다.
            new(LastShiftTutorialStep.HandsOff,
                "손을 뗀다",
                "붙일 자리는 정하는 사람 몫이다. 안 되면 빨간 한 줄이 뜬다",
                "빨간 줄을 읽으면 된다 — 규칙은 도면이 들고 있다",
                20f)
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
                : new LastShiftTutorialLine(LastShiftTutorialStep.None, string.Empty, string.Empty, string.Empty, 0f);
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
