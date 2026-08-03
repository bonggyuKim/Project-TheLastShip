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

            Assert.That(LastShiftSceneBuilder.HasUnsavedActiveSceneChanges(), Is.True);
            Assert.Throws<InvalidOperationException>(() => LastShiftSceneBuilder.RebuildSandboxForAutomation());
            Assert.That(EditorSceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(GameObject.Find("UnsavedMarker"), Is.Not.Null);
        }

        [Test]
        public void SavedSandboxRebuildsReopensAndVerifies()
        {
            LastShiftSceneVerifier.VerifySavedSandboxLifecycle();
        }
    }
}
