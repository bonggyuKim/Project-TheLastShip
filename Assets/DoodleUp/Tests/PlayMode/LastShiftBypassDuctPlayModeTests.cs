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
    /// 갑판 하부 우회 통로를 <b>실제 캡슐로 지나간다.</b>
    ///
    /// EditMode 검사는 전부 상수끼리의 관계라 "단면 0.9 == 웅크림 0.9" 같은 식이 참이면 통과한다.
    /// 그런데 사용자 플레이에서 막힌 것은 그 등식이 참일 때였다 — CharacterController 는 자기
    /// 치수 그대로가 아니라 skinWidth 만큼 더 큰 자리를 차지하고, 승강구 단처럼 밟고 서면
    /// 머리가 천장 위로 나오는 형상은 좌표 검사에 안 걸린다. 그래서 이 파일만 씬의 진짜
    /// 콜라이더 위에서 프리팹 캡슐을 걷게 한다.
    /// </summary>
    public sealed class LastShiftBypassDuctPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController player;

        [UnitySetUp]
        public IEnumerator LoadSoloScene()
        {
            // 같은 UDP 포트를 연속으로 잡으면 SetUp 부터 죽는다(다른 PlayMode 파일과 같은 이유).
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftAirlock.Clear();
            LastShiftVoyage.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            foreach (var networkObject in roots.SelectMany(root =>
                         root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true)))
                networkObject.AutoObjectParentSync = false;

            var session = Object.FindAnyObjectByType<LastShiftNetworkSession>();
            Assert.That(session, Is.Not.Null, "network session missing from the scene");
            var crew = Object.Instantiate(session.PlayerPrefab.gameObject);
            crew.name = "PlayerOne";
            player = crew.GetComponent<LastShiftPlayerController>();
            Assert.That(player, Is.Not.Null, "player prefab must carry LastShiftPlayerController");
            player.ResetPlayer(LastShiftShipDimensions.SpawnPoint);

            // 시계는 이 파일의 관심사가 아니다.
            sandbox.enabled = false;
        }

        [TearDown]
        public void DestroyCrew()
        {
            if (player != null) Object.Destroy(player.gameObject);
            LastShiftAirlock.Clear();
            LastShiftVoyage.Clear();
        }

        /// <summary>
        /// <b>이 카드의 재현이다.</b> 조종석 승강구로 내려가 Ctrl 로 웅크린 채 선미 쪽으로 걷는다.
        /// 예전 형상에서는 승강구 바닥의 단 위에 올라선 채로 덕트 천장에 막혀 한 발도 못 나갔고,
        /// 단을 치워도 캡슐 높이가 단면과 같으면 관 입구에서 그대로 섰다.
        /// </summary>
        [UnityTest]
        public IEnumerator CrouchedCrewDescendsTheShaftAndWalksIntoTheDuct()
        {
            const int shaft = LastShiftBypassDuct.ForeShaft;
            var mouth = LastShiftBypassDuct.ShaftMouth(shaft);
            sandbox.SetHatchOpen(shaft, true);
            yield return WaitForHatch(shaft, expectedOpen: true);

            // 구멍 바로 위에서 놓는다. 갑판을 걸어와 빠지는 것과 같은 상태다.
            player.ResetPlayer(new Vector3(mouth.x, LastShiftBypassDuct.DeckY + 0.05f, mouth.z));
            player.SetAimDirectionForProbe(Vector3.forward);
            yield return Walk(Vector2.zero, 2f);

            Assert.That(player.transform.position.y,
                Is.EqualTo(LastShiftBypassDuct.FloorY).Within(0.1f),
                $"승강구로 안 내려간다 — y={player.transform.position.y:F2}, 덕트 바닥 {LastShiftBypassDuct.FloorY:F2}. {UnderfootReport()}");

            // Ctrl. 웅크림은 머리 위 공간을 안 보므로 어디서든 켜진다.
            player.SetCrouching(true);
            Assert.That(player.IsCrouching, Is.True, "Ctrl 로 웅크림이 안 켜진다.");

            // 선수 다리는 +z 로 달린다. 천장은 z = ForeShaftZ + Section/2 에서 시작하므로,
            // 그 선을 넘었다는 것이 곧 "관 안으로 들어갔다" 이다.
            var ceilingStartZ = LastShiftBypassDuct.ForeShaftZ + LastShiftBypassDuct.Section * 0.5f;
            yield return Walk(new Vector2(0f, 1f), 5f);

            var landed = player.transform.position;
            Assert.That(landed.z, Is.GreaterThan(ceilingStartZ + LastShiftShipPhysics.CrewRadius),
                $"웅크렸는데도 관 입구에서 막힌다 — z={landed.z:F2}, 천장 시작 {ceilingStartZ:F2}.");
            Assert.That(LastShiftBypassDuct.Contains(landed), Is.True,
                $"승무원이 덕트 안으로 안 잡힌다 — {landed}.");
            Assert.That(player.IsCrouching, Is.True,
                "관 안에서 자세가 풀렸다 — 그대로면 캡슐이 천장을 뚫는다.");

            // L 자 모서리(= 에어록 위)까지 간다. 여기 바닥은 판이 아니라 닫힌 안쪽 해치라,
            // 이 한 줄이 "해치를 뚫어 두고도 닫혀 있으면 걸어서 지나간다" 를 같이 잰다.
            yield return Walk(new Vector2(0f, 1f), 4f);
            Assert.That(player.transform.position.z,
                Is.GreaterThan(LastShiftBypassDuct.RunZ - LastShiftBypassDuct.Section * 0.5f),
                "모서리(에어록 위)에서 막힌다 — 닫힌 안쪽 해치가 바닥을 안 메운다.");
            Assert.That(player.transform.position.y,
                Is.EqualTo(LastShiftBypassDuct.FloorY).Within(0.1f),
                "에어록 위에서 발밑이 꺼졌다.");
        }

        /// <summary>
        /// 안쪽 해치를 열면 <b>실제로 뚫린다.</b> 덕트 바닥 판에 에어록 자리를 안 비워 두면
        /// 상태만 열리고 발밑은 그대로라 EVA 로 갈 방법이 없다 — 이 카드에서 그랬다.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningTheInnerHatchActuallyOpensTheDuctFloor()
        {
            const int shaft = LastShiftBypassDuct.ForeShaft;
            var mouth = LastShiftBypassDuct.ShaftMouth(shaft);
            sandbox.SetHatchOpen(shaft, true);
            yield return WaitForHatch(shaft, expectedOpen: true);

            player.ResetPlayer(new Vector3(mouth.x, LastShiftBypassDuct.DeckY + 0.05f, mouth.z));
            player.SetAimDirectionForProbe(Vector3.forward);
            yield return Walk(Vector2.zero, 2f);
            player.SetCrouching(true);
            yield return Walk(new Vector2(0f, 1f), 8f);

            var onAirlock = player.transform.position;
            Assert.That(onAirlock.y, Is.EqualTo(LastShiftBypassDuct.FloorY).Within(0.1f),
                "모서리까지 못 갔다 — 이 검사의 전제가 안 선다.");

            // 인터록 — 갑판 해치를 닫아야 안쪽이 열린다. 기항이어야 열 수 있다.
            sandbox.SetHatchOpen(shaft, false);
            yield return WaitForHatch(shaft, expectedOpen: false);
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
            Assert.That(LastShiftAirlock.IsAtPort, Is.True, "기항이 아니면 안쪽 해치를 못 연다.");
            Assert.That(LastShiftAirlock.TryOpenInner(anyDeckHatchOpen: false), Is.True);
            yield return Walk(Vector2.zero, 3f);

            Assert.That(player.transform.position.y,
                Is.LessThan(LastShiftBypassDuct.FloorY - 0.5f),
                $"안쪽 해치를 열었는데 발밑이 그대로다 — y={player.transform.position.y:F2}. " +
                "덕트 바닥 판이 에어록 자리를 안 비웠다는 뜻이다.");
        }

        /// <summary>왜 못 내려갔는지 — 발밑에 실제로 걸린 콜라이더를 이름째로 적는다.</summary>
        private string UnderfootReport()
        {
            var origin = player.transform.position + Vector3.up * 0.4f;
            var report = UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit, 3f, ~0,
                QueryTriggerInteraction.Ignore)
                ? $"발밑={Path(hit.collider.transform)} y={hit.point.y:F3}"
                : "발밑=없음";
            var hatches = Object.FindObjectsByType<LastShiftDeckHatch>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hatch in hatches)
                report += $" | hatch{hatch.Shaft} open={hatch.IsOpen} amount={hatch.OpenAmount:F2}";

            var controller = player.GetComponent<CharacterController>();
            var foot = player.transform.position + Vector3.up * controller.radius;
            var head = player.transform.position + Vector3.up * (controller.height - controller.radius);
            report += $" | 캡슐 h={controller.height:F2} r={controller.radius:F2} 접촉=";
            foreach (var touched in UnityEngine.Physics.OverlapCapsule(foot, head,
                         controller.radius + controller.skinWidth, ~0, QueryTriggerInteraction.Ignore))
            {
                if (touched.transform.IsChildOf(player.transform)) continue;
                report += Path(touched.transform) + ",";
            }

            return report;
        }

        private static string Path(Transform node)
        {
            var name = node.name;
            for (var parent = node.parent; parent != null; parent = parent.parent) name = $"{parent.name}/{name}";
            return name;
        }

        /// <summary>해치 판이 물러나고 차단면이 꺼질 때까지 민다. 개폐는 <c>0.8초</c>다.</summary>
        private IEnumerator WaitForHatch(int shaft, bool expectedOpen)
        {
            var hatch = Object.FindObjectsByType<LastShiftDeckHatch>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(candidate => candidate.Shaft == shaft);
            var guard = 0f;
            while (hatch.IsMoving && guard < 3f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            Assert.That(hatch.IsOpen, Is.EqualTo(expectedOpen), "해치 상태가 안 따라왔다.");
        }

        /// <summary>고정 스텝으로 이동을 민다. 프레임 시간과 assertion 이 경쟁하지 않게 한다.</summary>
        private IEnumerator Walk(Vector2 move, float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                player.MoveForProbe(move, Time.fixedDeltaTime);
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
