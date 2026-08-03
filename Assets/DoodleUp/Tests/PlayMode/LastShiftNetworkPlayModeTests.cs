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

        [TearDown]
        public void RemoveTestKeyboard()
        {
            activeController = null;
            var manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (manager != null && manager.IsListening) manager.Shutdown();
            InputSystem.settings.updateMode = previousUpdateMode;
            if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
        }

        [UnityTest]
        public IEnumerator HostStartsOwnsStateAndExercisesHeldItemLifecycle()
        {
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

            controller.TargetCamera.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
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
            yield return null;

            var session = Object.FindFirstObjectByType<LastShiftNetworkSession>(FindObjectsInactive.Include);
            var sandbox = Object.FindFirstObjectByType<LastShiftSandboxController>(FindObjectsInactive.Include);
            var networkSandbox = Object.FindFirstObjectByType<LastShiftNetworkSandbox>(FindObjectsInactive.Include);
            Assert.That(session.StartHost(), Is.True);
            yield return null;

            var player = session.NetworkManager.LocalClient.PlayerObject.GetComponent<LastShiftNetworkPlayer>();
            var controller = player.GetComponent<LastShiftPlayerController>();
            activeController = controller;
            var cooling = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .Single(item => item.Grabbable.Role == LastShiftItemRole.CoolingCanister);
            AimAtItem(controller, cooling);
            Assert.That(controller.InteractionPrompt, Does.Contain("접근 필요"));

            yield return HoldKey(Key.W, 0.5f);

            PositionForKeyboardInteraction(player, controller, cooling);
            Assert.That(controller.InteractionPrompt, Does.Contain("[E]").And.Contain("CoolingCanister"));
            yield return PressAndRelease(Key.E);
            yield return WaitFor(() => player.HeldItem == cooling);
            Assert.That(player.HeldItem, Is.SameAs(cooling));

            yield return PressAndRelease(Key.E);
            yield return WaitFor(() => player.HeldItem == null);
            Assert.That(player.HeldItem, Is.Null);

            var resetBefore = sandbox.ResetGeneration;
            yield return PressAndRelease(Key.Digit2);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.ResetGeneration, Is.EqualTo(resetBefore + 1));
            var battery = Object.FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None)
                .Single(item => item.Grabbable.Role == LastShiftItemRole.Battery);
            PositionForKeyboardInteraction(player, controller, battery);
            yield return PressAndRelease(Key.E);
            yield return WaitFor(() => player.HeldItem == battery);
            player.transform.position += battery.Grabbable.NominalPosition - player.HoldSocket.position;
            UnityEngine.Physics.SyncTransforms();
            yield return null;
            yield return PressAndRelease(Key.F);
            yield return WaitFor(() => player.HeldItem == null && battery.IsSecured);
            Assert.That(battery.IsSecured, Is.True);

            yield return PressAndRelease(Key.Digit1);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.HighHeatHighThrust);
            yield return PressAndRelease(Key.Digit3);
            yield return WaitFor(() => networkSandbox.Snapshot.Preset == LastShiftPreset.BadAttitudeHighOxygen);
            resetBefore = sandbox.ResetGeneration;
            yield return PressAndRelease(Key.R);
            yield return WaitFor(() => sandbox.ResetGeneration == resetBefore + 1);
            Assert.That(networkSandbox.Snapshot.Preset, Is.EqualTo(LastShiftPreset.BadAttitudeHighOxygen));
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

        private static IEnumerator WaitFor(System.Func<bool> predicate)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True);
        }

        private static void AimAtItem(LastShiftPlayerController controller, LastShiftNetworkGrabbable item)
        {
            var target = item.GetComponentInChildren<Collider>().bounds.center;
            var cameraTransform = controller.TargetCamera.transform;
            cameraTransform.rotation = Quaternion.LookRotation((target - cameraTransform.position).normalized, Vector3.up);
            UnityEngine.Physics.SyncTransforms();
        }

        private static void PositionForKeyboardInteraction(
            LastShiftNetworkPlayer player,
            LastShiftPlayerController controller,
            LastShiftNetworkGrabbable item)
        {
            var target = item.GetComponentInChildren<Collider>().bounds.center;
            var cameraTransform = controller.TargetCamera.transform;
            var forward = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).normalized;
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
