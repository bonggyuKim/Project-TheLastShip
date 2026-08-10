using System.Collections;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 재생 중에 <b>실제로 이미지 기반 UI 가 서는지</b>를 본다.
    ///
    /// EditMode 검사는 계층과 값을 고정하지만 캔버스가 실제로 그려지는 상태인지는 못 본다.
    /// 여기서는 재생 중에 층이 저절로 서고, 스프라이트가 붙고, 안 쓰는 조각이 저절로 꺼지는
    /// 것까지 확인한다 — 마지막 하나가 특히 중요한데, IMGUI 의 "안 그리면 안 보인다" 를
    /// UGUI 에서 흉내 낸 부분이라 여기가 틀어지면 판정 화면 위에 낡은 게이지가 남는다.
    /// </summary>
    public sealed class LastShiftUiLayerPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            LastShiftUiLayer.DestroyInstance();
            LastShiftUiLayer.ScreenSizeOverride = Vector2.zero;
        }

        /// <summary>층은 씬 저작 없이 저절로 선다. 씬 넷이 각각 다른 경로로 뜨기 때문이다.</summary>
        [UnityTest]
        public IEnumerator LayerBuildsItsCanvasWithoutSceneAuthoring()
        {
            var layer = LastShiftUiLayer.Instance;

            Assert.That(layer, Is.Not.Null, "재생 중에는 층이 스스로 서야 한다.");
            Assert.That(layer.Canvas, Is.Not.Null);
            Assert.That(layer.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));

            var scaler = layer.Canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(LastShiftUiTheme.ReferenceResolution));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(LastShiftUiTheme.ScreenMatch).Within(0.0001f));

            // 표시 전용 층이라 레이캐스터가 없어야 한다 — 있으면 조준 클릭이 UI 에 먹힌다.
            Assert.That(layer.Canvas.GetComponent<GraphicRaycaster>(), Is.Null);

            yield return null;
        }

        /// <summary>구운 키트가 실제로 게이지·패널에 붙는다.</summary>
        [UnityTest]
        public IEnumerator BakedSpritesReachTheGaugeAndPanel()
        {
            var layer = LastShiftUiLayer.Instance;

            var gauge = layer.Gauge("play", LastShiftUiIcon.Oxygen, LastShiftGaugeChannel.Oxygen,
                new Rect(28f, 56f, 680f, 32f));
            gauge.SetValue(0.6f);
            var panel = layer.Panel("playPanel", new Rect(16f, 16f, 680f, 290f));

            yield return null;

            Assert.That(gauge.Fill.sprite, Is.Not.Null, "채움 스프라이트가 없으면 무늬가 사라진다.");
            Assert.That(gauge.Icon.sprite, Is.Not.Null);
            Assert.That(gauge.Fill.sprite, Is.Not.SameAs(gauge.Icon.sprite),
                "외곽선과 채움이 같은 장이면 값이 0 일 때도 아이콘이 꽉 차 보인다.");
            Assert.That(panel.sprite, Is.Not.Null);
            Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(gauge.Fill.fillAmount, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(gauge.Fill.canvas, Is.Not.Null, "캔버스에 안 붙었으면 그려지지 않는다.");
        }

        /// <summary>
        /// 임대를 안 갱신하면 조각이 저절로 꺼진다. <c>OnGUI</c> 는 <c>LateUpdate</c> 뒤에
        /// 돌기 때문에 한 프레임 여유를 두고 있고, 그래서 두 프레임을 기다린다.
        /// </summary>
        [UnityTest]
        public IEnumerator UnusedPiecesTurnThemselvesOff()
        {
            var layer = LastShiftUiLayer.Instance;
            var panel = layer.Panel("temporary", new Rect(16f, 16f, 400f, 200f));

            Assert.That(panel.gameObject.activeSelf, Is.True);

            yield return null;
            yield return null;
            yield return null;

            Assert.That(panel.gameObject.activeSelf, Is.False,
                "판정 화면으로 넘어갈 때 낡은 게이지가 남는 사고가 여기서 막힌다.");

            layer.Panel("temporary", new Rect(16f, 16f, 400f, 200f));
            Assert.That(panel.gameObject.activeSelf, Is.True, "다시 빌리면 곧바로 켜져야 한다.");
        }

        /// <summary>프롬프트는 판·아이콘·키캡·글자 넷이 한 묶음으로 선다.</summary>
        [UnityTest]
        public IEnumerator PromptShowsPlateIconKeycapAndBody()
        {
            var layer = LastShiftUiLayer.Instance;
            var view = LastShiftPromptView.Create(layer.OverlayRoot, "PromptProbe");

            view.Apply(new Rect(100f, -400f, 420f, LastShiftPlayerController.PromptBoxHeight),
                "[E] 잔해 뜯기");

            yield return null;

            Assert.That(view.Plate.sprite, Is.Not.Null);
            Assert.That(view.Keycap.gameObject.activeSelf, Is.True);
            Assert.That(view.BodyText.text, Is.EqualTo("잔해 뜯기"));

            view.Apply(new Rect(100f, -400f, 420f, LastShiftPlayerController.PromptBoxHeight),
                "에어록: 조작 불가");
            Assert.That(view.Keycap.gameObject.activeSelf, Is.False,
                "키가 없는 문장에 빈 키캡이 남으면 누를 수 있는 것처럼 보인다.");
        }

        /// <summary>
        /// 상시 HUD 줄이 서로 안 겹친다. 좌표가 코드 곳곳에 흩어져 있던 시절 실제로 났던
        /// 사고이고, 자리표를 한 곳에 모은 것이 그 대책이다.
        /// </summary>
        [UnityTest]
        public IEnumerator HudRowsNeverOverlap()
        {
            var rows = new System.Collections.Generic.List<Rect>();
            for (var index = 0; index < LastShiftHudLayout.SystemGaugeCount; index++)
                rows.Add(LastShiftHudLayout.SystemGaugeRect(index));
            for (var index = 0; index < LastShiftHudLayout.ResourceGaugeCount; index++)
                rows.Add(LastShiftHudLayout.ResourceGaugeRect(index));

            for (var a = 0; a < rows.Count; a++)
            for (var b = a + 1; b < rows.Count; b++)
                Assert.That(rows[a].Overlaps(rows[b]), Is.False, $"{a}번 줄과 {b}번 줄이 겹친다.");

            var panel = LastShiftHudLayout.PanelRect;
            foreach (var row in rows)
                Assert.That(panel.Contains(new Vector2(row.xMax, row.yMax)), Is.True,
                    "게이지가 패널 밖으로 나가면 배경 없이 떠 있는 줄이 생긴다.");

            Assert.That(LastShiftHudLayout.DebugPanelRect.Overlaps(panel), Is.False,
                "F3 디버그 층이 상시 패널과 겹치면 둘 다 못 읽는다.");

            yield return null;
        }
    }
}
