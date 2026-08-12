using System.Collections;
using System.Linq;
using System.Text;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 온보딩 중 <b>화면이 완전히 비는 프레임이 있는가</b>를 프레임 단위로 잰다(PM 요청).
    ///
    /// <b>상태값과 실제 렌더를 같이 본다.</b> 지금까지 두 번은 상태만 보고 고쳤는데 사용자가
    /// 또 재현했다 — 정적 분석으로는 <c>Tutorial.IsRunning</c> 이 참이면 배너 분기가 무조건
    /// 그리게 되어 있어서 빈 화면이 나올 자리가 안 보인다. 그러면 남은 가능성은 그 전제가
    /// 실제로는 안 맞는 것이고, 그건 재 봐야 안다.
    ///
    /// 매 프레임 다섯 값과 <b>실제로 캔버스에 글자가 있는지</b>를 같이 적고, 하나라도 빈
    /// 프레임이 나오면 그 시점의 값을 그대로 실패 메시지에 싣는다.
    /// </summary>
    public sealed class LastShiftOnboardingBlankScreenPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        private const ushort BlankProbePort = 7794;

        private LastShiftNetworkSession session;

        [TearDown]
        public void Cleanup()
        {
            if (session != null) session.StopSession();
            session = null;
            LastShiftWakeSequence.Clear();
            LastShiftTutorial.Clear();
            LastShiftVoyage.Clear();
            LastShiftPatrolNarration.Clear();
            LastShiftNarrationDirector.Clear();
            LastShiftExternalStimulus.Clear();
        }

        /// <summary>지금 캔버스에 실제로 글자가 하나라도 그려져 있는가.</summary>
        private static bool AnythingOnScreen()
        {
            foreach (var text in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
            {
                if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) continue;
                // 조준점은 "안내" 가 아니다 — 그것만 남은 화면이 곧 사용자가 말한 빈 화면이다.
                if (text.name == "Label:crosshair") continue;
                return true;
            }

            return false;
        }

        private static string Snapshot() =>
            $"patrol={LastShiftPatrolNarration.HasLine} " +
            $"director={LastShiftNarrationDirector.HasLine} " +
            $"tutorialRunning={LastShiftTutorial.IsRunning} " +
            $"step={LastShiftTutorial.Step} " +
            $"wake={LastShiftWakeSequence.IsRunning} " +
            $"standing={LastShiftStandingNarration.HasLine} " +
            $"armed={LastShiftTutorial.IsArmed}";

        /// <summary>
        /// 방을 만들고 순회를 끝까지 돌린 뒤, <b>빈 프레임이 하나라도 있었는지</b> 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator NothingGoesBlankAfterThePatrolFinishes()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftVoyage.Clear();
            LastShiftTutorial.Clear();
            LastShiftWakeSequence.Clear();
            LastShiftPatrolNarration.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(ScenePath));
            session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null);
            session.OverridePort(BlankProbePort);
            Assert.That(session.StartHost(), Is.True, "호스트가 안 섰다");
            yield return null;
            yield return null;

            var player = session.NetworkManager.LocalClient.PlayerObject
                .GetComponent<LastShiftPlayerController>();
            Assert.That(player, Is.Not.Null, "승무원이 안 떴다");

            // 도입부는 조작이 잠긴 구간이라 끝날 때까지 기다린다.
            var waited = 0f;
            while (LastShiftWakeSequence.IsRunning && waited < 30f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            var log = new StringBuilder();
            var blankFrames = 0;
            var firstBlank = string.Empty;

            IEnumerator Stand(Vector3 target, float seconds, string label)
            {
                Move(player, target);
                var left = seconds;
                while (left > 0f)
                {
                    yield return null;
                    left -= Time.deltaTime;
                    if (AnythingOnScreen()) continue;
                    blankFrames++;
                    if (firstBlank.Length == 0) firstBlank = $"[{label}] {Snapshot()}";
                }

                log.Append(label).Append(" -> ").Append(Snapshot())
                   .Append(" onScreen=").Append(AnythingOnScreen()).Append('\n');
            }

            // 광장으로 나와 순회를 연다.
            yield return Stand(Vector3.zero, 1.5f, "plaza");

            foreach (var space in new[]
                     {
                         LastShiftPlazaSpace.CockpitRoom,
                         LastShiftPlazaSpace.PowerRoom,
                         LastShiftPlazaSpace.CoolingRoom,
                         LastShiftPlazaSpace.LifeSupportRoom
                     })
            {
                var room = LastShiftPlazaLayout.Of(space);
                var centre = new Vector3((room.MinX + room.MaxX) * 0.5f, 0.2f,
                    (room.MinZ + room.MaxZ) * 0.5f);
                yield return Stand(centre, 2.5f, space.ToString());
                yield return Stand(Vector3.zero, 1.0f, "plaza-after-" + space);
            }

            // 마지막 줄이 흐르고 자리를 놓는 구간. 여기가 사용자가 말한 빈 화면 자리다.
            yield return Stand(Vector3.zero, 8f, "after-patrol");

            Debug.Log("[BLANK_PROBE]\n" + log);

            Assert.That(blankFrames, Is.Zero,
                $"화면이 완전히 빈 프레임이 {blankFrames} 개 있었다. 처음 빈 자리: {firstBlank}\n{log}");
        }

        /// <summary>
        /// <b>숙소 스폰이 1단계를 즉시 넘겨 버리는가</b>(PM 가설). 넘어가더라도 단계가
        /// <c>None</c> 으로 안 돌아가면 배너는 남는다 — 그 사실을 값으로 적어 둔다.
        /// </summary>
        [UnityTest]
        public IEnumerator SpawningInQuartersDoesNotEmptyTheTutorialStep()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftVoyage.Clear();
            LastShiftTutorial.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(ScenePath));
            session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            session.OverridePort((ushort)(BlankProbePort + 1));
            Assert.That(session.StartHost(), Is.True);
            yield return null;
            yield return null;

            var opened = LastShiftTutorial.Step;
            for (var frame = 0; frame < 120; frame++) yield return null;

            var line = $"스폰 직후 단계={opened} 120프레임 뒤={LastShiftTutorial.Step} {Snapshot()}";
            Debug.Log("[BLANK_PROBE] " + line);
            System.IO.File.WriteAllText("Temp/blank_probe_spawn.txt", line);

            Assert.That(LastShiftTutorial.Step, Is.Not.EqualTo(LastShiftTutorialStep.None),
                "단계가 None 으로 돌아갔다 — 그러면 배너 분기 3번이 통째로 안 그려진다");
            Assert.That(LastShiftTutorial.IsRunning, Is.True,
                "튜토리얼이 멈췄다 — 안내 배너가 사라진다");
        }

        /// <summary>승무원을 그 자리로 옮긴다. 컨트롤러를 껐다 켜는 것이 프로젝트 규약이다.</summary>
        private static void Move(LastShiftPlayerController player, Vector3 target)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = target;
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
