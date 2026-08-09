using System.Collections;
using System.IO;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 세이브 백본 2단계의 실측·배선 자리. 두 가지를 잰다.
    ///
    /// <list type="number">
    /// <item><b>§3.3 합격선</b> — 배치물 <c>30</c>개 + B층 주입 전체 경로를 한 번 재고 벽시계
    /// <c>10</c>초 이하. 측정 자리는 (가)/(나)/(다) 를 나눠 찍는다 — 넘었을 때 어디가 범인인지
    /// 알아야 한다. 1단계는 (나)만 잴 수 있었다(복원 경로가 없었다).</item>
    /// <item><b>재진입 가드와 쓰기 완료 판정</b>(§1.4-마) — 저장 중 또 누르는 경우와,
    /// "저장됨" 이 캡처가 아니라 쓰기 완료에 걸리는가.</item>
    /// </list>
    /// </summary>
    public sealed class LastShiftSaveFilePlayModeTests
    {
        /// <summary>§3.3 의 이어하기 예산. 넘으면 성계 구성을 다시 본다(§8.2).</summary>
        private const double RestoreBudgetMilliseconds = 10000.0;

        /// <summary>§3.3 이 지정한 배치물 수.</summary>
        private const int ModuleCount = 30;

        private string savePath;

        [SetUp]
        public void SetUp()
        {
            savePath = Path.Combine(Application.temporaryCachePath, "lastshift-save-test.json");
            if (File.Exists(savePath)) File.Delete(savePath);
            ClearStatics();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(savePath)) File.Delete(savePath);
            ClearStatics();
        }

        private static void ClearStatics()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftVoyage.Clear();
            LastShiftPlacementAuthority.Revoke();
        }

        /// <summary>
        /// §3.3 합격선. <b>(가) 배치물 재조립이 예산을 다 쓰는 유일한 조각</b>이라는 §3.2 의
        /// 분해가 맞는지도 같이 드러난다 — 셋을 나눠 찍으므로 로그가 곧 그 검산이다.
        ///
        /// <b>선체 판이 없는 칸에서 잰다.</b> <see cref="LastShiftBakedDoorways.Open"/> 이 자를
        /// 벽이 이 칸에 없으므로 실제 씬보다 문틀 절단만큼 싸다. 그래서 이 수치는 재조립 비용의
        /// <b>하한</b>이고, 예산을 넘기면 실제 씬은 더 넘는다.
        /// </summary>
        [UnityTest]
        public IEnumerator RestoringThirtyModulesAndTheSegmentFitsTheTenSecondBudget()
        {
            var harness = Harness.Create(savePath);
            yield return null;

            var placed = PlacePortWallModules(ModuleCount);
            Assert.That(placed, Is.EqualTo(ModuleCount),
                $"표본 {ModuleCount}개 중 {placed}개만 판정을 통과했다 — 예산이 아니라 표본 문제다.");

            harness.Sandbox.ApplyMeteorImpact();
            for (var step = 0; step < 20; step++) harness.Sandbox.AdvanceMission(0.1f);
            var saved = harness.Sandbox.CaptureRuntimeSnapshot();

            var json = LastShiftSaveFormat.Write(LastShiftSaveCapture.Capture(harness.Sandbox));
            var yard = new GameObject("ShipRoot").transform;

            // 복원 전에 판을 비운다. 표가 남아 있으면 재조립이 "이미 선 것을 다시 세우는" 값이
            // 아니라 "지우고 세우는" 값이 되는데, 이어하기의 실제 모양은 후자다.
            ClearStatics();

            var report = LastShiftSaveCapture.Restore(
                LastShiftSaveFormat.Read(json), harness.Sandbox, yard);

            Debug.Log(
                $"[LAST_SHIFT_SAVE_PROBE] stage=restore modules={report.ModulesBuilt} " +
                $"reassemble={report.ReassembleMilliseconds:F2}ms " +
                $"inject={report.InjectionMilliseconds:F4}ms " +
                $"pose={report.PoseMilliseconds:F3}ms total={report.TotalMilliseconds:F2}ms " +
                $"budget={RestoreBudgetMilliseconds:F0}ms " +
                $"result={(report.TotalMilliseconds <= RestoreBudgetMilliseconds ? "PASS" : "FAIL")}");

            Assert.That(report.CampaignComplete, Is.True, "복원이 표를 다 못 세웠다.");
            Assert.That(report.ModulesBuilt, Is.EqualTo(ModuleCount));
            Assert.That(report.SegmentRestored, Is.True);
            Assert.That(report.TotalMilliseconds, Is.LessThan(RestoreBudgetMilliseconds),
                "이어하기가 10초 예산을 넘었다 — 로그의 셋 중 어느 조각인지가 범인이다.");
            Assert.That(harness.Sandbox.CaptureRuntimeSnapshot().Equals(saved), Is.True,
                "씬을 세우는 경로를 지나도 B층은 그대로여야 한다.");

            Object.DestroyImmediate(yard.gameObject);
            harness.Dispose();
        }

        /// <summary>
        /// <b>"저장됨" 은 쓰기 완료에만 걸린다</b>(§1.4-마-2). 캡처 완료로 걸면 쓰기 실패를
        /// 성공으로 적는다. 그리고 캡처 시점이 <c>LateUpdate</c> 인지도 여기서 드러난다 —
        /// 요청한 프레임 안에서는 아직 아무 일도 안 일어나야 한다(§7.4).
        /// </summary>
        [UnityTest]
        public IEnumerator SavedStatusWaitsForTheWriteNotTheCapture()
        {
            var harness = Harness.Create(savePath);
            yield return null;

            harness.Service.RequestSave();
            Assert.That(harness.Service.Status, Is.EqualTo(LastShiftSaveStatus.Idle),
                "요청은 플래그만 세운다 — 캡처는 이 프레임의 LateUpdate 다(§7.4).");
            Assert.That(harness.Service.HasPendingRequest, Is.True);

            yield return null;
            Assert.That(harness.Service.Status, Is.EqualTo(LastShiftSaveStatus.Writing),
                "캡처가 끝났어도 아직 저장됨이 아니다 — 디스크가 남았다.");

            yield return WaitForIdle(harness.Service);

            Assert.That(harness.Service.Status, Is.EqualTo(LastShiftSaveStatus.Saved), harness.Service.LastError);
            Assert.That(File.Exists(savePath), Is.True, "저장됨인데 파일이 없다.");
            Assert.That(harness.Service.CompletedSaveCount, Is.EqualTo(1));

            var load = LastShiftSaveFormat.Read(File.ReadAllText(savePath));
            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.Loaded), load.Reason);
            Assert.That(load.File.HasSegment, Is.True);

            harness.Dispose();
        }

        /// <summary>
        /// 재진입 가드(§1.4-마-1). <b>같은 프레임의 연타는 하나로 접히고</b>, <b>쓰기 도중의
        /// 요청은 버려지지 않는다</b> — 버리면 누른 사람은 눌렀는데 아무 일도 안 일어난 판을 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator ReentrantRequestsCoalesceAndAreNotDropped()
        {
            var harness = Harness.Create(savePath);
            yield return null;

            harness.Service.RequestSave();
            harness.Service.RequestSave();
            harness.Service.RequestSave();
            yield return null;

            // 이 시점은 쓰기가 도는 중이다(위 테스트가 그 결정성을 지킨다). 여기서 누른 한 번이
            // 살아남아야 한다 — 그것이 "마지막 요청만 남기기" 다.
            Assert.That(harness.Service.IsWriting, Is.True);
            harness.Service.RequestSave();
            Assert.That(harness.Service.HasPendingRequest, Is.True, "쓰기 중 요청이 버려졌다.");

            yield return WaitForIdle(harness.Service);

            Assert.That(harness.Service.Status, Is.EqualTo(LastShiftSaveStatus.Saved), harness.Service.LastError);
            Assert.That(harness.Service.CompletedSaveCount, Is.EqualTo(2),
                "연타 셋은 하나로 접히고 쓰기 중 요청 하나가 뒤따라야 한다 — 합쳐 둘이다.");

            harness.Dispose();
        }

        /// <summary>서비스가 쓴 파일을 그대로 읽어 되세운다 — 디스크를 지나는 경로가 실제로 닫히는가.</summary>
        [UnityTest]
        public IEnumerator ServiceRoundTripsThroughDisk()
        {
            var harness = Harness.Create(savePath);
            yield return null;

            harness.Sandbox.ApplyMeteorImpact();
            for (var step = 0; step < 15; step++) harness.Sandbox.AdvanceMission(0.1f);
            var saved = harness.Sandbox.CaptureRuntimeSnapshot();

            harness.Service.RequestSave();
            yield return WaitForIdle(harness.Service);
            Assert.That(harness.Service.Status, Is.EqualTo(LastShiftSaveStatus.Saved), harness.Service.LastError);

            for (var step = 0; step < 40; step++) harness.Sandbox.AdvanceMission(0.1f);
            var report = harness.Service.LoadAndRestore();

            Assert.That(report.SegmentRestored, Is.True);
            Assert.That(harness.Sandbox.CaptureRuntimeSnapshot().Equals(saved), Is.True,
                "디스크를 지난 왕복이 B층 필드를 잃었다.");

            harness.Dispose();
        }

        private static IEnumerator WaitForIdle(LastShiftSaveService service)
        {
            for (var frame = 0; frame < 600; frame++)
            {
                if (!service.IsWriting && !service.HasPendingRequest) yield break;
                yield return null;
            }
            Assert.Fail("쓰기가 600 프레임 안에 안 끝났다.");
        }

        /// <summary>
        /// 좌현 긴 벽에 붙는 방들. <b>고정 구획이 하나도 안 붙은 벽</b>이라(고정 표의 선체 부착은
        /// 우현과 선수·선미다) <c>30</c>개를 겹침 없이 세울 수 있다. 전부 깊이 <c>1</c> 이라
        /// 사슬 깊이 상한(<c>6</c>)과도 무관하다 — 재는 것은 판정이 아니라 조립 비용이다.
        /// </summary>
        private static int PlacePortWallModules(int count)
        {
            const float width = 1f;
            const float depth = 3f;
            var wall = -LastShiftShipDimensions.HalfWidth;
            var startX = -(count * width) * 0.5f;
            var placed = 0;

            for (var index = 0; index < count; index++)
            {
                var minX = startX + index * width;
                var spec = new LastShiftCompartmentSpec(
                    LastShiftCompartments.NextModuleIndex,
                    minX, minX + width, wall - depth, wall,
                    LastShiftDoorPlane.AlongZ, wall, minX + width * 0.5f,
                    -1, LastShiftCompartmentAccess.Open);

                if (!LastShiftCompartments.TryRegister(spec, out _, out var verdict, LastShiftModuleCatalog.Corridor))
                {
                    Debug.LogWarning($"[LAST_SHIFT_SAVE_PROBE] sample={index} rejected={verdict.Rejection}");
                    break;
                }
                placed++;
            }
            return placed;
        }

        private readonly struct Harness
        {
            public readonly LastShiftSandboxController Sandbox;
            public readonly LastShiftSaveService Service;
            private readonly GameObject root;
            private readonly GameObject player;

            private Harness(
                GameObject root, LastShiftSandboxController sandbox,
                LastShiftSaveService service, GameObject player)
            {
                this.root = root;
                Sandbox = sandbox;
                Service = service;
                this.player = player;
            }

            public static Harness Create(string path)
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
                var service = root.AddComponent<LastShiftSaveService>();
                service.Configure(sandbox, path);
                return new Harness(root, sandbox, service, playerObject);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }
    }
}
