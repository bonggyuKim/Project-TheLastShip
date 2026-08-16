using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 선체 자세 관측 채널. 재는 것은 셋이다 — (1) 자세→롤 사상이 판정 임계와 같은 눈금을
    /// 쓰는가, (2) 흔들림이 정상 항해까지 번지지 않는가, (3) 롤이 조준·충격 흔들림을 지우지
    /// 않고 카메라에 실제로 얹히는가.
    ///
    /// 마지막 하나가 이 카드의 본체다. 자세 값은 이미 있었고 화면에 없던 것이 문제였으므로,
    /// "값이 맞다" 로는 같은 부채가 다시 성립한다.
    /// </summary>
    public sealed class LastShiftAttitudeFeedbackTests
    {
        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void SteadyRollOpposesAttitudeAndSaturatesAtClamp()
        {
            // 실내가 기우는 것처럼 보이려면 카메라는 자세와 반대로 돈다.
            Assert.That(LastShiftAttitudeFeedback.SteadyRollOf(60f), Is.EqualTo(-12f).Within(0.001f));
            Assert.That(LastShiftAttitudeFeedback.SteadyRollOf(-60f), Is.EqualTo(12f).Within(0.001f));
            Assert.That(LastShiftAttitudeFeedback.SteadyRollOf(0f), Is.EqualTo(0f).Within(0.001f));

            // 자세는 ±90 으로 잘리므로 롤도 상한에서 멈춘다.
            Assert.That(LastShiftAttitudeFeedback.SteadyRollOf(90f),
                Is.EqualTo(-LastShiftAttitudeFeedback.MaxCameraRollDegrees).Within(0.001f));
            Assert.That(LastShiftAttitudeFeedback.SteadyRollOf(200f),
                Is.EqualTo(-LastShiftAttitudeFeedback.MaxCameraRollDegrees).Within(0.001f));
        }

        [Test]
        public void BandBoundariesMatchAttitudeDriftThresholds()
        {
            Assert.That(LastShiftAttitudeFeedback.BandOf(0f), Is.EqualTo(LastShiftAttitudeBand.Level));
            Assert.That(LastShiftAttitudeFeedback.BandOf(44.9f), Is.EqualTo(LastShiftAttitudeBand.Level));
            Assert.That(LastShiftAttitudeFeedback.BandOf(LastShiftSituationTable.AttitudeReleaseDegrees),
                Is.EqualTo(LastShiftAttitudeBand.Listing));
            Assert.That(LastShiftAttitudeFeedback.BandOf(59.9f), Is.EqualTo(LastShiftAttitudeBand.Listing));
            Assert.That(LastShiftAttitudeFeedback.BandOf(LastShiftSituationTable.AttitudeTriggerDegrees),
                Is.EqualTo(LastShiftAttitudeBand.Critical));
            // 부호는 밴드를 안 가른다 — 좌현으로 기운 것도 같은 사고다.
            Assert.That(LastShiftAttitudeFeedback.BandOf(-72f), Is.EqualTo(LastShiftAttitudeBand.Critical));
        }

        [Test]
        public void SwayStaysSilentOnNominalAttitude()
        {
            // 정상 항해 프리셋의 자세(8°·12°)에서 흔들리면 흔들림이 정보를 잃는다.
            for (var seconds = 0f; seconds < 4f; seconds += 0.25f)
            {
                Assert.That(LastShiftAttitudeFeedback.SwayOf(8f, seconds), Is.EqualTo(0f).Within(1e-5f));
                Assert.That(LastShiftAttitudeFeedback.SwayOf(12f, seconds), Is.EqualTo(0f).Within(1e-5f));
                Assert.That(LastShiftAttitudeFeedback.SwayOf(
                    LastShiftSituationTable.AttitudeReleaseDegrees, seconds), Is.EqualTo(0f).Within(1e-5f));
            }
        }

        [Test]
        public void SwayAppearsPastReleaseThresholdAndIsDeterministic()
        {
            var peak = 0f;
            for (var seconds = 0f; seconds < 8f; seconds += 0.05f)
                peak = Mathf.Max(peak, Mathf.Abs(LastShiftAttitudeFeedback.SwayOf(72f, seconds)));

            Assert.That(peak, Is.GreaterThan(0.3f), "자세 72°에서 흔들림이 안 보인다");
            Assert.That(peak, Is.LessThanOrEqualTo(LastShiftAttitudeFeedback.MaxSwayDegrees + 1e-3f));

            // 시간의 순수 함수라 피어마다 같은 궤적을 만든다.
            Assert.That(LastShiftAttitudeFeedback.SwayOf(72f, 1.37f),
                Is.EqualTo(LastShiftAttitudeFeedback.SwayOf(72f, 1.37f)).Within(1e-6f));
        }

        [Test]
        public void TickConvergesToSteadyRollAndReturnsToLevel()
        {
            var feedback = NewFeedback();

            for (var frame = 0; frame < 240; frame++) feedback.Tick(72f, 1f / 60f);
            Assert.That(feedback.SteadyRollDegrees,
                Is.EqualTo(LastShiftAttitudeFeedback.SteadyRollOf(72f)).Within(0.05f));
            Assert.That(feedback.Band, Is.EqualTo(LastShiftAttitudeBand.Critical));

            // 자세가 잡히면 화면도 돌아온다 — 상시 채널이라 0 도 이 경로로 온다.
            for (var frame = 0; frame < 240; frame++) feedback.Tick(0f, 1f / 60f);
            Assert.That(feedback.RollDegrees, Is.EqualTo(0f).Within(0.05f));
            Assert.That(feedback.Band, Is.EqualTo(LastShiftAttitudeBand.Level));
        }

        [Test]
        public void RollReachesCrewCameraWithoutErasingAimOrShake()
        {
            var crew = NewCrew(out var camera);
            var feedback = NewFeedback();

            crew.SetAimPitchForProbe(20f);
            crew.SetCameraShakeOffset(new Vector3(1.5f, -0.5f, 0.75f));

            for (var frame = 0; frame < 240; frame++) feedback.Tick(72f, 1f / 60f);

            // 밀어 넣는 값은 흔들림까지 합친 RollDegrees 다. 정상 롤과 비교하면 흔들림 진폭
            // 만큼 어긋나므로, 전달 여부는 RollDegrees 와 대조해서 잰다.
            var pushedRoll = feedback.RollDegrees;
            Assert.That(crew.CameraAttitudeOffset.z, Is.EqualTo(pushedRoll).Within(0.001f),
                "자세 롤이 승무원 카메라에 전달되지 않았다");
            Assert.That(pushedRoll, Is.EqualTo(LastShiftAttitudeFeedback.SteadyRollOf(72f))
                .Within(LastShiftAttitudeFeedback.MaxSwayDegrees + 0.05f));

            // 실제로 카메라가 돌아야 한다. 조준(-pitch)과 충격 흔들림은 그대로 남는다.
            var euler = camera.transform.localEulerAngles;
            Assert.That(Mathf.DeltaAngle(0f, euler.x), Is.EqualTo(-20f + 1.5f).Within(0.05f));
            Assert.That(Mathf.DeltaAngle(0f, euler.y), Is.EqualTo(-0.5f).Within(0.05f));
            Assert.That(Mathf.DeltaAngle(0f, euler.z), Is.EqualTo(0.75f + pushedRoll).Within(0.05f));
        }

        private LastShiftAttitudeFeedback NewFeedback()
        {
            var go = new GameObject("AttitudeFeedback");
            spawned.Add(go);
            return go.AddComponent<LastShiftAttitudeFeedback>();
        }

        private LastShiftPlayerController NewCrew(out Camera camera)
        {
            var go = new GameObject("Crew");
            spawned.Add(go);
            go.AddComponent<CharacterController>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(go.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            var crew = go.AddComponent<LastShiftPlayerController>();
            crew.Configure(camera, null);
            return crew;
        }
    }
}
