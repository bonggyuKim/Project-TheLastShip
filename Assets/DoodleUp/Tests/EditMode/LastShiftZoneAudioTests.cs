using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CT-10 T3 = <c>A2</c> 구역별 사운드 감쇠. 고정하는 성질은 넷이다.
    ///   1. 씬에 귀가 있다. <b>AudioListener 가 없으면 2D 든 3D 든 아무 소리도 안 난다</b> —
    ///      A2 가 0 인 1차 원인이 spatialBlend 가 아니라 이것이었다.
    ///   2. 활성 리스너는 하나다. 여럿이면 3D 감쇠가 남의 자리 기준으로 계산된다.
    ///   3. 국소 소리는 구역을 가린다 — 호흡은 방을 못 넘고, 충격은 배 끝까지 간다.
    ///   4. 전선 사이렌은 2D 예외로 남는다.
    ///
    /// <b>커브 모양은 고정하지 않는다.</b> 볼륨·피치는 표현 요건이라 아트 소관이고, 여기서
    /// 재는 것은 "어디까지 들리는가" 라는 기하 성질뿐이다.
    /// </summary>
    public sealed class LastShiftZoneAudioTests
    {
        [Test]
        public void ShipWideSirenStaysTheOnlyTwoDimensionalChannel()
        {
            // 값이 아니라 <b>이름</b>이 고정 대상이다. 리터럴 0f 는 3D 일괄 전환에서 지워지지만
            // 이 상수는 지우려면 왜 2D 인지를 읽게 만든다. 0 이 곧 N9 의 구현이다.
            Assert.That(LastShiftZoneAudio.ShipWideSpatialBlend, Is.EqualTo(0f),
                "사이렌이 3D 가 되면 먼 구역에서 감쇠되어 소거법의 전제('울렸다')가 사라지고 165 회피가 깨진다.");
            Assert.That(LastShiftZoneAudio.LocalSpatialBlend, Is.EqualTo(1f));
        }

        [Test]
        public void OwnBodySoundsDoNotAttenuateByTheCrewsOwnHeight()
        {
            // 음원은 승무원 루트에, 리스너는 눈높이에 있다. MinDistance 가 눈높이보다 작으면
            // 자기 호흡음이 자기 키 때문에 줄어든다 — 값은 그럴듯하고 원인은 안 보인다.
            Assert.That(LastShiftZoneAudio.MinDistance,
                Is.GreaterThanOrEqualTo(LastShiftShipPhysics.EyeHeight),
                "MinDistance 가 눈높이를 덮지 않으면 자기 몸에서 나는 소리가 감쇠된다.");
        }

        [Test]
        public void BreathIsARoomCueAndImpactIsAShipCue()
        {
            // 호흡: 같은 방 안에서는 들리고, 통로를 건너면 안 들린다. 통로 건너까지 들리면
            // "근처에 누가 있다" 가 "누군가 살아 있다" 가 되어 위치 정보가 사라진다.
            Assert.That(LastShiftZoneAudio.BreathMaxDistance,
                Is.GreaterThanOrEqualTo(LastShiftShipDimensions.EndRoomLength),
                "호흡이 방을 못 덮으면 같은 방 안에서도 안 들린다.");
            Assert.That(LastShiftZoneAudio.BreathMaxDistance,
                Is.LessThan(LastShiftShipDimensions.EndRoomLength + LastShiftShipDimensions.PassageLength),
                "호흡이 통로를 건너면 통로의 청각 이점이 방 소리에 묻힌다.");

            // 충격: 상황의 시작이라 못 듣는 사람이 있으면 안 된다. 선내 최장 거리를 덮어야 한다.
            Assert.That(LastShiftZoneAudio.ImpactMaxDistance,
                Is.GreaterThanOrEqualTo(LastShiftShipDimensions.InteriorLength),
                "충격음이 배 끝까지 안 가면 반대편 승무원은 무슨 일이 났는지 모른다.");
        }

        [Test]
        public void LinearRolloffSilencesBreathAcrossAPassage()
        {
            // 선형 감쇠를 쓰는 이유가 여기 있다. 로그 감쇠는 최대 거리에서 0 에 도달하지 않아
            // "선내 어디서든 들린다" 와 구분이 안 되고, 그러면 A2 를 값으로 못 잰다.
            var source = NewSource(out var host);
            try
            {
                LastShiftZoneAudio.ConfigureLocal(source, LastShiftZoneAudio.BreathMaxDistance);
                Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
                Assert.That(source.spatialBlend, Is.EqualTo(LastShiftZoneAudio.LocalSpatialBlend));
                Assert.That(source.dopplerLevel, Is.EqualTo(0f),
                    "이동 4m/s 에 걸린 호흡 루프는 도플러가 켜져 있으면 음정이 흔들리는 잡음이 된다.");

                // 엔진실 방 중앙에서 통로 A 반대쪽 끝까지의 거리는 감쇠 범위 밖이다.
                var listener = new Vector3(LastShiftShipDimensions.UtilityCenterX, 0f, 0f);
                var farEnd = new Vector3(LastShiftShipDimensions.PassageMinX(0), 0f,
                    LastShiftShipDimensions.PassageCenterZ(0));
                Assert.That(Vector3.Distance(listener, farEnd),
                    Is.GreaterThan(LastShiftZoneAudio.BreathMaxDistance),
                    "통로 반대쪽 끝의 호흡이 엔진실 방에서 들리면 방향 판단이 소리로 안 갈린다.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SoloCrewGetsExactlyOneActiveEar()
        {
            // 리스너를 런타임에 만드는 경로를 그대로 태운다. 프리팹에 직렬화하지 않는 것은
            // 의도된 선택이다 — SP-02A 프리팹의 fileID 를 다시 흔들지 않기 위해서다.
            var host = new GameObject("Crew");
            try
            {
                var cameraObject = new GameObject("Player Camera");
                cameraObject.transform.SetParent(host.transform, false);
                var camera = cameraObject.AddComponent<Camera>();

                Assert.That(cameraObject.GetComponent<AudioListener>(), Is.Null,
                    "씬 빌더가 만든 카메라에는 리스너가 딸려 오지 않는다. 이것이 A2 가 0 이던 1차 원인이다.");

                LastShiftZoneAudio.EnsureListener(camera, true);
                var listener = cameraObject.GetComponent<AudioListener>();
                Assert.That(listener, Is.Not.Null);
                Assert.That(listener.enabled, Is.True);

                // 원격 승무원의 귀는 붙되 꺼진다. 두 번 불러도 컴포넌트가 늘지 않아야 한다.
                LastShiftZoneAudio.EnsureListener(camera, false);
                Assert.That(cameraObject.GetComponents<AudioListener>().Length, Is.EqualTo(1),
                    "리스너가 둘이면 어느 귀가 쓰이는지를 스폰 순서가 정한다.");
                Assert.That(cameraObject.GetComponent<AudioListener>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static AudioSource NewSource(out GameObject host)
        {
            host = new GameObject("Audio Host");
            return host.AddComponent<AudioSource>();
        }
    }
}
