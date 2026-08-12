using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// EVA 승강 샤프트를 <b>실제 씬에서</b> 돌린다.
    ///
    /// 이 파일은 갑판 하부 우회 통로를 캡슐로 지나가던 검사 둘을 대체한다. EVA 가 상향으로
    /// 뒤집히면서(기획 확정 2026-08-11) 그 통로가 없어졌고, 대신 광장 코어가 승강 샤프트 겸
    /// 감압 챔버가 됐다.
    ///
    /// <b>EditMode 로는 못 잡는 것만 여기서 잡는다.</b> 승강·감압의 겹침 계약 자체는
    /// <c>LastShiftEvaLiftTests</c> 가 시계를 직접 돌려 재고 있다. 여기서 재는 것은 그 시계를
    /// <b>누가 돌리는가</b> 다 — 실제로 sandbox 가 리프트를 tick 하지 않고 있었고, EditMode
    /// 검사는 자기가 직접 돌려서 통과하고 있었다. 게임에서는 리프트가 영영 안 움직였다.
    /// </summary>
    public sealed class LastShiftEvaShaftPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController player;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftAirlock.Clear();
            LastShiftEvaLift.Clear();
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
            player.ResetPlayer(LastShiftShipDimensions.SpawnPoint);
        }

        [TearDown]
        public void Cleanup()
        {
            if (player != null) Object.Destroy(player.gameObject);
            LastShiftAirlock.Clear();
            LastShiftEvaLift.Clear();
            LastShiftVoyage.Clear();
        }

        private static void EnterPort()
        {
            LastShiftVoyage.EnterSegment(LastShiftVoyage.SegmentOf(LastShiftPreset.HighHeatHighThrust));
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
        }

        /// <summary>
        /// <b>sandbox 가 리프트 시계를 돌리는가.</b> 이 검사가 없으면 <c>LastShiftEvaLift.Tick</c> 을
        /// 아무도 안 불러도 EditMode 는 전부 초록이다 — 실제로 그 상태였다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSandboxDrivesTheLiftAndTheCycleTogether()
        {
            EnterPort();
            // 온보딩을 안 돌리는 검사라 안내 줄이 비어 있는 것이 정상이다. 화면 공백 감시가
            // 1.5초 뒤 Error 로 한 번 짖는데(latch 라 딱 한 번), 이 검사는 승강에 6초 넘게
            // 기다리므로 반드시 걸린다 — 승강이 아니라 그 로그로 빨개지고 있었다.
            LogAssert.Expect(LogType.Error, new Regex("LAST_SHIFT_BANNER.*NO_GUIDANCE"));

            Assert.That(LastShiftAirlock.TryOpenInner(liftAwayFromDeck: false), Is.True);
            Assert.That(LastShiftEvaLift.TryAscend(), Is.True);
            Assert.That(LastShiftAirlock.IsCycling, Is.True, "출발과 동시에 사이클이 돌아야 한다");

            // 손으로 tick 하지 않는다. sandbox 가 돌리는지를 보는 것이 이 검사의 전부다.
            var waited = 0f;
            while (waited < LastShiftEvaShaft.AscentSeconds * 3f + 2f)
            {
                if (LastShiftEvaLift.IsAtHullTop && LastShiftAirlock.IsOuterHatchOpen) break;
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.That(LastShiftEvaLift.IsAtHullTop, Is.True,
                $"리프트가 안 올라갔다 — sandbox 가 Tick 을 안 부른다. y={LastShiftEvaLift.Y:F2}");
            Assert.That(LastShiftAirlock.IsOuterHatchOpen, Is.True,
                "도착했는데 감압이 안 끝났다 — 겹침이 실제로는 안 돈다.");
        }

        /// <summary>
        /// <b>게임 안에서 코어로 들어갈 수 있는가.</b> 위의 검사가 <c>TryOpenInner</c> 를 직접
        /// 부르기 때문에 초록이었지만, 사용자 플레이에서는 게이트가 영영 안 열렸다 —
        /// 조작 사거리가 삭제된 갑판 아래 에어록 자리에 남아 있어서 광장 어디에 서도 안 닿았다.
        /// 그래서 여기서는 <b>플레이어의 조작 키</b>로만 진행한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCrewOpensTheCoreGateAndRidesTheLiftWithTheSameKey()
        {
            EnterPort();

            // 조종석 방향에서 게이트에 다가선다. 발자국 밖 + 사거리 안이다.
            var approach = new Vector3(-(LastShiftEvaShaft.HalfExtent + 1f), 1.0f, 0f);
            player.ResetPlayer(approach);
            for (var frame = 0; frame < 3; frame++) yield return null;

            var eye = new Vector3(approach.x, LastShiftZoneDoor.OpeningHeight * 0.5f, 0f);
            Assert.That(Physics.Raycast(eye, Vector3.right, 1.2f), Is.True,
                "닫힌 게이트가 안 막는다 — SIMUL_ZONES 가드레일이 뚫려 있다.");

            Assert.That(LastShiftEvaLift.NextAction(player.transform.position),
                Is.EqualTo(LastShiftLiftAction.OpenGate),
                "광장에서 게이트를 열 수단이 없다 — 조작 사거리가 코어에 안 붙어 있다.");
            Assert.That(player.TryOperateNearestDoor(), Is.True, "같은 키로 게이트가 안 열린다.");
            Assert.That(LastShiftAirlock.IsInnerHatchOpen, Is.True);
            yield return null;

            Assert.That(Physics.Raycast(eye, Vector3.right, 1.2f), Is.False,
                "게이트를 열었는데도 코어로 들어가는 길이 막혀 있다.");

            // 발판에 올라선다. 같은 키가 여기서는 상승이다.
            player.ResetPlayer(new Vector3(0f, 1.0f, 0f));
            yield return null;
            Assert.That(LastShiftEvaLift.NextAction(player.transform.position),
                Is.EqualTo(LastShiftLiftAction.Ascend), "발판에서 올라갈 수단이 없다.");
            Assert.That(player.TryOperateNearestDoor(), Is.True, "발판에서 상승이 안 걸린다.");
            Assert.That(LastShiftAirlock.IsCycling, Is.True, "출발과 동시에 사이클이 돌아야 한다.");
        }

        /// <summary>
        /// <b>판이 승무원을 데리고 올라가는가.</b> <c>CharacterController</c> 는 움직이는 콜라이더에
        /// 얹혀 따라가지 않는 것이 기본이라, 판만 올라가고 캡슐은 갑판에 남을 수 있다 —
        /// 그러면 상승·감압 시계는 전부 초록인데 화면에서는 아무도 안 올라간다.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePlatformCarriesTheCrewUpTheShaft()
        {
            EnterPort();
            LogAssert.Expect(LogType.Error, new Regex("LAST_SHIFT_BANNER.*NO_GUIDANCE"));

            // 저중력이라 착지가 느리다. 30프레임은 아직 떨어지는 중이고, 그 좌표를 기준값으로
            // 잡으면 "안 올라갔다" 와 "아직 착지 안 했다" 가 안 갈린다.
            player.ResetPlayer(new Vector3(0f, 1.0f, 0f));
            for (var frame = 0; frame < 120; frame++) yield return null;
            var startY = player.transform.position.y;
            var visual = Object.FindAnyObjectByType<LastShiftEvaLiftVisual>();
            Assert.That(visual, Is.Not.Null, "승강 판 시각 컴포넌트가 씬에 없다.");
            var geometry = $"startY={startY:F2} platformTop={visual.PlatformTopY:F2} liftY={LastShiftEvaLift.Y:F2}";

            Assert.That(player.TryOperateNearestDoor(), Is.True, "코어 안에서 게이트가 안 열린다.");
            yield return null;
            Assert.That(player.TryOperateNearestDoor(), Is.True, "발판에서 상승이 안 걸린다.");

            var waited = 0f;
            while (waited < LastShiftEvaShaft.AscentSeconds * 3f + 2f)
            {
                if (LastShiftEvaLift.IsAtHullTop) break;
                waited += Time.deltaTime;
                yield return null;
            }
            for (var frame = 0; frame < 10; frame++) yield return null;

            Assert.That(LastShiftEvaLift.IsAtHullTop, Is.True, "판이 정상까지 안 올라갔다.");
            Assert.That(player.transform.position.y, Is.GreaterThan(startY + 1f),
                "판만 올라가고 승무원은 남았다 — 승강 중 캡슐이 안 따라간다. " +
                $"y={player.transform.position.y:F2} {geometry} nowTop={visual.PlatformTopY:F2} " +
                $"pivotY={visual.PlatformY:F2} liftY={LastShiftEvaLift.Y:F2} enabled={visual.enabled}");
        }

        /// <summary>
        /// 광장 한가운데에 <b>구멍이 없는가</b>. 예전에는 갑판에 승강구 둘이 뚫려 있어서 바닥
        /// 판을 그 자리만 비웠는데, 상향으로 바뀌면서 그 구멍이 사라졌다. 비운 채로 두면
        /// 리프트를 타려고 코어에 선 승무원이 그대로 빠진다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCoreFloorCarriesTheCrewWaitingForTheLift()
        {
            var start = new Vector3(0f, 1.5f, 0f);
            player.ResetPlayer(start);
            for (var frame = 0; frame < 60; frame++) yield return null;

            var y = player.transform.position.y;
            Assert.That(y, Is.GreaterThan(LastShiftEvaShaft.DeckY - 0.5f),
                $"코어에서 승무원이 빠졌다 — 갑판에 구멍이 남아 있다. y={y:F2}");
            Assert.That(LastShiftEvaShaft.Contains(player.transform.position.x, player.transform.position.z),
                Is.True, "승무원이 코어 밖으로 밀렸다");
        }
    }
}
