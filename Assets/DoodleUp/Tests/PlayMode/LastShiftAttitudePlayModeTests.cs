using System.Collections;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 선체 자세 연출을 <b>실제 씬에서</b> 돌린다.
    ///
    /// EditMode 로는 못 잡는 것 하나만 잡는다 — <b>누가 이 채널을 켜는가</b>. 사상과 합성은
    /// <c>LastShiftAttitudeFeedbackTests</c> 가 이미 재고 있고, 그게 전부 초록이어도 씬에
    /// 컴포넌트가 안 붙으면 자세는 다시 F3 문자열로만 존재한다 — 이 카드가 정확히 그
    /// 상태였다.
    ///
    /// 이미 구워진 씬에는 이 컴포넌트가 없다. 붙이는 것은
    /// <c>LastShiftSandboxController.EnsureAttitudeFeedback</c> 이고, 여기서 재는 것이 그
    /// 경로다 — 씬을 다시 굽지 않아도 화면이 맞아야 한다.
    /// </summary>
    public sealed class LastShiftAttitudePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        private LastShiftSandboxController sandbox;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftWakeSequence.Clear();
            LastShiftTutorial.Clear();
            LastShiftVoyage.Clear();
            LastShiftExternalStimulus.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            sandbox = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true))
                .FirstOrDefault();
            Assert.That(sandbox, Is.Not.Null, "sandbox missing from the scene");
        }

        [TearDown]
        public void Cleanup()
        {
            LastShiftWakeSequence.Clear();
            LastShiftTutorial.Clear();
            LastShiftVoyage.Clear();
            LastShiftExternalStimulus.Clear();
        }

        /// <summary>
        /// 씬에 자세 채널이 서고, 자세를 밀면 롤이 따라온다. 자세 값만 확인하는 검사는
        /// 이 부채를 못 잡는다 — 값은 이미 있었다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheShipVisiblyRollsWithAttitude()
        {
            var feedback = Object.FindFirstObjectByType<LastShiftAttitudeFeedback>(FindObjectsInactive.Include);
            Assert.That(feedback, Is.Not.Null,
                "씬에 자세 연출 채널이 없다 — 자세가 다시 F3 문자열로만 존재한다");

            // 수평에서 시작해 롤이 0 으로 잦아드는 것을 먼저 확인한다.
            sandbox.ApplyControl(sandbox.CurrentState.ThrustDemand, 0f);
            yield return Settle();
            Assume.That(Mathf.Abs(feedback.RollDegrees), Is.LessThan(0.5f),
                $"수평인데 롤이 남아 있다 — roll={feedback.RollDegrees:F2}");

            // AttitudeDrift 발동값까지 기울인다.
            sandbox.ApplyControl(sandbox.CurrentState.ThrustDemand,
                LastShiftSituationTable.AttitudeTriggerDegrees);
            Assume.That(sandbox.CurrentState.ShipAttitudeDegrees,
                Is.EqualTo(LastShiftSituationTable.AttitudeTriggerDegrees).Within(0.01f),
                "조종 입력이 자세에 안 들어갔다 — 조향 지연 경로를 확인해야 한다");

            yield return Settle();

            Assert.That(feedback.Band, Is.EqualTo(LastShiftAttitudeBand.Critical));
            var expected = LastShiftAttitudeFeedback.SteadyRollOf(
                LastShiftSituationTable.AttitudeTriggerDegrees);
            Assert.That(feedback.RollDegrees, Is.EqualTo(expected)
                .Within(LastShiftAttitudeFeedback.MaxSwayDegrees + 0.2f),
                "자세가 발동값인데 롤이 안 붙었다");

            // 화면까지 도달했는가. 호스트를 안 띄운 씬에는 승무원이 없으므로 프리팹으로
            // 하나 세운다 — 자동 재탐색(PlayerRescanSeconds)이 도중에 들어온 승무원을
            // 집어 오는가까지 같이 재는 자리다.
            var member = SpawnCrewFromPrefab();
            yield return Settle();

            Assert.That(member.CameraAttitudeOffset.z, Is.EqualTo(feedback.RollDegrees).Within(0.01f),
                $"{member.name} 카메라에 롤이 안 얹혔다");
            Assert.That(member.TargetCamera, Is.Not.Null, "승무원 프리팹에 카메라가 없다");
            Assert.That(Mathf.DeltaAngle(0f, member.TargetCamera.transform.localEulerAngles.z),
                Is.EqualTo(feedback.RollDegrees).Within(0.05f),
                $"{member.name} 카메라 transform 이 안 돌았다");

            Debug.Log($"[LAST_SHIFT_ATTITUDE_EVIDENCE] attitude={sandbox.CurrentState.ShipAttitudeDegrees:F1} " +
                      $"band={feedback.Band} roll={feedback.RollDegrees:F2} " +
                      $"cameraRollZ={Mathf.DeltaAngle(0f, member.TargetCamera.transform.localEulerAngles.z):F2}");
        }

        /// <summary>
        /// 세션이 들고 있는 플레이어 프리팹으로 승무원 하나를 세운다. 경로를 테스트가 따로
        /// 적으면 빌더가 프리팹을 옮겼을 때 여기만 조용히 뒤처진다.
        /// </summary>
        private static LastShiftPlayerController SpawnCrewFromPrefab()
        {
            var session = Object.FindAnyObjectByType<LastShiftNetworkSession>();
            Assert.That(session, Is.Not.Null, "network session missing from the scene");
            var prefab = session.PlayerPrefab;
            Assert.That(prefab, Is.Not.Null, "session is not wired to a player prefab");
            var crew = Object.Instantiate(prefab.gameObject);
            crew.name = "PlayerOne";
            var controller = crew.GetComponent<LastShiftPlayerController>();
            Assert.That(controller, Is.Not.Null, "player prefab must carry LastShiftPlayerController");
            controller.transform.position = LastShiftShipDimensions.SpawnPoint;
            return controller;
        }

        /// <summary>
        /// 껐던 자동 호스트를 되돌린다. 정적 값이라 안 되돌리면 같은 Play 세션의 네트워크
        /// 테스트가 host 없이 돌아 원인을 알 수 없는 실패가 된다.
        /// </summary>
        [OneTimeTearDown]
        public void RestoreAutoHost()
        {
            LastShiftNetworkSession.AutoStartHost = true;
        }

        /// <summary>
        /// 롤이 목표에 닿을 만큼 <b>시간</b>을 흘린다. 프레임 수로 세면 안 된다 — 헤드리스
        /// 배치는 프레임을 가능한 한 빨리 돌려서 120 프레임이 0.1초도 안 되고, 지수 추종은
        /// 프레임이 아니라 경과 시간에 붙는다. 실제로 이 차이로 롤이 목표의 1/5 에서 멈췄다.
        /// 추종 상수 4/초에서 2.5초면 목표의 99.99% 다.
        /// </summary>
        private static IEnumerator Settle()
        {
            var waited = 0f;
            while (waited < 2.5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
    }
}
