using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// UGUI 조각을 코드로 짓는다.
    ///
    /// <b>왜 프리팹이 아닌가.</b> 이 프로젝트의 프리팹 배선은 이미 한 번 통째로 날아간 적이
    /// 있고(부트스트랩 메뉴가 79개 참조를 초기값으로 되돌렸다), HUD 는 씬 넷과 네트워크
    /// 프리팹에서 각각 뜬다. 코드로 지으면 씬을 건드리지 않고, EditMode 에서 계층을 그대로
    /// 세워 검증할 수 있다 — <c>OnGUI</c> 로는 못 하던 일이 이것이다.
    ///
    /// 모든 함수가 <b>좌상단 기준</b>으로 자리를 잡는다. IMGUI 좌표를 그대로 옮겨 오는 화면이
    /// 아직 남아 있어서, 앵커를 화면마다 다르게 두면 두 층이 어긋난다.
    /// </summary>
    public static class LastShiftUiFactory
    {
        private static Font cachedFont;

        /// <summary>
        /// 내장 폰트. 유니티 2022.2 에서 <c>Arial.ttf</c> 가 <c>LegacyRuntime.ttf</c> 로 바뀌었고
        /// 둘 다 동적 폰트라 한글은 OS 폰트로 대체된다 — IMGUI 기본 스킨과 같은 경로다.
        /// </summary>
        public static Font DefaultFont
        {
            get
            {
                if (cachedFont != null) return cachedFont;
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                             ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return cachedFont;
            }
        }

        /// <summary>
        /// 오버레이 캔버스 한 장. <b><see cref="GraphicRaycaster"/> 를 안 붙인다</b> — 이 UI 는
        /// 전부 표시 전용이고, 레이캐스터를 붙이면 <see cref="UnityEngine.EventSystems.EventSystem"/>
        /// 이 없는 씬에서 경고가 뜨는 대신 조준 클릭이 UI 에 먹힌다.
        /// </summary>
        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = LastShiftUiTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = LastShiftUiTheme.ScreenMatch;
            return canvas;
        }

        /// <summary>좌상단 앵커를 가진 빈 사각형. 자리는 <see cref="Place"/> 가 잡는다.</summary>
        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return rect;
        }

        /// <summary>
        /// 스프라이트 하나. <paramref name="sprite"/> 가 <c>null</c> 이면 단색 사각형이 되고
        /// 배치는 그대로다 — 키트를 아직 안 구운 상태에서도 계층과 값이 검증된다.
        /// </summary>
        public static Image CreateImage(Transform parent, string name, Sprite sprite, Image.Type type = Image.Type.Simple)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            // 9-slice 만 스프라이트가 있어야 성립한다. 나머지 모드는 스프라이트가 없으면
            // 흰 사각형으로 떨어지므로 그대로 둔다 — 채움 비율은 그림 없이도 읽힌다.
            image.type = sprite == null && type == Image.Type.Sliced ? Image.Type.Simple : type;
            image.raycastTarget = false;
            if (type == Image.Type.Sliced) image.pixelsPerUnitMultiplier = 1f;
            return image;
        }

        /// <summary>
        /// 9-slice 패널. 모서리 16px 아래로 줄이면 모서리끼리 겹쳐 테두리가 무너지므로
        /// 크기를 <see cref="LastShiftUiTheme.PanelMinSize"/> 로 바닥친다.
        /// </summary>
        public static Image CreatePanel(Transform parent, string name)
        {
            var kit = LastShiftUiKit.Instance;
            var image = CreateImage(parent, name, kit != null ? kit.Panel : null, Image.Type.Sliced);
            image.color = kit != null ? Color.white : new Color(LastShiftUiTheme.PanelNavy.r,
                LastShiftUiTheme.PanelNavy.g, LastShiftUiTheme.PanelNavy.b, 0.82f);
            return image;
        }

        /// <summary>화자가 있는 AI 온보딩만 쓰는 계기판 패널.</summary>
        public static Image CreateOnboardingPanel(Transform parent, string name)
        {
            var kit = LastShiftUiKit.Instance;
            var sprite = kit != null ? kit.OnboardingPanel : null;
            var image = CreateImage(parent, name, sprite, Image.Type.Sliced);
            image.color = sprite != null ? Color.white : new Color(0.094f, 0.149f, 0.188f, 0.94f);
            return image;
        }

        public static Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
        {
            var rect = CreateRect(parent, name);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        /// <summary>
        /// 캔버스 좌표 사각형에 앉힌다. 인자는 <see cref="LastShiftUiTheme.ScreenRectToCanvas"/>
        /// 가 돌려준 형식 그대로다 — y 는 이미 음수다.
        /// </summary>
        public static void Place(RectTransform rect, Rect canvasRect)
        {
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(canvasRect.x, canvasRect.y);
            rect.sizeDelta = new Vector2(canvasRect.width, canvasRect.height);
        }

        /// <summary>패널 하한을 물린 뒤 앉힌다.</summary>
        public static void PlacePanel(RectTransform rect, Rect canvasRect)
        {
            Place(rect, new Rect(
                canvasRect.x,
                canvasRect.y,
                Mathf.Max(canvasRect.width, LastShiftUiTheme.PanelMinSize.x),
                Mathf.Max(canvasRect.height, LastShiftUiTheme.PanelMinSize.y)));
        }
    }
}
