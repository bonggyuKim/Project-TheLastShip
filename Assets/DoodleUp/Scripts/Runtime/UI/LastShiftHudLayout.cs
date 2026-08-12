using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 상시 HUD 의 자리표.
    ///
    /// <b>좌표가 코드 곳곳에 흩어져 있던 것이 이 화면의 오래된 문제였다</b> — 막대 하나를
    /// 옮기면 그 아래 다섯 줄을 손으로 다시 세어야 했고, 실제로 줄이 겹친 채로 커밋된 적이
    /// 있다. 줄 간격을 상수 하나로 두고 나머지를 거기서 파생시킨다.
    ///
    /// 단위는 <b>화면 픽셀·원점 좌상단</b>이다. 아직 IMGUI 로 남은 글자와 같은 자를 써야
    /// 겹치지 않기 때문이고, UGUI 로 넘길 때는 <see cref="LastShiftUiTheme.ScreenRectToCanvas"/>
    /// 가 한 번에 옮긴다.
    /// </summary>
    public static class LastShiftHudLayout
    {
        /// <summary>머리줄 글자 크기(화면 픽셀). 목표 줄·지배 줄·디버그 머리가 같이 쓴다.</summary>
        public const int HeadingFontSize = 20;

        /// <summary>본문 글자 크기(화면 픽셀).</summary>
        public const int BodyFontSize = 14;

        /// <summary>잔액 배지. 본문과 머리 사이 한 칸이라 튀지 않고도 눈에 든다.</summary>
        public const int BadgeFontSize = 16;

        /// <summary>
        /// 상시 HUD 아이콘 한 변. 아트 규격 <c>last-shift-hud-icon-only-v1.md</c> 값이다.
        /// </summary>
        public const float HudIconSize = 56f;

        /// <summary>아이콘 사이 세로 간격.</summary>
        public const float HudIconGap = 12f;

        /// <summary>화면 오른쪽 끝에서 아이콘까지. 기준 <c>1920</c> 에서 <c>x 1816</c> 이 되는 값이다.</summary>
        public const float HudIconRightMargin = 48f;

        /// <summary>화면 위에서 첫 아이콘까지.</summary>
        public const float HudIconTop = 28f;

        /// <summary>상시 HUD 아이콘 수 — 산소 · 전력 · 열.</summary>
        public const int HudIconCount = 3;

        /// <summary>
        /// 상시 HUD 아이콘 자리. <b>우측 상단에 세로로 셋</b>이고 그 밖에는 아무것도 없다
        /// (아트 규격). 화면 폭에서 오른쪽 여백을 빼므로 해상도가 달라도 오른쪽에 붙는다.
        /// </summary>
        public static Rect HudIconRect(float screenWidth, int slot) =>
            new(screenWidth - HudIconRightMargin - HudIconSize,
                HudIconTop + slot * (HudIconSize + HudIconGap),
                HudIconSize, HudIconSize);

        public const float PanelX = 16f;
        public const float PanelY = 16f;
        public const float PanelWidth = 720f;

        /// <summary>패널 안쪽 왼쪽 여백. 게이지와 글자가 같이 쓴다.</summary>
        public const float ContentX = 28f;

        /// <summary>게이지가 쓰는 가로 폭. 아이콘·프레임·바깥 숫자를 다 담는다.</summary>
        public const float GaugeWidth = 680f;

        /// <summary>게이지 한 줄 높이와 줄 간격.</summary>
        public const float GaugeHeight = 32f;
        public const float GaugePitch = 36f;

        /// <summary>첫 계통 게이지(추력)의 윗변. 위에는 목표 줄이 있다.</summary>
        public const float SystemGaugeTop = 56f;

        /// <summary>계통 셋 + 도킹 하나.</summary>
        public const int SystemGaugeCount = 4;

        /// <summary>자원 넷의 첫 줄. 계통 묶음과 한 칸 띄운다 — 두 묶음은 다른 종류의 정보다.</summary>
        public const float ResourceGaugeTop = SystemGaugeTop + SystemGaugeCount * GaugePitch + 12f;

        public const int ResourceGaugeCount = 4;

        /// <summary>구역 압력 4칸 줄.</summary>
        public const float ZoneCellsTop = ResourceGaugeTop + ResourceGaugeCount * GaugePitch + 8f;

        /// <summary>지배 문제 1행과 배터리 한 줄.</summary>
        public const float DominantLineTop = ZoneCellsTop + 30f;

        /// <summary>구역 안에서만 뜨는 원인 1행.</summary>
        public const float DiagnosisTop = DominantLineTop + 32f;

        public const float PanelHeight = DiagnosisTop + 24f + 12f - PanelY;

        /// <summary>패널 전체.</summary>
        public static Rect PanelRect => new(PanelX, PanelY, PanelWidth, PanelHeight);

        /// <summary>계통·도킹 게이지 <paramref name="index"/> 번째 줄(0=추력).</summary>
        public static Rect SystemGaugeRect(int index) =>
            new(ContentX, SystemGaugeTop + index * GaugePitch, GaugeWidth, GaugeHeight);

        /// <summary>자원 게이지 <paramref name="index"/> 번째 줄(0=정비여력).</summary>
        public static Rect ResourceGaugeRect(int index) =>
            new(ContentX, ResourceGaugeTop + index * GaugePitch, GaugeWidth, GaugeHeight);

        /// <summary>디버그 층. 상시 패널 <b>아래</b>에 붙는다 — 겹치면 둘 다 못 읽는다.</summary>
        public static Rect DebugPanelRect => new(PanelX, PanelY + PanelHeight + 8f, PanelWidth, 208f);

        /// <summary>튜토리얼 띠. 화면 아래에 붙으므로 높이를 인자로 받는다.</summary>
        public static Rect TutorialBannerRect(float screenHeight) =>
            new(PanelX, screenHeight - 120f, 680f, 104f);

        /// <summary>AI 내레이션은 하단 중앙·화면 폭 70% 이내의 계기판으로 표시한다.</summary>
        public static Rect OnboardingNarrationRect(float screenWidth, float screenHeight)
        {
            var width = Mathf.Min(screenWidth * 0.70f, 1344f);
            return new Rect((screenWidth - width) * 0.5f, screenHeight - 216f, width, 184f);
        }

        /// <summary>개인 예비 산소 줄. 튜토리얼 띠 위로 쌓는다.</summary>
        public static Rect SuitGaugeRect(float screenHeight, int row) =>
            new(24f, screenHeight - 96f - row * GaugePitch, 420f, GaugeHeight);
    }
}
