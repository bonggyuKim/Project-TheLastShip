using System.Collections;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// <b>호스트로 시작한 판도 항해가 열려 있는가.</b>
    ///
    /// 비네트워크 <c>Start()</c> 는 <c>BeginVoyage(); ResetPreset(CurrentPreset)</c> 순서로
    /// 부르는데, 그 <c>Start()</c> 는 <c>NetworkObject</c> 가 붙어 있으면 첫 줄에서 반환한다.
    /// 네트워크 경로는 <c>LastShiftNetworkSandbox.OnNetworkSpawn</c> 으로 넘어가는데 거기에
    /// <c>BeginVoyage</c> 호출이 없었다.
    ///
    /// <b>그 한 줄이 빠지면 튜토리얼이 통째로 죽는다.</b> <c>armed</c> 가 안 서고, 그러면
    /// <c>LastShiftTutorial.ArriveAtPort</c> 가 매번 조기 반환한다 — 그 안에 기상 도입부·
    /// 내레이션 디렉터·순회·상시 라인이 전부 들어 있다. 사용자가 방을 만들어 재현한
    /// "프롤로그가 영영 안 뜬다" 가 그 상태다.
    ///
    /// EditMode 로는 못 잡는다. 갈라지는 자리가 <c>NetworkObject</c> 유무이고, 그건 실제로
    /// spawn 이 돌아야 생긴다.
    /// </summary>
    public sealed class LastShiftNetworkVoyageStartPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        private const ushort VoyageStartPort = 7791;

        private LastShiftNetworkSession session;

        [TearDown]
        public void Cleanup()
        {
            if (session != null) session.StopSession();
            session = null;
            LastShiftWakeSequence.Clear();
            LastShiftTutorial.Clear();
            LastShiftVoyage.Clear();
            LastShiftExternalStimulus.Clear();
        }

        /// <summary>
        /// 호스트가 서면 <b>항해가 열리고 튜토리얼이 무장한다</b>. 이 둘이 이 버그의 전부다.
        /// </summary>
        [UnityTest]
        public IEnumerator StartingAsHostOpensTheVoyageAndArmsTheTutorial()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftVoyage.Clear();
            LastShiftTutorial.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(ScenePath));
            session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            var sandbox = Object.FindFirstObjectByType<LastShiftSandboxController>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null, "세션이 씬에 없다");
            Assert.That(sandbox, Is.Not.Null, "sandbox 가 씬에 없다");

            session.OverridePort(VoyageStartPort);
            Assert.That(session.StartHost(), Is.True, "호스트가 안 섰다");
            yield return null;
            yield return null;

            Assert.That(LastShiftVoyage.IsRunning, Is.True,
                "호스트로 시작했는데 항해가 안 열렸다 — OnNetworkSpawn 에 BeginVoyage 가 없다");
            Assert.That(LastShiftVoyage.SegmentIndex, Is.EqualTo(LastShiftVoyage.FirstSegment),
                "구간이 1 에서 시작하지 않는다");
            Assert.That(LastShiftTutorial.IsArmed, Is.True,
                "튜토리얼이 무장하지 않았다 — 이 상태면 ArriveAtPort 가 매번 조기 반환해서 " +
                "프롤로그·내레이션·순회가 전부 안 뜬다");
            Assert.That(sandbox.ResetGeneration, Is.GreaterThan(0), "프리셋이 안 잡혔다");
        }

        /// <summary>
        /// 무장했으므로 <b>기항에 들어가면 프롤로그가 실제로 열린다</b>. 앞 검사는 조건을
        /// 재고, 이 검사는 그 조건이 실제로 화면 연출까지 이어지는지를 잰다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHostSessionActuallyReachesThePrologue()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftVoyage.Clear();
            LastShiftTutorial.Clear();
            LastShiftWakeSequence.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(ScenePath));
            session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            session.OverridePort((ushort)(VoyageStartPort + 1));
            Assert.That(session.StartHost(), Is.True);
            yield return null;
            yield return null;

            Assume.That(LastShiftTutorial.IsArmed, Is.True, "무장 자체가 안 됐다");

            // <b>SettleSegment 를 손으로 안 부른다.</b> 예전 검사는 여기서 구간 판정을 강제해
            // 프롤로그를 열었는데, 그 강제가 정확히 게임에는 없는 단계라 "검사는 초록인데
            // 방을 만들면 아무 연출도 안 나오는" 상태를 못 잡았다. 방을 만든 그 순간에
            // 이미 돌고 있어야 한다.
            Assert.That(LastShiftWakeSequence.IsRunning, Is.True,
                "호스트로 방을 만들었는데 프롤로그가 안 돈다 — 새 항해가 이미 출항한 상태로 열렸다");
            Assert.That(LastShiftTutorial.Step, Is.Not.EqualTo(LastShiftTutorialStep.None),
                "튜토리얼 단계가 안 열렸다");
        }
    }
}
