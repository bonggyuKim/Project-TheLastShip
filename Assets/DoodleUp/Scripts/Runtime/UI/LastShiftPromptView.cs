using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 상호작용 문장에서 <b>입력 키를 떼어낸다</b>. 문장은 <c>"[E] 잔해 뜯기"</c> 처럼
    /// 대괄호 키가 앞에 붙어 있는데, 키캡 그림에 앉히려면 그 조각이 따로 필요하다.
    ///
    /// <b>문장 쪽을 안 고치는 것이 요점이다.</b> 프롬프트를 만드는 자리는 열 곳이 넘고
    /// (에어록·잔해·아이템·유령·서버 거부…), 거기서 키를 따로 넘기게 바꾸면 그 열 곳이
    /// 전부 UI 형식을 알아야 한다. 형식을 아는 곳은 여기 하나로 둔다.
    /// </summary>
    public static class LastShiftPromptText
    {
        /// <summary>
        /// 앞머리 <c>[X]</c> 를 떼어 <paramref name="key"/> 로 돌려주고 나머지를 본문으로 준다.
        /// 키가 없으면 <paramref name="key"/> 는 빈 문자열이고 본문은 원문 그대로다.
        /// </summary>
        public static void Split(string prompt, out string key, out string body)
        {
            key = string.Empty;
            body = prompt ?? string.Empty;
            if (body.Length < 3 || body[0] != '[') return;

            var close = body.IndexOf(']');
            // 키는 한두 글자다(<c>E</c>, <c>Q</c>, <c>F1</c>). 그보다 길면 키가 아니라
            // <c>[조종석]</c> 같은 <b>구역 이름</b>이고, 그걸 키캡에 밀어 넣으면 본문
            // 앞부분이 통째로 사라진다 — 실제로 이 검사에서 걸렸다.
            if (close < 2 || close > 3) return;

            key = body.Substring(1, close - 1);
            body = body.Substring(close + 1).TrimStart();
        }
    }

    /// <summary>
    /// 상호작용 프롬프트 — <c>prompt_plate</c> + <c>icon_interact</c> + 키캡.
    ///
    /// 자리 계산(<see cref="LastShiftPlayerController.ResolvePromptRect"/>)은 그대로 두고
    /// <b>그리는 방식만 바꿨다</b>. 그 함수가 EditMode 에서 검증되는 유일한 이유가 계산과
    /// 그리기를 갈라 놓은 것이었고, UGUI 로 옮기면서 그걸 되돌릴 이유는 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftPromptView : MonoBehaviour
    {
        /// <summary>판 안쪽 여백. 키트가 판 모서리를 둥글게 깎아 놔서 이보다 좁으면 글자가 곡선에 닿는다.</summary>
        public const float PlateInset = 12f;

        /// <summary>조각 사이 간격.</summary>
        public const float Gap = 8f;

        /// <summary>프롬프트 안 아이콘·키캡 한 변.</summary>
        public const float GlyphSize = 28f;

        private RectTransform root;
        private Image plate;
        private Image icon;
        private Image keycap;
        private Text keyText;
        private Text bodyText;

        public Image Plate => plate;
        public Image Keycap => keycap;
        public Text BodyText => bodyText;

        /// <summary>키캡 자리를 뺀 순수 글자 폭. 호출자가 판 폭을 잡을 때 쓴다.</summary>
        public static float ChromeWidth(bool hasKey) =>
            PlateInset * 2f + GlyphSize + Gap + (hasKey ? GlyphSize + Gap : 0f);

        public static LastShiftPromptView Create(Transform parent, string name)
        {
            var rect = LastShiftUiFactory.CreateRect(parent, name);
            var view = rect.gameObject.AddComponent<LastShiftPromptView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            var kit = LastShiftUiKit.Instance;
            root = (RectTransform)transform;

            plate = LastShiftUiFactory.CreateImage(root, "Plate",
                kit != null ? kit.SlicedPromptPlate : null, Image.Type.Sliced);
            plate.color = kit != null ? Color.white : new Color(0.05f, 0.07f, 0.11f, 0.92f);

            icon = LastShiftUiFactory.CreateImage(root, "Icon",
                kit != null ? kit.IconOf(LastShiftUiIcon.Interact) : null);
            icon.color = LastShiftUiTheme.Ivory;

            keycap = LastShiftUiFactory.CreateImage(root, "Keycap", kit != null ? kit.Keycap : null);
            keyText = LastShiftUiFactory.CreateText(root, "Key", 15, TextAnchor.MiddleCenter, new Color(0.07f, 0.09f, 0.14f));
            bodyText = LastShiftUiFactory.CreateText(root, "Body", 15, TextAnchor.MiddleLeft, Color.white);
        }

        /// <summary>
        /// 글자만 먼저 재 본다. 판 폭은 문장 길이로 정해지는데, UGUI 에는 IMGUI 의
        /// <c>GUIStyle.CalcSize</c> 같은 즉석 측정이 없어서 <b>글자를 실제로 넣어 보고</b>
        /// <c>preferredWidth</c> 를 읽는 것이 정확한 유일한 방법이다. 이어서 부르는
        /// <see cref="Apply"/> 가 같은 문장을 다시 넣으므로 낭비되는 일도 없다.
        /// </summary>
        public float MeasureBody(string prompt)
        {
            LastShiftPromptText.Split(prompt, out var key, out var body);
            bodyText.text = body;
            return bodyText.preferredWidth + ChromeWidth(key.Length > 0);
        }

        /// <summary>
        /// 한 프레임 분 배치. <paramref name="canvasRect"/> 는 판 전체 자리이고 안쪽 조각은
        /// 판 높이에서 파생된다 — 해상도가 바뀌어도 조각끼리의 비례가 안 흔들린다.
        /// </summary>
        public void Apply(Rect canvasRect, string prompt, float fontScale = 1f)
        {
            LastShiftUiFactory.Place(root, canvasRect);
            LastShiftUiFactory.Place((RectTransform)plate.transform, new Rect(0f, 0f, canvasRect.width, canvasRect.height));

            LastShiftPromptText.Split(prompt, out var key, out var body);
            var hasKey = key.Length > 0;

            var glyphY = -(canvasRect.height - GlyphSize) * 0.5f;
            var x = PlateInset;
            LastShiftUiFactory.Place((RectTransform)icon.transform, new Rect(x, glyphY, GlyphSize, GlyphSize));
            x += GlyphSize + Gap;

            keycap.gameObject.SetActive(hasKey);
            keyText.gameObject.SetActive(hasKey);
            if (hasKey)
            {
                LastShiftUiFactory.Place((RectTransform)keycap.transform, new Rect(x, glyphY, GlyphSize, GlyphSize));
                LastShiftUiFactory.Place((RectTransform)keyText.transform, new Rect(x, glyphY, GlyphSize, GlyphSize));
                keyText.text = key;
                keyText.fontSize = Mathf.Max(9, Mathf.RoundToInt(15f * fontScale));
                x += GlyphSize + Gap;
            }

            LastShiftUiFactory.Place((RectTransform)bodyText.transform,
                new Rect(x, -(canvasRect.height - GlyphSize) * 0.5f,
                    Mathf.Max(1f, canvasRect.width - x - PlateInset), GlyphSize));
            bodyText.text = body;
            bodyText.fontSize = Mathf.Max(9, Mathf.RoundToInt(15f * fontScale));
        }
    }
}
