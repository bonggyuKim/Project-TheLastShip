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
    /// <b>닫힌 압력문은 못 지나간다.</b> 이 계약을 아무 검사도 안 보고 있었다 — 갑판 해치는
    /// 차단 콜라이더 검사가 있는데 구역 문에는 없었고, 사용자 플레이에서 "문이 그냥 통과된다"
    /// 는 보고가 올라왔을 때 코드를 읽는 것 말고 확인할 방법이 없었다.
    ///
    /// <b>씬에서 잰다.</b> 차단은 씬 조립(빌더가 세운 blocker)과 상태기와 <c>SIMUL_ZONES</c>
    /// 원장이 함께 만드는 결과라, 셋 중 하나만 흉내내면 진짜 실패를 놓친다.
    /// </summary>
    public sealed class LastShiftZoneDoorBlockPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        private LastShiftSandboxController sandbox;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            LastShiftNetworkSession.AutoStartHost = false;

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            sandbox = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true))
                .Single();
        }

        /// <summary>
        /// <b>기본은 전부 열림이다</b>(<c>LastShiftDoorState.AllOpen</c>). 통과되는 것이 맞고,
        /// 그것이 "안전 설계가 죽었다" 가 아니라는 것을 여기서 못박는다 — 배는 한 덩어리로
        /// 시작하고 격리는 승무원이 만든다.
        /// </summary>
        [UnityTest]
        public IEnumerator DoorsStartOpenAndThatIsTheDesign()
        {
            yield return null;

            var doors = Object.FindObjectsByType<LastShiftZoneDoor>(FindObjectsSortMode.None);
            Assert.That(doors, Is.Not.Empty, "씬에 구역 문이 하나도 없다");

            foreach (var door in doors)
                Assert.That(door.IsOpen, Is.True, $"{door.name} 이 시작부터 닫혀 있다");
        }

        /// <summary>
        /// <b>이 검사가 이 파일의 전부다.</b> 문을 닫으면 그 자리에 실제로 통행을 막는
        /// 콜라이더가 켜지는가 — 상태기의 <c>bool</c> 이 아니라 <b>물리</b>로 확인한다.
        /// </summary>
        [UnityTest]
        public IEnumerator ClosingADoorPutsRealCollisionInTheGap()
        {
            var doors = Object.FindObjectsByType<LastShiftZoneDoor>(FindObjectsSortMode.None);
            Assume.That(doors, Is.Not.Empty);

            foreach (var door in doors)
            {
                var gap = door.transform.position + Vector3.up;
                Assert.That(Blocked(gap), Is.False,
                    $"{door.name} 이 열려 있는데 구멍이 막혀 있다");
            }

            // 전부 닫는다. 닫히는 데 0.8초가 걸리므로 그만큼 돌린다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                sandbox.SetDoorOpen(boundary, false);

            var waited = 0f;
            while (waited < LastShiftRecoveryTuning.ZoneDoorTransitionSeconds * 2f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            UnityEngine.Physics.SyncTransforms();

            foreach (var door in doors)
            {
                Assert.That(door.IsOpen, Is.False, $"{door.name} 이 안 닫혔다");
                Assert.That(Blocked(door.transform.position + Vector3.up), Is.True,
                    $"{door.name} 이 닫혔는데 구멍에 콜라이더가 없다 — 승무원이 그대로 지나간다");
            }
        }

        /// <summary>
        /// 다시 열면 길이 돌아온다. 막는 것만 검사하면 <b>영영 갇히는</b> 실패를 못 잡는다.
        /// </summary>
        [UnityTest]
        public IEnumerator ReopeningGivesTheGapBack()
        {
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                sandbox.SetDoorOpen(boundary, false);
            var waited = 0f;
            while (waited < LastShiftRecoveryTuning.ZoneDoorTransitionSeconds * 2f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                sandbox.SetDoorOpen(boundary, true);
            waited = 0f;
            while (waited < LastShiftRecoveryTuning.ZoneDoorTransitionSeconds * 2f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            UnityEngine.Physics.SyncTransforms();

            foreach (var door in Object.FindObjectsByType<LastShiftZoneDoor>(FindObjectsSortMode.None))
                Assert.That(Blocked(door.transform.position + Vector3.up), Is.False,
                    $"{door.name} 을 다시 열었는데 아직 막혀 있다");
        }

        /// <summary>승무원 어깨 폭만 한 상자로 그 자리를 찔러 본다.</summary>
        private static bool Blocked(Vector3 point)
        {
            var hits = UnityEngine.Physics.OverlapBox(point, new Vector3(0.15f, 0.4f, 0.15f),
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            return hits != null && hits.Length > 0;
        }
    }
}
