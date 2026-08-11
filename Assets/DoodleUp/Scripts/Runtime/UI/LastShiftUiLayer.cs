using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// LAST SHIFT 화면들이 공유하는 UGUI 층 하나.
    ///
    /// <b>왜 한 장인가.</b> 화면을 그리는 컴포넌트가 다섯이고(샌드박스·플레이어·도면·로비·
    /// 네트워크 플레이어) 각자 캔버스를 세우면 정렬 순서가 씬마다 달라진다. 실제로 IMGUI
    /// 시절에도 로비 위에 HUD 가 겹치는 사고가 났고, 그때 쓴 해법이 <c>IsBlockingGameplay</c>
    /// 같은 전역 차단이었다. 층이 하나면 순서는 형제 순서 하나로 정해진다.
    ///
    /// <b>수명은 프레임 단위 임대다.</b> 화면은 자기가 이번 프레임에 필요한 조각을 이름으로
    /// 빌려 가고, 아무도 안 빌려 간 조각은 다음 프레임에 꺼진다. 그래서 판정이 나거나 로비로
    /// 돌아갈 때 <b>지우는 코드를 따로 안 써도</b> 화면이 비는데, 이건 IMGUI 의 "안 그리면
    /// 안 보인다" 와 같은 사용감이라 옮겨 오는 쪽 코드가 거의 안 바뀐다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftUiLayer : MonoBehaviour
    {
        /// <summary>
        /// 꺼지기까지 봐 주는 프레임 수. <c>OnGUI</c> 는 <c>LateUpdate</c> <b>뒤에</b> 도므로
        /// 여유가 0 이면 OnGUI 에서 빌린 조각이 매 프레임 한 번씩 꺼졌다 켜져 깜빡인다.
        /// </summary>
        private const int LeaseGraceFrames = 1;

        private static LastShiftUiLayer instance;
        private static bool quitting;

        private Canvas canvas;
        private RectTransform panelRoot;
        private RectTransform gaugeRoot;
        private RectTransform overlayRoot;

        private readonly Dictionary<string, Lease<Image>> panels = new();
        private readonly Dictionary<string, Lease<LastShiftGaugeView>> gauges = new();
        private readonly Dictionary<string, Lease<Text>> labels = new();

        private struct Lease<T>
        {
            public T Item;
            public int Frame;
        }

        /// <summary>
        /// 화면 크기 대체값. 0 이면 실제 화면을 본다. EditMode 는 <c>Screen</c> 이
        /// 에디터 창 크기를 돌려주므로, 좌표를 검증하려면 이 자리가 필요하다.
        /// </summary>
        public static Vector2 ScreenSizeOverride { get; set; }

        public static Vector2 ScreenSize =>
            ScreenSizeOverride.x > 0f && ScreenSizeOverride.y > 0f
                ? ScreenSizeOverride
                : new Vector2(Screen.width, Screen.height);

        /// <summary>
        /// 층 하나. 없으면 만든다. <b>씬에 미리 두지 않는다</b> — 솔로 씬·네트워크 씬·
        /// 테스트 팩토리가 각각 다른 경로로 서고, 씬 저작에 의존하면 그중 하나는 반드시 빠진다.
        /// </summary>
        public static LastShiftUiLayer Instance
        {
            get
            {
                if (instance != null) return instance;
                if (quitting || !Application.isPlaying) return null;
                return EnsureInstance();
            }
        }

        /// <summary>재생 중이 아닐 때(EditMode 테스트)도 세운다. 정리는 부른 쪽 몫이다.</summary>
        public static LastShiftUiLayer EnsureInstance()
        {
            if (instance != null) return instance;
            var go = new GameObject("LastShiftUiLayer");
            instance = go.AddComponent<LastShiftUiLayer>();
            return instance;
        }

        public Canvas Canvas => canvas;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // 씬 전환이 겹치면 두 장이 살 수 있다. 늦게 온 쪽이 물러난다.
                Destroy(gameObject);
                return;
            }

            instance = this;
            canvas = LastShiftUiFactory.CreateCanvas("Canvas", 0);
            canvas.transform.SetParent(transform, false);

            // 형제 순서가 곧 그리는 순서다. 패널이 가장 먼저, 프롬프트가 가장 나중이다.
            panelRoot = LastShiftUiFactory.CreateRect(canvas.transform, "Panels");
            gaugeRoot = LastShiftUiFactory.CreateRect(canvas.transform, "Gauges");
            overlayRoot = LastShiftUiFactory.CreateRect(canvas.transform, "Overlay");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnApplicationQuit()
        {
            quitting = true;
        }

        /// <summary>
        /// 9-slice 패널 하나를 이번 프레임에 빌린다. <paramref name="screenRect"/> 는
        /// IMGUI 와 같은 좌표(원점 좌상단, 화면 픽셀)라 <c>GUI.Box</c> 를 한 줄로 갈아 끼운다.
        /// </summary>
        public Image Panel(string id, Rect screenRect, float alpha = 1f)
        {
            var image = Borrow(panels, id, () =>
            {
                var created = LastShiftUiFactory.CreatePanel(panelRoot, $"Panel:{id}");
                return created;
            });

            LastShiftUiFactory.PlacePanel((RectTransform)image.transform,
                LastShiftUiTheme.ScreenRectToCanvas(screenRect, ScreenSize));
            var color = image.color;
            image.color = new Color(color.r, color.g, color.b, alpha);
            return image;
        }

        /// <summary>온보딩 전용 9-slice 패널. 일반 HUD와 스프라이트를 공유하지 않는다.</summary>
        public Image OnboardingPanel(string id, Rect screenRect, float alpha = 1f)
        {
            var image = Borrow(panels, id, () =>
                LastShiftUiFactory.CreateOnboardingPanel(panelRoot, $"OnboardingPanel:{id}"));
            LastShiftUiFactory.PlacePanel((RectTransform)image.transform,
                LastShiftUiTheme.ScreenRectToCanvas(screenRect, ScreenSize));
            image.color = new Color(1f, 1f, 1f, alpha);
            return image;
        }

        /// <summary>
        /// 이미 캔버스 단위로 잡아 둔 자리에 패널을 빌린다. 프롬프트·조작줄처럼 <b>완전히</b>
        /// UGUI 로 옮긴 화면이 쓴다 — 그쪽은 애초에 1920×1080 자로 계산하므로 화면 픽셀로
        /// 갔다가 돌아오는 왕복이 반올림만 남긴다.
        /// </summary>
        public Image PanelCanvas(string id, Rect canvasTopLeftRect, float alpha = 1f)
        {
            var image = Borrow(panels, id, () => LastShiftUiFactory.CreatePanel(panelRoot, $"Panel:{id}"));
            LastShiftUiFactory.PlacePanel((RectTransform)image.transform, LastShiftUiTheme.FlipY(canvasTopLeftRect));
            var color = image.color;
            image.color = new Color(color.r, color.g, color.b, alpha);
            return image;
        }

        /// <summary>이미 캔버스 단위인 자리에 글자를 빌린다. 글자 크기는 그대로 쓴다.</summary>
        public Text LabelCanvas(string id, Rect canvasTopLeftRect, string text, int fontSize, TextAnchor anchor, Color color)
        {
            var label = Borrow(labels, id, () =>
                LastShiftUiFactory.CreateText(overlayRoot, $"Label:{id}", fontSize, anchor, color));
            LastShiftUiFactory.Place((RectTransform)label.transform, LastShiftUiTheme.FlipY(canvasTopLeftRect));
            Apply(label, text, fontSize, anchor, color, FontStyle.Normal, false);
            return label;
        }

        /// <summary>
        /// IMGUI 좌표(원점 좌상단, 화면 픽셀)에 글자를 빌린다. <c>GUI.Label</c> 을 한 줄로
        /// 갈아 끼우는 자리라 인자 순서와 단위가 그쪽과 같다 — <b>자리도 글자 크기도 화면
        /// 픽셀</b>이고, 캔버스 단위 환산은 여기서 한 번에 한다.
        ///
        /// <c>GUIStyle</c> 대신 색·굵기·줄바꿈을 인자로 받는 이유는, IMGUI 시절 스타일 객체가
        /// 화면마다 따로 만들어져 같은 본문색이 파일 셋에 서로 다른 값으로 적혀 있었기 때문이다.
        /// </summary>
        public Text Label(string id, Rect screenRect, string text, int screenFontSize, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, FontStyle fontStyle = FontStyle.Normal, bool wrap = false)
        {
            var canvasFontSize = LastShiftUiTheme.ScreenFontSizeToCanvas(screenFontSize, ScreenSize);
            var label = Borrow(labels, id, () =>
                LastShiftUiFactory.CreateText(overlayRoot, $"Label:{id}", canvasFontSize, anchor, color));
            LastShiftUiFactory.Place((RectTransform)label.transform,
                LastShiftUiTheme.ScreenRectToCanvas(screenRect, ScreenSize));
            Apply(label, text, canvasFontSize, anchor, color, fontStyle, wrap);
            return label;
        }

        /// <summary>
        /// 빌려 준 글자의 모양을 <b>매번 전부</b> 다시 적는다. 조각은 재사용되므로 굵기나
        /// 줄바꿈을 빠뜨리면 이전에 그 조각을 쓰던 화면의 설정이 남는다.
        /// </summary>
        private static void Apply(Text label, string text, int fontSize, TextAnchor anchor, Color color,
            FontStyle fontStyle, bool wrap)
        {
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.color = color;
            label.fontStyle = fontStyle;
            label.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        }

        /// <summary>
        /// 게이지 한 줄을 빌린다. 축은 처음 빌릴 때 정해지고 그대로 남는다.
        ///
        /// <b>축 인자가 하나다.</b> 아이콘 자체가 게이지라 외곽선과 채움이 같은 그림의 짝이고,
        /// 둘을 따로 받으면 어긋난 조합을 부를 수 있다.
        /// </summary>
        public LastShiftGaugeView Gauge(string id, LastShiftUiIcon icon, Rect screenRect)
        {
            var view = Borrow(gauges, id, () =>
                LastShiftGaugeView.Create(gaugeRoot, $"Gauge:{id}", icon));
            view.SetLayout(LastShiftUiTheme.ScreenRectToCanvas(screenRect, ScreenSize));
            return view;
        }

        /// <summary>프롬프트처럼 자기 계층을 직접 짓는 화면이 쓰는 부모.</summary>
        public RectTransform OverlayRoot => overlayRoot;

        private T Borrow<T>(Dictionary<string, Lease<T>> table, string id, System.Func<T> create)
            where T : Component
        {
            if (!table.TryGetValue(id, out var lease) || lease.Item == null)
            {
                lease = new Lease<T> { Item = create() };
            }

            lease.Frame = Time.frameCount;
            table[id] = lease;
            if (!lease.Item.gameObject.activeSelf) lease.Item.gameObject.SetActive(true);
            return lease.Item;
        }

        private void LateUpdate()
        {
            Sweep(panels);
            Sweep(gauges);
            Sweep(labels);
        }

        private static void Sweep<T>(Dictionary<string, Lease<T>> table) where T : Component
        {
            foreach (var pair in table)
            {
                var lease = pair.Value;
                if (lease.Item == null) continue;
                var expired = Time.frameCount - lease.Frame > LeaseGraceFrames;
                if (expired == !lease.Item.gameObject.activeSelf) continue;
                lease.Item.gameObject.SetActive(!expired);
            }
        }

        /// <summary>테스트가 층을 통째로 걷어낸다.</summary>
        public static void DestroyInstance()
        {
            if (instance == null) return;
            var go = instance.gameObject;
            instance = null;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
