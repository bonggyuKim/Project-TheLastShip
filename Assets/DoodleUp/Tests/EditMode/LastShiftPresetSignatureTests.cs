using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// SP-01(<c>0fb18e77</c>) 승인 기준의 기계 검사 — <b>세 프리셋이 서로 다른 첫 지배 문제를
    /// 만드는가.</b> 그 판정이 사람 눈에만 걸려 있어서 카드가 미승인으로 돌아왔으므로,
    /// 기준 자체를 여기로 내린다.
    ///
    /// <b>실측으로 드러난 것 두 가지를 먼저 적는다.</b> 둘 다 화면 문제가 아니라 내용 문제다.
    ///
    /// <list type="number">
    /// <item><b>부품이 제자리에 있는 동안은 상황이 거의 안 켜진다.</b> 프리셋 상태값만으로
    /// 발동선을 넘는 것은 <c>BadAttitudeHighOxygen</c> 의 <c>S-T1</c> 하나뿐이다. 나머지는
    /// 운석이 부품을 떼어낸 뒤에야 켜진다 — 즉 "첫 지배 문제" 는 원래 운석 이후 개념이고,
    /// 예전 HUD 가 그 줄을 운석 뒤로 막아 둔 것 자체는 틀리지 않았다. 틀린 것은 그 외의
    /// 모든 것(원시 수치 상시 노출, 등급 없음)이었다.</item>
    /// <item><b><c>PowerOverloadLooseBattery</c> 가 전력 상황을 하나도 안 켜던 시기가 있었다.</b>
    /// <c>BusPower 0.98</c> 이 S-P1 발동선 <c>0.65</c> 위라, 배터리가 실제로 bus 에서 빠져
    /// 있는데도(<c>[LAST_SHIFT_DAMAGE] power=True</c>) 전력으로는 아무 말도 못 했다.
    /// HUD 로는 못 고치는 내용 결함이었고 balance 가 <c>0.62</c> 로 정정했다(§2.2 A-3).</item>
    /// </list>
    /// </summary>
    public sealed class LastShiftPresetSignatureTests
    {
        /// <summary>
        /// 세 프리셋 전부. <c>PowerOverloadLooseBattery</c> 는 <c>BusPower</c> 가 <c>0.98</c> 이던
        /// 동안 상황을 하나도 못 켜서 잠시 빠져 있었고, balance 가 <c>0.62</c> 로 정정한 뒤
        /// 되돌렸다(기획 §2.2 A-3).
        /// </summary>
        private static readonly LastShiftPreset[] SignallingPresets =
        {
            LastShiftPreset.HighHeatHighThrust,
            LastShiftPreset.PowerOverloadLooseBattery,
            LastShiftPreset.BadAttitudeHighOxygen
        };

        /// <summary>
        /// 운석이 부품을 떼어낸 뒤 두 프리셋이 서로 다른 상황을 만드는가. 이것이 화면에
        /// 그려질 내용의 원본이다.
        /// </summary>
        [Test]
        public void SignallingPresetsProduceDifferentSituationsAfterDetachment()
        {
            var signatures = SignallingPresets.ToDictionary(preset => preset, preset => SituationsOf(preset, false));

            for (var a = 0; a < SignallingPresets.Length; a++)
            for (var b = a + 1; b < SignallingPresets.Length; b++)
            {
                var left = signatures[SignallingPresets[a]];
                var right = signatures[SignallingPresets[b]];
                Assert.That(left.SetEquals(right), Is.False,
                    $"{SignallingPresets[a]} 와 {SignallingPresets[b]} 의 상황 집합이 같다 — " +
                    $"{Describe(left)} / {Describe(right)}. 두 프리셋이 화면에서 구분되지 않는다.");
            }
        }

        /// <summary>
        /// 화면 전체가 갈리는가. <b>구역 4칸만 보면 안 된다</b> — v0.3(§5.7.3)에서 4칸은
        /// 산소 전용으로 좁혀졌고, 열·전력·추진은 배 전역 단일값이라 막대와 지배 문제 1행이
        /// 맡는다. 그래서 화면 서명은 "산소 4칸 + 지배 계통" 을 함께 봐야 한다.
        /// </summary>
        [Test]
        public void SignallingPresetsProduceDifferentScreenSignatures()
        {
            var signatures = SignallingPresets.ToDictionary(preset => preset, preset => ScreenSignatureOf(preset, false));

            for (var a = 0; a < SignallingPresets.Length; a++)
            for (var b = a + 1; b < SignallingPresets.Length; b++)
                Assert.That(signatures[SignallingPresets[a]], Is.Not.EqualTo(signatures[SignallingPresets[b]]),
                    $"{SignallingPresets[a]} 와 {SignallingPresets[b]} 의 화면 서명이 같다 — " +
                    $"{signatures[SignallingPresets[a]]}. 화면상 두 프리셋이 같은 그림이다.");
        }

        /// <summary>
        /// 화면이 실제로 무언가 말하는가. 상황 집합이 달라도 전부 <c>정상</c> 으로 접히면
        /// 화면은 여전히 침묵한다 — 배선이 끊겼을 때 정확히 그 증상이 나온다.
        /// </summary>
        [Test]
        public void SignallingPresetsRaiseSomethingAboveNormal()
        {
            foreach (var preset in SignallingPresets)
                Assert.That(ScreenSignatureOf(preset, false), Does.Not.EqualTo(SilentSignature()),
                    $"{preset} 이 부품 이탈 상태에서도 전부 정상이다 — 화면에 표시할 위험이 없다.");
        }

        /// <summary>
        /// §5.7.5 — <b>상시 요소는 운석 전에도 현재 상황을 반영해야 한다.</b>
        /// <c>BadAttitudeHighOxygen</c> 은 부품이 제자리에 있는 t=0 에 이미 <c>S-T1</c> 이
        /// 활성이므로 화면이 그때부터 말해야 한다. 예전처럼 <c>M</c> 을 눌러야 바뀌는 구조면
        /// 이 검사가 운다.
        ///
        /// 나머지 두 프리셋은 t=0 에 발동선을 안 넘으므로 여기서 요구하지 않는다 — 그건
        /// 화면이 아니라 프리셋 수치의 성질이다.
        /// </summary>
        [Test]
        public void PreMeteorStateIsAlreadyReflected()
        {
            var signature = ScreenSignatureOf(LastShiftPreset.BadAttitudeHighOxygen, true);
            Assert.That(signature, Does.Not.EqualTo(SilentSignature()),
                "BadAttitudeHighOxygen 이 운석 전 t=0 에 아무것도 안 보여준다 — " +
                "상시 요소가 사건 이후에만 채워지고 있다(§5.7.5 위반).");
        }

        /// <summary>
        /// 전력 프리셋이 전력 상황을 켜는가. <b>한때 안 켰다</b> — <c>BusPower</c> 가
        /// <c>0.98</c> 이라 S-P1 발동선 <c>0.65</c> 를 못 넘어서, 배터리가 bus 에서 빠져 있어도
        /// (<c>[LAST_SHIFT_DAMAGE] power=True</c> 로 확인) 전력으로는 아무 말도 못 했다.
        /// balance 가 <c>0.62</c> 로 정정해 켜졌고, 이 검사가 그 상태를 고정한다.
        /// </summary>
        [Test]
        public void PowerPresetRaisesAPowerSituation()
        {
            var situations = SituationsOf(LastShiftPreset.PowerOverloadLooseBattery, false);
            Assert.That(
                situations.Any(situation =>
                    LastShiftSituationTable.ChannelOf(situation) == LastShiftSystemChannel.Power),
                Is.True,
                $"전력 프리셋이 전력 상황을 하나도 안 켠다 — {Describe(situations)}");
        }

        // ── 도우미 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="intactParts"/> 가 참이면 운석 전(부품 제자리), 거짓이면 운석이
        /// 부품을 떼어낸 뒤다. 컨트롤러도 같은 규칙으로 판단한다 — 손상되지 않은 계통은
        /// 복구된 것으로 본다(<c>LastShiftSandboxController.IsSystemRestored</c>).
        /// </summary>
        private static HashSet<LastShiftSituation> SituationsOf(LastShiftPreset preset, bool intactParts)
        {
            var tracker = EvaluateAt(preset, intactParts);
            var active = new HashSet<LastShiftSituation>();

            foreach (var channel in Channels)
            {
                var situation = tracker.StatusOf(channel).Situation;
                if (situation != LastShiftSituation.None) active.Add(situation);
            }

            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var situation = tracker.OxygenStatusOf((LastShiftZone)index).Situation;
                if (situation != LastShiftSituation.None) active.Add(situation);
            }

            return active;
        }

        /// <summary>
        /// 화면 서명 = 산소 4칸 + 계통 3개 등급. <b>화면과 같은 규칙으로 접는다</b> —
        /// 4칸은 산소만 보고(§5.7.3), 계통값은 칸에 겹치지 않고 따로 선다.
        /// </summary>
        private static string ScreenSignatureOf(LastShiftPreset preset, bool intactParts)
        {
            var tracker = EvaluateAt(preset, intactParts);
            var parts = new List<string>();

            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var zone = (LastShiftZone)index;
                var grade = LastShiftSituationTable.GradeOf(tracker.OxygenStatusOf(zone).Situation);
                parts.Add($"{LastShiftZoneAtlas.ShortLabelOf(zone)}={LastShiftSituationText.GradeLabel(grade)}");
            }

            foreach (var channel in Channels)
            {
                var grade = LastShiftSituationTable.GradeOf(tracker.StatusOf(channel).Situation);
                parts.Add($"{LastShiftSituationText.ChannelLocationLabel(channel)}={LastShiftSituationText.GradeLabel(grade)}");
            }

            return string.Join(" ", parts);
        }

        /// <summary>전부 정상인 서명. 화면이 아무 말도 안 하는 상태다.</summary>
        private static string SilentSignature()
        {
            var parts = new List<string>();
            var normal = LastShiftSituationText.GradeLabel(LastShiftSituationGrade.Normal);
            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
                parts.Add($"{LastShiftZoneAtlas.ShortLabelOf((LastShiftZone)index)}={normal}");
            foreach (var channel in Channels)
                parts.Add($"{LastShiftSituationText.ChannelLocationLabel(channel)}={normal}");
            return string.Join(" ", parts);
        }

        private static readonly LastShiftSystemChannel[] Channels =
        {
            LastShiftSystemChannel.Heat,
            LastShiftSystemChannel.Power,
            LastShiftSystemChannel.Propulsion
        };

        private static LastShiftSituationTracker EvaluateAt(LastShiftPreset preset, bool intactParts)
        {
            var state = LastShiftPresetFactory.Create(preset);
            var pressures = LastShiftZonePressures.Uniform(state.OxygenPressure);
            var containment = new LastShiftContainment
            {
                CoolingRestored = intactParts, PowerRestored = intactParts, OxygenRestored = intactParts
            };

            var tracker = new LastShiftSituationTracker();
            tracker.Evaluate(LastShiftSituationInput.From(state, pressures, containment), 0f);
            return tracker;
        }

        private static string Describe(IEnumerable<LastShiftSituation> situations) =>
            "{" + string.Join(", ", situations.OrderBy(situation => (int)situation)) + "}";
    }
}
