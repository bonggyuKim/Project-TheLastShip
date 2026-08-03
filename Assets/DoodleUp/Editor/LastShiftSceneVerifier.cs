using System;
using System.Linq;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class LastShiftSceneVerifier
    {
        [MenuItem("Last Shift/SP-01/Verify Sandbox Structure")]
        public static void VerifySandboxStructure()
        {
            var scene = EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            VerifyScene(scene);
        }

        public static void VerifySavedSandboxLifecycle()
        {
            var setup = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(setup.IsValid() && setup.isLoaded, "lifecycle setup scene must be loaded");
            LastShiftSceneBuilder.RebuildSandboxForAutomation();
            var savedScene = SceneManager.GetActiveScene();
            Require(savedScene.path == LastShiftSceneBuilder.ScenePath, "rebuilt scene path mismatch");
            Require(!savedScene.isDirty, "rebuilt scene must be saved");
            VerifyScene(savedScene);

            var reopened = EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            Require(reopened.IsValid() && reopened.isLoaded, "saved scene must reopen");
            Require(!reopened.isDirty, "reopened scene must be clean");
            VerifyScene(reopened);
            Debug.Log($"[LAST_SHIFT_LIFECYCLE] scene={LastShiftSceneBuilder.ScenePath} rebuild=PASS reopen=PASS result=PASS");
        }

        public static void VerifyScene(Scene scene)
        {
            Require(scene.IsValid() && scene.isLoaded, "scene must be loaded");
            Require(scene.path == LastShiftSceneBuilder.ScenePath, "scene path mismatch");
            Require(SceneManager.GetActiveScene() == scene, "SP-01 scene must be active");
            Require(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == LastShiftSceneBuilder.ScenePath), "SP-01 scene must be enabled in build settings");

            var roots = scene.GetRootGameObjects();
            Require(roots.All(root => root.activeInHierarchy), "all root objects must be active");
            var all = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Require(all.Count(x => x.name == LastShiftSceneBuilder.CockpitZoneName) == 1, "cockpit zone count must be 1");
            Require(all.Count(x => x.name == LastShiftSceneBuilder.UtilityZoneName) == 1, "utility zone count must be 1");
            Require(all.Count(x => x.name == LastShiftSceneBuilder.LifeSupportZoneName) == 1, "life support zone count must be 1");
            Require(all.Count(x => x.name == "PlayerOne") == 1, "player one count must be 1");
            Require(all.All(x => x.name != "PlayerTwo"), "SP-01 must not contain player two");
            Require(all.Count(x => x.name == "CanonicalMeteorStimulus") == 1, "canonical meteor count must be 1");
            var meteorVisual = all.Single(x => x.name == "CanonicalMeteorStimulus");
            var meteor = LastShiftMeteorStimulus.Canonical;
            Require(Vector3.Distance(meteorVisual.position, meteor.ImpactPoint - meteor.ImpactVector * 2f) < 0.0001f, "canonical meteor visual origin mismatch");

            var sandboxes = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).ToArray();
            Require(sandboxes.Length == 1, "sandbox controller count must be 1");
            Require(sandboxes[0].isActiveAndEnabled, "sandbox controller must be active and enabled");
            Require(sandboxes[0].Players != null && sandboxes[0].Players.Length == 1, "sandbox player wiring must contain exactly player one");
            Require(sandboxes[0].Items != null && sandboxes[0].Items.Length == 4, "sandbox item wiring must contain exactly four items");

            var players = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftPlayerController>(true)).ToArray();
            Require(players.Length == 1, "player controller count must be 1");
            var player = players[0];
            Require(player.isActiveAndEnabled, "player controller must be active and enabled");
            Require(player.PlayerSlot == LastShiftPlayerSlot.PlayerOne, "solo player must use player one slot");
            Require(player.GetComponent<CharacterController>() != null, "player CharacterController missing");
            Require(player.TargetCamera != null && player.TargetCamera.isActiveAndEnabled, "active player camera missing");
            Require(player.TargetCamera.CompareTag("MainCamera"), "solo camera must be MainCamera");
            Require(player.TargetCamera.rect == new Rect(0f, 0f, 1f, 1f), "solo camera viewport mismatch");
            Require(player.HoldSocket != null && player.HoldSocket.IsChildOf(player.TargetCamera.transform), "player hold socket wiring mismatch");

            var items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();
            Require(items.Length == 4, "grabbable item count must be 4");
            foreach (LastShiftItemRole role in Enum.GetValues(typeof(LastShiftItemRole)))
                Require(items.Count(item => item.Role == role) == 1, $"{role} count must be 1");
            Require(items.All(item => item.isActiveAndEnabled), "all grabbables must be active and enabled");
            Require(items.All(item => item.GetComponent<Rigidbody>() != null), "grabbable Rigidbody missing");
            Require(items.All(item => item.GetComponent<Collider>() != null), "grabbable Collider missing");
            Require(items.All(item => item.GetComponent<Collider>().enabled), "grabbable Collider must be enabled");
            Require(items.All(item => item.Body != null), "grabbable Rigidbody cache not configured");
            Require(items.All(item => item.NominalPosition == item.transform.position), "grabbable nominal position must match saved scene position");
            Require(items.All(item => item.Secured == item.Body.isKinematic), "secured/kinematic wiring mismatch");
            Require(sandboxes[0].Players[0] == player, "sandbox player reference mismatch");
            Require(sandboxes[0].Items.All(item => items.Contains(item)), "sandbox item references must match scene grabbables");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<DoodleUp.Stroke.Du03AStrokeDriver>(true)).Any() == false, "drawing runtime must not be coupled to SP-01");
            Debug.Log($"[LAST_SHIFT_VERIFY] scene={LastShiftSceneBuilder.ScenePath} active=1 zones=3 players=1 cameras=1 sockets=1 items=4 rigidbodies=4 colliders=4 meteor=1 drawingDependency=0 result=PASS");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"LAST SHIFT SP-01 verification failed: {message}.");
        }
    }
}
