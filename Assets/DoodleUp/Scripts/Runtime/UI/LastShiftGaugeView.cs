using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 게이지 한 줄 — 아이콘 한 장 · 이름 · 바깥 숫자 · 눈금.
    ///
    /// <b>가로 막대가 없다.</b> 키트 v1 이 별도 바를 없애고 아이콘 자체를 계기로 삼았다
    /// (<c>docs/art/last-shift-ui-art-kit-v1.md</c> §"트레이드오프"). 축마다 <c>base</c>(빈
    /// 외곽선)와 <c>fill</c>(컬러 채움) 두 장이 <b>같은 좌표에 겹쳐</b> 오고, 채움 쪽만
    /// 아래에서 위로 차오른다 — 그림을 굽는 <c>icon_pair</c> 가 그 용도로 짝을 맞춰 낸다.
    ///
    /// <b>채움은 <see cref="Image.fillAmount"/> 만 움직인다</b>(키트 §"UGUI 연결 규격").
    /// 사각형 크기를 직접 줄이면 아이콘 실루엣이 같이 눌려서, 색각 이상에서 축을 갈라 주던
    /// 모양이 값에 따라 변형된다.
    ///
    /// <b>숫자는 아이콘 바깥에 둔다</b> — 30% 이하 잔량에서 채움 위 흰 글자는 대비가 무너진다.
    ///
    /// 눈금은 둘로 갈린다. <see cref="SetThresholds"/> 는 고정 임계선(아이보리)이고
    /// <see cref="SetMovingMarker"/> 는 매 초 움직이는 필요선(연녹)이다. 같은 색이면 둘이
    /// 같은 종류의 약속으로 읽힌다 — IMGUI 시절에도 이 구분이 있었고 그대로 지킨다.
    /// <b>눈금은 가로줄이다</b> — 채움이 세로로 차오르므로 값이 같은 자리는 수평선이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftGaugeView : MonoBehaviour
    {
        /// <summary>고정 임계선 굵기(캔버스 단위).</summary>
        public const float ThresholdWidth = 2f;

        /// <summary>이동선 굵기. 고정선보다 굵어야 겹칠 때 가려지지 않는다.</summary>
        public const float MovingMarkerWidth = 4f;

        /// <summary>아이콘과 이름 사이 여백.</summary>
        private const float IconGap = 8f;

        private RectTransform root;
        private Image iconImage;
        private Image fillImage;
        private Image movingMarker;
        private Text valueLabel;
        private Text nameLabel;
        private readonly List<Image> thresholdMarks = new();

        /// <summary>아이콘이 차지하는 캔버스 사각형. 눈금이 이 높이를 기준으로 자리를 잡는다.</summary>
        private Rect iconRect;

        private float[] thresholdValues = System.Array.Empty<float>();
        private float movingMarkerValue = -1f;

        public float Value { get; private set; }

        public Image Fill => fillImage;
        public Image Icon => iconImage;
        public Text ValueLabel => valueLabel;

        /// <summary>
        /// 한 줄을 짓는다. 자리는 <see cref="SetLayout"/> 가 따로 잡는다 — HUD 가 줄 수를
        /// 상황에 따라 바꾸므로 만드는 시점에 자리를 못 정한다.
        /// </summary>
        public static LastShiftGaugeView Create(Transform parent, string name, LastShiftUiIcon icon)
        {
            var rect = LastShiftUiFactory.CreateRect(parent, name);
            var view = rect.gameObject.AddComponent<LastShiftGaugeView>();
            view.Build(icon);
            return view;
        }

        private void Build(LastShiftUiIcon icon)
        {
            var kit = LastShiftUiKit.Instance;
            root = (RectTransform)transform;

            iconImage = LastShiftUiFactory.CreateImage(root, "IconBase", kit != null ? kit.IconOf(icon) : null);
            iconImage.color = LastShiftUiTheme.Ivory;

            // 채움은 외곽선의 자식이 아니라 형제다. 자식으로 두면 부모가 먼저 그려지는 순서라
            // 외곽선이 채움을 덮고, 낮은 값에서 채움이 통째로 안 보인다.
            fillImage = LastShiftUiFactory.CreateImage(root, "IconFill", kit != null ? kit.FillOf(icon) : null);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            fillImage.fillAmount = 0f;
            fillImage.color = LastShiftUiTheme.Nominal;

            movingMarker = LastShiftUiFactory.CreateImage(root, "MovingMarker", null);
            movingMarker.color = LastShiftUiTheme.Nominal;
            movingMarker.gameObject.SetActive(false);

            nameLabel = LastShiftUiFactory.CreateText(root, "Name", 16, TextAnchor.MiddleLeft, LastShiftUiTheme.BodyText);
            valueLabel = LastShiftUiFactory.CreateText(root, "Value", 16, TextAnchor.MiddleRight, LastShiftUiTheme.Ivory);
        }

        /// <summary>
        /// 줄 하나가 놓일 자리. <paramref name="canvasRect"/> 는 <b>아이콘부터 숫자까지</b>
        /// 전체 폭이고, 이름은 아이콘과 숫자 사이에 남는 만큼을 갖는다.
        /// </summary>
        public void SetLayout(Rect canvasRect, float labelWidth = 96f)
        {
            LastShiftUiFactory.Place(root, canvasRect);

            var height = canvasRect.height;
            var iconSize = Mathf.Min(LastShiftUiTheme.IconSizeHud, height);

            // 아이콘은 줄 높이 안에서 세로 가운데다. 외곽선과 채움이 <b>같은 사각형</b>을 쓰는
            // 것이 이 화면의 전부다 — 어긋나면 채움이 실루엣 밖으로 새어 나온다.
            iconRect = new Rect(0f, -(height - iconSize) * 0.5f, iconSize, iconSize);
            LastShiftUiFactory.Place((RectTransform)iconImage.transform, iconRect);
            LastShiftUiFactory.Place((RectTransform)fillImage.transform, iconRect);

            var textX = iconSize + IconGap;
            var nameWidth = Mathf.Max(32f, canvasRect.width - textX - labelWidth - IconGap);
            LastShiftUiFactory.Place((RectTransform)nameLabel.transform,
                new Rect(textX, -(height - 20f) * 0.5f, nameWidth, 20f));
            LastShiftUiFactory.Place((RectTransform)valueLabel.transform,
                new Rect(textX + nameWidth + IconGap, -(height - 20f) * 0.5f, labelWidth, 20f));

            RefreshMarkerPositions();
        }

        /// <summary>이름 줄. 아이콘 오른쪽이라 채움과 겹치지 않는다.</summary>
        public void SetName(string text)
        {
            if (nameLabel != null) nameLabel.text = text ?? string.Empty;
        }

        public void SetValue(float value01)
        {
            Value = Mathf.Clamp01(float.IsNaN(value01) ? 0f : value01);
            if (fillImage != null) fillImage.fillAmount = Value;
        }

        /// <summary>숫자는 보조 정보다. 비우면 아이콘만 남는다.</summary>
        public void SetValueLabel(string text)
        {
            if (valueLabel != null) valueLabel.text = text ?? string.Empty;
        }

        public void SetTone(Color color)
        {
            if (fillImage != null) fillImage.color = color;
        }

        public void SetVisible(bool visible)
        {
            if (root != null && root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }

        /// <summary>고정 임계선. 넘겨준 개수만큼 표식을 늘리고 남는 것은 끈다.</summary>
        public void SetThresholds(params float[] thresholds)
        {
            var count = thresholds?.Length ?? 0;
            while (thresholdMarks.Count < count)
            {
                var mark = LastShiftUiFactory.CreateImage(root, $"Threshold{thresholdMarks.Count}", null);
                mark.color = LastShiftUiTheme.Ivory;
                thresholdMarks.Add(mark);
            }

            for (var index = 0; index < thresholdMarks.Count; index++)
            {
                var active = index < count;
                thresholdMarks[index].gameObject.SetActive(active);
                if (active) thresholdMarks[index].name = $"Threshold{index}:{thresholds[index]:F2}";
            }

            thresholdValues = thresholds ?? System.Array.Empty<float>();
            RefreshMarkerPositions();
        }

        /// <summary>이동선. 음수면 끈다 — 운석 전에는 그릴 값 자체가 없다.</summary>
        public void SetMovingMarker(float value01)
        {
            movingMarkerValue = value01;
            if (movingMarker == null) return;
            var visible = value01 >= 0f;
            if (movingMarker.gameObject.activeSelf != visible) movingMarker.gameObject.SetActive(visible);
            if (visible) RefreshMarkerPositions();
        }

        /// <summary>
        /// 눈금 자리. 채움이 아래에서 위로 차오르므로 <b>값이 같은 자리는 가로줄</b>이고,
        /// 아이콘 아래변에서 값 비율만큼 올라간 높이에 놓인다.
        ///
        /// y 가 음수로 커지는 좌표라, 아래변은 <c>iconRect.y - iconRect.height</c> 이고
        /// 위로 가려면 <b>더한다</b>.
        /// </summary>
        private void RefreshMarkerPositions()
        {
            if (iconRect.height <= 0f) return;
            var bottom = iconRect.y - iconRect.height;

            for (var index = 0; index < thresholdMarks.Count && index < thresholdValues.Length; index++)
            {
                if (!thresholdMarks[index].gameObject.activeSelf) continue;
                LastShiftUiFactory.Place((RectTransform)thresholdMarks[index].transform, new Rect(
                    iconRect.x,
                    bottom + iconRect.height * Mathf.Clamp01(thresholdValues[index]) + ThresholdWidth * 0.5f,
                    iconRect.width,
                    ThresholdWidth));
            }

            if (movingMarker != null && movingMarker.gameObject.activeSelf)
                LastShiftUiFactory.Place((RectTransform)movingMarker.transform, new Rect(
                    iconRect.x,
                    bottom + iconRect.height * Mathf.Clamp01(movingMarkerValue) + MovingMarkerWidth * 0.5f,
                    iconRect.width,
                    MovingMarkerWidth));
        }
    }
}
