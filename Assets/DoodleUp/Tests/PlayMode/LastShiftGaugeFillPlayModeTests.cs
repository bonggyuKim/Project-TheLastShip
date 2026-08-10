using System.Collections;
using System.Reflection;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 아이콘 게이지가 <b>실제로 그려지는 모양</b>을 본다.
    ///
    /// <c>fillAmount</c> 값만 확인하는 검사는 이미 있는데, 그것만으로는 화면에서 채움이
    /// 어느 방향으로 얼마나 올라오는지 알 수 없다 — <c>fillMethod</c> 나 <c>fillOrigin</c> 이
    /// 어긋나도 <c>fillAmount</c> 는 그대로 0.25 다. 그래서 여기서는 <see cref="Image"/> 가
    /// 실제로 만들어 내는 <b>정점</b>을 꺼내 채움 띠의 높이를 잰다.
    ///
    /// <b>왜 캡처가 아닌가.</b> 그림을 찍어 눈으로 보는 방법은 <c>-nographics</c> 에서 아예
    /// 안 되고, 된다 해도 "25% 가 맞나" 는 사람이 눈대중으로 판정하게 된다. 정점 높이는
    /// 같은 것을 숫자로 답한다.
    ///
    /// 축 여덟 개를 <b>전부</b> 돈다. 도킹·식량은 아트 교체 전까지 검사에 한 번도 안 걸려
    /// 있었고, 채움이 곧 실루엣인 구조에서는 축이 하나만 어긋나도 다른 축 모양이 차오른다.
    /// </summary>
    public sealed class LastShiftGaugeFillPlayModeTests
    {
        /// <summary>완료 조건 그대로의 세 점. 0 과 1 은 경계라 따로 본다.</summary>
        private static readonly float[] Checkpoints = { 0.25f, 0.5f, 0.75f };

        private static readonly Rect ProbeRect = new(28f, 56f, 680f, 32f);

        [TearDown]
        public void TearDown()
        {
            LastShiftUiLayer.DestroyInstance();
            LastShiftUiLayer.ScreenSizeOverride = Vector2.zero;
        }

        /// <summary>
        /// 축 여덟 개가 각자 자기 짝(<c>_base</c>/<c>_fill</c>)을 달고 캔버스에 붙는다.
        /// 배선이 어긋나면 아래 채움 높이 검사가 "왜" 틀렸는지 못 말해 주므로 먼저 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryAxisCarriesItsOwnBaseAndFillOnTheCanvas()
        {
            var kit = LastShiftUiKit.Instance;
            Assert.That(kit, Is.Not.Null, "구운 키트가 없으면 게이지가 단색 사각형으로 떨어진다.");

            var layer = LastShiftUiLayer.Instance;
            foreach (LastShiftUiIcon icon in System.Enum.GetValues(typeof(LastShiftUiIcon)))
            {
                var gauge = layer.Gauge($"axis:{icon}", icon, ProbeRect);
                gauge.SetValue(0.5f);

                Assert.That(gauge.Icon.sprite, Is.SameAs(kit.IconOf(icon)), $"{icon} 외곽선이 딴 축 그림이다.");
                Assert.That(gauge.Fill.sprite, Is.SameAs(kit.FillOf(icon)), $"{icon} 채움이 딴 축 그림이다.");
                Assert.That(gauge.Fill.canvas, Is.Not.Null, $"{icon} 채움이 캔버스에 안 붙었다 — 안 그려진다.");
                Assert.That(gauge.Icon.type, Is.Not.EqualTo(Image.Type.Filled),
                    $"{icon} 외곽선까지 채움 방식이면 값이 낮을 때 아이콘 윤곽이 같이 잘린다.");
            }

            yield return null;
        }

        /// <summary>
        /// <b>완료 조건.</b> 여덟 축 × 25/50/75% 에서 <b>정점으로 잰 채움 띠</b>가 아이콘
        /// 아래변에서 그 비율만큼 올라온다.
        ///
        /// 기준선은 같은 게이지의 <c>1.0</c> 상태다 — 스프라이트 여백이나 PPU 때문에 그리는
        /// 사각형이 <see cref="RectTransform"/> 과 딱 같지 않을 수 있어, 절대 좌표 대신
        /// 가득 찬 상태 대비 <b>비율</b>로 잰다.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryAxisFillsFromTheBottomAtQuarterHalfAndThreeQuarters()
        {
            var layer = LastShiftUiLayer.Instance;
            yield return null;

            foreach (LastShiftUiIcon icon in System.Enum.GetValues(typeof(LastShiftUiIcon)))
            {
                var gauge = layer.Gauge($"fill:{icon}", icon, ProbeRect);
                var fill = gauge.Fill;

                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled), $"{icon}: 채움 방식이 아니다.");
                Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Vertical),
                    $"{icon}: 가로로 채우면 실루엣이 좌우로 잘린다.");
                Assert.That(fill.fillOrigin, Is.EqualTo((int)Image.OriginVertical.Bottom),
                    $"{icon}: 위에서부터 차면 잔량이 거꾸로 읽힌다.");
                Assert.That(fill.sprite, Is.Not.Null, $"{icon}: 채움 스프라이트가 없다.");

                // 가득 찬 상태가 자다. 여기서 잰 아래변·위변이 100% 기준이 된다.
                gauge.SetValue(1f);
                var full = FilledSpan(fill);
                Assert.That(full.height, Is.GreaterThan(0f), $"{icon}: 가득 찬 상태에서도 그릴 것이 없다.");

                var iconRect = (RectTransform)gauge.Icon.transform;
                var fillRect = (RectTransform)fill.transform;
                Assert.That(fillRect.anchoredPosition, Is.EqualTo(iconRect.anchoredPosition),
                    $"{icon}: 채움과 외곽선이 다른 자리에 있다 — 채움이 실루엣 밖으로 샌다.");
                Assert.That(fillRect.sizeDelta, Is.EqualTo(iconRect.sizeDelta), $"{icon}: 채움 사각형이 외곽선과 다르다.");

                foreach (var value in Checkpoints)
                {
                    gauge.SetValue(value);
                    Assert.That(fill.fillAmount, Is.EqualTo(value).Within(0.0001f), $"{icon} @{value:P0}: 값이 안 들어갔다.");

                    var span = FilledSpan(fill);

                    Assert.That(span.yMin, Is.EqualTo(full.yMin).Within(0.01f),
                        $"{icon} @{value:P0}: 채움 아래변이 움직였다 — 아래에서 위로 차오르지 않는다.");
                    Assert.That(span.height / full.height, Is.EqualTo(value).Within(0.01f),
                        $"{icon} @{value:P0}: 실제로 차오른 높이가 {span.height / full.height:P0} 다.");

                    // 폭은 값과 무관하다. 사각형을 줄이는 방식이면 여기서 걸린다.
                    Assert.That(span.width, Is.EqualTo(full.width).Within(0.01f),
                        $"{icon} @{value:P0}: 채움 폭이 값에 따라 변한다 — 실루엣이 눌린다.");
                }

                gauge.SetValue(0f);
                Assert.That(FilledSpan(fill).height, Is.LessThan(full.height * 0.01f),
                    $"{icon} @0%: 바닥인데 채움이 남아 있다.");
            }

            yield return null;
        }

        /// <summary>
        /// 자원 게이지 넷과 계통 넷이 <b>HUD 자리표대로</b> 값을 받는다. 위 검사는 게이지
        /// 하나를 직접 몰아서 보는 것이라, 상시 HUD 가 쓰는 줄에서도 같은지 따로 확인한다.
        /// </summary>
        [UnityTest]
        public IEnumerator HudRowsShowTheSameFractionAsTheValueTheyGet()
        {
            var layer = LastShiftUiLayer.Instance;
            yield return null;

            // 상시 HUD 가 실제로 쓰는 줄·축 조합. 도킹은 계통 넷째 줄, 식량은 자원 넷째 줄이다.
            var rows = new (string Id, LastShiftUiIcon Icon, Rect Rect, float Value)[]
            {
                ("thrust", LastShiftUiIcon.Thrust, LastShiftHudLayout.SystemGaugeRect(0), 0.25f),
                ("power", LastShiftUiIcon.Warning, LastShiftHudLayout.SystemGaugeRect(1), 0.5f),
                ("heat", LastShiftUiIcon.Warning, LastShiftHudLayout.SystemGaugeRect(2), 0.75f),
                ("docking", LastShiftUiIcon.Docking, LastShiftHudLayout.SystemGaugeRect(3), 0.25f),
                ("maintenance", LastShiftUiIcon.Maintenance, LastShiftHudLayout.ResourceGaugeRect(0), 0.5f),
                ("materials", LastShiftUiIcon.Materials, LastShiftHudLayout.ResourceGaugeRect(1), 0.75f),
                ("oxygen", LastShiftUiIcon.Oxygen, LastShiftHudLayout.ResourceGaugeRect(2), 0.25f),
                ("food", LastShiftUiIcon.Food, LastShiftHudLayout.ResourceGaugeRect(3), 0.75f)
            };

            foreach (var row in rows)
            {
                var gauge = layer.Gauge(row.Id, row.Icon, row.Rect);
                gauge.SetValue(1f);
                var full = FilledSpan(gauge.Fill);

                gauge.SetValue(row.Value);
                var span = FilledSpan(gauge.Fill);

                Assert.That(span.height / full.height, Is.EqualTo(row.Value).Within(0.01f),
                    $"{row.Id} 줄이 {row.Value:P0} 를 {span.height / full.height:P0} 로 그린다.");
                Assert.That(gauge.Icon.sprite, Is.Not.Null, $"{row.Id} 줄에 외곽선 그림이 없다.");
            }

            yield return null;
        }

        /// <summary>
        /// <see cref="Image"/> 가 이번 프레임에 만들 <b>정점의 사각형</b>. UGUI 는 채움 비율을
        /// 메시 단계에서 잘라 내므로, 여기서 잰 높이가 곧 화면에 보이는 띠의 높이다.
        ///
        /// <c>OnPopulateMesh</c> 는 <c>protected</c> 라 반사로 부른다. 이걸 우회해서 잴 방법이
        /// 없다 — <see cref="CanvasRenderer"/> 는 만들어진 메시를 돌려주지 않고, 픽셀을 읽는
        /// 방법은 <c>-nographics</c> 에서 못 쓴다.
        /// </summary>
        private static Rect FilledSpan(Image image)
        {
            var method = image.GetType().GetMethod(
                "OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(VertexHelper) },
                null);
            Assert.That(method, Is.Not.Null, "UGUI 의 메시 생성 함수를 못 찾았다 — 유니티 버전이 바뀌었는지 확인.");

            using var helper = new VertexHelper();
            method.Invoke(image, new object[] { helper });

            var count = helper.currentVertCount;
            if (count == 0) return Rect.zero;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var vertex = new UIVertex();
            for (var index = 0; index < count; index++)
            {
                helper.PopulateUIVertex(ref vertex, index);
                min = Vector2.Min(min, vertex.position);
                max = Vector2.Max(max, vertex.position);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
