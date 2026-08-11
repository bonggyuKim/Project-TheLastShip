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
            Assert.That(LastShiftAirlock.TryOpenInner(liftAwayFromDeck: false), Is.True);
            Assert.That(LastShiftEvaLift.TryAscend(), Is.True);
            Assert.That(LastShiftAirlock.IsCycling, Is.True, "출발과 동시에 사이클이 돌아야 한다");

            // 손으로 tick 하지 않는다. sandbox 가 돌리는지를 보는 것이 이 검사의 전부다.
            var waited = 0f;
            while (waited < LastShiftEvaShaft.LiftSeconds * 3f + 2f)
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
