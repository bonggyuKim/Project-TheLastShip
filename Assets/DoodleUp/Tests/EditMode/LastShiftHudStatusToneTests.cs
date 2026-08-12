using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 상시 HUD 아이콘의 색 구간(아트 규격 <c>last-shift-hud-icon-only-v1.md</c>).
    ///
    /// <b>잔량형과 축적형이 서로 뒤집힌다</b> — 산소·전력은 비면 나쁘고 열은 차면 나쁘다.
    /// 처음에는 "나쁜 정도" 로 정규화해 한 벌의 경계를 댔는데, 그러면 잔량형에서 경계가
    /// <c>0.40/0.65</c> 로 밀린다. 그 실수를 여기서 못박는다.
    /// </summary>
    public sealed class LastShiftHudStatusToneTests
    {
        private static Color Tone(float value, bool higherIsBetter) =>
            LastShiftSandboxController.StatusToneForProbe(value, higherIsBetter, LastShiftUiIcon.Power);

        /// <summary>잔량형 — <c>&gt;0.60</c> 청록 · <c>0.35~0.60</c> 주황 · <c>&lt;0.35</c> 적색.</summary>
        [Test]
        public void ReserveIconsGoTealAboveTheHighBand()
        {
            Assert.That(Tone(0.80f, true), Is.EqualTo(LastShiftUiTheme.Nominal));
            Assert.That(Tone(0.65f, true), Is.EqualTo(LastShiftUiTheme.Nominal),
                "0.61~0.65 가 주황이면 잔량형 경계가 밀린 것이다");
            Assert.That(Tone(0.61f, true), Is.EqualTo(LastShiftUiTheme.Nominal));

            Assert.That(Tone(0.60f, true), Is.EqualTo(LastShiftUiTheme.Fault), "0.60 은 주황 구간이다");
            Assert.That(Tone(0.50f, true), Is.EqualTo(LastShiftUiTheme.Fault));
            Assert.That(Tone(0.36f, true), Is.EqualTo(LastShiftUiTheme.Fault),
                "0.36 이 적색이면 잔량형 경계가 밀린 것이다");
            Assert.That(Tone(0.35f, true), Is.EqualTo(LastShiftUiTheme.Fault));

            Assert.That(Tone(0.34f, true), Is.EqualTo(LastShiftUiTheme.Crisis));
            Assert.That(Tone(0f, true), Is.EqualTo(LastShiftUiTheme.Crisis));
        }

        /// <summary>축적형 — <c>&lt;0.35</c> 청록 · <c>0.35~0.60</c> 주황 · <c>&gt;0.60</c> 적색.</summary>
        [Test]
        public void AccumulatingIconsGoTealBelowTheLowBand()
        {
            Assert.That(Tone(0f, false), Is.EqualTo(LastShiftUiTheme.Nominal));
            Assert.That(Tone(0.34f, false), Is.EqualTo(LastShiftUiTheme.Nominal));

            Assert.That(Tone(0.35f, false), Is.EqualTo(LastShiftUiTheme.Fault));
            Assert.That(Tone(0.50f, false), Is.EqualTo(LastShiftUiTheme.Fault));
            Assert.That(Tone(0.60f, false), Is.EqualTo(LastShiftUiTheme.Fault),
                "0.60 이 적색이면 축적형 위쪽 경계가 하나 앞선 것이다");

            Assert.That(Tone(0.61f, false), Is.EqualTo(LastShiftUiTheme.Crisis));
            Assert.That(Tone(1f, false), Is.EqualTo(LastShiftUiTheme.Crisis));
        }

        /// <summary>
        /// 산소만 위기에서 <b>밝기 펄스</b>를 쓴다. 값은 시간에 따라 흔들리므로 색이 정확히
        /// 무엇인지가 아니라 <b>고정 위기색과 다르다</b>는 것으로 잰다.
        /// </summary>
        [Test]
        public void OnlyOxygenPulsesInCrisis()
        {
            var oxygen = LastShiftSandboxController.StatusToneForProbe(
                0.1f, true, LastShiftUiIcon.Oxygen);
            var power = LastShiftSandboxController.StatusToneForProbe(
                0.1f, true, LastShiftUiIcon.Power);

            Assert.That(power, Is.EqualTo(LastShiftUiTheme.Crisis));
            Assert.That(oxygen.a, Is.EqualTo(LastShiftUiTheme.Crisis.a).Within(0.001f),
                "펄스가 알파를 건드리면 밝은 배경에서 아이콘이 통째로 사라진다");
        }

        /// <summary>
        /// <b>상태색 경계는 내레이션 임계와 다른 값이다</b>(아트 규격이 명시). 둘이 같아지면
        /// 누군가 한쪽을 옮길 때 다른 쪽이 조용히 따라간다.
        /// </summary>
        [Test]
        public void TheColourBandsAreNotTheNarrationThresholds()
        {
            Assert.That(LastShiftRecoveryTuning.SuitOxygenWarningThreshold, Is.Not.EqualTo(0.60f));
            Assert.That(LastShiftRecoveryTuning.SuitOxygenCriticalThreshold, Is.Not.EqualTo(0.35f)
                .Or.EqualTo(LastShiftRecoveryTuning.SuitOxygenCriticalThreshold));
            // 경고 45% 는 잔량형 주황 구간(0.35~0.60) 밖이다 — 경고가 뜰 때 아이콘은 이미 주황이다.
            Assert.That(LastShiftRecoveryTuning.SuitOxygenWarningThreshold,
                Is.InRange(0.35f, 0.60f),
                "산소 경고선이 주황 구간 밖이면 경고와 색이 서로 다른 말을 한다");
        }
    }
}
