using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 한 판이 끝난 시점의 요약. <b>결과 화면이 읽는 유일한 입력이다</b>.
    ///
    /// 값을 여기서 새로 계산하지 않는다 — 전부 판정 순간의 상태·장부에 이미 있던 것이고
    /// (<c>docs/game-feel-loop-review-v1.md</c> §3.1-a), 이 구조체는 그것을 판정 시점에
    /// 한 번 얼려 두는 자리다. 얼리지 않으면 결과 화면이 떠 있는 동안 배경 값이 변할 때
    /// 원인 줄이 같이 흔들린다.
    /// </summary>
    public readonly struct LastShiftRunSummary
    {
        public readonly LastShiftVerdict Verdict;
        public readonly float DockProgress;
        public readonly float ElapsedSeconds;
        public readonly float ThrustAtSettle;
        public readonly float HeatProtectionSeconds;
        public readonly int SacrificeCount;
        public readonly int QuickBypassCount;
        public readonly int BypassLapseCount;
        public readonly LastShiftZone AsphyxiationZone;

        public LastShiftRunSummary(
            LastShiftVerdict verdict,
            float dockProgress,
            float elapsedSeconds,
            float thrustAtSettle,
            float heatProtectionSeconds,
            int sacrificeCount,
            int quickBypassCount,
            int bypassLapseCount,
            LastShiftZone asphyxiationZone)
        {
            Verdict = verdict;
            DockProgress = dockProgress;
            ElapsedSeconds = elapsedSeconds;
            ThrustAtSettle = thrustAtSettle;
            HeatProtectionSeconds = heatProtectionSeconds;
            SacrificeCount = sacrificeCount;
            QuickBypassCount = quickBypassCount;
            BypassLapseCount = bypassLapseCount;
            AsphyxiationZone = asphyxiationZone;
        }

        /// <summary>
        /// 런 평균 추력. <c>DockProgress</c> 가 실제 추력적분(<c>thrust·s</c>)이므로
        /// 경과 초로 나누면 그대로 평균이 된다 — 별도 누적기를 두지 않는 이유다.
        /// </summary>
        public float AverageThrust => ElapsedSeconds <= 0f ? 0f : DockProgress / ElapsedSeconds;
    }

    /// <summary>결과 화면 요약 <c>4</c>칸 중 하나.</summary>
    public readonly struct LastShiftResultCell
    {
        public readonly string Label;
        public readonly string Value;

        /// <summary><c>0</c> 이라 색을 낮출 칸인가. "안 쓴 것" 은 조용해야 한다(아트 §3).</summary>
        public readonly bool Muted;

        /// <summary>판정색으로 칠할 칸인가. 도킹 진척 칸 하나뿐이다(아트 §3).</summary>
        public readonly bool UsesVerdictColor;

        public LastShiftResultCell(string label, string value, bool muted, bool usesVerdictColor)
        {
            Label = label;
            Value = value;
            Muted = muted;
            UsesVerdictColor = usesVerdictColor;
        }
    }

    /// <summary>
    /// 결과 화면 카피·색(<c>G-1</c>). <b>MonoBehaviour 도 GUI 도 아니다</b> — 판정 <c>5</c>종의
    /// 문장이 EditMode 에서 그대로 검사되어야 하기 때문이고, 그리기는
    /// <see cref="LastShiftResultScreen"/> 이 이 값을 받아서만 한다.
    ///
    /// 어휘는 <c>docs/game-feel-loop-review-v1.md</c> §3.1 표를 그대로 쓴다(새 용어 금지).
    /// 칩·색은 <c>docs/art/last-shift-result-screen-layout-v1.md</c> §5 다.
    /// </summary>
    public static class LastShiftResultCopy
    {
        public static readonly Color NominalColor = new(0.31f, 0.85f, 0.63f);      // #4FD8A0
        public static readonly Color CompromisedColor = new(1f, 0.86f, 0.35f);     // #FFDB59
        public static readonly Color FailureColor = new(1f, 0.35f, 0.30f);         // #FF5A4D

        public static string ChipOf(LastShiftVerdict verdict) => verdict switch
        {
            LastShiftVerdict.FailureAsphyxiation => "산소",
            LastShiftVerdict.FailureInsufficientThrust => "추력",
            _ => "도킹"
        };

        public static string HeadlineOf(LastShiftVerdict verdict) => verdict switch
        {
            LastShiftVerdict.SuccessNominalDocking => "정상 도킹",
            LastShiftVerdict.SuccessCompromised => "절충 생환",
            LastShiftVerdict.FailureAsphyxiation => "질식",
            LastShiftVerdict.FailureAdrift => "표류",
            LastShiftVerdict.FailureInsufficientThrust => "추력 부족",
            _ => string.Empty
        };

        /// <summary>
        /// 판정색. <b>실패 <c>3</c>종은 한 색이다</b> — 색으로 쪼개면 매 판 새 색을 배우게 되고,
        /// 셋의 구분은 칩과 원인 줄이 맡는다(아트 §1).
        /// </summary>
        public static Color ColorOf(LastShiftVerdict verdict) => verdict switch
        {
            LastShiftVerdict.SuccessNominalDocking => NominalColor,
            LastShiftVerdict.SuccessCompromised => CompromisedColor,
            LastShiftVerdict.Pending => Color.white,
            _ => FailureColor
        };

        /// <summary>
        /// 원인 줄. <b>이 처방의 요점이다</b>(§3.1-a) — 지금까지 실패한 플레이어가 얻는 정보는
        /// 아무것도 없었고 <c>dock=138.0</c> 은 로그를 열어야 보였다.
        ///
        /// 정상 도킹만 비어 있다. 성공에는 설명할 실패가 없고 <b>그 여백 자체가 정보다</b>(아트 §2).
        /// </summary>
        public static string CauseOf(in LastShiftRunSummary summary)
        {
            switch (summary.Verdict)
            {
                case LastShiftVerdict.SuccessCompromised:
                    return $"포기한 계통 {summary.SacrificeCount}개";
                case LastShiftVerdict.FailureAsphyxiation:
                    return $"{LastShiftZoneAtlas.ShortLabelOf(summary.AsphyxiationZone)} 산소 고갈 · 예비 산소 소진";
                case LastShiftVerdict.FailureAdrift:
                    return $"도킹 진척 {summary.DockProgress:F0}/{LastShiftRecoveryTuning.DockTargetThrustSeconds:F0} — " +
                           $"추력이 {ElapsedLabel(summary.ElapsedSeconds)} 평균 {summary.AverageThrust:F2}이었다";
                case LastShiftVerdict.FailureInsufficientThrust:
                    // 잠금이 한 번도 안 걸린 판에서 "(엔진 보호 잠금 0초)" 를 적으면 없던 사건을
                    // 원인으로 읽게 된다. 괄호는 실제로 잠긴 판에만 붙는다.
                    return summary.HeatProtectionSeconds >= 1f
                        ? $"도착 시점 추력 {summary.ThrustAtSettle:F2} " +
                          $"(엔진 보호 잠금 {summary.HeatProtectionSeconds:F0}초)"
                        : $"도착 시점 추력 {summary.ThrustAtSettle:F2}";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 요약 <c>4</c>칸(§3.1-b). 라벨은 <c>CT-01</c> §5.5 문장 그대로다.
        /// 도킹 진척만 값 색 규칙이 다르다 — 언제나 판정색이라 성공 화면에서 한 칸이 밝게
        /// 남고 미달이면 실패 화면에서 왜 졌는지가 한 번 더 보인다(아트 §3).
        /// </summary>
        public static LastShiftResultCell[] CellsOf(in LastShiftRunSummary summary)
        {
            return new[]
            {
                new LastShiftResultCell("포기한 것", $"{summary.SacrificeCount}", summary.SacrificeCount == 0, false),
                new LastShiftResultCell("임시 수리", $"{summary.QuickBypassCount}회", summary.QuickBypassCount == 0, false),
                new LastShiftResultCell("재이탈", $"{summary.BypassLapseCount}회", summary.BypassLapseCount == 0, false),
                new LastShiftResultCell("도킹 진척",
                    $"{summary.DockProgress:F0}/{LastShiftRecoveryTuning.DockTargetThrustSeconds:F0}", false, true)
            };
        }

        /// <summary>
        /// 다음 판 줄. 프리셋 이름은 <b>여기 하나뿐이고</b> 방금 끝난 판의 이름은 적지 않는다
        /// (<c>docs/last-shift-preset-names-v1.md</c> §4).
        /// </summary>
        public static string NextRunLineOf(LastShiftPreset nextPreset) =>
            $"다음 판 — {LastShiftSituationText.PresetDisplayName(nextPreset)} · [Space]";

        /// <summary>경과 시간 표기. 분 단위가 성립할 때만 분으로 적는다.</summary>
        public static string ElapsedLabel(float elapsedSeconds)
        {
            return elapsedSeconds >= 60f
                ? $"{Mathf.RoundToInt(elapsedSeconds / 60f)}분"
                : $"{Mathf.Max(0f, elapsedSeconds):F0}초";
        }
    }

    /// <summary>
    /// 결과 화면 그리기(<c>G-1</c>). 좌표·타이포·모션은
    /// <c>docs/art/last-shift-result-screen-layout-v1.md</c> §4·§7 을 그대로 옮긴 것이고,
    /// 무엇을 적을지는 <see cref="LastShiftResultCopy"/> 가 정한다.
    ///
    /// <b>씬 전환이 아니라 딤 + 중앙 카드다</b> — 판이 끝난 배가 뒤에 남아 있어야 "우리가 한 판"
    /// 이 결과와 같은 화면에 있다(아트 §1).
    /// </summary>
    public static class LastShiftResultScreen
    {
        /// <summary>아트 §4 좌표표의 기준 해상도. 그린 전체를 이 비율로 감싼다.</summary>
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        /// <summary>
        /// 다음 판 입력을 받기 시작하는 시각(아트 §7). <b>보이지 않는 입력을 먼저 받으면
        /// 결과를 못 읽고 넘어간 판이 생긴다.</b>
        /// </summary>
        public const float NextRunInputDelay = 0.80f;

        private static readonly Color DimColor = new(0.055f, 0.102f, 0.169f, 0.68f);   // #0E1A2B 68%
        private static readonly Color CardColor = new(0.102f, 0.180f, 0.282f);         // #1A2E48
        private static readonly Color RuleColor = new(0.188f, 0.298f, 0.427f);         // #304C6D
        private static readonly Color LabelColor = new(0.561f, 0.651f, 0.749f);        // #8FA6BF
        private static readonly Color MutedValueColor = new(0.431f, 0.510f, 0.600f);   // #6E8299
        private static readonly Color CauseColor = new(0.847f, 0.894f, 0.949f);        // #D8E4F2
        private static readonly Color NextRunColor = new(0.624f, 0.706f, 0.800f);      // #9FB4CC

        private static GUIStyle chipStyle;
        private static GUIStyle headlineStyle;
        private static GUIStyle causeStyle;
        private static GUIStyle cellLabelStyle;
        private static GUIStyle cellValueStyle;
        private static GUIStyle nextRunStyle;

        /// <summary>
        /// 스타일 여섯의 바탕. <b>검사 때문에 갈라 둔 자리다</b> — <c>GUI.skin</c> 은
        /// <c>OnGUI</c> 밖에서 읽으면 던지고 헤드리스 배치 모드에는 <c>OnGUI</c> 자체가 없어서,
        /// 이 한 줄이 없으면 "판정 줄이 번들 폰트로 그려지는가" 를 자동 검사로 물을 수가 없다.
        /// 실행할 때는 언제나 아래 기본값 그대로다.
        /// </summary>
        private static System.Func<GUIStyle> baseLabelStyle = () => new GUIStyle(GUI.skin.label);

        /// <summary>
        /// <paramref name="secondsSinceSettle"/> 는 판정 이후 경과한 실시간이다. 모션이 전부
        /// 여기에 걸려 있고, <see cref="NextRunInputDelay"/> 이전에는 하단 입력 줄이 없다.
        /// </summary>
        public static void Draw(in LastShiftRunSummary summary, LastShiftPreset nextPreset, float secondsSinceSettle)
        {
            if (summary.Verdict == LastShiftVerdict.Pending) return;
            EnsureStyles();

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;

            // 좌표표가 1920×1080 절대 픽셀이라 다른 해상도에서는 카드가 화면 비율을 잃는다(아트 §4).
            var scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            var offsetX = (Screen.width - ReferenceWidth * scale) * 0.5f;
            var offsetY = (Screen.height - ReferenceHeight * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));

            var verdictColor = LastShiftResultCopy.ColorOf(summary.Verdict);
            var dimAlpha = Ease01(secondsSinceSettle, 0f, 0.15f);
            var cardAlpha = Ease01(secondsSinceSettle, 0.10f, 0.30f);
            var cardRise = Mathf.Lerp(24f, 0f, cardAlpha);

            Fill(new Rect(0f, 0f, ReferenceWidth, ReferenceHeight), DimColor, dimAlpha);
            if (cardAlpha <= 0f)
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            const float cardX = 400f;
            var cardY = 250f + cardRise;
            const float cardWidth = 1120f;

            // 카드는 9-slice 패널로 바뀌었다(아트 키트 v1 §"화면별 적용"). <b>좌표는 v1 그대로다</b> —
            // 판정→원인→4칸이라는 읽는 순서가 이 좌표에 들어 있고, 바꾼 것은 배경 한 겹뿐이다.
            //
            // 캔버스가 아니라 화면 픽셀로 넘기는 이유는 이 화면이 <c>GUI.matrix</c> 로 자기
            // 배율(가로세로 중 작은 쪽 기준)을 따로 쓰기 때문이다. 캔버스의 match 0.5 와 다르므로
            // 같은 변환을 태워야 글자와 판이 안 어긋난다.
            LastShiftUiLayer.Instance?.Panel("resultCard",
                new Rect(offsetX + cardX * scale, offsetY + cardY * scale, cardWidth * scale, 460f * scale),
                cardAlpha);
            Fill(new Rect(cardX, cardY, cardWidth, 14f), verdictColor, cardAlpha);

            DrawChip(summary.Verdict, verdictColor, cardY, cardAlpha);
            DrawHeadline(summary.Verdict, verdictColor, cardY, cardAlpha, secondsSinceSettle);

            // 정상 도킹은 원인 줄이 비지만 자리는 그대로 둔다(아트 §2).
            var cause = LastShiftResultCopy.CauseOf(summary);
            if (!string.IsNullOrEmpty(cause))
                Label(new Rect(440f, cardY + 178f, 1040f, 40f), cause, causeStyle, CauseColor, cardAlpha);

            Fill(new Rect(440f, cardY + 252f, 1040f, 1f), RuleColor, cardAlpha);
            DrawCells(summary, verdictColor, cardY, cardAlpha, secondsSinceSettle);
            Fill(new Rect(440f, cardY + 372f, 1040f, 1f), RuleColor, cardAlpha);

            if (secondsSinceSettle >= NextRunInputDelay)
                Label(new Rect(440f, cardY + 396f, 1040f, 34f),
                    LastShiftResultCopy.NextRunLineOf(nextPreset), nextRunStyle, NextRunColor, 1f);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static void DrawChip(LastShiftVerdict verdict, Color verdictColor, float cardY, float alpha)
        {
            var chip = LastShiftResultCopy.ChipOf(verdict);
            var chipWidth = chipStyle.CalcSize(new GUIContent(chip)).x + 28f;
            var chipRect = new Rect(440f, cardY + 36f, chipWidth, 34f);
            Fill(chipRect, Color.Lerp(CardColor, verdictColor, 0.18f), alpha);
            Label(new Rect(chipRect.x + 14f, chipRect.y + 2f, chipWidth, 30f), chip, chipStyle, verdictColor, alpha);
        }

        private static void DrawHeadline(LastShiftVerdict verdict, Color verdictColor, float cardY, float alpha,
            float secondsSinceSettle)
        {
            var rect = new Rect(440f, cardY + 76f, 1040f, 84f);
            var pop = 1f + 0.06f * (1f - Ease01(secondsSinceSettle, 0.30f, 0.42f));
            var previousMatrix = GUI.matrix;
            if (secondsSinceSettle < 0.42f)
            {
                var pivot = new Vector2(rect.x, rect.y + rect.height * 0.5f);
                GUI.matrix *= Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one)
                              * Matrix4x4.Scale(new Vector3(pop, pop, 1f))
                              * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            }

            Label(rect, LastShiftResultCopy.HeadlineOf(verdict), headlineStyle, verdictColor, alpha);
            GUI.matrix = previousMatrix;
        }

        private static void DrawCells(in LastShiftRunSummary summary, Color verdictColor, float cardY, float alpha,
            float secondsSinceSettle)
        {
            var cells = LastShiftResultCopy.CellsOf(summary);
            for (var index = 0; index < cells.Length; index++)
            {
                // 좌→우 0.06s 간격(아트 §7). 값 카운트업은 없다 — 세는 값이 0~3 이라 성립하지 않는다.
                var cellAlpha = alpha * Ease01(secondsSinceSettle, 0.45f + 0.06f * index, 0.75f + 0.06f * index);
                if (cellAlpha <= 0f) continue;

                var x = 440f + 260f * index;
                Label(new Rect(x, cardY + 276f, 260f, 26f), cells[index].Label, cellLabelStyle, LabelColor, cellAlpha);
                var valueColor = cells[index].UsesVerdictColor
                    ? verdictColor
                    : cells[index].Muted ? MutedValueColor : Color.white;
                Label(new Rect(x, cardY + 302f, 260f, 48f), cells[index].Value, cellValueStyle, valueColor, cellAlpha);
            }
        }

        /// <summary>구간 [from, to] 을 ease-out 으로 0→1 로 민다. 구간 밖은 각각 0·1 이다.</summary>
        private static float Ease01(float time, float from, float to)
        {
            if (to <= from) return time >= to ? 1f : 0f;
            var t = Mathf.Clamp01((time - from) / (to - from));
            return 1f - (1f - t) * (1f - t);
        }

        private static void Fill(Rect rect, Color color, float alpha)
        {
            if (alpha <= 0f) return;
            GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color, float alpha)
        {
            if (alpha <= 0f || string.IsNullOrEmpty(text)) return;
            style.normal.textColor = color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }

        private static void EnsureStyles()
        {
            // 폰트는 전부 번들 한글 폰트로 간다(<see cref="LastShiftFonts"/>). 이 화면이 그 카드를
            // 부른 자리다 — 판정 줄이 64px 이라 OS 폴백 서체가 PC 마다 다른 게 여기서 제일 크게
            // 보였다(아트 §6).
            //
            // 판정 큰 줄만 Bold 가 아닌 것은 그대로 둔다. 번들 폰트에도 Bold 웨이트를 따로 넣지
            // 않아 64px 합성 굵기는 여전히 획이 뭉치고, 위계는 크기와 색으로 이미 선다(아트 §6).
            headlineStyle ??= Style(64, FontStyle.Normal);
            chipStyle ??= Style(22, FontStyle.Bold);
            causeStyle ??= Style(28, FontStyle.Normal);
            cellLabelStyle ??= Style(20, FontStyle.Normal);
            cellValueStyle ??= Style(36, FontStyle.Bold);
            nextRunStyle ??= Style(24, FontStyle.Normal);
        }

        private static GUIStyle Style(int fontSize, FontStyle fontStyle)
        {
            var style = baseLabelStyle();
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            return LastShiftFonts.Apply(style);
        }
    }
}
