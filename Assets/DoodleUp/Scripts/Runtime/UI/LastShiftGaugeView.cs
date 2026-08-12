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

        // <b>직렬화해야 프리팹에 실린다.</b> 이 뷰는 원래 런타임에 Create() 로만 세워졌고,
        // 그때는 이 참조들이 메모리에만 있으면 됐다. 상시 HUD 를 프리팹으로 구우면서 사정이
        // 바뀌었다 — 직렬화가 안 되면 프리팹에는 오브젝트만 남고 참조가 전부 비어서,
        // 인스턴스화한 HUD 가 값을 받아도 <b>아무것도 안 그린다</b>. 화면에는 빈 자리만 남고
        // 예외도 안 나므로 원인을 못 가린다.
        [SerializeField] private RectTransform root;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image movingMarker;
        [SerializeField] private Text valueLabel;
        [SerializeField] private Text nameLabel;
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

        /// <summary>
        /// <b>아이콘만 쓰는 배치</b>(아트 규격 <c>last-shift-hud-icon-only-v1.md</c>). 외곽선과
        /// 채움이 <b>사각형 전체</b>를 쓰고 글자·눈금·이동선은 전부 끈다.
        ///
        /// 같은 컴포넌트를 모드로 나눈 이유는 <b>채움 규약이 이미 맞기 때문</b>이다 —
        /// <c>Filled · Vertical · Bottom</c> 은 이 뷰가 처음부터 쓰던 값이고, 규격이 요구하는
        /// 것도 그것이다. 새 뷰를 세우면 그 세 줄만 복사된다.
        /// </summary>
        public void SetIconOnlyLayout(Rect canvasRect)
        {
            LastShiftUiFactory.Place(root, canvasRect);

            iconRect = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
            LastShiftUiFactory.Place((RectTransform)iconImage.transform, iconRect);
            LastShiftUiFactory.Place((RectTransform)fillImage.transform, iconRect);
            MakeIconOnly();
        }

        /// <summary>
        /// 아이콘 전용 <b>모양</b>만 잡는다 — <b>자리는 안 건드린다</b>.
        ///
        /// 프리팹으로 구운 HUD 가 이쪽을 쓴다. 위치까지 잡는
        /// <see cref="SetIconOnlyLayout"/> 을 쓰면 프리팹에서 끌어 옮긴 자리가 첫 프레임에
        /// 덮여서, 에디터 수정이 "저장은 되는데 게임에서는 안 보이는" 상태가 된다.
        ///
        /// 규격이 "숫자·%·이름·임계 눈금·이동선 없음" 이라 만들지 않는 대신 끈다 — 임대
        /// 구조에서는 조각이 이미 있고, 여기서 지우면 다음 프레임에 다시 만든다.
        /// </summary>
        public void MakeIconOnly()
        {
            // 아이콘과 채움은 부모를 가득 채운다. 부모 크기는 프리팹(또는 호출자)이 정한다.
            Stretch((RectTransform)iconImage.transform);
            Stretch((RectTransform)fillImage.transform);

            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
            if (valueLabel != null) valueLabel.gameObject.SetActive(false);
            SetThresholds();
            SetMovingMarker(-1f);
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
