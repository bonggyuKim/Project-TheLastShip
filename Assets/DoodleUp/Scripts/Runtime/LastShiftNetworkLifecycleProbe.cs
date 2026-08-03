using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DoodleUp.Runtime
{
    public sealed class LastShiftNetworkLifecycleProbe : MonoBehaviour
    {
        private bool enabledByCommandLine;

        private void Start()
        {
            enabledByCommandLine = HasArgument("-lastShiftLifecycleProbe");
            if (enabledByCommandLine) StartCoroutine(RunClientProbe());
        }

        private IEnumerator RunClientProbe()
        {
            yield return null;
            var manager = GetComponent<NetworkManager>();
            var observedListening = manager != null && manager.IsListening;
            var connectDeadline = Time.realtimeSinceStartup + 12f;
            while (manager != null && !manager.IsConnectedClient && Time.realtimeSinceStartup < connectDeadline)
            {
                if (manager.IsListening) observedListening = true;
                else if (observedListening) break;
                yield return null;
            }
            if (manager == null || !manager.IsConnectedClient)
            {
                Debug.LogError("[LAST_SHIFT_LIFECYCLE_PROBE] phase=connect terminal=timeout-or-stopped result=FAIL");
                yield break;
            }
            var spawnDeadline = Time.realtimeSinceStartup + 12f;
            while (manager.IsConnectedClient && manager.IsListening &&
                   (manager.LocalClient == null || manager.LocalClient.PlayerObject == null) &&
                   Time.realtimeSinceStartup < spawnDeadline)
                yield return null;
            if (!manager.IsConnectedClient || !manager.IsListening || manager.LocalClient == null || manager.LocalClient.PlayerObject == null)
            {
                Debug.LogError("[LAST_SHIFT_LIFECYCLE_PROBE] phase=player-spawn terminal=timeout-disconnect-or-stopped result=FAIL");
                yield break;
            }
            var player = manager.LocalClient.PlayerObject.GetComponent<LastShiftNetworkPlayer>();
            var controller = player.GetComponent<LastShiftPlayerController>();
            controller.SetCursorManagement(false);
            controller.enabled = false;
            var networkSandbox = FindFirstObjectByType<LastShiftNetworkSandbox>();
            if (HasArgument("-lastShiftKeyboardLifecycleProbe"))
            {
                yield return RunKeyboardLifecycleProbe(manager, player, controller, networkSandbox);
                yield break;
            }
            if (HasArgument("-lastShiftPresetSyncProbe"))
            {
                yield return RunClientPresetSyncProbe(manager, player, controller, networkSandbox);
                yield break;
            }
            if (HasArgument("-lastShiftContentionOnlyProbe"))
            {
                // 경합·전달만 본다. 앞 단계의 R 리셋이 host 점유를 해제해 경합 상황을 무너뜨리므로
                // 이 시나리오에서는 aim/reset 단계를 실행하지 않는다.
                yield return RunContentionAndHandoffProbe(manager, player, controller);
                yield break;
            }
            if (HasArgument("-lastShiftClientAimResetProbe"))
            {
                yield return RunClientAimAndResetProbe(manager, player, controller, networkSandbox);
                yield break;
            }
            var verifiesInitialGeneration = !HasArgument("-lastShiftObserverOnlyProbe");
            var startupGenerationPass = networkSandbox != null && (!verifiesInitialGeneration || networkSandbox.Snapshot.ResetGeneration == 1);
            var visualDeadline = Time.realtimeSinceStartup + 12f;
            LastShiftNetworkPlayer remotePlayer = null;
            while (manager.IsConnectedClient && manager.IsListening && Time.realtimeSinceStartup < visualDeadline)
            {
                remotePlayer = FindObjectsByType<LastShiftNetworkPlayer>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate != null && candidate != player && candidate.IsBodyVisible);
                if (remotePlayer != null) break;
                yield return null;
            }
            var ownerVisualHidden = !player.IsBodyVisible;
            var remoteVisualVisible = remotePlayer != null;
            var colorsDistinct = remotePlayer != null && ColorDistance(player.PlayerColor, remotePlayer.PlayerColor) > 0.1f;
            var presentationPass = player.IsOwner && controller.TargetCamera.enabled && ownerVisualHidden && remoteVisualVisible && colorsDistinct && startupGenerationPass;
            Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} ownerController={player.IsOwner} camera={controller.TargetCamera.enabled} ownerVisualHidden={ownerVisualHidden} remoteVisualVisible={remoteVisualVisible} colorsDistinct={colorsDistinct} startupGeneration={networkSandbox?.Snapshot.ResetGeneration} verifiesInitialGeneration={verifiesInitialGeneration} phase=owner-presentation result={(presentationPass ? "PASS" : "FAIL")}");
            if (manager.IsServer)
            {
                if (remotePlayer != null) yield return VerifyRemoteVisualTransform(manager, remotePlayer);
                if (HasArgument("-lastShiftVisualOnlyProbe")) yield return QuitProbe(manager, 2f);
                yield break;
            }
            if (HasArgument("-lastShiftVisualMotionProbe"))
            {
                StageOwnerMotionForRemoteVisual(player);
                if (HasArgument("-lastShiftVisualOnlyProbe"))
                {
                    yield return QuitProbe(manager, 4f);
                    yield break;
                }
            }
            if (HasArgument("-lastShiftObserverOnlyProbe"))
            {
                var observerDeadline = Time.realtimeSinceStartup + 12f;
                LastShiftNetworkGrabbable observedItem = null;
                while (manager.IsConnectedClient && manager.IsListening && Time.realtimeSinceStartup < observerDeadline)
                {
                    observedItem = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                        .FirstOrDefault(candidate => candidate != null && candidate.IsClaimed && candidate.Grabbable.IsHeld && candidate.HasResolvedHolder);
                    if (observedItem != null) break;
                    yield return null;
                }
                Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} phase=held-observer-restore holderResolved={observedItem != null} heldPhysics={observedItem != null && observedItem.Grabbable.IsHeld} result={(observedItem != null ? "PASS" : "FAIL")}");
                if (observedItem != null && HasArgument("-lastShiftObserveReleaseProbe"))
                {
                    var observedHolder = manager.ConnectedClients.TryGetValue(observedItem.HolderClientId, out var observedClient) && observedClient.PlayerObject != null
                        ? observedClient.PlayerObject.GetComponent<LastShiftNetworkPlayer>()
                        : null;
                    var releaseDeadline = Time.realtimeSinceStartup + 12f;
                    while (manager.IsConnectedClient && manager.IsListening && observedItem.IsClaimed && Time.realtimeSinceStartup < releaseDeadline)
                        yield return null;
                    var staleCleared = !observedItem.IsClaimed && observedHolder != null && observedHolder.HeldItem != observedItem;
                    Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} phase=held-observer-release staleCleared={staleCleared} result={(staleCleared ? "PASS" : "FAIL")}");
                }
                if (HasArgument("-lastShiftDisconnectAfterProbe")) manager.Shutdown();
                yield break;
            }

            var positionBeforeReset = player.transform.position;
            networkSandbox.RequestPresetResetRpc(LastShiftPreset.PowerOverloadLooseBattery);
            yield return new WaitForSecondsRealtime(0.75f);
            var resetPositionError = Vector3.Distance(player.transform.position, positionBeforeReset);
            var item = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && !candidate.Grabbable.Secured);
            if (item == null)
            {
                Debug.LogError("[LAST_SHIFT_LIFECYCLE_PROBE] phase=find-item result=FAIL");
                yield break;
            }

            PositionPlayerForInteraction(player, controller, item, 0.35f);
            AimAtItem(controller, item);
            yield return new WaitForSecondsRealtime(0.75f);
            var verticalAim = Vector3.Dot(player.AuthoritativeAimDirection, Vector3.up);
            Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} phase=vertical-aim vertical={verticalAim:F2} result={(Mathf.Abs(verticalAim) >= 0.2f ? "PASS" : "FAIL")}");
            player.RequestGrab(item);
            yield return WaitFor(() => player.HeldItem != null, "grab");
            if (player.HeldItem == null) yield break;
            controller.TargetCamera.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            var ownerPoseError = Vector3.Distance(player.HoldPosition, controller.HoldSocket.position);
            var ownerRotationError = Quaternion.Angle(player.HoldRotation, controller.HoldSocket.rotation);
            yield return null;
            var heldFollowError = Vector3.Distance(item.transform.position, controller.HoldSocket.position);
            var ownerPosePass = ownerPoseError < 0.001f && ownerRotationError < 0.01f && heldFollowError < 0.02f;
            Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} phase=owner-local-hold-pose poseError={ownerPoseError:F4} rotationError={ownerRotationError:F3} followError={heldFollowError:F4} result={(ownerPosePass ? "PASS" : "FAIL")}");
            if (HasArgument("-lastShiftHoldForObserverProbe"))
            {
                Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} phase=hold-ready result=PASS");
                if (HasArgument("-lastShiftReleaseForObserverProbe"))
                {
                    yield return new WaitForSecondsRealtime(5f);
                    player.RequestDrop(Vector3.zero);
                    yield return WaitFor(() => player.HeldItem == null, "holder-release-for-observer");
                    Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} phase=holder-release-for-observer result={(player.HeldItem == null ? "PASS" : "FAIL")}");
                    yield break;
                }
                var holdDeadline = Time.realtimeSinceStartup + 15f;
                while (manager.IsConnectedClient && manager.IsListening && Time.realtimeSinceStartup < holdDeadline)
                    yield return null;
                yield break;
            }

            player.RequestDrop(Vector3.zero);
            yield return WaitFor(() => player.HeldItem == null, "drop");
            if (player.HeldItem != null) yield break;

            networkSandbox.RequestPresetResetRpc(LastShiftPreset.HighHeatHighThrust);
            yield return new WaitForSecondsRealtime(0.75f);
            resetPositionError = Mathf.Max(resetPositionError, Vector3.Distance(player.transform.position, positionBeforeReset));
            var droppedItem = item;
            item = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate != droppedItem && !candidate.Grabbable.Secured);
            if (item == null)
            {
                Debug.LogError("[LAST_SHIFT_LIFECYCLE_PROBE] phase=find-different-item-after-drop result=FAIL");
                yield break;
            }

            PositionPlayerForInteraction(player, controller, item);
            AimAtItem(controller, item);
            yield return new WaitForSecondsRealtime(0.75f);
            player.RequestGrab(item);
            yield return WaitFor(() => player.HeldItem == item, "different-grab-after-drop");
            if (player.HeldItem != item) yield break;

            player.transform.position += item.Grabbable.NominalPosition - controller.HoldSocket.position;
            UnityEngine.Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.75f);
            player.RequestSecureHeldItem();
            yield return WaitFor(() => player.HeldItem == null && item.IsSecured, "secure");
            if (player.HeldItem != null || !item.IsSecured) yield break;

            networkSandbox.RequestPresetResetRpc(LastShiftPreset.BadAttitudeHighOxygen);
            yield return new WaitForSecondsRealtime(0.75f);
            resetPositionError = Mathf.Max(resetPositionError, Vector3.Distance(player.transform.position, positionBeforeReset));
            var securedItem = item;
            item = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate != securedItem && !candidate.Grabbable.Secured);
            if (item == null)
            {
                Debug.LogError("[LAST_SHIFT_LIFECYCLE_PROBE] phase=find-different-item-after-secure result=FAIL");
                yield break;
            }

            PositionPlayerForInteraction(player, controller, item);
            AimAtItem(controller, item);
            yield return new WaitForSecondsRealtime(0.75f);
            player.RequestGrab(item);
            yield return WaitFor(() => player.HeldItem == item, "different-grab-after-secure");
            if (player.HeldItem != item) yield break;
            player.RequestDrop(Vector3.zero);
            yield return WaitFor(() => player.HeldItem == null, "final-drop");
            if (player.HeldItem != null) yield break;

            Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} phases=grab,drop,different-grab,secure,different-grab,drop presetResetPositionError={resetPositionError:F2} result={(resetPositionError < 0.1f ? "PASS" : "FAIL")}");
            if (HasArgument("-lastShiftDisconnectAfterProbe")) manager.Shutdown();
        }

        private static IEnumerator RunKeyboardLifecycleProbe(
            NetworkManager manager,
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller,
            LastShiftNetworkSandbox networkSandbox)
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            var cooling = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.Grabbable.Role == LastShiftItemRole.CoolingCanister);
            if (cooling == null || networkSandbox == null)
            {
                Debug.LogError("[LAST_SHIFT_KEYBOARD_PROBE] phase=setup result=FAIL");
                yield break;
            }

            AimAtItem(controller, cooling);
            var initialPrompt = controller.InteractionPrompt;
            var initialPromptPass = initialPrompt.Contains("접근 필요");
            QueueKey(keyboard, Key.W, true);
            controller.ProcessKeyboardInput(keyboard, 1f / 60f);
            QueueKey(keyboard, Key.W, false);
            controller.ProcessKeyboardInput(keyboard, 1f / 60f);
            PositionPlayerForInteraction(player, controller, cooling);
            controller.ResetPlayer(player.transform.position, player.transform.rotation);
            AimAtItem(controller, cooling);
            yield return new WaitForSecondsRealtime(0.25f);
            AimAtItem(controller, cooling);
            UnityEngine.Physics.SyncTransforms();
            var approachPromptPass = controller.InteractionPrompt.Contains("[E]");
            Debug.Log($"[LAST_SHIFT_KEYBOARD_PROBE] phase=approach movementKeyPath=True prompt={controller.InteractionPrompt} distance={Vector3.Distance(player.transform.position, cooling.transform.position):F2} result={(approachPromptPass ? "PASS" : "FAIL")}");

            yield return PressAndRelease(controller, keyboard, Key.E);
            yield return WaitFor(() => player.HeldItem == cooling, "keyboard-grab");
            var grabPass = player.HeldItem == cooling;
            yield return PressAndRelease(controller, keyboard, Key.E);
            yield return WaitFor(() => player.HeldItem == null, "keyboard-drop");
            var dropPass = player.HeldItem == null;

            var resetGeneration = networkSandbox.Snapshot.ResetGeneration;
            yield return PressAndRelease(controller, keyboard, Key.Digit2);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.PowerOverloadLooseBattery, "keyboard-reset-2");
            var battery = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .First(candidate => candidate.Grabbable.Role == LastShiftItemRole.Battery);
            PositionPlayerForInteraction(player, controller, battery);
            controller.ResetPlayer(player.transform.position, player.transform.rotation);
            AimAtItem(controller, battery);
            yield return new WaitForSecondsRealtime(0.25f);
            AimAtItem(controller, battery);
            yield return PressAndRelease(controller, keyboard, Key.E);
            yield return WaitFor(() => player.HeldItem == battery, "keyboard-different-grab");
            var differentGrabPass = player.HeldItem == battery;
            player.transform.position += battery.Grabbable.NominalPosition - controller.HoldSocket.position;
            UnityEngine.Physics.SyncTransforms();
            yield return null;
            yield return PressAndRelease(controller, keyboard, Key.F);
            yield return WaitFor(() => player.HeldItem == null && battery.IsSecured, "keyboard-secure");
            var securePass = player.HeldItem == null && battery.IsSecured;

            yield return PressAndRelease(controller, keyboard, Key.Digit1);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.HighHeatHighThrust, "keyboard-reset-1");
            yield return PressAndRelease(controller, keyboard, Key.Digit3);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.BadAttitudeHighOxygen, "keyboard-reset-3");
            resetGeneration = networkSandbox.Snapshot.ResetGeneration;
            yield return PressAndRelease(controller, keyboard, Key.R);
            yield return WaitFor(() => networkSandbox.Snapshot.ResetGeneration == resetGeneration + 1, "keyboard-reset-r");

            var patch = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .First(candidate => candidate.Grabbable.Role == LastShiftItemRole.PatchPlate);
            PositionPlayerForInteraction(player, controller, patch);
            controller.ResetPlayer(player.transform.position, player.transform.rotation);
            AimAtItem(controller, patch);
            yield return new WaitForSecondsRealtime(0.25f);
            AimAtItem(controller, patch);
            yield return PressAndRelease(controller, keyboard, Key.E);
            yield return WaitFor(() => player.HeldItem == patch, "keyboard-regrab-after-reset");
            var regrabPass = player.HeldItem == patch;
            var resetPass = networkSandbox.Snapshot.Preset == LastShiftPreset.BadAttitudeHighOxygen &&
                            networkSandbox.Snapshot.ResetGeneration >= resetGeneration + 1;
            var pass = initialPromptPass && approachPromptPass && grabPass && dropPass && differentGrabPass &&
                       securePass && resetPass && regrabPass;
            Debug.Log($"[LAST_SHIFT_KEYBOARD_PROBE] client={manager.LocalClientId} initialPrompt={initialPromptPass} approachPrompt={approachPromptPass} grab={grabPass} drop={dropPass} differentGrab={differentGrabPass} secure={securePass} keys123R={resetPass} regrab={regrabPass} result={(pass ? "PASS" : "FAIL")}");
            if (manager.IsListening) manager.Shutdown();
            yield return null;
            Application.Quit(pass ? 0 : 1);
        }

        /// <summary>
        /// SP-03 원격 클라이언트 입력 검증. 1/2/3/R 실제 Keyboard 경로가 서버 권위 reset 을
        /// 거쳐 host 에도 같은 preset/generation 으로 보이는지 client 쪽에서 확인한다.
        /// </summary>
        private IEnumerator RunClientPresetSyncProbe(
            NetworkManager manager,
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller,
            LastShiftNetworkSandbox networkSandbox)
        {
            if (manager.IsServer)
            {
                Debug.Log($"[LAST_SHIFT_SP03_PROBE] client={manager.LocalClientId} phase=skipped reason=host-side result=PASS");
                yield break;
            }

            var keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            var previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;

            var pass = true;
            var expected = new[]
            {
                (Key.Digit2, LastShiftPreset.PowerOverloadLooseBattery, "2"),
                (Key.Digit3, LastShiftPreset.BadAttitudeHighOxygen, "3"),
                (Key.Digit1, LastShiftPreset.HighHeatHighThrust, "1")
            };
            foreach (var step in expected)
            {
                var generationBefore = networkSandbox.Snapshot.ResetGeneration;
                yield return PressAndRelease(controller, keyboard, step.Item1);
                yield return WaitFor(
                    () => networkSandbox.Snapshot.Preset == step.Item2 &&
                          networkSandbox.Snapshot.ResetGeneration > generationBefore,
                    $"sp03-preset-{step.Item3}");
                var stepPass = networkSandbox.Snapshot.Preset == step.Item2 &&
                               networkSandbox.Snapshot.ResetGeneration > generationBefore;
                pass &= stepPass;
                Debug.Log($"[LAST_SHIFT_SP03_PROBE] client={manager.LocalClientId} key={step.Item3} preset={networkSandbox.Snapshot.Preset} generation={networkSandbox.Snapshot.ResetGeneration} result={(stepPass ? "PASS" : "FAIL")}");
            }

            var resetGeneration = networkSandbox.Snapshot.ResetGeneration;
            var resetPreset = networkSandbox.Snapshot.Preset;
            yield return PressAndRelease(controller, keyboard, Key.R);
            yield return WaitFor(
                () => networkSandbox.Snapshot.ResetGeneration > resetGeneration,
                "sp03-preset-r");
            var resetPass = networkSandbox.Snapshot.Preset == resetPreset &&
                            networkSandbox.Snapshot.ResetGeneration > resetGeneration;
            pass &= resetPass;
            Debug.Log($"[LAST_SHIFT_SP03_PROBE] client={manager.LocalClientId} key=R preset={networkSandbox.Snapshot.Preset} generation={networkSandbox.Snapshot.ResetGeneration} preservedPreset={resetPreset} result={(resetPass ? "PASS" : "FAIL")}");
            Debug.Log($"[LAST_SHIFT_SP03_PROBE] client={manager.LocalClientId} phase=summary serverAuthoritativePresetSync={pass} result={(pass ? "PASS" : "FAIL")}");

            InputSystem.settings.updateMode = previousUpdateMode;
            if (keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (manager.IsListening) manager.Shutdown();
            yield return null;
            Application.Quit(pass ? 0 : 1);
        }

        /// <summary>
        /// SP-02 원격 클라이언트 검증. host 단독으로는 재현되지 않는 두 결함만 좁혀서 본다.
        /// 1) 아래를 조준한 grab 이 서버에서 수락되는지 (조준 origin/direction 복제)
        /// 2) R 리셋 위치가 owner-authoritative transform 때문에 되돌아오지 않는지
        /// </summary>
        private IEnumerator RunClientAimAndResetProbe(
            NetworkManager manager,
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller,
            LastShiftNetworkSandbox networkSandbox)
        {
            if (manager.IsServer)
            {
                Debug.Log($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=skipped reason=host-side result=PASS");
                yield break;
            }

            var cooling = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.Grabbable.Role == LastShiftItemRole.CoolingCanister);
            if (cooling == null)
            {
                Debug.LogError($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=find-item result=FAIL");
                yield return QuitProbe(manager);
                yield break;
            }

            // 아이템보다 높은 지점에 서서 확실히 아래로 내려다보게 만든다.
            PositionPlayerForInteraction(player, controller, cooling, -0.55f);
            AimAtItem(controller, cooling);
            yield return new WaitForSecondsRealtime(0.9f);
            AimAtItem(controller, cooling);
            UnityEngine.Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.4f);

            var verticalAim = Vector3.Dot(controller.AimDirection, Vector3.up);
            var aimsDownward = verticalAim <= -0.2f;
            player.RequestGrab(cooling);
            yield return WaitFor(() => player.HeldItem == cooling, "sp02-downward-grab");
            var downwardGrabPass = player.HeldItem == cooling;
            Debug.Log($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=downward-aim-grab verticalAim={verticalAim:F3} aimsDownward={aimsDownward} held={(player.HeldItem != null ? player.HeldItem.Grabbable.Role.ToString() : "none")} result={(aimsDownward && downwardGrabPass ? "PASS" : "FAIL")}");

            player.RequestDrop(Vector3.zero);
            yield return WaitFor(() => player.HeldItem == null, "sp02-drop");

            // 리셋 위치 복원. 슬롯 위치에서 멀리 떨어진 뒤 R 로 리셋하고, 되돌아오지 않는지 유지 확인한다.
            var slotPosition = LastShiftNetworkSession.SpawnForSlot(1);
            var displaced = slotPosition + new Vector3(2.4f, 0f, 1.1f);
            player.transform.position = displaced;
            UnityEngine.Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.6f);
            var displacedDistance = Vector3.Distance(player.transform.position, slotPosition);

            var generationBefore = networkSandbox != null ? networkSandbox.Snapshot.ResetGeneration : 0;
            player.RequestCurrentPresetReset();
            yield return WaitFor(
                () => networkSandbox != null && networkSandbox.Snapshot.ResetGeneration > generationBefore,
                "sp02-reset-generation");
            yield return WaitFor(() => Vector3.Distance(player.transform.position, slotPosition) < 0.6f, "sp02-reset-position");
            var resetApplied = Vector3.Distance(player.transform.position, slotPosition) < 0.6f;

            // owner 가 이전 위치를 계속 송신해 리셋이 되돌아가는지 확인하기 위해 유지 여부를 본다.
            yield return new WaitForSecondsRealtime(1.5f);
            var resetHeld = Vector3.Distance(player.transform.position, slotPosition) < 0.6f;
            Debug.Log($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=reset-position displacedDistance={displacedDistance:F2} resetApplied={resetApplied} resetHeldAfterDelay={resetHeld} finalDistance={Vector3.Distance(player.transform.position, slotPosition):F2} result={(resetApplied && resetHeld ? "PASS" : "FAIL")}");

            var pass = aimsDownward && downwardGrabPass && resetApplied && resetHeld;
            Debug.Log($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=summary downwardGrab={downwardGrabPass} resetPosition={(resetApplied && resetHeld)} result={(pass ? "PASS" : "FAIL")}");
            if (HasArgument("-lastShiftContentionProbe"))
            {
                yield return RunContentionAndHandoffProbe(manager, player, controller);
                yield break;
            }
            if (manager.IsListening) manager.Shutdown();
            yield return null;
            Application.Quit(pass ? 0 : 1);
        }

        /// <summary>
        /// 같은 물건 동시 잡기 경합과 전달. host 가 먼저 점유한 물건을 client 가 노려 거부되고,
        /// host 가 놓은 뒤에는 같은 물건의 소유권이 client 로 넘어가는지 확인한다.
        /// </summary>
        private IEnumerator RunContentionAndHandoffProbe(
            NetworkManager manager,
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller)
        {
            var tether = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.Grabbable.Role == LastShiftItemRole.Tether);
            if (tether == null)
            {
                Debug.LogError($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=contention-find-item result=FAIL");
                yield return QuitProbe(manager);
                yield break;
            }

            // host 가 Tether 를 먼저 잡을 시간을 준 뒤 같은 물건을 요청한다.
            var claimDeadline = Time.realtimeSinceStartup + 12f;
            while (manager.IsConnectedClient && !tether.IsClaimed && Time.realtimeSinceStartup < claimDeadline)
                yield return null;
            var hostClaimed = tether.IsClaimed;

            // host 가 들고 있는 동안은 물건이 host 손 위치로 따라다닌다. 그 실제 위치를 조준해
            // 사거리·조준 판정을 통과시킨 뒤 서버가 점유 사유로 거부하는지 본다.
            PositionPlayerForInteraction(player, controller, tether);
            AimAtItem(controller, tether);
            yield return new WaitForSecondsRealtime(0.7f);
            PositionPlayerForInteraction(player, controller, tether);
            AimAtItem(controller, tether);
            UnityEngine.Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.3f);
            var aimedAtHeldItem = LastShiftPlayerController.TryResolveGrabTarget(
                controller.AimOrigin,
                controller.AimDirection,
                out var contendedTarget,
                out _) && contendedTarget == tether;
            player.RequestGrab(tether);
            yield return new WaitForSecondsRealtime(1.2f);
            var contentionRejected = hostClaimed && aimedAtHeldItem && player.HeldItem != tether;
            Debug.Log($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=contention hostClaimed={hostClaimed} aimedAtHeldItem={aimedAtHeldItem} clientHeld={(player.HeldItem != null ? player.HeldItem.Grabbable.Role.ToString() : "none")} rejection={controller.InteractionPrompt} result={(contentionRejected ? "PASS" : "FAIL")}");

            // host 가 놓으면 같은 물건을 client 가 잡을 수 있어야 한다.
            var releaseDeadline = Time.realtimeSinceStartup + 15f;
            while (manager.IsConnectedClient && tether.IsClaimed && Time.realtimeSinceStartup < releaseDeadline)
                yield return null;
            var hostReleased = !tether.IsClaimed;
            // 놓인 물건은 낙하·복제 지연으로 위치가 계속 변한다. 안정될 시간을 준 뒤 재배치·재조준한다.
            yield return new WaitForSecondsRealtime(1.2f);
            PositionPlayerForInteraction(player, controller, tether);
            AimAtItem(controller, tether);
            yield return new WaitForSecondsRealtime(0.5f);
            PositionPlayerForInteraction(player, controller, tether);
            AimAtItem(controller, tether);
            UnityEngine.Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.3f);
            player.RequestGrab(tether);
            yield return WaitFor(() => player.HeldItem == tether, "sp02-handoff");
            var handoffPass = hostReleased && player.HeldItem == tether && tether.HolderClientId == manager.LocalClientId;
            Debug.Log($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=handoff hostReleased={hostReleased} clientHeld={player.HeldItem == tether} holderClient={tether.HolderClientId} result={(handoffPass ? "PASS" : "FAIL")}");

            var pass = contentionRejected && handoffPass;
            Debug.Log($"[LAST_SHIFT_SP02_PROBE] client={manager.LocalClientId} phase=contention-summary contention={contentionRejected} handoff={handoffPass} result={(pass ? "PASS" : "FAIL")}");
            if (manager.IsListening) manager.Shutdown();
            yield return null;
            Application.Quit(pass ? 0 : 1);
        }

        private static IEnumerator PressAndRelease(LastShiftPlayerController controller, Keyboard keyboard, Key key)
        {
            QueueKey(keyboard, key, false);
            controller.ProcessKeyboardInput(keyboard, 1f / 60f);
            yield return null;
            QueueKey(keyboard, key, true);
            controller.ProcessKeyboardInput(keyboard, 1f / 60f);
            yield return null;
            QueueKey(keyboard, key, false);
            controller.ProcessKeyboardInput(keyboard, 1f / 60f);
            yield return null;
        }

        private static void QueueKey(Keyboard keyboard, Key key, bool pressed)
        {
            InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(key) : new KeyboardState());
            keyboard.MakeCurrent();
            InputSystem.Update();
        }

        private static IEnumerator QuitProbe(NetworkManager manager, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (manager != null && manager.IsListening) manager.Shutdown();
            yield return null;
            Application.Quit();
        }

        private static void StageOwnerMotionForRemoteVisual(LastShiftNetworkPlayer player)
        {
            var targetPosition = LastShiftNetworkSession.SpawnForSlot(1) + new Vector3(1.25f, 0f, 0.75f);
            var targetRotation = LastShiftNetworkSession.RotationForSlot(1) * Quaternion.Euler(0f, 67f, 0f);
            player.transform.SetPositionAndRotation(targetPosition, targetRotation);
            UnityEngine.Physics.SyncTransforms();
            Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={player.OwnerClientId} phase=owner-visual-motion-staged position={player.transform.position:F3} yaw={player.transform.eulerAngles.y:F1} result=PASS");
        }

        private static IEnumerator VerifyRemoteVisualTransform(NetworkManager manager, LastShiftNetworkPlayer remotePlayer)
        {
            var initialPosition = remotePlayer.transform.position;
            var initialRotation = remotePlayer.transform.rotation;
            var deadline = Time.realtimeSinceStartup + 8f;
            while (manager.IsListening && Time.realtimeSinceStartup < deadline &&
                   (Vector3.Distance(remotePlayer.transform.position, initialPosition) < 0.5f ||
                    Quaternion.Angle(remotePlayer.transform.rotation, initialRotation) < 20f))
                yield return null;

            var body = remotePlayer.BodyRenderer != null ? remotePlayer.BodyRenderer.transform : null;
            var moved = Vector3.Distance(remotePlayer.transform.position, initialPosition) >= 0.5f;
            var rotated = Quaternion.Angle(remotePlayer.transform.rotation, initialRotation) >= 20f;
            var localPoseCanonical = body != null &&
                                     Vector3.Distance(body.localPosition, new Vector3(0f, 0.85f, 0f)) < 0.001f &&
                                     Quaternion.Angle(body.localRotation, Quaternion.identity) < 0.01f;
            var expectedWorldPosition = remotePlayer.transform.TransformPoint(new Vector3(0f, 0.85f, 0f));
            var worldPositionFollows = body != null && Vector3.Distance(body.position, expectedWorldPosition) < 0.001f;
            var worldRotationFollows = body != null && Quaternion.Angle(body.rotation, remotePlayer.transform.rotation) < 0.01f;
            var pass = moved && rotated && localPoseCanonical && worldPositionFollows && worldRotationFollows;
            Debug.Log($"[LAST_SHIFT_LIFECYCLE_PROBE] client={manager.LocalClientId} remoteClient={remotePlayer.OwnerClientId} phase=remote-visual-transform moved={moved} rotated={rotated} localPoseCanonical={localPoseCanonical} worldPositionFollows={worldPositionFollows} worldRotationFollows={worldRotationFollows} result={(pass ? "PASS" : "FAIL")}");
        }

        private static float ColorDistance(Color left, Color right)
        {
            var difference = new Vector4(left.r - right.r, left.g - right.g, left.b - right.b, left.a - right.a);
            return difference.magnitude;
        }

        private static void PositionPlayerForInteraction(
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller,
            LastShiftNetworkGrabbable item,
            float verticalOffset = 0f)
        {
            var cameraTransform = controller.TargetCamera.transform;
            var targetPosition = item.GetComponentInChildren<Collider>().bounds.center;
            var forward = Vector3.ProjectOnPlane(targetPosition - player.transform.position, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            player.transform.SetPositionAndRotation(
                targetPosition - forward * 1.5f - Vector3.up * (cameraTransform.localPosition.y + verticalOffset),
                Quaternion.LookRotation(forward));
            UnityEngine.Physics.SyncTransforms();
        }

        private static void AimAtItem(LastShiftPlayerController controller, LastShiftNetworkGrabbable item)
        {
            var cameraTransform = controller.TargetCamera.transform;
            var targetPosition = item.GetComponentInChildren<Collider>().bounds.center;
            cameraTransform.rotation = Quaternion.LookRotation((targetPosition - cameraTransform.position).normalized, Vector3.up);
        }

        private static IEnumerator WaitFor(Func<bool> predicate, string phase)
        {
            var deadline = Time.realtimeSinceStartup + 8f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!predicate()) Debug.LogError($"[LAST_SHIFT_LIFECYCLE_PROBE] phase={phase} result=FAIL");
        }

        private static bool HasArgument(string name)
        {
            return Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
