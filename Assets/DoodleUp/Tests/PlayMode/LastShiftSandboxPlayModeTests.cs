using System.Collections;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    public sealed class LastShiftSandboxPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP01.unity";

        [UnityTest]
        public IEnumerator SavedSoloSceneLoadsAndRunsOneShotLifecycle()
        {
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.path, Is.EqualTo(ScenePath));
            var roots = scene.GetRootGameObjects();
            var sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            var players = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftPlayerController>(true)).ToArray();
            var items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();
            var battery = items.Single(item => item.Role == LastShiftItemRole.Battery);

            Assert.That(players.Length, Is.EqualTo(1));
            Assert.That(items.Length, Is.EqualTo(4));
            Assert.That(sandbox.HasAppliedImpact, Is.False);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.None));

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            var nominal = battery.NominalPosition;
            Assert.That(sandbox.ApplyMeteorImpact(), Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(sandbox.HasAppliedImpact, Is.True);
            Assert.That(sandbox.ImpactApplicationCount, Is.EqualTo(1));
            Assert.That(battery.transform.position, Is.Not.EqualTo(nominal));
            Assert.That(battery.DisplacementFromNominal, Is.GreaterThan(0f));
            Assert.That(sandbox.ApplyMeteorImpact(), Is.False);

            sandbox.ResetPreset(LastShiftPreset.PowerOverloadLooseBattery);
            Assert.That(sandbox.HasAppliedImpact, Is.False);
            Assert.That(sandbox.LastResult.Problem, Is.EqualTo(LastShiftDominantProblem.None));
            Assert.That(battery.transform.position, Is.EqualTo(nominal));
        }
    }
}
