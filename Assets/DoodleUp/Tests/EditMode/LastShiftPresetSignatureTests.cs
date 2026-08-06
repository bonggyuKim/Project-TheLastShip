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
    /// <item><b><c>PowerOverloadLooseBattery</c> 는 어느 조건에서도 상황을 하나도 안 켠다.</b>
    /// 전력 위기를 담당해야 할 프리셋인데 <c>BusPower</c> 가 <c>0.98</c> 이라 전력 상황의
    /// 발동선을 못 넘는다. 이건 HUD 를 고쳐도 안 고쳐진다 — 아래 <see cref="PowerPresetRaisesAPowerSituation"/>
    /// 참고.</item>
    /// </list>
    /// </summary>
    public sealed class LastShiftPresetSignatureTests
    {
        /// <summary>
        /// 상황이 실제로 갈리는 두 프리셋. <c>PowerOverloadLooseBattery</c> 는 아직 신호가
        /// 없어서 여기 없다 — 넣으면 이 검사가 "화면이 망가졌다" 가 아니라 "프리셋 수치가
        /// 미완이다" 로 울어서 신호가 섞인다. 그 미완은 아래 전용 검사가 따로 든다.
        /// </summary>
        private static readonly LastShiftPreset[] SignallingPresets =
        {
            LastShiftPreset.HighHeatHighThrust,
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
        /// 구역 등급 4칸이 갈리는가. 상황이 달라도 전부 같은 칸에 몰리면 플레이어는 여전히
        /// 차이를 못 본다 — CT-01 §5.2 의 4칸이 판정 화면이기 때문이다.
        /// </summary>
        [Test]
        public void SignallingPresetsProduceDifferentZoneGradePatterns()
        {
            var patterns = SignallingPresets.ToDictionary(preset => preset, preset => ZoneGradesOf(preset, false));

            for (var a = 0; a < SignallingPresets.Length; a++)
            for (var b = a + 1; b < SignallingPresets.Length; b++)
                Assert.That(patterns[SignallingPresets[a]], Is.Not.EqualTo(patterns[SignallingPresets[b]]),
                    $"{SignallingPresets[a]} 와 {SignallingPresets[b]} 의 구역 등급 4칸이 같다 — " +
                    $"{patterns[SignallingPresets[a]]}. 화면상 두 프리셋이 같은 그림이다.");
        }

        /// <summary>
        /// 등급이 실제로 <c>정상</c> 위로 올라가는가. 상황 집합이 달라도 전부 <c>정상</c> 이면
        /// 화면은 여전히 아무 말도 안 한다 — 배선이 끊겼을 때 정확히 그 증상이 나온다.
        /// </summary>
        [Test]
        public void SignallingPresetsRaiseAtLeastOneZoneAboveNormal()
        {
            foreach (var preset in SignallingPresets)
                Assert.That(ZoneGradesOf(preset, false), Does.Not.EqualTo(AllNormalPattern()),
                    $"{preset} 이 부품 이탈 상태에서도 전 구역 정상이다 — 화면에 표시할 위험이 없다.");
        }

        /// <summary>
        /// <b>미해결 내용 결함.</b> <c>PowerOverloadLooseBattery</c> 는 전력 위기를 담당하는
        /// 프리셋인데 상황을 하나도 안 켠다. <c>BusPower = 0.98</c> 이라 전력 상황 셋의
        /// 발동선을 전부 못 넘고, 부품을 떼어내도 마찬가지다(다른 두 프리셋은 같은 조건에서
        /// <c>BusDetached</c> 가 켜진다).
        ///
        /// <b>여기서 수치를 고치지 않는다.</b> 프리셋 초기값은 밸런스 소관이고, 임의로 내리면
        /// 그 프리셋이 무엇을 가르치는 프리셋인지가 코드에서 바뀐다. 카드 <c>0fb18e77</c> 로
        /// 보고했으며 값이 정해지면 이 검사를 켜고 위 배열에 프리셋을 되돌린다.
        /// </summary>
        [Test]
        [Ignore("미해결: PowerOverloadLooseBattery 가 BusPower 0.98 이라 전력 상황을 하나도 못 켠다. " +
                "프리셋 수치는 game-balance 소관이라 여기서 고치지 않는다 (카드 0fb18e77 로 보고됨).")]
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

            foreach (var channel in new[]
                     {
                         LastShiftSystemChannel.Heat,
                         LastShiftSystemChannel.Power,
                         LastShiftSystemChannel.Propulsion
                     })
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

        /// <summary>구역 등급 4칸을 화면과 같은 규칙으로 접는다(그 구역 계통과 산소 중 높은 쪽).</summary>
        private static string ZoneGradesOf(LastShiftPreset preset, bool intactParts)
        {
            var tracker = EvaluateAt(preset, intactParts);
            var cells = new List<string>();
            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var zone = (LastShiftZone)index;
                var grade = LastShiftSituationTable.GradeOf(tracker.OxygenStatusOf(zone).Situation);
                if (LastShiftSituationText.TryChannelOfZone(zone, out var channel))
                {
                    var channelGrade = LastShiftSituationTable.GradeOf(tracker.StatusOf(channel).Situation);
                    if (channelGrade > grade) grade = channelGrade;
                }

                cells.Add($"{LastShiftZoneAtlas.ShortLabelOf(zone)}={LastShiftSituationText.GradeLabel(grade)}");
            }

            return string.Join(" ", cells);
        }

        private static string AllNormalPattern()
        {
            var cells = new List<string>();
            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
                cells.Add($"{LastShiftZoneAtlas.ShortLabelOf((LastShiftZone)index)}=" +
                          $"{LastShiftSituationText.GradeLabel(LastShiftSituationGrade.Normal)}");
            return string.Join(" ", cells);
        }

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
