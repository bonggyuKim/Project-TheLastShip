using System.Collections;
using System.Text;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 온보딩을 <b>배속으로 통째로 돌려</b> 안내가 끊기는 자리를 스스로 찾는다.
    ///
    /// 앞의 재현 검사들은 승무원을 텔레포트로 옮기고 상태를 손으로 초기화했다. 그 경로로는
    /// 재현이 안 됐고, 그동안 실제 재현은 사람이 반복해야 했다. 여기서는 <b>실제 시작 경로</b>
    /// 그대로(자동 호스트) 열고, <see cref="Time.timeScale"/> 을 올려 몇 분치를 몇 초에 돌린다.
    ///
    /// 잡는 것은 하나다 — <c>Label:tutorialGuide</c> 가 오래 비는 구간. 그 줄에 배너 세 갈래가
    /// 전부 글자를 싣기 때문에, 비어 있으면 그것이 곧 "아무것도 안 뜬다" 다.
    /// </summary>
    public sealed class LastShiftOnboardingSoakPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        private const float Speed = 12f;
        private const float SimulatedSeconds = 240f;
        private const float GapAlarmSeconds = 2f;

        private Keyboard testKeyboard;
        private InputSettings.UpdateMode previousUpdateMode;

        [SetUp]
        public void SetUpInput()
        {
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        }

        [TearDown]
        public void Cleanup()
        {
            InputSystem.settings.updateMode = previousUpdateMode;
            if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
            Time.timeScale = 1f;
            LastShiftWakeSequence.Clear();
            LastShiftTutorial.Clear();
            LastShiftVoyage.Clear();
            LastShiftPatrolNarration.Clear();
            LastShiftNarrationDirector.Clear();
            LastShiftExternalStimulus.Clear();
        }

        private static bool HasGuidance()
        {
            foreach (var label in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
            {
                if (label.name != "Label:tutorialGuide") continue;
                if (label.isActiveAndEnabled && !string.IsNullOrEmpty(label.text)) return true;
            }

            return false;
        }

        private static string State() =>
            $"armed={LastShiftTutorial.IsArmed} step={LastShiftTutorial.Step}" +
            $" tutorialRunning={LastShiftTutorial.IsRunning}" +
            $" patrol={LastShiftPatrolNarration.HasLine}" +
            $" patrolComplete={LastShiftPatrolNarration.IsComplete}" +
            $" director={LastShiftNarrationDirector.HasLine}" +
            $" standing={LastShiftStandingNarration.HasLine}" +
            $" wake={LastShiftWakeSequence.IsRunning}" +
            $" wakeLine={(LastShiftWakeSequence.HasLine ? LastShiftWakeSequence.Current.Id : "-")}" +
            $" gate={LastShiftWakeSequence.Gate}" +
            $" awaitDoor={LastShiftWakeSequence.IsAwaitingQuartersDoor}" +
            $" canMove={LastShiftWakeSequence.CanMove}" +
            $" atPort={LastShiftAirlock.IsAtPort}" +
            $" patrolRunning={LastShiftPatrolNarration.IsRunning}" +
            $" voyage={LastShiftVoyage.IsRunning} resolved={LastShiftVoyage.IsSegmentSettled}";

        /// <summary>
        /// 실제 시작 경로로 열고 배속으로 흘리며, 안내가 <see cref="GapAlarmSeconds"/> 넘게
        /// 끊기는 첫 자리를 잡는다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGuidanceNeverGoesQuietForLong()
        {
            // <b>실제 시작 경로로 연다.</b> 앞서는 AutoStartHost 로 띄웠는데, 그러면 로비가
            // 뜬 채로 아무도 "방 만들기" 를 안 눌러 항해가 영영 안 열린다 — 그 상태를 버그로
            // 잘못 읽었다. 버튼이 부르는 것은 session.OpenRoom(code) 이고 StartHost 가 아니다.
            LastShiftNetworkSession.AutoStartHost = false;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(ScenePath));

            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null, "세션이 씬에 없다");
            Assert.That(session.OpenRoom(LastShiftRoomCode.Generate()), Is.True, "방이 안 열렸다");
            yield return null;
            yield return null;

            Time.timeScale = Speed;

            // <b>승무원을 실제로 움직인다.</b> 가만히 서 있으면 도입부가 안 끝나고 순회도
            // 안 열려서, 사용자가 말한 "순회 안내가 끝나면" 구간에 도달조차 못 한다.
            var player = session.NetworkManager != null
                         && session.NetworkManager.LocalClient?.PlayerObject != null
                ? session.NetworkManager.LocalClient.PlayerObject
                    .GetComponent<LastShiftPlayerController>()
                : null;
            Assert.That(player, Is.Not.Null, "승무원이 안 떴다");

            var route = new System.Collections.Generic.List<Vector3>();
            // 숙소 문 앞 -> 광장 -> 네 방 -> 광장. 방 순서는 순회가 순서 무관이라 아무래도 좋다.
            route.Add(new Vector3(4.8f, 0.2f, 6.5f));
            route.Add(Vector3.zero);
            foreach (var space in new[]
                     {
                         LastShiftPlazaSpace.CockpitRoom,
                         LastShiftPlazaSpace.PowerRoom,
                         LastShiftPlazaSpace.CoolingRoom,
                         LastShiftPlazaSpace.LifeSupportRoom
                     })
            {
                var room = LastShiftPlazaLayout.Of(space);
                route.Add(new Vector3((room.MinX + room.MaxX) * 0.5f, 0.2f,
                    (room.MinZ + room.MaxZ) * 0.5f));
                route.Add(Vector3.zero);
            }

            var report = new StringBuilder();
            var elapsed = 0f;
            var gap = 0f;
            var worstGap = 0f;
            var worstAt = string.Empty;
            var firstGap = string.Empty;

            var leg = 0;
            var legSeconds = 0f;
            const float LegHold = 14f;

            while (elapsed < SimulatedSeconds)
            {
                yield return null;
                var step = Time.unscaledDeltaTime * Speed;
                elapsed += step;

                // 경로를 차례로 밟는다. 도입부가 도는 동안은 제자리에 둔다 - 그때는 조작이
                // 잠긴 구간이고, 문 사거리 신호만 있으면 알아서 진행한다.
                // <b>텔레포트는 이동 입력이 아니다.</b> 도입부는 첫 이동 입력
                // (LastShiftWakeSequence.NotifyFirstMove)을 기다리는데, 자리를 옮기는 것만으로는
                // 그 신호가 안 온다 - 그래서 도입부가 안 끝나고 기상 문구가 계속 떠 있었다.
                if (LastShiftWakeSequence.IsRunning && LastShiftWakeSequence.CanMove)
                {
                    InputSystem.QueueStateEvent(testKeyboard,
                        new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W));
                    InputSystem.Update();
                }
                else
                {
                    InputSystem.QueueStateEvent(testKeyboard,
                        new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    InputSystem.Update();
                }

                // 마지막 도입부 줄은 <b>숙소 문 사거리</b>를 기다린다. 키만 눌러서는 안 오므로
                // 문 앞으로 붙여 그 신호를 채운다 - 사람이 실제로 걸어가는 그 자리다.
                if (LastShiftWakeSequence.IsAwaitingQuartersDoor) Move(player, route[0]);

                if (!LastShiftWakeSequence.IsRunning && leg < route.Count)
                {
                    legSeconds += step;
                    Move(player, route[leg]);
                    if (legSeconds >= LegHold) { legSeconds = 0f; leg++; }
                }
                else if (LastShiftWakeSequence.IsRunning)
                {
                    // 숙소 문 앞으로 붙여 도입부의 문 사거리 신호를 채운다.
                    Move(player, route[0]);
                }

                if (HasGuidance())
                {
                    gap = 0f;
                    continue;
                }

                gap += Time.unscaledDeltaTime * Speed;
                if (gap <= worstGap) continue;
                worstGap = gap;
                worstAt = $"t={elapsed:F1}s gap={gap:F1}s {State()}";
                if (gap >= GapAlarmSeconds && firstGap.Length == 0) firstGap = worstAt;
            }

            report.Append("soak ").Append(SimulatedSeconds).Append("s @x").Append(Speed)
                  .Append(" 최장 무안내 구간=").Append(worstGap.ToString("F1")).Append("s\n")
                  .Append("그 지점: ").Append(worstAt).Append('\n')
                  .Append("끝 상태: ").Append(State()).Append('\n');
            Debug.Log("[SOAK]\n" + report);
            System.IO.File.WriteAllText("Temp/soak.txt", report.ToString());

            report.Append("route=").Append(leg).Append("/").Append(route.Count);

            Assert.That(worstGap, Is.LessThan(GapAlarmSeconds),
                $"안내가 {worstGap:F1}초 동안 끊겼다.\n{firstGap}\n{report}");
        }

        /// <summary>승무원을 그 자리로 옮긴다. 컨트롤러를 껐다 켜는 것이 프로젝트 규약이다.</summary>
        private static void Move(LastShiftPlayerController player, Vector3 target)
        {
            if (player == null) return;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = target;
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
