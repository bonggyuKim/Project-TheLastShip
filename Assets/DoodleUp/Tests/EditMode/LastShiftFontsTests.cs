using System.Linq;
using System.Reflection;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 번들 한글 폰트가 <b>실제로 잡히는가</b>.
    ///
    /// 이 검사가 없으면 회귀가 조용하다. 폰트를 못 찾아도 유니티는 내장 폰트로 떨어져
    /// 글자를 계속 그리고, 화면은 "조금 다른 서체" 로만 보인다 — 즉 <b>깨지지 않고 틀린다.</b>
    /// 그리고 그 "조금 다른 서체" 가 실행 PC 마다 달라서, 만든 사람 화면에서는 영영 안 보인다.
    /// 그래서 여기서는 폰트가 <c>null</c> 이 아닌지가 아니라 <b>내장 폰트가 아닌지</b>를 묻는다.
    /// </summary>
    public sealed class LastShiftFontsTests
    {
        /// <summary>결과 화면 판정 줄 5 종과 칩에 실제로 들어가는 글자들.</summary>
        private const string HeadlineGlyphs = "정상도킹절충생환질식표류추력부족산소";

        private static Font BuiltinFont =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        [Test]
        public void KoreanFontResolvesToTheBundledAsset()
        {
            var font = LastShiftFonts.Korean;

            Assert.That(font, Is.Not.Null, "번들 폰트도 내장 폴백도 없다");
            Assert.That(LastShiftFonts.HasBundledKorean, Is.True,
                $"{LastShiftFonts.KoreanResourcePath} 를 못 찾아 내장 폰트로 떨어졌다 — " +
                "한글이 다시 OS 폴백으로 그려진다");
            Assert.That(font, Is.Not.SameAs(BuiltinFont));
        }

        /// <summary>
        /// 서브셋 폰트라 <b>커버리지가 곧 완료 조건이다</b>. 판정 줄 글자가 하나라도 빠지면
        /// 그 글자만 OS 폴백으로 떨어져 단어 중간에서 서체가 갈린다.
        /// </summary>
        [Test]
        public void BundledFontCoversTheVerdictHeadlines()
        {
            var font = LastShiftFonts.Korean;
            var missing = HeadlineGlyphs.Where(c => !font.HasCharacter(c)).ToArray();

            Assert.That(missing, Is.Empty,
                $"번들 폰트에 없는 글자: {string.Join(",", missing)}");
        }

        /// <summary>한글 음절 블록 전체를 넣었으므로 판정 줄 밖의 글자도 다 있어야 한다.</summary>
        [Test]
        public void BundledFontCoversTheWholeHangulSyllableBlock()
        {
            var font = LastShiftFonts.Korean;

            // 11172 자를 다 물으면 검사가 느려진다. 블록의 양 끝과 가운데, 그리고 대사에서
            // 실제로 나오는 드문 글자 몇 개면 "블록째 넣었는가" 는 갈린다.
            foreach (var c in new[] { '가', '힣', '똠', '뷁', '쒜', '앉', '핡' })
                Assert.That(font.HasCharacter(c), Is.True, $"U+{(int)c:X4}({c}) 가 없다");
        }

        [Test]
        public void UiFactoryTextUsesTheBundledFont()
        {
            var text = LastShiftUiFactory.CreateText(null, "probe", 20, TextAnchor.UpperLeft, Color.white);

            try
            {
                Assert.That(text.font, Is.SameAs(LastShiftFonts.Korean));
            }
            finally
            {
                Object.DestroyImmediate(text.gameObject);
            }
        }

        /// <summary>
        /// 이 카드를 부른 자리. <c>64px</c> 판정 줄이 번들 폰트로 그려지는지 본다.
        ///
        /// 리플렉션을 쓰는 이유는 두 가지다. 스타일이 <c>private static</c> 이고, 바탕 스타일이
        /// <c>GUI.skin.label</c> 이라 <c>OnGUI</c> 밖에서는 못 읽는다 — 헤드리스 배치 모드에는
        /// <c>OnGUI</c> 가 아예 없으므로 바탕만 스킨 없는 스타일로 바꿔 끼우고 <b>실제
        /// <c>EnsureStyles</c> 를 그대로 돌린다</b>. 폰트를 물리는 줄은 검사와 실행이 같은 코드다.
        /// </summary>
        [Test]
        public void ResultScreenStylesUseTheBundledFont()
        {
            var type = typeof(LastShiftResultScreen);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

            var baseField = type.GetField("baseLabelStyle", flags);
            var original = baseField.GetValue(null);
            var styleNames = new[]
            {
                "headlineStyle", "chipStyle", "causeStyle",
                "cellLabelStyle", "cellValueStyle", "nextRunStyle"
            };

            try
            {
                baseField.SetValue(null, new System.Func<GUIStyle>(() => new GUIStyle()));
                foreach (var name in styleNames) type.GetField(name, flags).SetValue(null, null);

                type.GetMethod("EnsureStyles", flags).Invoke(null, null);

                foreach (var name in styleNames)
                {
                    var style = (GUIStyle)type.GetField(name, flags).GetValue(null);
                    Assert.That(style, Is.Not.Null, $"{name} 이 안 세워졌다");
                    Assert.That(style.font, Is.SameAs(LastShiftFonts.Korean), $"{name} 이 폴백 폰트다");
                }

                var headline = (GUIStyle)type.GetField("headlineStyle", flags).GetValue(null);
                Assert.That(headline.fontSize, Is.EqualTo(64), "판정 줄 크기가 바뀌었다");
            }
            finally
            {
                baseField.SetValue(null, original);
                foreach (var name in styleNames) type.GetField(name, flags).SetValue(null, null);
            }
        }

        /// <summary>
        /// HUD 는 코드가 아니라 프리팹에서 온다(<c>프리팹이 정본</c>). 그래서 폰트도 코드가
        /// 아니라 프리팹 안의 참조가 정본이고, 여기서만 회귀를 잡을 수 있다.
        /// </summary>
        [Test]
        public void HudPrefabTextsUseTheBundledFont()
        {
            var prefab = Resources.Load<LastShiftHudView>(LastShiftHudView.ResourcePath);
            Assert.That(prefab, Is.Not.Null,
                $"{LastShiftHudView.ResourcePath} 프리팹이 없다 — Last Shift/UI/Build HUD Prefab");

            var texts = prefab.GetComponentsInChildren<Text>(true);
            Assert.That(texts, Is.Not.Empty, "HUD 프리팹에 Text 가 하나도 없다");

            foreach (var text in texts)
                Assert.That(text.font, Is.SameAs(LastShiftFonts.Korean),
                    $"{text.name} 의 폰트가 {(text.font == null ? "null" : text.font.name)} 이다");
        }
    }
}
