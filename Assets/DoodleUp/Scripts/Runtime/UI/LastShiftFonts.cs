using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 번들 한글 폰트 한 장. <b>이 프로젝트의 모든 글자가 여기를 지난다</b> — UGUI
    /// <see cref="UnityEngine.UI.Text"/> 도, 아직 남아 있는 IMGUI 화면 셋도.
    ///
    /// <b>왜 번들하나.</b> 여기 오기 전까지 프로젝트에는 폰트 에셋이 <c>0</c>개였고 한글은
    /// 내장 <c>LegacyRuntime.ttf</c> 가 못 그려서 OS 폴백이 대신 그렸다. 즉 <b>어떤 서체가
    /// 나오는지가 실행하는 PC 에 달려 있었다.</b> HUD 의 <c>14~20px</c> 에서는 티가 안 났지만
    /// 결과 화면 판정 줄은 <c>64px</c> 이고 매 판 끝에 <c>5</c>초 넘게 보는 자리다
    /// (<c>docs/art/last-shift-result-screen-layout-v1.md</c> §6).
    ///
    /// <b>왜 <see cref="Resources"/> 인가.</b> 이 프로젝트의 UI 는 프리팹이 아니라 코드로
    /// 서고(<see cref="LastShiftUiFactory"/>), IMGUI 화면은 <see cref="MonoBehaviour"/> 조차
    /// 아니다. 인스펙터에 꽂을 자리가 없으니 씬을 안 건드리고 잡을 수 있는 경로가 이것뿐이다 —
    /// 대사(<c>Text/ko.json</c>)·나레이션 오디오가 이미 쓰는 방식과 같다.
    /// </summary>
    public static class LastShiftFonts
    {
        /// <summary><c>Resources.Load</c> 경로. 확장자는 빼고 적는다.</summary>
        public const string KoreanResourcePath = "Fonts/NotoSansKR-Regular";

        private static Font cached;
        private static bool resolved;

        /// <summary>
        /// 본문 폰트. 에셋을 못 찾으면 <c>null</c> 을 돌려주지 않고 내장 폰트로 떨어진다 —
        /// 서체가 예전으로 돌아갈 뿐 <b>글자가 사라지지는 않아야</b> 하기 때문이다.
        /// </summary>
        public static Font Korean
        {
            get
            {
                // Resources.Load 는 없는 이름이면 매번 디스크를 훑는다. 폰트를 못 찾은 판에서
                // OnGUI 가 프레임마다 이 프로퍼티를 때리므로 실패도 한 번만 기억한다.
                if (resolved && cached != null) return cached;
                cached = Resources.Load<Font>(KoreanResourcePath) ?? Fallback();
                resolved = true;
                return cached;
            }
        }

        /// <summary>
        /// 번들 폰트가 실제로 잡혔는가. <c>false</c> 면 내장 폰트로 떨어진 상태이고
        /// 한글은 다시 OS 폴백이 그린다 — 테스트가 이 한 줄로 회귀를 잡는다.
        /// </summary>
        public static bool HasBundledKorean => Korean != null && Korean != Fallback();

        /// <summary>
        /// IMGUI 스타일에 폰트를 물린다. <c>GUI.skin.font</c> 를 통째로 바꾸지 않는 이유는
        /// 그것이 <b>에디터 전역 스킨</b>이라 플레이 중 대입하면 에디터 UI 까지 따라 바뀌고
        /// 플레이가 끝나도 안 돌아오기 때문이다. 스타일마다 꽂으면 그리는 화면만 바뀐다.
        /// </summary>
        public static GUIStyle Apply(GUIStyle style)
        {
            if (style != null) style.font = Korean;
            return style;
        }

        /// <summary>
        /// 내장 폰트. 유니티 <c>2022.2</c> 에서 <c>Arial.ttf</c> 가 <c>LegacyRuntime.ttf</c> 로
        /// 바뀌었고 둘 다 한글 글리프가 없다 — 번들 폰트를 못 찾았을 때만 여기로 떨어진다.
        /// </summary>
        private static Font Fallback()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
