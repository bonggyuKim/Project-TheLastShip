using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 자극에 걸린 두 상한(PM 확정 2026-08-12)과, 조종석 피격의 실제 비용.
    ///
    /// <b>둘 다 밸런스 값이 아니라 조항이다.</b> 넘으면 그 방에 못 들어가거나(파공) 도킹을
    /// 못 채우는 판이 되어(연료), 복구 행동이 있어도 못 이긴다 — <c>RG-3</c> 이 막으려는
    /// 상태와 같은 모양이다. 그래서 튜닝으로 조용히 넘어가지 않게 여기서 못박는다.
    /// </summary>
    public sealed class LastShiftStimulusCapTests
    {
        private const float Step = 1f / 60f;

        [SetUp]
        public void SetUp() => LastShiftExternalStimulus.Clear();

        [TearDown]
        public void TearDown() => LastShiftExternalStimulus.Clear();

        /// <summary>
        /// <b>자극은 방 압력을 바닥 밑으로 못 민다 — balance 지정 3케이스.</b>
        ///
        /// 바닥을 <b>손실량</b>이 아니라 <b>결과 압력</b>에 대는 것이 요점이다. 손실 합계를
        /// 자르면 시작 압력이 이미 낮을 때 보호가 안 된다 — <c>0.40</c> 에서 맞으면
        /// <c>0.05</c> 까지 그대로 뚫려서 조항이 막으려던 상황이 안 막힌다.
        /// </summary>
        [Test]
        public void TheStimulusCannotPushPressureBelowTheFloor()
        {
            Assert.That(LastShiftExternalStimulus.StimulusPressureFloor,
                Is.EqualTo(LastShiftSituationTable.ZoneLowPressureTrigger).Within(0.0001f),
                "바닥이 저압 판정선에서 안 나온다 — 임계가 움직이면 보장이 조용히 깨진다");

            Assert.That(LastShiftExternalStimulus.ApplyStimulusPressure(1.000f, -0.015f),
                Is.EqualTo(0.985f).Within(0.0001f), "높은 압력에서 바닥이 잘못 걸렸다");
            Assert.That(LastShiftExternalStimulus.ApplyStimulusPressure(0.360f, -0.015f),
                Is.EqualTo(0.350f).Within(0.0001f), "바닥에 안 닿고 지나갔다");
            Assert.That(LastShiftExternalStimulus.ApplyStimulusPressure(0.300f, -0.015f),
                Is.EqualTo(0.300f).Within(0.0001f),
                "이미 바닥 밑인 방이 자극으로 더 내려갔거나 올라갔다");
        }

        /// <summary>
        /// <b>바닥이 압력을 올리지는 않는다.</b> 이미 바닥 밑인 방을 자극이 끌어올리면
        /// 맞은 것이 이득이 된다 — 안쪽 <c>Min</c> 이 그것을 막는다.
        /// </summary>
        [Test]
        public void TheFloorNeverLiftsARoomThatIsAlreadyBelowIt()
        {
            foreach (var start in new[] { 0.34f, 0.20f, 0.00f })
                Assert.That(LastShiftExternalStimulus.ApplyStimulusPressure(start, -0.05f),
                    Is.EqualTo(start).Within(0.0001f), $"{start} 에서 압력이 움직였다");
        }

        /// <summary>
        /// <b><see cref="LastShiftExternalStimulus.DeltaFor"/> 는 순수 증분만 준다.</b> 바닥을
        /// 여기서 대면 소비처가 현재 압력을 모르는 채로 잘린 값을 받게 되고, 그러면 시작
        /// 압력에 따라 보호가 달라지는 것 자체가 표현되지 않는다.
        /// </summary>
        [Test]
        public void TheDeltaStaysAPlainIncrement()
        {
            var oxygen = LastShiftExternalStimulus.DeltaFor(LastShiftStimulusRoom.LifeSupport, 1f, 1f);
            var expected = -(LastShiftExternalStimulus.BreachPressureLoss
                             + LastShiftExternalStimulus.LifeSupportOxygenLoss);

            Assert.That(oxygen.ZonePressure, Is.EqualTo(expected).Within(0.0001f),
                "산소실 증분이 이미 잘려서 나온다 — 바닥이 소비처가 아니라 여기 걸렸다");
        }

        /// <summary>
        /// <b>연료 상한은 항해 단위다.</b> 조종석을 여러 번 맞아도 이 총합을 넘지 않는다 —
        /// 넘으면 도킹에 필요한 추력적분을 못 채우는 판이 되고, 그건 복구 행동이 있어도
        /// 못 이기는 판이다.
        /// </summary>
        [Test]
        public void TheVoyageNeverLosesMoreFuelThanTheCap()
        {
            Assert.That(LastShiftExternalStimulus.VoyageFuelLossCap, Is.EqualTo(0.18f).Within(0.001f));

            var lost = 0f;
            // 조종석이 나올 때까지 구간을 열고, 나오면 램프를 끝까지 돌린다. 여러 항해분을
            // 한 항해에 몰아 때리는 극단이라 상한이 안 걸리면 여기서 새어 나온다.
            for (var segment = 0; segment < 60; segment++)
            {
                LastShiftExternalStimulus.BeginSegment(segment);
                if (LastShiftExternalStimulus.Room != LastShiftStimulusRoom.Cockpit) continue;

                LastShiftExternalStimulus.FireAtForProbe(0f);
                var left = LastShiftExternalStimulus.DamageSeconds + 1f;
                while (left > 0f)
                {
                    var dt = Mathf.Min(Step, left);
                    lost += -LastShiftExternalStimulus.Tick(dt).FuelReserve;
                    left -= dt;
                }
            }

            Assert.That(lost, Is.LessThanOrEqualTo(LastShiftExternalStimulus.VoyageFuelLossCap + 0.001f),
                $"항해 하나가 자극으로 연료를 {lost:F3} 잃었다 — 상한 " +
                $"{LastShiftExternalStimulus.VoyageFuelLossCap} 를 넘었다");
            Assert.That(LastShiftExternalStimulus.VoyageFuelLost,
                Is.EqualTo(lost).Within(0.001f), "누적 기록과 실제로 나간 양이 다르다");
        }

        /// <summary>새 항해는 연료 상한도 새로 받는다.</summary>
        [Test]
        public void ANewVoyageGetsItsFuelBudgetBack()
        {
            LastShiftExternalStimulus.BeginSegment(0);
            LastShiftExternalStimulus.FireAtForProbe(0f);
            for (var i = 0; i < 600; i++) LastShiftExternalStimulus.Tick(Step);

            LastShiftExternalStimulus.Clear();

            Assert.That(LastShiftExternalStimulus.VoyageFuelLost, Is.Zero,
                "연료 상한이 항해를 넘어 따라왔다");
        }

        /// <summary>
        /// <b>조종석 피격의 자세 이탈은 연료를 안 먹는다 — 실측(PM 질문 2).</b>
        ///
        /// 자세는 추력계와 분리된 <b>직접 입력값</b>이다. 연료는
        /// <c>LastShiftRecoveryTuning.FuelDrainPerThrustSecond × ThrustDemand × dt</c> 로만
        /// 빠지고 그 식에 자세가 안 들어간다. 그래서 <c>15</c>도를 되돌리는 데 드는 추력-초는
        /// <c>0</c> 이고, 교정분이 직접 연료손실 <c>0.06</c> 을 잠식할 일이 없다.
        ///
        /// 이 검사는 그 사실이 나중에 조용히 바뀌는 것을 막는다 — 자세에 연료를 물리는
        /// 순간 조종석 피격의 안전선 계산이 통째로 다시 열린다.
        /// </summary>
        [Test]
        public void CorrectingAttitudeCostsNoFuel()
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.BadAttitudeHighOxygen);
            state.ThrustDemand = 0f;
            var before = state.FuelReserve;

            // 자세만 15도 틀어 놓고 시간을 흘려도 연료가 안 준다.
            state.ShipAttitudeDegrees += LastShiftExternalStimulus.CockpitAttitudeDrift;
            var containment = default(LastShiftContainment);
            for (var i = 0; i < 600; i++)
                LastShiftDeterioration.Tick(ref state, containment, Step);

            Assert.That(state.FuelReserve, Is.EqualTo(before).Within(0.0001f),
                "자세가 틀어진 것만으로 연료가 줄었다 — 자세가 추력계에 묶였다는 뜻이다");
        }

        /// <summary>
        /// <b>15도 하나로는 자세 상황이 안 뜬다 — 실측.</b> 트리거가 <c>60</c>도이고 프리셋
        /// 시작 자세가 <c>8</c>·<c>12</c>도라, 한 번 맞아서는 <c>23</c>·<c>27</c>도까지밖에
        /// 안 간다. 조종석 피격의 (B)가 실질적으로 연료 손실 하나로 남는다는 뜻이라
        /// game-balance 가 알아야 할 값이다.
        /// </summary>
        [Test]
        public void OneHitCannotByItselfTriggerTheAttitudeSituation()
        {
            foreach (var preset in new[]
                     {
                         LastShiftPreset.HighHeatHighThrust,
                         LastShiftPreset.PowerOverloadLooseBattery
                     })
            {
                var start = LastShiftPresetFactory.Create(preset).ShipAttitudeDegrees;
                var after = Mathf.Abs(start) + LastShiftExternalStimulus.CockpitAttitudeDrift;

                Assert.That(after, Is.LessThan(LastShiftSituationTable.AttitudeTriggerDegrees),
                    $"{preset} 은 한 번 맞고 바로 자세 상황이 뜬다 — 서서히 원칙이 깨진다");
            }
        }
    }
}
