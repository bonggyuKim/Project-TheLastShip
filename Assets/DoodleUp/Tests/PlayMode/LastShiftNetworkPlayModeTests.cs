using System.Collections;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    public sealed class LastShiftNetworkPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";
        private const ushort KeyboardTestPort = 7981;
        private Keyboard testKeyboard;
        private LastShiftPlayerController activeController;
        private InputSettings.UpdateMode previousUpdateMode;
        private Key? injectedKey;

        [SetUp]
        public void AddTestKeyboard()
        {
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            testKeyboard.MakeCurrent();
            previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        }

        /// <summary>
        /// Shutdown 요청 후 UDP 소켓이 실제로 풀리기까지 몇 프레임이 걸린다. 동기 TearDown 으로
        /// 끝내면 다음 테스트의 StartHost 가 같은 포트에 bind 하려다
        /// "address is already in use" 로 실패한다. 그래서 해제를 프레임 단위로 기다린다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator RemoveTestKeyboard()
        {
            activeController = null;
            var manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                var deadline = Time.realtimeSinceStartup + 5f;
                while (manager != null && manager.IsListening && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return null;
            }
            InputSystem.settings.updateMode = previousUpdateMode;
            if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
        }

        [UnityTest]
        public IEnumerator HostStartsOwnsStateAndExercisesHeldItemLifecycle()
        {
            // 이 파일은 자동 host 로 뜬 세션을 전제로 승무원 spawn 을 검사한다. 같은 Play
            // 세션의 시뮬레이션 테스트가 그것을 꺼 두므로, 순서 운에 맡기지 않고 스스로 켠다.
            LastShiftNetworkSession.AutoStartHost = true;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;

            var scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            SceneManager.SetActiveScene(scene);
            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            var sandbox = Object.FindFirstObjectByType<LastShiftSandboxController>(FindObjectsInactive.Include);
            var networkSandbox = Object.FindFirstObjectByType<LastShiftNetworkSandbox>(FindObjectsInactive.Include);
            Assert.That(session, Is.Not.Null);
            Assert.That(sandbox, Is.Not.Null);
            Assert.That(networkSandbox, Is.Not.Null);
            Assert.That(session.StartHost(), Is.True);
            yield return null;

            var player = session.NetworkManager.LocalClient.PlayerObject.GetComponent<LastShiftNetworkPlayer>();
            var controller = player.GetComponent<LastShiftPlayerController>();
            activeController = controller;
            var item = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .First(candidate => !candidate.Grabbable.Secured);
            Assert.That(session.NetworkManager.IsHost, Is.True);
            Assert.That(session.NetworkManager.ConnectedClients.Count, Is.EqualTo(1));
            Assert.That(sandbox.ResetGeneration, Is.EqualTo(1));
            Assert.That(networkSandbox.Snapshot.ResetGeneration, Is.EqualTo(1));
            Assert.That(sandbox.enabled, Is.True);
            Assert.That(controller.enabled, Is.True);
            Assert.That(controller.TargetCamera.enabled, Is.True);
            Assert.That(player.BodyRenderer, Is.Not.Null);
            Assert.That(player.IsBodyVisible, Is.False);
            Assert.That(sandbox.Players, Does.Contain(controller));

            var originalPosition = player.transform.position;
            player.transform.position = item.transform.position + Vector3.back * (LastShiftPlayerController.GrabDistance + 1f);
            UnityEngine.Physics.SyncTransforms();
            Assert.That(player.TryGrabFromServer(player.OwnerClientId, item), Is.False);
            player.transform.position = originalPosition;
            Assert.That(player.TryGrabFromServer(player.OwnerClientId + 1, item), Is.False);

            // 조준은 카메라 transform 이 아니라 조준 상태(pitch)에서 나온다. 충격 흔들림이
            // 서버 grab 판정에 섞이지 않게 하려는 설계라, 카메라 회전을 직접 써도 조준은
            // 움직이지 않는다. 조준을 세울 때는 SetAimPitchForProbe 를 쓴다.
            controller.SetAimPitchForProbe(25f);
            item.transform.position = controller.TargetCamera.transform.position + controller.TargetCamera.transform.forward;
            UnityEngine.Physics.SyncTransforms();
            Assert.That(Vector3.Dot(player.AuthoritativeAimDirection, Vector3.up), Is.GreaterThan(0.35f));
            Assert.That(player.TryGrabFromServer(player.OwnerClientId, item), Is.True);
            Assert.That(item.TryBeginHold(player), Is.False);
            Assert.That(player.HeldItem, Is.SameAs(item));
            Assert.That(item.HolderClientId, Is.EqualTo(player.OwnerClientId));
            Assert.That(item.NetworkObject.OwnerClientId, Is.EqualTo(player.OwnerClientId));

            Assert.That(item.DropFromServer(player, Vector3.forward), Is.True);
            Assert.That(player.HeldItem, Is.Null);
            Assert.That(item.IsClaimed, Is.False);
            Assert.That(item.NetworkObject.OwnerClientId, Is.EqualTo(NetworkManager.ServerClientId));

            Assert.That(item.TryBeginHold(player), Is.True);
            player.transform.position += item.Grabbable.NominalPosition - player.HoldSocket.position;
            item.transform.position = item.Grabbable.NominalPosition;
            UnityEngine.Physics.SyncTransforms();
            Assert.That(item.SecureFromServer(player), Is.True);
            Assert.That(item.IsSecured, Is.True);
            Assert.That(player.HeldItem, Is.Null);

            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            networkSandbox.PublishSnapshot();
            Assert.That(networkSandbox.Snapshot.HasAppliedImpact, Is.True);
            Assert.That(networkSandbox.Snapshot.ImpactApplicationCount, Is.EqualTo(1));

            var resetItem = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None).First();
            resetItem.Grabbable.SetSecured(false);
            resetItem.SyncSecuredFromServer();
            Assert.That(resetItem.TryBeginHold(player), Is.True);
            var generationBeforeReset = sandbox.ResetGeneration;
            player.transform.position += new Vector3(5f, 0f, 0f);
            networkSandbox.ResetPresetFromServer(LastShiftPreset.BadAttitudeHighOxygen);
            yield return null;
            Assert.That(player.HeldItem, Is.Null);
            Assert.That(resetItem.IsClaimed, Is.False);
            Assert.That(resetItem.NetworkObject.OwnerClientId, Is.EqualTo(NetworkManager.ServerClientId));
            var resetOffset = player.transform.position - LastShiftNetworkSession.SpawnForSlot(0);
            Assert.That(Vector2.Distance(new Vector2(resetOffset.x, resetOffset.z), Vector2.zero), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(resetOffset.y), Is.LessThan(0.05f));
            Assert.That(networkSandbox.Snapshot.Preset, Is.EqualTo(LastShiftPreset.BadAttitudeHighOxygen));
            Assert.That(networkSandbox.Snapshot.ResetGeneration, Is.EqualTo(generationBeforeReset + 1));
            Assert.That(networkSandbox.Snapshot.ResetGeneration, Is.EqualTo(sandbox.ResetGeneration));
            session.StopSession();
        }

        [UnityTest]
        public IEnumerator KeyboardInputDrivesNetworkGrabDropSecureAndPresetReset()
        {
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            // 앞 테스트의 NetworkManager 가 완전히 파괴되고 새 씬 인스턴스가 Awake 를 마칠 시간을 준다.
            // 한 프레임만 기다리면 두 번째 StartHost 에서 씬 내 NetworkObject 가 하나도 spawn 되지 않는다.
            for (var frame = 0; frame < 5; frame++) yield return null;

            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            var sandbox = Object.FindFirstObjectByType<LastShiftSandboxController>(FindObjectsInactive.Include);
            var networkSandbox = Object.FindFirstObjectByType<LastShiftNetworkSandbox>(FindObjectsInactive.Include);
            // 앞 테스트가 쓴 포트의 UDP 소켓이 아직 살아 있을 수 있어 다른 포트로 띄운다.
            session.OverridePort(KeyboardTestPort);
            Assert.That(session.StartHost(), Is.True);
            // 씬 내 NetworkObject 는 host 시작 직후 한 프레임에 모두 spawn 되지 않는다.
            yield return WaitFor(
                () => session.NetworkManager.IsListening && session.NetworkManager.LocalClient?.PlayerObject != null,
                "host-player-spawned",
                15f);

            var player = session.NetworkManager.LocalClient.PlayerObject.GetComponent<LastShiftNetworkPlayer>();
            var controller = player.GetComponent<LastShiftPlayerController>();
            activeController = controller;
            // 앞 테스트가 Shutdown 한 직후에는 아이템 NetworkObject 가 아직 재spawn 되지 않았고,
            // 이전 씬의 파괴 예정 인스턴스가 조회에 걸릴 수도 있다. spawn 된 인스턴스를 다시 찾는다.
            LastShiftNetworkGrabbable cooling = null;
            yield return WaitFor(
                () =>
                {
                    cooling = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                        .FirstOrDefault(item =>
                            item.Grabbable != null &&
                            item.Grabbable.Role == LastShiftItemRole.CoolingCanister &&
                            item.NetworkObject != null &&
                            item.NetworkObject.IsSpawned);
                    return cooling != null;
                },
                "cooling-spawned",
                15f,
                () =>
                {
                    var all = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None);
                    return $"items={all.Length} spawned={all.Count(i => i.NetworkObject != null && i.NetworkObject.IsSpawned)} " +
                           $"roles={string.Join(",", all.Select(i => $"{i.Grabbable?.Role}:{(i.NetworkObject != null && i.NetworkObject.IsSpawned ? 1 : 0)}"))} scenes={SceneManager.sceneCount}";
                });
            // Tether 가 시작 사거리 안에 놓이면서 spawn 조준에는 Tether 가 먼저 걸린다.
            // 이 테스트가 보려는 것은 "사거리 밖에서는 아무것도 안 뜬다" 이므로,
            // 다른 loose 아이템이 조준 후보로 끼어들지 않는 자리에서 CoolingCanister 만 조준한다.
            StandOffFacingOnly(player, controller, cooling);
            AimAtItem(controller, cooling);
            // 정조준하고 있어도 사거리(GrabDistance) 밖이면 빈 문자열이다. 예전에는 여기서
            // "접근 필요 1.6m" 가 떴고, 그 단계가 화면을 상시로 채우는 원인이었다.
            Assert.That(controller.InteractionPrompt, Is.Empty,
                "사거리 밖 정조준에서 문장이 뜨면 접근 힌트 단계가 이름만 바꿔 돌아온 것이다.");

            yield return HoldKey(Key.W, 0.5f);

            PositionForKeyboardInteraction(player, controller, cooling);
            Assert.That(controller.InteractionPrompt, Does.Contain("[E]").And.Contain("CoolingCanister"));
            Debug.Log($"[LAST_SHIFT_TEST] phase=pre-grab prompt={controller.InteractionPrompt} coolSpawned={cooling.NetworkObject != null && cooling.NetworkObject.IsSpawned} secured={cooling.IsSecured} claimed={cooling.IsClaimed} coolPos={cooling.transform.position} aimOrigin={controller.AimOrigin}");
            yield return PressAndRelease(Key.E);
            yield return WaitFor(() => player.HeldItem == cooling, "grab-cooling");
            Assert.That(player.HeldItem, Is.SameAs(cooling));

            yield return PressAndRelease(Key.E);
            yield return WaitFor(() => player.HeldItem == null, "drop-cooling");
            Assert.That(player.HeldItem, Is.Null);

            var resetBefore = sandbox.ResetGeneration;
            yield return PressAndRelease(Key.Digit2);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.PowerOverloadLooseBattery, "preset-2");
            Assert.That(sandbox.ResetGeneration, Is.EqualTo(resetBefore + 1));
            var battery = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .Single(item => item.Grabbable.Role == LastShiftItemRole.Battery);
            // 프리셋 리셋 직후에는 아이템 spawn 상태와 secured 복제가 아직 정착되지 않는다.
            yield return WaitFor(() => battery.NetworkObject != null && battery.NetworkObject.IsSpawned && !battery.IsSecured, "battery-spawned-loose");
            PositionForKeyboardInteraction(player, controller, battery);
            yield return PressAndRelease(Key.E);
            yield return WaitFor(() => player.HeldItem == battery, "grab-battery");
            // 한 프레임 옮기고 바로 F 를 누르면 간헐적으로 실패했다(카드 0fb18e77).
            // 진단 훅이 잡은 실패 상태는 distToNominal=1.02 였고 SecureDistance 는 0.9 다 —
            // 유령도 소유권도 아니고 <b>배터리가 아직 제자리에 안 들어와 있었다</b>.
            //
            // 플레이어에 CharacterController 가 붙어 있어 transform 을 직접 밀면 그 프레임에
            // 그대로 서지 않고, 들고 있는 물건은 소켓을 따라오므로 오차가 물건 위치에 그대로
            // 남는다. 그래서 한 번 밀고 마는 대신 <b>실제로 들어올 때까지</b> 민다.
            yield return WaitFor(
                () =>
                {
                    player.transform.position += battery.Grabbable.NominalPosition - player.HoldSocket.position;
                    UnityEngine.Physics.SyncTransforms();
                    return Vector3.Distance(battery.transform.position, battery.Grabbable.NominalPosition)
                           <= LastShiftSandboxController.SecureDistance * 0.5f;
                },
                "battery-at-nominal",
                diagnostics: () =>
                    $"dist={Vector3.Distance(battery.transform.position, battery.Grabbable.NominalPosition):F2} " +
                    $"limit={LastShiftSandboxController.SecureDistance:F2}");
            yield return PressAndRelease(Key.F);
            // 위 경쟁이 잡히기 전까지 이 대기가 타임아웃으로만 보였다. 진단 훅은 남겨 둔다 —
            // 다음에 다른 이유로 떨어지면 그때도 한 줄로 갈린다.
            yield return WaitFor(() => player.HeldItem == null && battery.IsSecured, "secure-battery",
                diagnostics: () =>
                {
                    var crew = controller.GetComponent<LastShiftCrewOxygen>();
                    var nominal = battery.Grabbable.NominalPosition;
                    return $"ghost={controller.IsGhost} dead={(crew != null ? crew.IsDead.ToString() : "n/a")} " +
                           $"suitO2={(crew != null ? crew.SuitOxygen.ToString("F2") : "n/a")} " +
                           $"held={(player.HeldItem != null ? player.HeldItem.name : "null")} " +
                           $"secured={battery.IsSecured} grabbableSecured={battery.Grabbable.Secured} " +
                           $"distToNominal={Vector3.Distance(battery.transform.position, nominal):F2} " +
                           $"impact={sandbox.HasAppliedImpact} resolved={sandbox.IsResolved} " +
                           $"bus={sandbox.CurrentState.BusPower:F2}";
                });
            Assert.That(battery.IsSecured, Is.True);

            yield return PressAndRelease(Key.Digit1);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.HighHeatHighThrust, "preset-1");
            yield return PressAndRelease(Key.Digit3);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.BadAttitudeHighOxygen, "preset-3");
            resetBefore = sandbox.ResetGeneration;
            yield return PressAndRelease(Key.R);
            yield return WaitFor(() => sandbox.ResetGeneration == resetBefore + 1, "reset-r");
            Assert.That(networkSandbox.Snapshot.Preset, Is.EqualTo(LastShiftPreset.BadAttitudeHighOxygen));

            // M(운석). <b>이 줄이 없어서 M 이 host 씬에서 아무 데서도 안 먹었다</b> —
            // 서버 RPC 는 있는데 부르는 곳이 없었고, LastShiftSandboxController 의 키 블록은
            // 네트워크 샌드박스가 스폰되면 통째로 꺼진다. 사용자 플레이에서 잡혔다.
            // 다른 키와 달리 M 은 여기서만 검사되므로 이 assertion 이 유일한 방벽이다.
            Assert.That(sandbox.HasAppliedImpact, Is.False, "리셋 직후에는 사건 전이어야 한다.");
            yield return PressAndRelease(Key.M);
            yield return WaitFor(() => sandbox.HasAppliedImpact, "meteor-m",
                diagnostics: () => $"impact={sandbox.HasAppliedImpact} count={sandbox.ImpactApplicationCount} " +
                                   $"ghost={controller.IsGhost} spawned={networkSandbox.IsSpawned}");
            Assert.That(sandbox.ImpactApplicationCount, Is.EqualTo(1));

            session.StopSession();
        }

        private IEnumerator PressAndRelease(Key key)
        {
            yield return ReleaseKeys();
            yield return PressKey(key);
            yield return ReleaseKeys();
        }

        private IEnumerator HoldKey(Key key, float seconds)
        {
            injectedKey = key;
            var deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                ProcessInjectedKey();
                yield return null;
            }
            yield return ReleaseKeys();
        }

        private IEnumerator PressKey(Key key)
        {
            injectedKey = key;
            ProcessInjectedKey();
            yield return null;
        }

        private IEnumerator ReleaseKeys()
        {
            injectedKey = null;
            ProcessInjectedKey();
            yield return null;
        }

        private void ProcessInjectedKey()
        {
            InputSystem.QueueStateEvent(testKeyboard,
                injectedKey.HasValue
                    ? new UnityEngine.InputSystem.LowLevel.KeyboardState(injectedKey.Value)
                    : new UnityEngine.InputSystem.LowLevel.KeyboardState());
            testKeyboard.MakeCurrent();
            InputSystem.Update();
            activeController?.ProcessKeyboardInput(testKeyboard, 1f / 60f);
        }

        private static IEnumerator WaitFor(
            System.Func<bool> predicate,
            string phase = null,
            float timeoutSeconds = 5f,
            System.Func<string> diagnostics = null)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!predicate() && diagnostics != null)
                Debug.Log($"[LAST_SHIFT_TEST] phase={phase} timeout diagnostics={diagnostics()}");
            Assert.That(predicate(), Is.True, phase != null ? $"timed out waiting for {phase}" : null);
        }

        private static void AimAtItem(LastShiftPlayerController controller, LastShiftNetworkGrabbable item)
        {
            var target = item.GetComponentInChildren<Collider>().bounds.center;
            // 조준은 카메라 transform 이 아니라 조준 상태(yaw/pitch)에서 나온다. 카메라
            // world rotation 을 직접 쓰면 조준이 갱신되지 않으므로 정식 경로를 쓴다.
            controller.SetAimDirectionForProbe(target - controller.AimOrigin);
            UnityEngine.Physics.SyncTransforms();
        }

        /// <summary>
        /// 대상 아이템만 조준 후보가 되는 자리에 선다. 대상은 사거리 밖에 두어 <b>정조준해도
        /// 아무것도 안 뜬다</b>를 확인할 수 있게 하고, 다른 loose 아이템이 조준선 근처에
        /// 들어오지 않는 방향을 고른다. 아이템의 collider 나 Rigidbody 를 건드리면 loose 물체가
        /// 낙하하거나 nominal 이 흔들려 이후 grab·secure 단계가 깨지므로, 플레이어 배치만으로
        /// 격리한다.
        /// </summary>
        /// <summary>설 자리를 고를 때 다른 loose 아이템을 훑는 범위. 표시 판정과 무관하다.</summary>
        private const float StandOffScanRange = 8f;

        private static void StandOffFacingOnly(
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller,
            LastShiftNetworkGrabbable item)
        {
            var target = item.GetComponentInChildren<Collider>().bounds.center;
            var others = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .Where(candidate => candidate != item && !candidate.Grabbable.Secured)
                .ToArray();
            var cameraHeight = controller.TargetCamera.transform.localPosition.y;
            var standoff = LastShiftPlayerController.GrabDistance + 1.6f;

            var bestPosition = target - Vector3.forward * standoff - Vector3.up * cameraHeight;
            var bestPenalty = float.PositiveInfinity;
            for (var degrees = 0; degrees < 360; degrees += 10)
            {
                var direction = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                var candidatePosition = target - direction * standoff - Vector3.up * cameraHeight;
                var aimOrigin = candidatePosition + Vector3.up * cameraHeight;
                var aim = (target - aimOrigin).normalized;
                var penalty = 0f;
                foreach (var other in others)
                {
                    var offset = other.transform.position - aimOrigin;
                    // 자리 고르기용 훑는 범위일 뿐 프롬프트 사거리가 아니다. 프롬프트 쪽의
                    // 8m 접근 힌트 상수는 제거됐고, 여기 값은 "이 방 안쪽" 정도의 의미다.
                    if (offset.magnitude > StandOffScanRange) continue;
                    penalty += Mathf.Max(0f, Vector3.Dot(aim, offset.normalized));
                }
                if (penalty >= bestPenalty) continue;
                bestPenalty = penalty;
                bestPosition = candidatePosition;
                if (penalty <= 0f) break;
            }

            var forward = (target - (bestPosition + Vector3.up * cameraHeight)).normalized;
            controller.ResetPlayer(bestPosition, Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up).normalized));
            UnityEngine.Physics.SyncTransforms();
        }

        /// <summary>
        /// 부품 앞 1.5m 에 세우고 조준시킨다. <b>다가서는 쪽은 언제나 방 안쪽이다</b> —
        /// 광장 쪽에서 접근하면 문 앞에 서게 되고, 문 프롬프트가 아이템 프롬프트보다 앞이라
        /// (<c>LastShiftPlayerController.BuildInteractionPrompt</c>) 잡기 안내가 가려진다.
        /// 방사형에서는 문이 전부 광장을 보고 있으므로 "광장 반대편" 이 곧 방 안쪽이다.
        /// 일자 스파인 시절에는 부품이 문에서 멀어 서 있던 쪽이 문제가 안 됐다.
        /// </summary>
        private static void PositionForKeyboardInteraction(
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller,
            LastShiftNetworkGrabbable item)
        {
            var target = item.GetComponentInChildren<Collider>().bounds.center;
            var cameraTransform = controller.TargetCamera.transform;
            var outward = Vector3.ProjectOnPlane(target, Vector3.up).normalized;
            var forward = -outward;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            player.transform.SetPositionAndRotation(
                target - forward * 1.5f - Vector3.up * cameraTransform.localPosition.y,
                Quaternion.LookRotation(forward));
            controller.ResetPlayer(player.transform.position, player.transform.rotation);
            AimAtItem(controller, item);
            UnityEngine.Physics.SyncTransforms();
        }
    }
}
