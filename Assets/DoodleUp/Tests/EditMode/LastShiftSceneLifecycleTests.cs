using System;
using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftSceneLifecycleTests
    {
        [Test]
        public void AutomationRebuildRefusesDirtyActiveScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("UnsavedMarker");
            EditorSceneManager.MarkSceneDirty(scene);

            Assert.Throws<InvalidOperationException>(() => LastShiftNetworkSceneBuilder.RebuildSandboxForAutomation());
            Assert.That(EditorSceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(GameObject.Find("UnsavedMarker"), Is.Not.Null);
        }

        [Test]
        public void SavedSandboxRebuildsReopensAndVerifies()
        {
            LastShiftNetworkSceneVerifier.VerifySavedSandboxLifecycle();
        }
    }
}
