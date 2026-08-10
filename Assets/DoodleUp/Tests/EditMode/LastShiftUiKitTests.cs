using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// UGUI 전환의 바닥 — <b>좌표 변환·키트 배선·게이지 채움</b>.
    ///
    /// <c>OnGUI</c> 시절에는 이 셋이 전부 그리는 함수 안에 있어서 검증할 방법이 없었다.
    /// "게이지가 값에 따라 이미지로 차오르는가" 라는 완료 조건이 사람 눈으로만 확인되던
    /// 이유가 그것이고, 이 파일이 그 조건을 자동 검사로 내린다.
    ///
    /// <b>키트 에셋이 없어도 도는 검사와 있어야 도는 검사를 갈라 뒀다.</b> 배선 검사는
    /// 실제 에셋을 보고, 나머지는 스프라이트가 <c>null</c> 이어도 성립한다 — 배치와 값은
    /// 그림과 독립이어야 하고, 그래야 그림이 빠진 상태에서 화면이 통째로 안 죽는다.
    /// </summary>
    public sealed class LastShiftUiKitTests
    {
        [TearDown]
        public void TearDown()
        {
            LastShiftUiLayer.DestroyInstance();
            LastShiftUiLayer.ScreenSizeOverride = Vector2.zero;
            LastShiftUiKit.ResetLookup();
        }

        /// <summary>기준 해상도에서는 배율이 정확히 1 이라 캔버스 좌표가 화면 픽셀과 같다.</summary>
        [Test]
        public void ReferenceResolutionLeavesCoordinatesUntouched()
        {
            var screen = LastShiftUiTheme.ReferenceResolution;

            Assert.That(LastShiftUiTheme.ScaleFactor(screen), Is.EqualTo(1f).Within(0.0001f));

            var canvasRect = LastShiftUiTheme.ScreenRectToCanvas(new Rect(16f, 16f, 680f, 290f), screen);
            Assert.That(canvasRect.x, Is.EqualTo(16f).Within(0.001f));
            Assert.That(canvasRect.width, Is.EqualTo(680f).Within(0.001f));
            Assert.That(canvasRect.y, Is.EqualTo(-16f).Within(0.001f),
                "UGUI 는 y 가 위로 증가한다. 부호를 안 뒤집으면 좌상단 패널이 화면 위로 튀어나간다.");
        }

        /// <summary>
        /// 배율은 <see cref="CanvasScaler"/> 가 실제로 쓰는 값과 같아야 한다. 다르면 IMGUI
        /// 글자와 UGUI 패널이 해상도를 바꿀 때마다 조금씩 어긋난다.
        /// </summary>
        [Test]
        public void ScaleFactorMatchesCanvasScalerFormula()
        {
            var screen = new Vector2(2560f, 1440f);

            // 1920×1080 → 2560×1440 은 가로세로 배율이 둘 다 4/3 이라 match 와 무관하게 4/3 이다.
            Assert.That(LastShiftUiTheme.ScaleFactor(screen), Is.EqualTo(4f / 3f).Within(0.0001f));

            var canvas = LastShiftUiTheme.CanvasSize(screen);
            Assert.That(canvas.x, Is.EqualTo(1920f).Within(0.01f));
            Assert.That(canvas.y, Is.EqualTo(1080f).Within(0.01f));
        }

        /// <summary>가로세로비가 다르면 match 0.5 가 두 배율의 기하평균을 고른다.</summary>
        [Test]
        public void UltrawideSplitsTheDifferenceBetweenAxes()
        {
            var screen = new Vector2(3840f, 1080f);
            var scale = LastShiftUiTheme.ScaleFactor(screen);

            Assert.That(scale, Is.EqualTo(Mathf.Sqrt(2f)).Within(0.0001f),
                "가로만 두 배면 match 0.5 는 √2 를 쓴다 — 가로에 맞추면 세로가 잘리고 반대면 남는다.");
            Assert.That(scale, Is.GreaterThan(1f).And.LessThan(2f));
        }

        /// <summary>키트 에셋이 실제로 구워져 있고 17칸이 다 채워져 있는가.</summary>
        [Test]
        public void BakedKitAssetIsFullyWired()
        {
            var kit = Resources.Load<LastShiftUiKit>(LastShiftUiKit.ResourcePath);

            Assert.That(kit, Is.Not.Null,
                $"{LastShiftUiKit.AssetPath} 가 없다. DoodleUp/LAST SHIFT/UI 아트 키트 굽기 를 돌려라.");
            Assert.That(kit.IsFullyWired(), Is.True, "빈 칸이 있으면 그 화면만 조용히 단색으로 떨어진다.");
            Assert.That(kit.Panel.border, Is.EqualTo(new Vector4(16f, 16f, 16f, 16f)),
                "9-slice 경계가 없으면 패널을 늘릴 때 둥근 모서리가 같이 눌린다.");
            Assert.That(kit.SlicedPromptPlate.border.x, Is.GreaterThan(0f),
                "프롬프트판은 문장 길이만큼 늘어난다 — 경계가 0 이면 왼쪽 삼각 표식이 같이 눌린다.");
            Assert.That(kit.IconOf(LastShiftUiIcon.Oxygen).rect.size,
                Is.EqualTo(kit.FillOf(LastShiftGaugeChannel.Oxygen).rect.size),
                "외곽선과 채움은 같은 좌표에 겹친다 — 크기가 다르면 채움이 실루엣 밖으로 샌다.");
        }

        /// <summary>
        /// 완료 조건 그대로 — <b>값을 넣으면 채움 이미지가 그만큼 찬다</b>. 폭이 아니라
        /// <c>fillAmount</c> 가 움직여야 채움 무늬가 안 눌린다.
        /// </summary>
        [Test]
        public void GaugeFillTracksValueThroughFillAmount()
        {
            var layer = LastShiftUiLayer.EnsureInstance();
            var gauge = layer.Gauge("probe", LastShiftUiIcon.Oxygen, LastShiftGaugeChannel.Oxygen,
                new Rect(28f, 56f, 680f, 32f));

            var widthAtZero = ((RectTransform)gauge.Fill.transform).sizeDelta.x;

            gauge.SetValue(0.42f);
            Assert.That(gauge.Fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(gauge.Fill.fillMethod, Is.EqualTo(Image.FillMethod.Vertical),
                "아이콘은 아래에서 위로 찬다 — 가로로 채우면 실루엣이 좌우로 잘린다.");
            Assert.That(gauge.Fill.fillAmount, Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(((RectTransform)gauge.Fill.transform).sizeDelta.x, Is.EqualTo(widthAtZero).Within(0.001f),
                "채움 사각형의 폭이 값에 따라 변하면 사선·기포 무늬가 같이 늘었다 줄었다 한다.");

            gauge.SetValue(2f);
            Assert.That(gauge.Fill.fillAmount, Is.EqualTo(1f).Within(0.0001f));

            gauge.SetValue(float.NaN);
            Assert.That(gauge.Fill.fillAmount, Is.EqualTo(0f).Within(0.0001f),
                "NaN 이 들어오면 0 으로 떨어져야 한다 — 채움이 통째로 사라지는 것보다 낫다.");
        }

        /// <summary>채움은 외곽선과 <b>같은 사각형</b>을 쓴다. 어긋나면 채움이 실루엣 밖으로 샌다.</summary>
        [Test]
        public void GaugeFillSharesTheIconRect()
        {
            var layer = LastShiftUiLayer.EnsureInstance();
            var gauge = layer.Gauge("inset", LastShiftUiIcon.Materials, LastShiftGaugeChannel.Materials,
                new Rect(28f, 56f, 680f, 32f));

            var icon = (RectTransform)gauge.Icon.transform;
            var fill = (RectTransform)gauge.Fill.transform;

            Assert.That(fill.anchoredPosition, Is.EqualTo(icon.anchoredPosition));
            Assert.That(fill.sizeDelta, Is.EqualTo(icon.sizeDelta));
            Assert.That(icon.sizeDelta.x, Is.EqualTo(icon.sizeDelta.y).Within(0.001f),
                "아이콘은 정사각형이다 — 한 축만 눌리면 32px 에서 실루엣이 무너진다.");
        }

        /// <summary>임계선과 이동선은 서로 다른 굵기·색이라 같은 약속으로 안 읽힌다.</summary>
        [Test]
        public void ThresholdAndMovingMarkersStayDistinguishable()
        {
            Assert.That(LastShiftGaugeView.MovingMarkerWidth,
                Is.GreaterThan(LastShiftGaugeView.ThresholdWidth),
                "이동선이 더 가늘면 고정 임계선 위에 겹칠 때 사라진다.");
        }

        /// <summary>패널은 9-slice 모서리 아래로 안 줄어든다.</summary>
        [Test]
        public void PanelNeverShrinksBelowTheNineSliceCorners()
        {
            var layer = LastShiftUiLayer.EnsureInstance();
            var panel = layer.Panel("tiny", new Rect(0f, 0f, 24f, 12f));
            var rect = (RectTransform)panel.transform;

            Assert.That(rect.sizeDelta.x, Is.GreaterThanOrEqualTo(LastShiftUiTheme.PanelMinSize.x));
            Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(LastShiftUiTheme.PanelMinSize.y));
        }

        /// <summary>같은 이름으로 두 번 빌려도 조각은 하나다.</summary>
        [Test]
        public void BorrowingTheSameIdReusesOnePiece()
        {
            var layer = LastShiftUiLayer.EnsureInstance();
            var first = layer.Panel("hud", new Rect(16f, 16f, 680f, 290f));
            var second = layer.Panel("hud", new Rect(16f, 16f, 680f, 290f));

            Assert.That(second, Is.SameAs(first),
                "프레임마다 새로 만들면 화면이 도는 동안 계층이 무한히 자란다.");
        }
    }
}
