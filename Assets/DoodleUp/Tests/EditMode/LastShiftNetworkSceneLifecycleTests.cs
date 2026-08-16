using System;
using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftNetworkSceneLifecycleTests
    {
        [TearDown]
        public void RestoreCleanSceneAfterDirtyGuardTests()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.IsNullOrEmpty(activeScene.path))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void SavedNetworkSandboxRebuildsReopensAndVerifies()
        {
            LastShiftNetworkSceneVerifier.VerifySavedSandboxLifecycle();
        }

        [Test]
        public void SavedVerifierRejectsDirtyActiveSceneWithoutDiscardingChanges()
        {
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var marker = new GameObject("Dirty Scene Marker");
            marker.transform.position = new Vector3(2f, 3f, 4f);
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var sceneHandle = dirtyScene.handle;
            var markerInstanceId = marker.GetInstanceID();

            var exception = Assert.Throws<InvalidOperationException>(LastShiftNetworkSceneVerifier.VerifySavedScene);
            Assert.That(exception.Message, Does.Contain("unsaved changes"));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.True);
            Assert.That(marker.GetInstanceID(), Is.EqualTo(markerInstanceId));
            Assert.That(marker.transform.position, Is.EqualTo(new Vector3(2f, 3f, 4f)));
        }

        [Test]
        public void BuildEntryRejectsDirtyActiveSceneWithoutDiscardingChanges()
        {
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var marker = new GameObject("Dirty Build Marker");
            marker.transform.localScale = new Vector3(2f, 3f, 4f);
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var sceneHandle = dirtyScene.handle;
            var markerInstanceId = marker.GetInstanceID();

            var exception = Assert.Throws<InvalidOperationException>(LastShiftNetworkBuild.BuildWindowsPlayer);
            Assert.That(exception.Message, Does.Contain("unsaved changes"));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.True);
            Assert.That(marker.GetInstanceID(), Is.EqualTo(markerInstanceId));
            Assert.That(marker.transform.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
        }

        [Test]
        public void CleanSavedSceneVerifierOpensAndPasses()
        {
            var setup = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(setup.isDirty, Is.False);
            LastShiftNetworkSceneBuilder.RebuildSandboxForAutomation();
            var scene = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath, OpenSceneMode.Single);
            Assert.That(scene.isDirty, Is.False);
            LastShiftNetworkSceneVerifier.VerifySavedScene();
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LastShiftNetworkSceneBuilder.ScenePath));
        }

        [Test]
        public void VerifierRejectsMissingOrDisabledRemoteBodyVisual()
        {
            var scene = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath, OpenSceneMode.Single);
            var manager = Array.Find(scene.GetRootGameObjects(), root => root.GetComponent<Unity.Netcode.NetworkManager>() != null)
                .GetComponent<Unity.Netcode.NetworkManager>();
            var bodyRenderer = DoodleUp.Runtime.LastShiftCrewBody.PrimaryUnderRoot(
                manager.NetworkConfig.PlayerPrefab.transform);
            Assert.That(bodyRenderer, Is.Not.Null, "승무원 몸 렌더러를 못 찾았다");
            bodyRenderer.enabled = false;
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => LastShiftNetworkSceneVerifier.VerifyScene(scene));
                Assert.That(exception.Message, Does.Contain("remote body renderer must default enabled"));
            }
            finally
            {
                bodyRenderer.enabled = true;
            }

            LastShiftNetworkSceneVerifier.VerifyScene(scene);
        }

        [Test]
        public void VerifierRejectsNonCanonicalPlayerPrefabGeometry()
        {
            var scene = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath, OpenSceneMode.Single);
            var manager = Array.Find(scene.GetRootGameObjects(), root => root.GetComponent<Unity.Netcode.NetworkManager>() != null)
                .GetComponent<Unity.Netcode.NetworkManager>();
            var prefab = manager.NetworkConfig.PlayerPrefab;
            var characterController = prefab.GetComponent<CharacterController>();
            var originalRadius = characterController.radius;
            try
            {
                characterController.radius = originalRadius + 0.1f;
                var exception = Assert.Throws<InvalidOperationException>(() => LastShiftNetworkSceneVerifier.VerifyScene(scene));
                Assert.That(exception.Message, Does.Contain("CharacterController radius mismatch"));
            }
            finally
            {
                characterController.radius = originalRadius;
            }

            LastShiftNetworkSceneVerifier.VerifyScene(scene);
        }
    }
}
