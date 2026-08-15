namespace DoodleUp.Runtime
{
    /// <summary>온보딩 띠를 지금 누가 잡고 있는가. <c>None</c> 이 곧 빈 화면이다.</summary>
    public enum LastShiftOnboardingBannerSource
    {
        /// <summary>아무도 안 잡고 있다 — <b>화면에 안내가 한 줄도 없다</b>.</summary>
        None = 0,

        /// <summary>상시 경고. 하던 일과 무관하게 끼어드는 상태 통지라 가장 위다.</summary>
        Standing = 1,

        /// <summary>기상 도입부.</summary>
        Wake = 2,

        /// <summary>순회 안내.</summary>
        Patrol = 3,

        /// <summary>진행 대사(디렉터).</summary>
        Director = 4,

        /// <summary>단계 안내 띠.</summary>
        Tutorial = 5
    }

    /// <summary>
    /// 온보딩 띠의 <b>주인을 고르는 자리</b>. 예전에는 이 우선순위가
    /// <c>LastShiftSandboxController.DrawTutorialBanner</c> 안의 <c>if</c> 사슬로만 있었고,
    /// 그래서 <b>어느 블록도 안 잡는 상태</b>를 화면 밖에서 잴 방법이 없었다.
    ///
    /// <b>그게 진단이 세 번 0 건으로 돌아온 이유다.</b> 배치 모드에는 <c>OnGUI</c> 가 아예 안
    /// 돌아서 그 사슬이 한 번도 실행되지 않는다 — 캔버스에 글자가 없는 것을 세는 검사는
    /// 헤드리스에서 <b>항상</b> 비고, 그 안의 계기도 <b>한 줄도</b> 안 남는다. 우선순위를 여기로
    /// 꺼내면 같은 판정을 렌더 없이 재게 되고, 그래서 이 파일이 검사 대상이다.
    ///
    /// <b>그리지 않는다.</b> 여기는 "무엇을 그릴지" 만 답하고 실제 배치·색·알파는
    /// 컨트롤러가 한다 — 상태기가 화면을 부르기 시작하면 이 판정을 다시 못 재게 된다.
    /// </summary>
    public static class LastShiftOnboardingBanner
    {
        /// <summary>지금 띠를 잡고 있는 블록. 순서가 컨트롤러의 그리기 순서와 <b>같아야 한다</b>.</summary>
        public static LastShiftOnboardingBannerSource Source
        {
            get
            {
                if (LastShiftStandingNarration.HasLine) return LastShiftOnboardingBannerSource.Standing;
                if (LastShiftWakeSequence.HasLine) return LastShiftOnboardingBannerSource.Wake;
                if (LastShiftPatrolNarration.HasLine && !LastShiftNarrationDirector.HasLine)
                    return LastShiftOnboardingBannerSource.Patrol;
                if (LastShiftNarrationDirector.HasLine) return LastShiftOnboardingBannerSource.Director;
                if (LastShiftTutorial.IsRunning) return LastShiftOnboardingBannerSource.Tutorial;
                return LastShiftOnboardingBannerSource.None;
            }
        }

        /// <summary>
        /// 그 블록이 이 프레임에 <b>실제로 실을 글자</b>. 타이핑 중이면 찍힌 만큼이고,
        /// 재촉으로 갈렸으면 재촉 문장이다 — 컨트롤러가 그리는 것과 같은 문자열이어야 한다.
        /// </summary>
        public static string Text
        {
            get
            {
                switch (Source)
                {
                    case LastShiftOnboardingBannerSource.Standing:
                        return LineText(LastShiftStandingNarration.Current,
                            LastShiftStandingNarration.LineElapsedSeconds);
                    case LastShiftOnboardingBannerSource.Wake:
                        return LineText(LastShiftWakeSequence.Current,
                            LastShiftWakeSequence.LineElapsedSeconds,
                            typed: !LastShiftWakeSequence.IsOpeningLine);
                    case LastShiftOnboardingBannerSource.Patrol:
                        return LineText(LastShiftPatrolNarration.Current,
                            LastShiftPatrolNarration.LineElapsedSeconds,
                            typingSeconds: LastShiftPatrolNarration.TypingSeconds);
                    case LastShiftOnboardingBannerSource.Director:
                        return LineText(LastShiftNarrationDirector.Current,
                            LastShiftNarrationDirector.LineElapsedSeconds);
                    case LastShiftOnboardingBannerSource.Tutorial:
                        return LastShiftTutorialCopy.Guide(
                            LastShiftTutorial.Step, LastShiftTutorial.StepElapsedSeconds);
                    default:
                        return string.Empty;
                }
            }
        }

        /// <summary>
        /// 그 글자의 진하기. <b>도입부 첫 줄만 <c>0</c> 에서 떠오른다</b> — 그 구간은 정상적으로
        /// 안 보이는 시간이라, 빈 화면 판정이 이 값을 같이 봐야 한다.
        /// </summary>
        public static float Alpha =>
            Source == LastShiftOnboardingBannerSource.Wake ? LastShiftWakeSequence.LineAlpha : 1f;

        /// <summary>이 프레임에 안내가 <b>읽히는가</b>. 글자가 있고 투명하지 않다.</summary>
        public static bool IsVisible => Alpha > 0f && !string.IsNullOrEmpty(Text);

        /// <summary>진단 한 줄. 로그와 검사가 같은 문자열을 쓴다.</summary>
        public static string Describe() =>
            $"source={Source} visible={IsVisible} alpha={Alpha:F2} " +
            $"chars={(Text != null ? Text.Length : 0)} " +
            $"standing={LastShiftStandingNarration.HasLine} " +
            $"wake={LastShiftWakeSequence.IsRunning}/{LastShiftWakeSequence.HasLine} " +
            $"patrol={LastShiftPatrolNarration.HasLine} " +
            $"patrolComplete={LastShiftPatrolNarration.IsComplete} " +
            $"director={LastShiftNarrationDirector.HasLine} " +
            $"tutorialRunning={LastShiftTutorial.IsRunning} " +
            $"armed={LastShiftTutorial.IsArmed} step={(int)LastShiftTutorial.Step}";

        /// <summary>
        /// 한 줄이 이 시점에 내는 문자열. <b>재촉 교체와 타이핑이 여기 한 곳에 있다</b> —
        /// 그리는 쪽과 재는 쪽이 각자 계산하면 그 둘이 갈리는 순간을 못 찾는다.
        /// </summary>
        public static string LineText(in LastShiftNarrationScript.Line line, float lineElapsed,
            bool typed = true, float typingSeconds = 0f)
        {
            var swapped = line.HasNudge && lineElapsed >= line.NudgeAfterSeconds;
            var text = swapped ? line.Nudge : LastShiftNarrationScript.Format(line);
            if (!typed) return text;
            return LastShiftNarrationScript.Reveal(text,
                swapped ? lineElapsed - line.NudgeAfterSeconds : lineElapsed,
                typingSeconds > 0f ? typingSeconds : LastShiftNarrationScript.TypingSeconds);
        }
    }
}
