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
    /// 외부 랜덤 자극을 <b>실제 씬에서</b> 돌린다.
    ///
    /// <b>EditMode 로는 못 잡는 것 하나만 잡는다 — 누가 이 시계를 돌리는가.</b> 규칙(구간당
    /// 한 번·창·강도·방별 계통)은 <c>LastShiftExternalStimulusTests</c> 가 시계를 직접 돌려
    /// 이미 재고 있다. 여기서 재는 것은 <c>LastShiftSandboxController</c> 가 정말로
    /// <c>Tick</c> 을 부르는가와, 터졌을 때 배 상태가 실제로 움직이는가 둘이다 — 같은 자리에서
    /// <c>LastShiftEvaLift.Tick</c> 이 아무 데서도 안 불리고 있었고 EditMode 는 전부 초록이었다.
    /// </summary>
    public sealed class LastShiftExternalStimulusPlayModeTests
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
        /// <b>sandbox 가 자극 시계를 돌리는가.</b> 이 검사가 없으면 아무도 <c>Tick</c> 을
        /// 안 불러도 EditMode 는 전부 초록이고, 게임에서는 <b>자극이 영영 안 뜬다</b> —
        /// 디버그 키로만 존재하던 예전 상태로 조용히 되돌아간다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSandboxDrivesTheStimulusClock()
        {
            LastShiftExternalStimulus.BeginSegment(5);
            Assume.That(LastShiftExternalStimulus.IsArmed, Is.True);
            var before = LastShiftExternalStimulus.Elapsed;

            for (var frame = 0; frame < 30; frame++) yield return null;

            Assert.That(LastShiftExternalStimulus.Elapsed, Is.GreaterThan(before),
                "구간 시계가 안 흐른다 — sandbox 가 Tick 을 안 부른다");
        }

        /// <summary>
        /// 터지면 <b>배 상태가 실제로 움직이는가</b>. 규칙이 맞아도 컨트롤러가 델타를 안
        /// 받으면 화면에서는 아무 일도 안 난다.
        ///
        /// 증거는 <c>[STIMULUS_EVIDENCE]</c> 로 남긴다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheStimulusActuallyChangesTheShip()
        {
            // 강도를 최대로 고정해 관측을 또렷하게 한다. 방은 씨앗이 정한 그대로 둔다.
            LastShiftExternalStimulus.BeginSegment(2);
            LastShiftExternalStimulus.FireAtForProbe(0.1f);
            var room = LastShiftExternalStimulus.Room;

            var beforeImpacts = sandbox.ImpactApplicationCount;

            var waited = 0f;
            while (waited < 2f && !LastShiftExternalStimulus.HasFired)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.That(LastShiftExternalStimulus.HasFired, Is.True,
                $"예약 시점이 지났는데 안 터졌다 — elapsed={LastShiftExternalStimulus.Elapsed:F2}");
            Assert.That(sandbox.ImpactApplicationCount, Is.GreaterThan(beforeImpacts),
                "자극이 터졌는데 충격이 배에 안 들어갔다");

            // 램프가 도는 동안 값이 계속 밀린다.
            var rampStart = sandbox.CurrentState;
            var rampWaited = 0f;
            while (rampWaited < LastShiftExternalStimulus.DamageSeconds * 0.5f)
            {
                rampWaited += Time.deltaTime;
                yield return null;
            }

            var now = sandbox.CurrentState;
            Debug.Log($"[STIMULUS_EVIDENCE] room={room} severity={LastShiftExternalStimulus.Severity:F3} " +
                      $"impacts={sandbox.ImpactApplicationCount} " +
                      $"bus {rampStart.BusPower:F3}->{now.BusPower:F3} " +
                      $"heat {rampStart.EngineHeat:F3}->{now.EngineHeat:F3} " +
                      $"fuel {rampStart.FuelReserve:F3}->{now.FuelReserve:F3} " +
                      $"attitude {rampStart.ShipAttitudeDegrees:F2}->{now.ShipAttitudeDegrees:F2}");

            Assert.That(LastShiftExternalStimulus.IsDamaging, Is.True,
                "램프 중간인데 손상이 이미 끝났다 — 즉발로 들어갔다는 뜻이다");
        }

        /// <summary>
        /// <b>도입부 중에는 시계가 안 돈다.</b> 암전으로 화면이 덮이고 입력이 잠긴 동안 사고가
        /// 나면 대응할 수 없는 시간이 생긴다 — <c>RG-3</c> 이 막으려는 상태와 같은 모양이다
        /// (미결 3 에 대한 tech 판단).
        /// </summary>
        [UnityTest]
        public IEnumerator TheClockHoldsWhileTheOpeningPlays()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
            Assume.That(LastShiftWakeSequence.IsRunning, Is.True, "도입부가 안 열렸다");

            LastShiftExternalStimulus.BeginSegment(9);
            var before = LastShiftExternalStimulus.Elapsed;

            for (var frame = 0; frame < 30; frame++) yield return null;

            Assert.That(LastShiftExternalStimulus.Elapsed, Is.EqualTo(before),
                "도입부가 도는데 자극 시계가 같이 흘렀다");
        }
    }
}
