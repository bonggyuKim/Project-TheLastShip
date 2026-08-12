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
    /// 기상 도입부를 <b>실제 씬에서</b> 돌린다.
    ///
    /// <b>EditMode 로는 못 잡는 것만 여기서 잡는다.</b> 해금 시각과 암전 곡선은
    /// <c>LastShiftWakeSequenceTests</c> 가 시계를 직접 돌려 재고 있다. 여기서 재는 것은
    /// <b>누가 그 시계를 돌리는가</b> 와 <b>암전이 실제로 캔버스에 걸리는가</b> 둘이다 —
    /// 같은 자리에서 <c>LastShiftEvaLift.Tick</c> 이 아무 데서도 안 불리고 있었고, EditMode 는
    /// 자기가 직접 돌려서 전부 초록이었다.
    /// </summary>
    public sealed class LastShiftWakeSequencePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftWakeSequence.Clear();
            LastShiftTutorial.Clear();
            LastShiftVoyage.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Assert.That(roots.SelectMany(root =>
                    root.GetComponentsInChildren<LastShiftSandboxController>(true)).Any(),
                Is.True, "sandbox missing from the scene");
        }

        [TearDown]
        public void Cleanup()
        {
            LastShiftWakeSequence.Clear();
            LastShiftTutorial.Clear();
            LastShiftVoyage.Clear();
        }

        /// <summary>
        /// 기항 도착까지 민다. <b>튜토리얼 쪽을 직접 부르지 않는다</b> — 무장은
        /// <see cref="LastShiftVoyage.BeginVoyage"/> 의 맨 끝에서 서고, 그 안의
        /// <c>EnterSegment</c> 가 <c>LeavePort</c> 를 부르므로 앞에서 부르면 그 자리에서 도로
        /// 꺼진다(처음에 그렇게 짰다가 셋 다 여기서 걸렸다). 도착 알림도
        /// <c>SettleSegment</c> 안에 이미 있다.
        /// </summary>
        private static void ArriveAtFirstPort()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
        }

        /// <summary>
        /// <b>sandbox 가 도입부 시계를 돌리는가.</b> 이 검사가 없으면 <c>Tick</c> 을 아무도 안
        /// 불러도 EditMode 는 전부 초록이고, 게임에서는 <b>암전이 영영 안 걷히고 조작이
        /// 잠긴 채로 남는다</b> — 리프트 때보다 나쁜 실패다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSandboxDrivesTheWakeClock()
        {
            ArriveAtFirstPort();
            Assert.That(LastShiftWakeSequence.IsRunning, Is.True, "기항 도착이 도입부를 안 열었다");
            Assert.That(LastShiftWakeSequence.CanMove, Is.False, "시작부터 이동이 열려 있다");

            // 손으로 tick 하지 않는다. sandbox 가 돌리는지가 이 검사의 전부다.
            var waited = 0f;
            while (waited < LastShiftWakeSequence.StandSeconds * 3f + 2f)
            {
                if (LastShiftWakeSequence.CanMove) break;
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.That(LastShiftWakeSequence.CanMove, Is.True,
                $"이동이 안 풀렸다 — sandbox 가 Tick 을 안 부른다. elapsed={LastShiftWakeSequence.Elapsed:F2}");
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(0f),
                "이동은 풀렸는데 화면이 아직 검다");
            Assert.That(LastShiftWakeSequence.Current.Id, Is.EqualTo("AI_W_05"));
        }

        /// <summary>
        /// 암전이 <b>캔버스에 실제로 걸리는가</b>. 층을 새로 하나 만들었으므로(계기 위·글자
        /// 아래) 그 층이 살아 있는지는 씬에서만 보인다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBlackoutReachesTheCanvas()
        {
            ArriveAtFirstPort();
            yield return null;
            yield return null;

            var layer = LastShiftUiLayer.Instance;
            Assert.That(layer, Is.Not.Null, "UI 레이어가 안 섰다");

            var fade = layer.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .FirstOrDefault(image => image.name == "Fade:screen");
            Assert.That(fade, Is.Not.Null, "암전 조각이 캔버스에 없다");
            Assert.That(fade.color.a, Is.GreaterThan(0.5f),
                $"암전 조각은 있는데 투명하다. a={fade.color.a:F2}");

            // 다 걷히면 임대가 안 갱신되어 저절로 꺼진다.
            var waited = 0f;
            while (waited < LastShiftWakeSequence.LookSeconds * 3f + 2f)
            {
                if (LastShiftWakeSequence.BlackoutAlpha <= 0f) break;
                waited += Time.deltaTime;
                yield return null;
            }
            yield return null;
            yield return null;

            Assert.That(fade == null || !fade.gameObject.activeSelf, Is.True,
                "암전이 걷혔는데 검은 조각이 캔버스에 남아 있다");
        }

        /// <summary>
        /// <b>연출이 실제로 화면에 나왔는가</b>를 프레임 단위로 적는다. 컴포넌트가 붙어 있는지가
        /// 아니라 <b>그 프레임에 화면이 검었는지 · 문장이 떴는지 · 소리가 났는지</b>를 본다 —
        /// 배선 검사만으로는 "돌긴 도는데 아무것도 안 보인다" 를 못 잡는다(PM 요청 2026-08-12).
        ///
        /// 증거는 <c>[WAKE_EVIDENCE]</c> 로 로그에 남는다. 사람이 읽는 것이 목적이라
        /// 단정보다 기록이 먼저다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOpeningActuallyShowsOnScreen()
        {
            ArriveAtFirstPort();
            LastShiftNarrationAudio.Clear();

            var shown = new System.Collections.Generic.Dictionary<string, string>();
            var order = new System.Collections.Generic.List<string>();
            var firstSeen = new System.Collections.Generic.Dictionary<string, float>();
            var sounds = new System.Collections.Generic.Dictionary<string, string>();
            var blackFrames = 0;
            var litFrames = 0;
            var maxAlpha = 0f;

            var waited = 0f;
            while (waited < LastShiftWakeSequence.StandSeconds + 1.5f)
            {
                // <b>그려진 뒤에 읽는다.</b> 코루틴은 그 프레임 Update 앞에서 깨므로 여기서
                // 바로 읽으면 라벨이 앞 프레임 내용이다 — 처음에 그렇게 재서 문장이 한 줄씩
                // 밀려 보였다.
                yield return new WaitForEndOfFrame();

                var alpha = LastShiftWakeSequence.BlackoutAlpha;
                maxAlpha = Mathf.Max(maxAlpha, alpha);
                if (alpha > 0.99f) blackFrames++;
                if (alpha <= 0f) litFrames++;

                if (LastShiftWakeSequence.HasLine)
                {
                    var id = LastShiftWakeSequence.Current.Id;
                    if (!order.Contains(id))
                    {
                        order.Add(id);
                        firstSeen[id] = waited;
                    }
                    // <b>매 프레임 모은다.</b> 줄이 바뀐 그 순간만 읽으면 타이핑이 아직 0글자라
                    // 빈 문자열이 잡힌다 — 그것도 처음에 그렇게 재서 <없음> 이 줄줄이 나왔다.
                    var text = ScreenText();
                    if (!string.IsNullOrEmpty(text)) shown[id] = text;
                    if (LastShiftNarrationAudio.LastPlayedId == id && !sounds.ContainsKey(id))
                        sounds[id] = LastShiftNarrationAudio.LastPlayedSfx.ToString();
                }

                waited += Time.deltaTime;
            }

            var log = new System.Text.StringBuilder("[WAKE_EVIDENCE] ");
            foreach (var id in order)
                log.Append(id).Append('@').Append(firstSeen[id].ToString("F2")).Append("s 화면=")
                   .Append(shown.TryGetValue(id, out var seen) ? seen : "<안뜸>")
                   .Append(" 소리=").Append(sounds.TryGetValue(id, out var sfx) ? sfx : "-")
                   .Append("  |  ");
            log.Append("검정프레임=").Append(blackFrames).Append(" 밝은프레임=").Append(litFrames)
               .Append(" 최대알파=").Append(maxAlpha.ToString("F2"));
            Debug.Log(log.ToString());

            Assert.That(maxAlpha, Is.EqualTo(1f).Within(0.01f), "화면이 한 번도 완전히 안 검었다");
            Assert.That(blackFrames, Is.GreaterThan(0), "암전 프레임이 하나도 없다");
            Assert.That(litFrames, Is.GreaterThan(0), "암전이 끝내 안 걷혔다");
            Assert.That(order, Does.Contain("AI_W_01").And.Contains("AI_W_05"), "도입부 줄이 안 떴다");
            foreach (var id in order)
                Assert.That(shown.ContainsKey(id), Is.True, $"{id} 이 화면에 한 번도 안 그려졌다");
            Assert.That(sounds.Keys, Does.Contain("AI_W_01"), "첫 줄에 신호음이 안 났다");
        }

        /// <summary>지금 온보딩 띠에 그려진 글자. UI 레이어가 빌려 쓰는 조각을 이름으로 찾는다.</summary>
        private static string ScreenText()
        {
            foreach (var text in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
                if (text.name == "Label:tutorialGuide" && text.enabled
                    && !string.IsNullOrEmpty(text.text)) return text.text;
            return null;
        }

        /// <summary>
        /// 출항하면 도입부가 닫힌다. 안 닫으면 <b>잠금이 항해까지 따라 나간다.</b>
        /// </summary>
        [UnityTest]
        public IEnumerator LeavingPortReleasesTheGate()
        {
            ArriveAtFirstPort();
            yield return null;
            Assume.That(LastShiftWakeSequence.CanMove, Is.False);

            LastShiftTutorial.LeavePort();

            Assert.That(LastShiftWakeSequence.IsRunning, Is.False);
            Assert.That(LastShiftWakeSequence.CanMove, Is.True);
            Assert.That(LastShiftWakeSequence.BlackoutAlpha, Is.EqualTo(0f));
        }
    }
}
