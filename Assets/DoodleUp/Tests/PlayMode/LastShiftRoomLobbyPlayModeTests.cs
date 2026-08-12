using System.Collections;
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
    /// 로비 단계의 화면 소유권을 잰다.
    ///
    /// 실제로 났던 실패는 둘이다. <b>하나</b>, 씬에 붙어 있는 HUD(샌드박스 계기·지도 안내)가
    /// 세션이 서기 전부터 <c>OnGUI</c> 를 돌려 로비 위에 겹쳐 나왔다. <b>둘</b>, 카메라는
    /// 승무원 프리팹에만 있어서 아무도 스폰되지 않은 로비 단계에는 씬에 카메라가 0개였고
    /// "No cameras rendering" 경고가 떴다.
    ///
    /// 그래서 여기서 재는 것은 그리기 결과가 아니라 <b>그리기 여부를 가르는 계약</b>
    /// (<see cref="LastShiftRoomLobby.IsBlockingGameplay"/>)과 카메라 존재다. IMGUI 픽셀을
    /// 비교하는 방식은 화면 문구가 바뀔 때마다 같이 깨지고, 정작 "겹쳐 나왔다" 는 사실은
    /// 못 잡는다.
    /// </summary>
    public sealed class LastShiftRoomLobbyPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        private const ushort LobbyTestPort = 7985;
        private const string BackdropName = "LAST_SHIFT_LOBBY_BACKDROP";

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
        public IEnumerator LobbyHoldsTheScreenUntilTheRoomOpens()
        {
            LastShiftNetworkSession.AutoStartHost = true;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            for (var frame = 0; frame < 3; frame++) yield return null;

            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<LastShiftRoomLobby>(FindObjectsInactive.Include), Is.Not.Null,
                "인자 없이 시작한 씬에는 방 코드 로비가 있어야 한다");

            // ── 로비 단계 ────────────────────────────────────────────────────
            Assert.That(session.NetworkManager.IsListening, Is.False, "로비 단계에서는 아직 세션이 서지 않는다");
            Assert.That(LastShiftRoomLobby.IsBlockingGameplay, Is.True, "로비가 화면을 잡고 있어야 HUD 가 물러난다");

            var backdrop = GameObject.Find(BackdropName);
            Assert.That(backdrop, Is.Not.Null, "로비 단계에도 화면을 칠할 카메라가 있어야 한다");
            var backdropCamera = backdrop.GetComponent<Camera>();
            Assert.That(backdropCamera, Is.Not.Null);
            // 판을 비추면 안 된다 — 3D 화면은 방에 들어간 뒤에 나온다.
            Assert.That(backdropCamera.cullingMask, Is.Zero);
            Assert.That(backdropCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(Camera.allCameras.Length, Is.GreaterThan(0), "카메라가 0개면 렌더링 경고가 뜬다");

            // ── 방을 열면 화면이 판으로 넘어간다 ────────────────────────────
            session.OverridePort(LobbyTestPort);
            Assert.That(session.OpenRoom(LastShiftRoomCode.Generate()), Is.True);

            yield return WaitFor(() => !LastShiftRoomLobby.IsBlockingGameplay, "lobby-released");
            yield return WaitFor(() => GameObject.Find(BackdropName) == null, "backdrop-retired");

            // 배경 카메라가 물러난 자리는 비어 있으면 안 된다. 승무원 카메라가 그것을 잇는다.
            var playerCameras = Camera.allCameras
                .Where(camera => camera.GetComponentInParent<LastShiftNetworkPlayer>() != null)
                .ToArray();
            Assert.That(playerCameras.Length, Is.GreaterThan(0), "방에 들어갔으면 승무원 카메라가 화면을 잡는다");
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
