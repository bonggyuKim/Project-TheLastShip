using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// UI 아트 키트 v1 의 수치 규격을 <b>한 곳에 모은 자리</b>다
    /// (<c>docs/art/last-shift-ui-art-kit-v1.md</c> §"UGUI 연결 규격").
    ///
    /// 색과 크기를 화면마다 다시 적으면 같은 규격이 화면 수만큼 갈라진다 — 실제로 IMGUI
    /// 시절에 정상색이 파일 셋에 서로 다른 값으로 적혀 있었다. 여기 있는 값만 쓴다.
    ///
    /// <b>좌표 변환이 이 파일에 있는 이유</b>는 IMGUI 와 UGUI 가 공존하기 때문이다. 게이지·
    /// 프롬프트는 UGUI 로 옮겼지만 도면·로비의 글자는 아직 <c>OnGUI</c> 라, 9-slice 패널이
    /// 그 글자와 어긋나지 않으려면 화면 픽셀 좌표를 캔버스 좌표로 정확히 옮겨야 한다.
    /// 변환은 순수 함수라 EditMode 에서 검증된다 — <c>OnGUI</c> 는 그렇지 않았다.
    /// </summary>
    public static class LastShiftUiTheme
    {
        /// <summary>기준 캔버스. 아트 키트가 이 자를 전제로 크기를 정했다.</summary>
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        /// <summary>가로·세로 중 어느 쪽에 맞출지. 0.5 는 양쪽 절반씩이다.</summary>
        public const float ScreenMatch = 0.5f;

        /// <summary>정상 <c>#4FD8A0</c>. 게이지 채움과 판정 칩이 같이 쓴다.</summary>
        public static readonly Color Nominal = new(0.31f, 0.85f, 0.63f);

        /// <summary>불안정 <c>#FFDB59</c>.</summary>
        public static readonly Color Unstable = new(1f, 0.86f, 0.35f);

        /// <summary>고장. 불안정과 위기 사이 한 칸이라 키트 문서에는 없고 등급 어휘에만 있다.</summary>
        public static readonly Color Fault = new(1f, 0.58f, 0.2f);

        /// <summary>위기 <c>#FF5A4D</c>. 이 색만 1.5Hz 이하 명도 펄스가 허용된다.</summary>
        public static readonly Color Crisis = new(1f, 0.35f, 0.30f);

        /// <summary>아이보리 외곽선·글자.</summary>
        public static readonly Color Ivory = new(0.95f, 0.94f, 0.88f);

        /// <summary>짙은 청람 패널. 알파는 화면이 각자 정한다.</summary>
        public static readonly Color PanelNavy = new(0.09f, 0.13f, 0.22f);

        /// <summary>본문 글자색. IMGUI 시절 값 그대로다 — 두 층이 같이 보이는 동안 톤이 갈리면 안 된다.</summary>
        public static readonly Color BodyText = new(0.88f, 0.94f, 1f);

        /// <summary>HUD 아이콘 한 변. 32px 미만은 실루엣이 무너져 금지다.</summary>
        public const float IconSizeHud = 40f;

        /// <summary>월드 프롬프트 아이콘 한 변.</summary>
        public const float IconSizeWorld = 48f;

        /// <summary>결과 칩 아이콘 한 변. 키트가 정한 하한이다.</summary>
        public const float IconSizeChip = 32f;

        /// <summary>패널 최소 크기. 9-slice 모서리가 16px 이라 이 아래로는 모서리가 겹친다.</summary>
        public static readonly Vector2 PanelMinSize = new(192f, 96f);

        /// <summary>
        /// 위기 등급 명도 펄스. <b>알파가 아니라 명도만 흔든다</b> — 알파 점멸은 배경이 밝은
        /// 조종석 위에서 글자를 통째로 지운다(키트 §"UGUI 연결 규격" 마지막 줄).
        /// 1.5Hz 상한을 지키려고 위상을 <c>Time.unscaledTime</c> 에 1.5 를 곱해 만든다.
        /// </summary>
        public static Color PulseCrisis(float unscaledTime)
        {
            var phase = 0.5f + 0.5f * Mathf.Sin(unscaledTime * Mathf.PI * 1.5f);
            // 곱셈은 알파까지 같이 깎는다. 그러면 명도 펄스가 아니라 알파 점멸이 되고,
            // 밝은 조종석 위에서 게이지가 통째로 사라진다 — 검사에서 실제로 걸렸다.
            var dim = new Color(Crisis.r * 0.72f, Crisis.g * 0.72f, Crisis.b * 0.72f, Crisis.a);
            return Color.Lerp(dim, Crisis, phase);
        }

        /// <summary>
        /// O-7 <c>AI_F_W3</c>/<c>IsAutoReturnFlash</c> 전용: warning 프레임을 빠르게 두 번만 켠다.
        /// 실제 <c>IsDead</c>에는 쓰지 않는다. 위기색·사망 암전·반복 경보와 분리해,
        /// 회수는 실패 통보가 아니라 왕복 손실로 읽힌다.
        /// </summary>
        public static bool IsAutoReturnWarningPulse(float elapsedSeconds)
        {
            const float onSeconds = 0.12f;
            const float offSeconds = 0.08f;
            const int pulses = 2;
            if (elapsedSeconds < 0f || elapsedSeconds >= (onSeconds + offSeconds) * pulses) return false;
            return elapsedSeconds % (onSeconds + offSeconds) < onSeconds;
        }

        /// <summary>
        /// <see cref="UnityEngine.UI.CanvasScaler"/> 가 <c>ScaleWithScreenSize</c>·
        /// <c>MatchWidthOrHeight</c> 에서 쓰는 배율과 <b>같은 식</b>이다. 유니티가 내부에서
        /// 계산하는 값을 우리가 다시 알아야 하는 이유는 IMGUI 좌표를 캔버스 좌표로 옮기기
        /// 때문이고, 식이 어긋나면 패널만 어긋난 자리에 뜬다.
        /// </summary>
        public static float ScaleFactor(Vector2 screenSize)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f) return 1f;
            var logWidth = Mathf.Log(screenSize.x / ReferenceResolution.x, 2f);
            var logHeight = Mathf.Log(screenSize.y / ReferenceResolution.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, ScreenMatch));
        }

        /// <summary>캔버스 루트가 갖는 논리 크기. 화면 픽셀을 배율로 나눈 값이다.</summary>
        public static Vector2 CanvasSize(Vector2 screenSize)
        {
            var scale = ScaleFactor(screenSize);
            return scale <= 0f ? ReferenceResolution : screenSize / scale;
        }

        /// <summary>
        /// 화면 점(원점 좌상단, 화면 픽셀)을 캔버스 <b>같은 방향</b> 좌표로 옮긴다.
        /// y 는 아직 아래로 증가한다 — 자리 계산을 IMGUI 시절 함수 그대로 태우려면
        /// 방향을 여기서 뒤집으면 안 되고, 뒤집기는 <see cref="FlipY"/> 가 맨 마지막에 한다.
        /// </summary>
        public static Vector2 ScreenPointToCanvas(Vector2 guiPoint, Vector2 screenSize)
        {
            var scale = ScaleFactor(screenSize);
            return scale <= 0f ? guiPoint : guiPoint / scale;
        }

        /// <summary>
        /// 좌상단 기준 사각형을 <see cref="RectTransform"/> 이 먹는 형태로 뒤집는다.
        /// UGUI 는 위로 증가하므로 y 만 음수가 된다.
        /// </summary>
        public static Rect FlipY(Rect topLeftRect) =>
            new(topLeftRect.x, -topLeftRect.y, topLeftRect.width, topLeftRect.height);

        /// <summary>
        /// IMGUI 글자 크기(화면 픽셀)를 캔버스 글자 크기로 옮긴다.
        ///
        /// <b>자리만 옮기고 글자 크기를 그대로 두면 4K 에서 글자가 상자를 넘친다.</b>
        /// <see cref="ScreenRectToCanvas"/> 가 상자를 배율로 나누는데 글자는 안 나누면,
        /// 캔버스가 다시 배율만큼 키울 때 상자는 제자리이고 글자만 두 배가 된다.
        /// IMGUI 와 같은 크기로 보이려면 여기서 같은 배율로 나눠야 한다.
        ///
        /// 1 아래로는 내려가지 않는다 — <c>Text.fontSize</c> 가 0 이면 아무것도 안 그린다.
        /// </summary>
        public static int ScreenFontSizeToCanvas(int screenFontSize, Vector2 screenSize)
        {
            var scale = ScaleFactor(screenSize);
            if (scale <= 0f) return Mathf.Max(1, screenFontSize);
            return Mathf.Max(1, Mathf.RoundToInt(screenFontSize / scale));
        }

        /// <summary>
        /// IMGUI 화면 사각형(원점 좌상단, 화면 픽셀)을 캔버스 좌표로 옮긴다. 돌려주는
        /// <see cref="Rect"/> 의 <c>x,y</c> 는 <b>좌상단 기준 앵커 위치</b>이고 <c>width,height</c>
        /// 는 <c>sizeDelta</c> 다 — 그대로 <see cref="RectTransform"/> 에 꽂으면 된다.
        ///
        /// y 부호가 뒤집히는 것이 핵심이다. IMGUI 는 아래로 증가하고 UGUI 는 위로 증가한다.
        /// </summary>
        public static Rect ScreenRectToCanvas(Rect screenRect, Vector2 screenSize)
        {
            var scale = ScaleFactor(screenSize);
            if (scale <= 0f) scale = 1f;
            return new Rect(
                screenRect.x / scale,
                -screenRect.y / scale,
                screenRect.width / scale,
                screenRect.height / scale);
        }
    }
}
