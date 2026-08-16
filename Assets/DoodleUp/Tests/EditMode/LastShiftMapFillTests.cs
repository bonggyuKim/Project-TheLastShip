using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <b>지도 조각이 요청한 크기로 서는가.</b>
    ///
    /// 이 파일이 생긴 이유가 있다. 팔레트를 다 옮기고 격자까지 넣은 뒤 실제 화면을 찍어
    /// 보니, 지도가 선과 표식이 아니라 <b>둥근 카드 더미</b>였다 — <c>layer.Panel</c> 이
    /// 9-slice 라 <see cref="LastShiftUiFactory.PlacePanel"/> 가 모든 조각을
    /// <see cref="LastShiftUiTheme.PanelMinSize"/>(<c>192x96</c>)로 바닥치고 있었다.
    /// <c>2</c>px 테두리도 <c>16</c>px 표식도 <c>1</c>px 격자도 전부 같은 카드가 됐다.
    ///
    /// <b>색 검사는 이것을 못 잡는다.</b> 상수만 재는 검사(<see cref="LastShiftMapPaletteTests"/>)는
    /// 전부 초록이었다 — 옳은 색을 보이지도 않는 형상에 칠하고 있었을 뿐이다. 그래서
    /// 여기서는 색이 아니라 <b>실제로 만들어진 RectTransform 의 크기</b>를 잰다.
    /// </summary>
    public sealed class LastShiftMapFillTests
    {
        [TearDown]
        public void TearDown()
        {
            LastShiftUiLayer.DestroyInstance();
            LastShiftUiLayer.ScreenSizeOverride = Vector2.zero;
            LastShiftUiKit.ResetLookup();
        }

        /// <summary>가장 가는 조각 — 격자 한 줄. 굵기가 <c>1</c>px 그대로여야 한다.</summary>
        [Test]
        public void AHairlineStaysAHairline()
        {
            LastShiftUiLayer.ScreenSizeOverride = LastShiftUiTheme.ReferenceResolution;
            var layer = LastShiftUiLayer.EnsureInstance();

            var line = layer.Fill("probe:line", new Rect(100f, 100f, 1f, 400f));
            var size = ((RectTransform)line.transform).sizeDelta;

            Assert.That(size.x, Is.EqualTo(1f).Within(0.001f),
                $"격자 줄이 부풀었다 — {size}. 9-slice 바닥({LastShiftUiTheme.PanelMinSize})에 걸린 것이다.");
            Assert.That(size.y, Is.EqualTo(400f).Within(0.001f), $"격자 줄 길이가 어긋났다 — {size}");
        }

        /// <summary>
        /// 사람 표식. <b>내 것과 남의 것이 크기로 갈리는 것이 규약이다</b>
        /// (<see cref="LastShiftMapView.SelfMarkerSize"/> 가 더 크다) — 둘 다 바닥에 걸리면
        /// 같은 카드가 돼서 그 규약이 화면에서 사라진다.
        /// </summary>
        [Test]
        public void TheTwoMarkerSizesStayApartOnScreen()
        {
            LastShiftUiLayer.ScreenSizeOverride = LastShiftUiTheme.ReferenceResolution;
            var layer = LastShiftUiLayer.EnsureInstance();

            var mine = layer.Fill("probe:self",
                LastShiftMapView.MarkerRect(new Vector2(400f, 300f), LastShiftMapView.SelfMarkerSize));
            var theirs = layer.Fill("probe:crew",
                LastShiftMapView.MarkerRect(new Vector2(500f, 300f), LastShiftMapView.CrewMarkerSize));

            var mineSize = ((RectTransform)mine.transform).sizeDelta;
            var theirsSize = ((RectTransform)theirs.transform).sizeDelta;

            Assert.That(mineSize.x, Is.EqualTo(LastShiftMapView.SelfMarkerSize).Within(0.001f),
                $"내 표식이 요청한 크기가 아니다 — {mineSize}");
            Assert.That(mineSize.x, Is.GreaterThan(theirsSize.x),
                $"두 표식이 화면에서 같은 크기다 — 나 {mineSize}, 남 {theirsSize}");
        }

        /// <summary>
        /// <b>9-slice 는 여전히 바닥을 친다.</b> <see cref="LastShiftUiLayer.Fill"/> 이 생겼다고
        /// 그쪽 규칙이 바뀐 것이 아니라는 것을 같이 못박는다 — 계기·대사 패널은 모서리가
        /// 겹치면 안 되므로 그 바닥이 옳고, 갈라 쓰는 것이 답이다.
        /// </summary>
        [Test]
        public void ThePanelPathStillFloorsToItsMinimum()
        {
            LastShiftUiLayer.ScreenSizeOverride = LastShiftUiTheme.ReferenceResolution;
            var layer = LastShiftUiLayer.EnsureInstance();

            var panel = layer.Panel("probe:panel", new Rect(100f, 100f, 1f, 400f));
            var size = ((RectTransform)panel.transform).sizeDelta;

            Assert.That(size.x, Is.EqualTo(LastShiftUiTheme.PanelMinSize.x).Within(0.001f),
                $"9-slice 바닥이 사라졌다 — {size}. 모서리 16px 가 겹쳐 테두리가 무너진다.");
        }

        /// <summary>채운 사각형이라 그림에 기대지 않는다 — 키트가 없어도 지도는 서야 한다.</summary>
        [Test]
        public void TheFillCarriesNoSprite()
        {
            LastShiftUiLayer.ScreenSizeOverride = LastShiftUiTheme.ReferenceResolution;
            var layer = LastShiftUiLayer.EnsureInstance();

            var fill = layer.Fill("probe:plain", new Rect(0f, 0f, 40f, 40f));

            Assert.That(fill.sprite, Is.Null, "지도 조각에 스프라이트가 붙었다 — 모서리 장식이 좌표를 흐린다");
            Assert.That(fill.type, Is.EqualTo(Image.Type.Simple), $"지도 조각이 단색이 아니다 — {fill.type}");
        }
    }
}
