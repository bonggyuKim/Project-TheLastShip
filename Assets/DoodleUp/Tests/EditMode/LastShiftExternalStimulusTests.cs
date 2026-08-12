using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 외부 랜덤 자극 1단계(운석 승격) — <c>docs/external-random-stimulus-layer-v1.md</c> §8 판정 기준.
    ///
    /// 재는 것은 그 문서가 PASS 조건으로 든 셋이다 — <b>구간마다 정확히 한 번</b>,
    /// <b>다섯 방에 랜덤한 강도로</b>, <b>§2.1-1 표대로 그 방의 계통이 반응</b>. 여기에
    /// <c>RG-4</c> 를 다시 열지 않기 위한 조건(강도가 검증 상한 안 · 손상이 즉발이 아님)을
    /// 더한다.
    ///
    /// <b>씬을 안 본다.</b> 시계를 직접 돌려 규칙만 잰다. 실제로 누가 이 시계를 돌리는지는
    /// PlayMode 몫이다 — EditMode 가 자기 손으로 돌려 전부 초록인데 게임에서는 아무도 안
    /// 부르는 상태가 <c>LastShiftEvaLift.Tick</c> 에서 이미 한 번 났다.
    /// </summary>
    public sealed class LastShiftExternalStimulusTests
    {
        private const float Step = 1f / 60f;

        [SetUp]
        public void SetUp() => LastShiftExternalStimulus.Clear();

        [TearDown]
        public void TearDown() => LastShiftExternalStimulus.Clear();

        /// <summary>구간을 끝까지 돌리며 터진 횟수를 센다.</summary>
        private static int FireCountOverOneSegment(int seed)
        {
            LastShiftExternalStimulus.BeginSegment(seed);
            var fires = 0;
            var wasFired = false;
            var left = LastShiftExternalStimulus.SegmentSeconds;
            while (left > 0f)
            {
                var dt = Mathf.Min(Step, left);
                LastShiftExternalStimulus.Tick(dt);
                if (!wasFired && LastShiftExternalStimulus.HasFired) fires++;
                wasFired = LastShiftExternalStimulus.HasFired;
                left -= dt;
            }

            return fires;
        }

        /// <summary>
        /// <b>구간당 정확히 한 번.</b> 0 회가 되는 구간이 생기면 다음 기항에 뜰 잔해 종류가
        /// 없어져 파밍 순환이 끊기고(§1), 2 회가 되면 구간 정의가 깨진다.
        /// </summary>
        [Test]
        public void EverySegmentGetsExactlyOneStimulus()
        {
            for (var seed = 0; seed < 40; seed++)
                Assert.That(FireCountOverOneSegment(seed), Is.EqualTo(1),
                    $"seed={seed} 구간에서 자극이 한 번이 아니다");
        }

        /// <summary>
        /// 창 밖에서는 안 터진다. 너무 이르면 준비할 시간이, 너무 늦으면 대응할 시간이 없다.
        /// </summary>
        [Test]
        public void TheStimulusLandsInsideItsWindow()
        {
            var earliest = LastShiftExternalStimulus.SegmentSeconds * LastShiftExternalStimulus.EarliestFraction;
            var latest = LastShiftExternalStimulus.SegmentSeconds * LastShiftExternalStimulus.LatestFraction;

            for (var seed = 0; seed < 60; seed++)
            {
                LastShiftExternalStimulus.BeginSegment(seed);
                Assert.That(LastShiftExternalStimulus.FireAtSeconds, Is.InRange(earliest, latest),
                    $"seed={seed} 발동 시점이 창 밖이다");
            }

            // 예약 시점 전에는 아무 일도 없다.
            LastShiftExternalStimulus.BeginSegment(7);
            var before = LastShiftExternalStimulus.FireAtSeconds - 1f;
            var left = before;
            while (left > 0f) { var dt = Mathf.Min(Step, left); LastShiftExternalStimulus.Tick(dt); left -= dt; }
            Assert.That(LastShiftExternalStimulus.HasFired, Is.False, "예약 시점 전에 터졌다");
        }

        /// <summary>
        /// <b>강도가 RG-4 가 훑은 범위를 안 넘는다.</b> 상한이 현행 고정값 <c>0.9924</c> 를
        /// 넘으면 전수검증 밖 강도가 되어 재검증 대상이 된다. 하한이 너무 낮으면 상황이
        /// 하나도 안 뜨는 자극이 되어 존재 이유가 없어진다.
        /// </summary>
        [Test]
        public void SeverityStaysInsideTheVerifiedEnvelope()
        {
            var canonical = LastShiftMeteorApplication.CalculateSeverity(LastShiftMeteorStimulus.Canonical);
            Assert.That(LastShiftExternalStimulus.MaxSeverity, Is.LessThanOrEqualTo(canonical),
                $"상한 {LastShiftExternalStimulus.MaxSeverity} 가 전수검증 기준 {canonical:F4} 를 넘는다");
            Assert.That(LastShiftExternalStimulus.MinSeverity, Is.EqualTo(0.70f).Within(0.001f));

            for (var seed = 0; seed < 60; seed++)
            {
                LastShiftExternalStimulus.BeginSegment(seed);
                Assert.That(LastShiftExternalStimulus.Severity,
                    Is.InRange(LastShiftExternalStimulus.MinSeverity, LastShiftExternalStimulus.MaxSeverity),
                    $"seed={seed} 강도가 범위 밖이다");
            }
        }

        /// <summary>다섯 방이 전부 나온다 — 하나라도 안 나오면 그 방은 없는 것과 같다.</summary>
        [Test]
        public void AllFiveRoomsComeUpAcrossSeeds()
        {
            var seen = new HashSet<LastShiftStimulusRoom>();
            for (var seed = 0; seed < 200; seed++)
            {
                LastShiftExternalStimulus.BeginSegment(seed);
                seen.Add(LastShiftExternalStimulus.Room);
            }

            Assert.That(seen.Count, Is.EqualTo(5), $"안 나온 방이 있다 — 나온 방 {seen.Count}개");
        }

        /// <summary>
        /// <b>같은 씨앗은 같은 구간이다.</b> 재현이 안 되면 "왜 졌는지" 를 못 가리고,
        /// 그것이 랜덤화를 여태 미뤄 온 원래 이유였다(§5).
        /// </summary>
        [Test]
        public void TheSameSeedRepeatsTheSameSegment()
        {
            LastShiftExternalStimulus.BeginSegment(1234);
            var room = LastShiftExternalStimulus.Room;
            var severity = LastShiftExternalStimulus.Severity;
            var at = LastShiftExternalStimulus.FireAtSeconds;

            LastShiftExternalStimulus.BeginSegment(1234);

            Assert.That(LastShiftExternalStimulus.Room, Is.EqualTo(room));
            Assert.That(LastShiftExternalStimulus.Severity, Is.EqualTo(severity).Within(0.0001f));
            Assert.That(LastShiftExternalStimulus.FireAtSeconds, Is.EqualTo(at).Within(0.0001f));
        }

        /// <summary>
        /// <b>서서히 들어간다 — 이 검사가 RG-4 를 다시 안 여는 근거다.</b> 한 tick 이 총량의
        /// 몇 퍼센트만 밀어야 계통이 중간 등급을 건너뛰지 않는다. 즉발이면 전수검증이 훑은
        /// 1,920 조합 밖의 상태가 생긴다.
        /// </summary>
        [Test]
        public void TheDamageArrivesGraduallyNotAllAtOnce()
        {
            Assert.That(LastShiftExternalStimulus.DamageSeconds, Is.GreaterThan(0f),
                "DamageSeconds 가 0 이면 즉발이고, 그 순간 RG-4 재검증 대상이 된다");

            LastShiftExternalStimulus.BeginSegment(3);
            // 발동 직전까지 민다.
            var left = LastShiftExternalStimulus.FireAtSeconds;
            while (left > 0f) { var dt = Mathf.Min(Step, left); LastShiftExternalStimulus.Tick(dt); left -= dt; }

            var first = LastShiftExternalStimulus.Tick(Step);
            Assume.That(LastShiftExternalStimulus.HasFired, Is.True);

            var share = Step / LastShiftExternalStimulus.DamageSeconds;
            Assert.That(Mathf.Abs(first.ZonePressure),
                Is.LessThanOrEqualTo(LastShiftExternalStimulus.BreachPressureLoss * share * 1.5f),
                "첫 tick 이 총량의 한 프레임 몫보다 크게 밀었다 — 즉발에 가깝다");
        }

        /// <summary>총량은 <see cref="LastShiftExternalStimulus.DamageSeconds"/> 뒤에 다 들어간다.</summary>
        [Test]
        public void TheWholeAmountLandsByTheEndOfTheRamp()
        {
            LastShiftExternalStimulus.BeginSegment(11);
            var left = LastShiftExternalStimulus.FireAtSeconds;
            while (left > 0f) { var dt = Mathf.Min(Step, left); LastShiftExternalStimulus.Tick(dt); left -= dt; }

            var pressure = 0f;
            left = LastShiftExternalStimulus.DamageSeconds + 1f;
            while (left > 0f)
            {
                var dt = Mathf.Min(Step, left);
                pressure += LastShiftExternalStimulus.Tick(dt).ZonePressure;
                left -= dt;
            }

            var expected = -LastShiftExternalStimulus.BreachPressureLoss * LastShiftExternalStimulus.Severity;
            Assert.That(pressure, Is.EqualTo(expected).Within(0.005f),
                "램프가 끝났는데 총량이 안 들어갔다");

            // 램프가 끝나면 더 안 민다 — 안 그러면 구간 내내 계속 깎인다.
            Assert.That(LastShiftExternalStimulus.Tick(Step).IsEmpty, Is.True,
                "램프가 끝났는데 계속 밀고 있다");
        }

        /// <summary>
        /// <b>§2.1-1 방 → 계통 대응표.</b> 방마다 <b>자기 계통만</b> 움직여야 한다 — 한 방이
        /// 여러 계통을 같이 때리면 그 조합이 <c>RG-4</c> 가 안 본 동시발생이 된다.
        /// </summary>
        [Test]
        public void EachRoomTouchesOnlyItsOwnSystem()
        {
            var cockpit = LastShiftExternalStimulus.DeltaFor(LastShiftStimulusRoom.Cockpit, 1f, 1f);
            Assert.That(cockpit.FuelReserve, Is.LessThan(0f), "조종석인데 연료가 안 샌다");
            Assert.That(cockpit.AttitudeDegrees, Is.Not.EqualTo(0f), "조종석인데 자세가 안 틀어진다");
            Assert.That(cockpit.BusPower, Is.EqualTo(0f), "조종석이 전력을 건드린다");
            Assert.That(cockpit.EngineHeat, Is.EqualTo(0f), "조종석이 열을 건드린다");

            var power = LastShiftExternalStimulus.DeltaFor(LastShiftStimulusRoom.Power, 1f, 1f);
            Assert.That(power.BusPower, Is.LessThan(0f), "전력실인데 전력이 안 떨어진다");
            Assert.That(power.EngineHeat, Is.EqualTo(0f));
            Assert.That(power.FuelReserve, Is.EqualTo(0f));

            var cooling = LastShiftExternalStimulus.DeltaFor(LastShiftStimulusRoom.Cooling, 1f, 1f);
            Assert.That(cooling.EngineHeat, Is.GreaterThan(0f), "냉각실인데 열이 안 오른다");
            Assert.That(cooling.BusPower, Is.EqualTo(0f));
            Assert.That(cooling.FuelReserve, Is.EqualTo(0f));
        }

        /// <summary>
        /// 산소실만 <b>(A)와 (B)가 같은 축에서 겹친다</b> — 파공도 산소, 고유 손상도 산소라
        /// 압력이 두 번 깎인다(§2.1-1 표의 "이중").
        /// </summary>
        [Test]
        public void TheOxygenRoomLosesPressureTwice()
        {
            var oxygen = LastShiftExternalStimulus.DeltaFor(LastShiftStimulusRoom.LifeSupport, 1f, 1f);
            var plain = LastShiftExternalStimulus.DeltaFor(LastShiftStimulusRoom.Quarters, 1f, 1f);

            Assert.That(oxygen.ZonePressure, Is.LessThan(plain.ZonePressure),
                "산소실 압력 손실이 파공만 있는 방과 같다 — 이중이 아니다");
            Assert.That(oxygen.BusPower, Is.EqualTo(0f));
            Assert.That(oxygen.EngineHeat, Is.EqualTo(0f));
        }

        /// <summary>
        /// <b>숙소는 고유 손상이 없다.</b> 억지로 다섯 번째 계통을 만들면 그것이 곧 13번째
        /// 상황이 되어 상황 표 동결선을 깬다 — 없는 것을 없는 대로 둔 것이 설계다.
        /// </summary>
        [Test]
        public void TheQuartersOnlyLoseAir()
        {
            var quarters = LastShiftExternalStimulus.DeltaFor(LastShiftStimulusRoom.Quarters, 1f, 1f);

            Assert.That(quarters.ZonePressure, Is.LessThan(0f), "숙소인데 파공도 없다");
            Assert.That(quarters.BusPower, Is.EqualTo(0f));
            Assert.That(quarters.EngineHeat, Is.EqualTo(0f));
            Assert.That(quarters.FuelReserve, Is.EqualTo(0f));
            Assert.That(quarters.AttitudeDegrees, Is.EqualTo(0f));
        }

        /// <summary>
        /// 방 → 구역. <b>숙소는 조종석과 한 구역이다</b> — 방이 다섯인데 구역이 넷인 겹침이
        /// 설계 그대로다.
        /// </summary>
        [Test]
        public void EveryRoomBreachesItsOwnZoneAndQuartersSharesTheCockpit()
        {
            Assert.That(LastShiftExternalStimulus.BreachZoneOf(LastShiftStimulusRoom.Cockpit),
                Is.EqualTo(LastShiftZone.Cockpit));
            Assert.That(LastShiftExternalStimulus.BreachZoneOf(LastShiftStimulusRoom.Power),
                Is.EqualTo(LastShiftZone.Power));
            Assert.That(LastShiftExternalStimulus.BreachZoneOf(LastShiftStimulusRoom.Cooling),
                Is.EqualTo(LastShiftZone.Cooling));
            Assert.That(LastShiftExternalStimulus.BreachZoneOf(LastShiftStimulusRoom.LifeSupport),
                Is.EqualTo(LastShiftZone.LifeSupport));
            Assert.That(LastShiftExternalStimulus.BreachZoneOf(LastShiftStimulusRoom.Quarters),
                Is.EqualTo(LastShiftZone.Cockpit),
                "숙소가 자기 구역을 가지면 배의 기압 구획이 하나 늘어난 것이다");
        }

        /// <summary>
        /// 강도를 바꿔 만든 운석이 <b>실제로 그 강도가 되는가</b>. 속도만 바꾸는데 에너지가
        /// 속도의 제곱이라, 비율을 그대로 곱하면 값이 안 맞는다.
        /// </summary>
        [Test]
        public void ScalingTheMeteorHitsTheAskedSeverity()
        {
            foreach (var target in new[] { 0.70f, 0.85f, 0.99f })
            {
                var scaled = LastShiftSandboxController.ScaleMeteorTo(
                    LastShiftMeteorStimulus.Canonical, target);
                Assert.That(LastShiftMeteorApplication.CalculateSeverity(scaled),
                    Is.EqualTo(target).Within(0.001f), $"목표 {target} 이 안 나온다");
            }
        }

        /// <summary>
        /// <b>새 상황도 새 동사도 안 생겼다.</b> 자극이 미는 것은 전부 기존 계통 값이고,
        /// 그 값이 임계를 넘으면 상황 표가 알아서 반응한다.
        /// </summary>
        [Test]
        public void TheStimulusOnlyMovesExistingSystems()
        {
            foreach (LastShiftStimulusRoom room in System.Enum.GetValues(typeof(LastShiftStimulusRoom)))
            {
                var delta = LastShiftExternalStimulus.DeltaFor(room, 1f, 1f);
                // 미는 축은 다섯뿐이다 — 구역 압력·전력·열·연료·자세. 여기에 없는 축이
                // 생기면 그것이 곧 새 손상 타입이다.
                Assert.That(delta.ZonePressure, Is.LessThanOrEqualTo(0f), $"{room} 이 압력을 올린다");
                Assert.That(delta.BusPower, Is.LessThanOrEqualTo(0f), $"{room} 이 전력을 올린다");
                Assert.That(delta.EngineHeat, Is.GreaterThanOrEqualTo(0f), $"{room} 이 열을 내린다");
                Assert.That(delta.FuelReserve, Is.LessThanOrEqualTo(0f), $"{room} 이 연료를 채운다");
            }
        }
    }
}
