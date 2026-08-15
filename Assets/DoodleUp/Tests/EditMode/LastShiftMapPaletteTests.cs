using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 지도(<c>M</c>) 팔레트. <b>"디지털로 보이는가" 를 눈 대신 수치로 잡아 둔 자리</b>다
    /// (2026-08-15 사용자 피드백 — "실제 탑뷰에서 보는 것과 비슷한데 디지털 느낌이 나야 한다").
    ///
    /// <b>스크린샷은 회귀를 못 막는다.</b> 색은 상수 하나를 고치면 조용히 되돌아가고, 그때
    /// 걸리는 것은 다음 플레이테스트다. 여기서 재는 것은 그림이 아니라 그 그림을 그렇게 보이게
    /// 하는 <b>관계</b> 넷이다 — 바탕이 어둡고 남보라인가, 선이 청록인가, 바탕과 선이 명도로
    /// 갈리는가, 아이콘이 이름보다 밝은가.
    ///
    /// <b>절대값을 안 박는다.</b> <c>#57D8E8</c> 같은 값을 그대로 비교하면 아트가 한 눈금
    /// 조정할 때마다 검사가 깨지는데, 그건 규격이 아니라 잠금이다. 부등호로 적으면 팔레트를
    /// 옮길 자유는 남기고 뒤집히는 것만 막는다.
    /// </summary>
    public sealed class LastShiftMapPaletteTests
    {
        /// <summary>사람 눈이 느끼는 밝기. 초록에 가중이 실리는 통상 계수다.</summary>
        private static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [Test]
        public void TheBackdropIsDarkAndLeansViolet()
        {
            var back = LastShiftUiTheme.MapBackdrop;

            // 어두워야 그 위의 선이 빛나는 것으로 읽힌다.
            Assert.That(Luma(back), Is.LessThan(0.2f),
                $"지도 바탕이 밝다 — 선이 바탕에 묻힌다. {back}");

            // 남보라. 파랑이 가장 세고 초록이 가장 약하면 청람이 아니라 보라 쪽이다.
            Assert.That(back.b, Is.GreaterThan(back.r), $"지도 바탕에 파랑이 없다 — {back}");
            Assert.That(back.r, Is.GreaterThan(back.g), $"지도 바탕이 청람으로 되돌아갔다 — {back}");

            // 순검정이면 인쇄된 도면으로 읽힌다. 색이 있어야 화면에 띄운 계기가 된다.
            Assert.That(back.b, Is.GreaterThan(0.1f), $"지도 바탕이 사실상 검정이다 — {back}");
        }

        [Test]
        public void TheRoomLineIsCyan()
        {
            var line = LastShiftUiTheme.MapLine;

            // 청록 — 초록과 파랑이 둘 다 세고 서로 비슷하며, 빨강이 뚜렷하게 약하다.
            Assert.That(line.g, Is.GreaterThan(0.6f), $"방 테두리에 초록이 모자라다 — {line}");
            Assert.That(line.b, Is.GreaterThan(0.6f), $"방 테두리에 파랑이 모자라다 — {line}");
            Assert.That(Mathf.Abs(line.g - line.b), Is.LessThan(0.2f),
                $"방 테두리가 청록이 아니다 — 초록/파랑이 갈렸다 {line}");
            Assert.That(line.r, Is.LessThan(line.g - 0.3f),
                $"방 테두리에 빨강이 남아 크림/오렌지로 읽힌다 — {line}");

            // 예전 값으로 되돌아갔는지. 아이보리·본문색은 빨강이 초록만큼 세다.
            Assert.That(line, Is.Not.EqualTo(LastShiftUiTheme.Ivory), "방 테두리가 아이보리로 되돌아갔다");
            Assert.That(line, Is.Not.EqualTo(LastShiftUiTheme.BodyText), "방 테두리가 본문색으로 되돌아갔다");
        }

        [Test]
        public void TheLineSeparatesFromTheBackdropByBrightness()
        {
            // 채도만으로 갈리면 배경이 밝은 화면·색약 조건에서 선이 사라진다.
            var gap = Luma(LastShiftUiTheme.MapLine) - Luma(LastShiftUiTheme.MapBackdrop);
            Assert.That(gap, Is.GreaterThan(0.5f),
                $"선과 바탕이 명도로 안 갈린다 — 차이 {gap:F2}");
        }

        [Test]
        public void TheDoorIsBrighterThanTheWallItOpens()
        {
            var door = LastShiftUiTheme.MapDoor;
            var line = LastShiftUiTheme.MapLine;

            // 문은 벽과 다른 색이 아니라 벽이 밝아진 자리다.
            Assert.That(Luma(door), Is.GreaterThan(Luma(line)),
                $"문이 벽보다 어둡다 — 문 {door}, 벽 {line}");
            Assert.That(Mathf.Abs(door.g - door.b), Is.LessThan(0.2f),
                $"문이 벽과 다른 계통이다 — {door}");

            // 초록은 지도에서 "나" 하나만 쓴다. 문이 정상색이면 문에 선 표식이 문에 묻힌다.
            Assert.That(door, Is.Not.EqualTo(LastShiftUiTheme.Nominal),
                "문이 다시 정상색이다 — 내 표식과 같은 초록이 된다");
        }

        [Test]
        public void TheIconOutshinesTheName()
        {
            // 아이콘이 먼저 읽히고 이름이 그것을 가르치는 순서. 예전에는 뒤집혀 있었다.
            Assert.That(Luma(LastShiftUiTheme.MapIcon),
                Is.GreaterThan(Luma(LastShiftUiTheme.MapLabel) + 0.1f),
                "아이콘이 이름보다 밝지 않다 — 이름이 먼저 읽힌다");
        }

        [Test]
        public void TheNameStaysReadableOnTheBackdrop()
        {
            // 어둡게 한 것과 지운 것은 다르다. 이름은 여전히 바탕에서 떠 있어야 한다.
            var gap = Luma(LastShiftUiTheme.MapLabel) - Luma(LastShiftUiTheme.MapBackdrop);
            Assert.That(gap, Is.GreaterThan(0.4f),
                $"방 이름이 바탕에 묻힌다 — 차이 {gap:F2}");
        }
    }
}
