using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 로비에서 방을 연 직후 승무원이 갑판 위에 서는지 잰다.
    ///
    /// 실제로 났던 실패는 "방(호스트)을 열면 캐릭터가 추락한다" 였다. 스폰 좌표는
    /// <see cref="LastShiftShipDimensions.SpawnPoint"/> 로 갑판(<c>y=0</c>) 바로 위인데,
    /// 화면에서는 승무원이 배 밑으로 떨어졌다. 그래서 재는 것은 스폰 <b>좌표</b>가 아니라
    /// 스폰 뒤 몇 초 동안의 <b>높이</b>다 — 좌표만 보면 첫 프레임은 늘 맞게 나온다.
    /// </summary>
    public sealed class LastShiftHostSpawnPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        private const ushort HostSpawnTestPort = 7986;
        private const ushort CrewFallTestPort = 7987;

        [UnityTearDown]
        public IEnumerator ShutDownSession()
        {
            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            if (session != null) session.StopSession();

            var manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                var deadline = Time.realtimeSinceStartup + 5f;
                while (manager != null && manager.IsListening && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CrewStandsOnTheDeckAfterTheRoomOpens()
        {
            LastShiftNetworkSession.AutoStartHost = true;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            for (var frame = 0; frame < 3; frame++) yield return null;

            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null);

            // 사람이 "방 열기" 를 누르기까지 로비가 잠시 떠 있다. 그 대기 자체가 조건이므로
            // 프레임 세 개가 아니라 실제로 흐르는 시간을 준다.
            var lobbyUntil = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < lobbyUntil) yield return null;

            session.OverridePort(HostSpawnTestPort);
            var lobby = Object.FindFirstObjectByType<LastShiftRoomLobby>(FindObjectsInactive.Include);
            Assert.That(lobby, Is.Not.Null);
            // 화면의 "방 열기" 버튼이 부르는 그 경로를 그대로 탄다.
            typeof(LastShiftRoomLobby)
                .GetMethod("HostRoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(lobby, null);
            Assert.That(session.NetworkManager.IsListening, Is.True);

            yield return WaitFor(
                () => Object.FindFirstObjectByType<LastShiftNetworkPlayer>() != null, "crew-spawned");
            var crew = Object.FindFirstObjectByType<LastShiftNetworkPlayer>();
            Assert.That(crew, Is.Not.Null);

            var controller = crew.GetComponent<LastShiftPlayerController>();
            var capsule = crew.GetComponent<CharacterController>();
            var heights = new List<float>();
            var sampleUntil = Time.realtimeSinceStartup + 6f;
            var nextReport = 0f;
            while (Time.realtimeSinceStartup < sampleUntil)
            {
                heights.Add(crew.transform.position.y);
                if (Time.realtimeSinceStartup >= nextReport)
                {
                    nextReport = Time.realtimeSinceStartup + 0.5f;
                    var origin = crew.transform.position + Vector3.up * 0.5f;
                    var floor = UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit, 40f)
                        ? $"{hit.collider.name}@{hit.point.y:F2}"
                        : "none";
                    var p = crew.transform.position;
                    Debug.Log(
                        $"[LAST_SHIFT_SPAWN_PROBE] pos=({p.x:F2},{p.y:F2},{p.z:F2}) " +
                        $"grounded={(capsule != null && capsule.isGrounded)} " +
                        $"ccEnabled={(capsule != null && capsule.enabled)} " +
                        $"ghost={(controller != null && controller.IsGhost)} " +
                        $"crouch={(controller != null && controller.IsCrouching)} floor={floor}");
                }
                yield return null;
            }

            var lowest = heights.Min();
            var final = crew.transform.position;
            var expected = LastShiftShipDimensions.SpawnPoint.y;
            Assert.That(lowest, Is.GreaterThan(-0.2f),
                $"승무원이 갑판을 뚫고 내려갔다. spawnY={expected:F2} lowestY={lowest:F2} " +
                $"final=({final.x:F2},{final.y:F2},{final.z:F2}) samples={heights.Count}");
            Assert.That(final.y, Is.LessThan(expected + 0.5f),
                $"승무원이 갑판 위로 떠 있다. final=({final.x:F2},{final.y:F2},{final.z:F2})");
        }

        /// <summary>
        /// 어떤 이유로든 배 밖으로 떨어진 승무원은 자기 슬롯으로 돌아온다.
        ///
        /// 스폰 자체가 멀쩡해도 낙하가 <b>끝나는 방법</b>이 없으면 한 번의 사고가 판 전체를
        /// 못 쓰게 만든다 — 저중력이라 죽지도 않고 계속 내려간다. 물건 쪽에는 이미 같은
        /// 안전망(<c>RecoverItemsOutsideSafetyBounds</c>)이 있고, 이 검사는 승무원 판이다.
        /// </summary>
        [UnityTest]
        public IEnumerator FallenCrewReturnsToItsSlot()
        {
            LastShiftNetworkSession.AutoStartHost = true;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            for (var frame = 0; frame < 3; frame++) yield return null;

            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null);
            session.OverridePort(CrewFallTestPort);
            Assert.That(session.OpenRoom(LastShiftRoomCode.Generate()), Is.True);

            yield return WaitFor(
                () => Object.FindFirstObjectByType<LastShiftNetworkPlayer>() != null, "crew-spawned");
            var crew = Object.FindFirstObjectByType<LastShiftNetworkPlayer>();
            var controller = crew.GetComponent<LastShiftPlayerController>();

            // 낙하를 흉내낸다. 좌표만 옮기면 되므로 실제 원인이 무엇이든 같은 상태가 된다.
            var abyss = new Vector3(0f, LastShiftNetworkSession.CrewFallFloorY - 20f, 0f);
            controller.ResetPlayer(abyss, Quaternion.identity);
            Assert.That(crew.transform.position.y, Is.LessThan(LastShiftNetworkSession.CrewFallFloorY));

            yield return WaitFor(
                () => crew.transform.position.y > LastShiftNetworkSession.CrewFallFloorY, "crew-recovered", 5f);

            var recovered = crew.transform.position;
            Assert.That(recovered.y, Is.EqualTo(LastShiftShipDimensions.SpawnPoint.y).Within(0.3f),
                $"슬롯 자리로 돌아와야 한다. recovered=({recovered.x:F2},{recovered.y:F2},{recovered.z:F2})");
            Assert.That(recovered.x, Is.EqualTo(LastShiftShipDimensions.SpawnPoint.x).Within(0.1f));
        }

        private static IEnumerator WaitFor(
            System.Func<bool> predicate, string phase, float timeoutSeconds = 10f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, $"timed out waiting for {phase}");
        }
    }
}
