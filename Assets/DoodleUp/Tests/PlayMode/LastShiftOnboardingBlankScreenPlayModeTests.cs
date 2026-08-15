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

        /// <summary>
        /// 지금 안내가 화면에 있는가.
        ///
        /// <b>캔버스를 안 센다.</b> 예전에는 활성 <c>Text</c> 를 훑었는데, 그 조각은
        /// <c>OnGUI</c> 가 매 프레임 빌려 주는 것이고 <b>배치 모드에는 OnGUI 가 아예 안
        /// 돈다</b> — 그래서 이 검사는 헤드리스에서 온보딩과 무관하게 항상 빨갛고(실측
        /// 10,343 프레임), 정작 컨트롤러 안의 계기는 한 줄도 안 남았다. 그리기 <b>직전</b>의
        /// 판정을 보면 렌더 없이도 같은 것을 재고, 이 픽스처가 비로소 회귀를 잡는다.
        /// </summary>
        private static bool AnythingOnScreen() => LastShiftOnboardingBanner.IsVisible;

        /// <summary>
        /// 안내 줄이 <b>실제로 캔버스에 있는가</b>. 판정과 렌더를 대조하는 쪽이다.
        ///
        /// <b>에디터에서만 뜻이 있다.</b> 배치 모드에는 <c>OnGUI</c> 가 안 돌아 조각이 애초에
        /// 안 빌려지므로 여기는 항상 거짓이다 — 그래서 화면이 있는 판에서만 검사한다
        /// (<see cref="CrossCheckRender"/>).
        /// </summary>
        private static bool CanvasHasGuidance()
        {
            foreach (var text in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
            {
                if (text.name != "Label:tutorialGuide") continue;
                if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 판정은 "떠 있다" 인데 캔버스가 비어 있던 <b>연속 시간</b>의 최댓값.
        ///
        /// <b>프레임 수로 세면 안 된다.</b> 코루틴은 <c>Update</c> 에서 도는데 <c>OnGUI</c> 는
        /// 그보다 뒤라, 줄이 바뀌는 프레임에는 캔버스가 아직 앞 프레임 값(타이핑 0 글자)을
        /// 들고 있다 — 에디터 실측에서 이 한 프레임 지연이 줄 수만큼(10 개) 잡혔고, 그건
        /// 렌더 사고가 아니라 순서다. 진짜 사고(층 없음·임대 만료·OnGUI 미실행)는 계속 어긋난다.
        /// </summary>
        private float worstRenderMismatch;

        private float renderMismatchStreak;

        /// <summary>그 어긋남을 처음 본 자리.</summary>
        private string firstRenderMismatch = string.Empty;

        private void CrossCheckRender(string label, float allowedSeconds)
        {
            if (Application.isBatchMode) return;
            if (!LastShiftOnboardingBanner.IsVisible || CanvasHasGuidance())
            {
                renderMismatchStreak = 0f;
                return;
            }

            renderMismatchStreak += Time.deltaTime;
            if (renderMismatchStreak <= worstRenderMismatch) return;
            worstRenderMismatch = renderMismatchStreak;
            if (worstRenderMismatch > allowedSeconds && firstRenderMismatch.Length == 0)
                firstRenderMismatch = $"[{label}] {LastShiftOnboardingBanner.Describe()}";
        }

        private static string Snapshot() => LastShiftOnboardingBanner.Describe();

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
            //
            // <b>기다리기만 하면 안 끝난다.</b> 마지막 두 줄(<c>AI_W_06</c>·<c>AI_W_07</c>)은
            // 시간이 아니라 <b>플레이어 행동</b>이 미는데, 이 검사는 좌표를 옮겨 놓을 뿐이라
            // 그 둘이 영영 안 온다 — 예전 실행에서 30초를 꽉 채우고도 <c>wake=True</c> 인 채로
            // 진행했고, 그러면 도입부가 띠를 계속 잡아 순회 뒤 구간을 아예 안 재게 된다.
            // 실제 플레이가 내는 신호를 여기서 그대로 낸다.
            var waited = 0f;
            while (LastShiftWakeSequence.IsRunning && waited < 30f)
            {
                LastShiftWakeSequence.NotifyFirstMove();
                LastShiftWakeSequence.NotifyQuartersDoorInRange();
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.That(LastShiftWakeSequence.IsRunning, Is.False,
                $"도입부가 30초 안에 안 닫혔다 — {Snapshot()}");

            var log = new StringBuilder();
            var blankStreak = 0f;
            var worstBlank = 0f;
            var firstBlank = string.Empty;

            // 줄이 막 바뀐 프레임은 타이핑이 0 글자라 정상적으로 비어 있다(조항 N-1 의
            // 찍는 연출). 그래서 <b>프레임 수가 아니라 연속으로 빈 시간</b>으로 잰다 —
            // 런타임 경보(EmptyTextAlarmSeconds 1.5초)보다 빡빡하게 잡아, 한 줄 찍히는
            // 시간(순회 0.83초)을 넘겨 비는 구간만 실패로 본다.
            const float AllowedBlankSeconds = 1.0f;

            void Sample(string label)
            {
                if (AnythingOnScreen())
                {
                    blankStreak = 0f;
                    return;
                }

                blankStreak += Time.deltaTime;
                if (blankStreak <= worstBlank) return;
                worstBlank = blankStreak;
                if (worstBlank > AllowedBlankSeconds && firstBlank.Length == 0)
                    firstBlank = $"[{label}] {Snapshot()}";
            }

            // 판정과 렌더를 <b>같은 프레임에</b> 대조한다(카드 요청). 배치 모드에서는
            // 세지 않는다 — OnGUI 가 안 도는 것이 게임 결함이 아니기 때문이다.
            void SampleBoth(string label)
            {
                Sample(label);
                CrossCheckRender(label, AllowedBlankSeconds);
            }

            IEnumerator Stand(Vector3 target, float seconds, string label)
            {
                Move(player, target);
                var left = seconds;
                while (left > 0f)
                {
                    yield return null;
                    left -= Time.deltaTime;
                    SampleBoth(label);
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

            // <b>안내가 실제로 닫힐 때까지 기다린다.</b> 예전에는 8초만 서 있다가 끝냈는데,
            // 마지막 줄들이 아직 줄 서 있어서(<c>pending</c>) <c>IsComplete</c> 가 거짓인 채로
            // 검사가 끝났다 — 정작 카드가 말하는 "순회 안내 종료 후" 구간을 한 번도 안 쟀다.
            Move(player, Vector3.zero);
            var closing = 0f;
            while (!LastShiftPatrolNarration.IsComplete && closing < 60f)
            {
                yield return null;
                closing += Time.deltaTime;
                SampleBoth("closing");
            }

            Assert.That(LastShiftPatrolNarration.IsComplete, Is.True,
                $"순회 안내가 60초 안에 안 닫혔다 — pending={LastShiftPatrolNarration.PendingCount} " +
                $"roomsLeft={LastShiftPatrolNarration.RoomsLeft} {Snapshot()}");
            log.Append("patrol-closed -> ").Append(Snapshot()).Append('\n');

            // 자리를 놓은 뒤가 사용자가 말한 그 자리다. 안내 띠가 이어받아야 한다.
            yield return Stand(Vector3.zero, 12f, "after-patrol");

            Debug.Log("[BLANK_PROBE]\n" + log);

            Assert.That(worstBlank, Is.LessThanOrEqualTo(AllowedBlankSeconds),
                $"안내가 {worstBlank:F2}초 연속으로 비었다. 처음 빈 자리: {firstBlank}\n{log}");

            // <b>판정은 떴다는데 캔버스가 비어 있던 구간.</b> 에디터에서만 잡히는 축이고,
            // 여기 걸리면 원인은 상태기가 아니라 그리는 쪽(층 없음·임대 만료·OnGUI 미실행)이다.
            Assert.That(worstRenderMismatch, Is.LessThanOrEqualTo(AllowedBlankSeconds),
                $"안내가 상태로는 떠 있는데 캔버스에서는 {worstRenderMismatch:F2}초 연속으로 비었다. " +
                $"처음 어긋난 자리: {firstRenderMismatch}\n{log}");
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

            // <b>가만히 서 있으면 1단계에 남아야 한다.</b> 전이 신호가 "조종석 밖" 이던 동안은
            // 숙소 스폰이 그 조건을 첫 틱에 이미 만족해서, 실측 로그가
            // <c>step=1 ENTER elapsed=0.0</c> · <c>step=2 ENTER elapsed=0.0</c> 두 줄로 찍혔다.
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.SightSalvage),
                $"숙소에서 깨자마자 단계가 넘어갔다 — 1단계 안내가 한 프레임도 못 뜬다. {line}");
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
