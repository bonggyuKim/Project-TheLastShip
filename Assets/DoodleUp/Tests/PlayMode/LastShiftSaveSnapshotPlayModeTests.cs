using System.Collections;
using System.Diagnostics;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 세이브 백본 1단계의 실측 자리. <c>docs/tech/save-backbone-feasibility-v1.md</c> §3.2 는
    /// 이어하기 <c>10</c>초 예산을 셋으로 쪼개고 <b>(나) B층 주입은 마이크로초 단위</b>라고
    /// 적었는데, 그건 코드 판독에서 나온 <b>추정</b>이었다. 여기서 그 한 조각을 실측으로 바꾼다.
    ///
    /// (가) 배치물 재조립과 (다) 물리 정지 배치는 <b>아직 못 잰다</b> — 씬 복원 경로 자체가
    /// 없기 때문이다(§3.1). 그 둘은 복원 경로를 내는 카드가 같은 자리에서 잰다.
    /// </summary>
    public sealed class LastShiftSaveSnapshotPlayModeTests
    {
        /// <summary>
        /// 한 번의 주입이 넘으면 안 되는 선. 10초 예산의 <c>0.1%</c> 인 <c>10</c>ms 를 쓴다 —
        /// 추정(마이크로초)이 두 자릿수 틀려도 통과하는 느슨한 선이고, 그래도 "B층이 예산을
        /// 건드리기 시작했다" 는 회귀는 잡는다.
        /// </summary>
        private const double InjectionBudgetMilliseconds = 10.0;

        private const int SampleCount = 200;

        [UnityTest]
        public IEnumerator BLayerInjectionCostsNothingAgainstTheTenSecondBudget()
        {
            var playerObject = new GameObject("Crew");
            playerObject.transform.position = LastShiftSandboxController.PlayerSpawn;
            playerObject.AddComponent<CharacterController>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            var socket = new GameObject("HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            var crew = playerObject.AddComponent<LastShiftPlayerController>();
            crew.Configure(camera, socket);

            var root = new GameObject("Runtime");
            var sandbox = root.AddComponent<LastShiftSandboxController>();
            sandbox.Configure(crew, new LastShiftGrabbable[0]);
            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            yield return null;

            var saved = sandbox.CaptureRuntimeSnapshot();
            var latches = sandbox.CaptureSituationLatches();

            // 첫 회는 JIT 과 배열 할당이 섞이므로 예열한다. 재는 것은 정상 상태의 주입 비용이다.
            sandbox.ApplyNetworkSnapshot(saved, LastShiftStateAuthority.Local, latches);

            var clock = Stopwatch.StartNew();
            for (var index = 0; index < SampleCount; index++)
                sandbox.ApplyNetworkSnapshot(saved, LastShiftStateAuthority.Local, latches);
            clock.Stop();

            var perInjection = clock.Elapsed.TotalMilliseconds / SampleCount;
            UnityEngine.Debug.Log(
                $"[LAST_SHIFT_SAVE_PROBE] stage=b-layer-injection samples={SampleCount} " +
                $"total={clock.Elapsed.TotalMilliseconds:F3}ms per_injection={perInjection:F4}ms " +
                $"budget={InjectionBudgetMilliseconds:F1}ms " +
                $"result={(perInjection <= InjectionBudgetMilliseconds ? "PASS" : "FAIL")}");

            Assert.That(perInjection, Is.LessThan(InjectionBudgetMilliseconds),
                "B층 주입이 이어하기 예산을 건드리기 시작했다면 §3.2 의 분해를 다시 봐야 한다.");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(playerObject);
        }

        [UnityTest]
        public IEnumerator CaptureDoesNotStopTheSimulation()
        {
            // 조항 S-10 — 저장이 판을 안 멈춘다. 캡처는 동기 값 복사이고 참조를 하나도 들고
            // 있지 않으므로, 캡처 이후의 플레이가 이미 뜬 스냅샷을 건드릴 방법이 없다(§1.4-다).
            var root = new GameObject("Runtime");
            var sandbox = root.AddComponent<LastShiftSandboxController>();
            // Start() 가 항해를 열며 ResetPreset 을 한 번 더 부른다. 그 프레임을 먼저 지나
            // 보내지 않으면 이 테스트가 밀어 둔 시계를 Start 가 300초로 되돌린다.
            yield return null;

            sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            sandbox.ApplyMeteorImpact();

            var saved = sandbox.CaptureRuntimeSnapshot();
            var savedHeat = saved.ShipState.EngineHeat;
            var savedTimer = saved.DockingSecondsRemaining;

            // 저장한 뒤에도 시뮬은 계속 돈다.
            for (var step = 0; step < 30; step++) sandbox.AdvanceMission(0.1f);

            Assert.That(sandbox.DockingSecondsRemaining, Is.LessThan(savedTimer),
                "저장 뒤에도 시계는 계속 가야 한다 — 저장은 세션을 멈추지 않는다.");
            Assert.That(saved.DockingSecondsRemaining, Is.EqualTo(savedTimer),
                "이미 뜬 스냅샷이 이후의 플레이로 흔들리면 값 타입 규약이 깨진 것이다.");
            Assert.That(saved.ShipState.EngineHeat, Is.EqualTo(savedHeat));

            Object.DestroyImmediate(root);
        }
    }
}
